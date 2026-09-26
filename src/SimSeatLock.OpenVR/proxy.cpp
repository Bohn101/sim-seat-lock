// SimSeatLock.OpenVR — AMS2 2015 C-API proxy.
#include "pose_math.h"
#include "shm.h"

#include <cmath>
#include <stdarg.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <windows.h>

namespace {

constexpr uint32_t kHmdIndex = 0;

#pragma pack(push, 8)
struct HmdMatrix34 { float m[3][4]; };
struct HmdVector3 { float v[3]; };
struct TrackedDevicePose {
    HmdMatrix34 deviceToAbsolute;
    HmdVector3 velocity;
    HmdVector3 angularVelocity;
    int32_t trackingResult;
    bool poseIsValid;
    bool deviceIsConnected;
};
#pragma pack(pop)

static_assert(sizeof(HmdMatrix34) == 48, "HmdMatrix34");
static_assert(sizeof(TrackedDevicePose) == 80, "TrackedDevicePose pack(8)");

HMODULE gOrig = nullptr;
SharedMemory gShm;
float gEyeX = 0.f, gEyeY = 1.10f, gEyeZ = 0.27f;
bool gGeom = false;
int gWaitLog = 0;

using WaitGetPoses_t = int (*)(void* instance, TrackedDevicePose* render, uint32_t nRender,
                               TrackedDevicePose* game, uint32_t nGame);
using GetDevicePose_t = void (*)(void* instance, int origin, float pred,
                                 TrackedDevicePose* poses, uint32_t count);
using Fn0 = void* (*)();
using FnBool = bool (*)();
using FnInit = void* (*)(int* peError, int eType);
using FnShutdown = void (*)();
using FnGipa = void* (*)(const char* name, int* peError);

WaitGetPoses_t pWaitGetPoses = nullptr;
WaitGetPoses_t pGetLastPoses = nullptr;
GetDevicePose_t pGetDevicePose = nullptr;
Fn0 pVRCompositor = nullptr;
Fn0 pVRSystem = nullptr;
FnInit pVRInit = nullptr;
FnShutdown pVRShutdown = nullptr;
FnBool pIsHmd = nullptr;
FnBool pIsRuntime = nullptr;
FnGipa pGipa = nullptr;

void* gOrigWaitVt = nullptr;

void Log(const char* fmt, ...) {
    char path[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&Log), &self);
    if (self) GetModuleFileNameA(self, path, MAX_PATH);
    char* slash = strrchr(path, '\\');
    if (slash) { slash[1] = 0; strcat_s(path, "simseatlock-openvr.log"); }
    else strcpy_s(path, "C:\\Users\\Bohnster\\sim-seat-lock\\publish\\openvr\\simseatlock-openvr.log");
    FILE* f = nullptr;
    fopen_s(&f, path, "a");
    if (!f) return;
    SYSTEMTIME st{};
    GetLocalTime(&st);
    fprintf(f, "%04d-%02d-%02d %02d:%02d:%02d.%03d ", st.wYear, st.wMonth, st.wDay,
            st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    va_list ap; va_start(ap, fmt); vfprintf(f, fmt, ap); va_end(ap);
    fputc('\n', f);
    fclose(f);
}

void EnsureGeom() {
    if (gGeom) return;
    LoadGeometry(&gEyeX, &gEyeY, &gEyeZ);
    gGeom = true;
}

Rigid MatrixToRigid(const HmdMatrix34& m) {
    Rigid r{};
    r.px = m.m[0][3]; r.py = m.m[1][3]; r.pz = m.m[2][3];
    const float t = m.m[0][0] + m.m[1][1] + m.m[2][2];
    if (t > 0.f) {
        const float s = std::sqrt(t + 1.f) * 2.f;
        r.qw = 0.25f * s;
        r.qx = (m.m[2][1] - m.m[1][2]) / s;
        r.qy = (m.m[0][2] - m.m[2][0]) / s;
        r.qz = (m.m[1][0] - m.m[0][1]) / s;
    } else if (m.m[0][0] > m.m[1][1] && m.m[0][0] > m.m[2][2]) {
        const float s = std::sqrt(1.f + m.m[0][0] - m.m[1][1] - m.m[2][2]) * 2.f;
        r.qw = (m.m[2][1] - m.m[1][2]) / s; r.qx = 0.25f * s;
        r.qy = (m.m[0][1] + m.m[1][0]) / s; r.qz = (m.m[0][2] + m.m[2][0]) / s;
    } else if (m.m[1][1] > m.m[2][2]) {
        const float s = std::sqrt(1.f + m.m[1][1] - m.m[0][0] - m.m[2][2]) * 2.f;
        r.qw = (m.m[0][2] - m.m[2][0]) / s; r.qx = (m.m[0][1] + m.m[1][0]) / s;
        r.qy = 0.25f * s; r.qz = (m.m[1][2] + m.m[2][1]) / s;
    } else {
        const float s = std::sqrt(1.f + m.m[2][2] - m.m[0][0] - m.m[1][1]) * 2.f;
        r.qw = (m.m[1][0] - m.m[0][1]) / s; r.qx = (m.m[0][2] + m.m[2][0]) / s;
        r.qy = (m.m[1][2] + m.m[2][1]) / s; r.qz = 0.25f * s;
    }
    return r;
}

void RigidToMatrix(const Rigid& r, HmdMatrix34* m) {
    const float x = r.qx, y = r.qy, z = r.qz, w = r.qw;
    const float xx = x * x, yy = y * y, zz = z * z;
    const float xy = x * y, xz = x * z, yz = y * z;
    const float wx = w * x, wy = w * y, wz = w * z;
    m->m[0][0] = 1.f - 2.f * (yy + zz); m->m[0][1] = 2.f * (xy - wz); m->m[0][2] = 2.f * (xz + wy); m->m[0][3] = r.px;
    m->m[1][0] = 2.f * (xy + wz); m->m[1][1] = 1.f - 2.f * (xx + zz); m->m[1][2] = 2.f * (yz - wx); m->m[1][3] = r.py;
    m->m[2][0] = 2.f * (xz - wy); m->m[2][1] = 2.f * (yz + wx); m->m[2][2] = 1.f - 2.f * (xx + yy); m->m[2][3] = r.pz;
}

void WriteAlive(int space, const Rigid& hmd) {
    GameBlock game{};
    game.Flags = RigFlags_GameLive;
    game.SpaceType = space;
    game.ViewCount = 1;
    game.Lpx = hmd.px; game.Lpy = hmd.py; game.Lpz = hmd.pz;
    game.Lqx = hmd.qx; game.Lqy = hmd.qy; game.Lqz = hmd.qz;
    game.Lqw = hmd.qw == 0 && hmd.qx == 0 && hmd.qy == 0 && hmd.qz == 0 ? 1.f : hmd.qw;
    game.Rqw = 1.f;
    gShm.WriteGame(game);
}

void PatchPose(TrackedDevicePose* poses, uint32_t count, int space) {
    if (!poses || count == 0) return;
    EnsureGeom();
    RigBlock rig{};
    const bool haveRig = gShm.TryReadRig(&rig);
    const bool armed = haveRig && rig.Armed != 0;
    const float eyeX = haveRig ? rig.EyeX : gEyeX;
    const float eyeY = haveRig ? rig.EyeY : gEyeY;
    const float eyeZ = haveRig ? rig.EyeZ : gEyeZ;
    TrackedDevicePose& hmd = poses[kHmdIndex];
    if (!hmd.poseIsValid) { WriteAlive(space, IdentityRigid()); return; }
    Rigid raw = MatrixToRigid(hmd.deviceToAbsolute);
    Rigid view = armed ? ApplyCompensateRigid(raw, rig, eyeX, eyeY, eyeZ) : raw;
    RigidToMatrix(view, &hmd.deviceToAbsolute);
    WriteAlive(space, view);
}

bool HookVtable(void* iface, int slot, void* hook, void** orig) {
    if (!iface || slot < 0 || !hook || !orig) return false;
    void** vt = *reinterpret_cast<void***>(iface);
    if (!vt) return false;
    if (vt[slot] == hook) return true;
    DWORD old = 0;
    if (!VirtualProtect(&vt[slot], sizeof(void*), PAGE_EXECUTE_READWRITE, &old)) return false;
    *orig = vt[slot];
    vt[slot] = hook;
    VirtualProtect(&vt[slot], sizeof(void*), old, &old);
    return true;
}

int HookedWaitGetPoses(void* self, TrackedDevicePose* render, uint32_t nRender,
                       TrackedDevicePose* game, uint32_t nGame) {
    auto fn = reinterpret_cast<WaitGetPoses_t>(gOrigWaitVt);
    const int err = fn ? fn(self, render, nRender, game, nGame) : 0;
    if (gWaitLog < 3) { Log("WaitGetPoses nR=%u nG=%u err=%d", nRender, nGame, err); ++gWaitLog; }
    PatchPose(render, nRender, 1);
    if (game && nGame > 0) PatchPose(game, nGame, 1);
    return err;
}

void HookCompositorIface(void* iface, const char* tag) {
    if (!iface) return;
    if (HookVtable(iface, 2, reinterpret_cast<void*>(&HookedWaitGetPoses), &gOrigWaitVt))
        Log("hooked %s WaitGetPoses slot 2", tag);
    else
        Log("FAILED hook %s slot 2", tag);
}

HMODULE LoadOrig() {
    char dir[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&LoadOrig), &self);
    GetModuleFileNameA(self, dir, MAX_PATH);
    char* slash = strrchr(dir, '\\');
    if (slash) *slash = 0;
    char path[MAX_PATH]{};
    const char* names[] = {"openvr_api_orig.dll", "openvr_api.stock.dll"};
    for (const char* n : names) {
        sprintf_s(path, "%s\\%s", dir, n);
        HMODULE m = LoadLibraryA(path);
        if (m) { Log("orig %s", path); return m; }
        Log("LoadLibrary failed %s gle=%lu", path, GetLastError());
    }
    return nullptr;
}

void EnsureOrig() {
    if (gOrig) return;
    gOrig = LoadOrig();
    if (!gOrig) return;
    pWaitGetPoses = reinterpret_cast<WaitGetPoses_t>(GetProcAddress(gOrig, "VR_IVRCompositor_WaitGetPoses"));
    pGetLastPoses = reinterpret_cast<WaitGetPoses_t>(GetProcAddress(gOrig, "VR_IVRCompositor_GetLastPoses"));
    pGetDevicePose = reinterpret_cast<GetDevicePose_t>(GetProcAddress(gOrig, "VR_IVRSystem_GetDeviceToAbsoluteTrackingPose"));
    pVRCompositor = reinterpret_cast<Fn0>(GetProcAddress(gOrig, "VRCompositor"));
    pVRSystem = reinterpret_cast<Fn0>(GetProcAddress(gOrig, "VRSystem"));
    pVRInit = reinterpret_cast<FnInit>(GetProcAddress(gOrig, "VR_Init"));
    pVRShutdown = reinterpret_cast<FnShutdown>(GetProcAddress(gOrig, "VR_Shutdown"));
    pIsHmd = reinterpret_cast<FnBool>(GetProcAddress(gOrig, "VR_IsHmdPresent"));
    pIsRuntime = reinterpret_cast<FnBool>(GetProcAddress(gOrig, "VR_IsRuntimeInstalled"));
    pGipa = reinterpret_cast<FnGipa>(GetProcAddress(gOrig, "VR_GetGenericInterface"));
    Log("orig binds init=%d gipa=%d wait=%d compositor=%d", pVRInit != nullptr, pGipa != nullptr,
        pWaitGetPoses != nullptr, pVRCompositor != nullptr);
}

} // namespace

extern "C" {

__declspec(dllexport) int SSL_IsOpenVrProxy() { return 1; }

__declspec(dllexport) void* VR_Init(int* peError, int eType) {
    EnsureOrig();
    Log("VR_Init type=%d orig=%p", eType, pVRInit);
    void* sys = pVRInit ? pVRInit(peError, eType) : nullptr;
    Log("VR_Init -> %p err=%d", sys, peError ? *peError : -1);
    if (sys) WriteAlive(0, IdentityRigid());
    return sys;
}

__declspec(dllexport) void VR_Shutdown() {
    EnsureOrig();
    Log("VR_Shutdown");
    if (pVRShutdown) pVRShutdown();
}

__declspec(dllexport) bool VR_IsHmdPresent() {
    EnsureOrig();
    const bool v = pIsHmd ? pIsHmd() : false;
    Log("VR_IsHmdPresent -> %d", v ? 1 : 0);
    return v;
}

__declspec(dllexport) bool VR_IsRuntimeInstalled() {
    EnsureOrig();
    return pIsRuntime ? pIsRuntime() : false;
}

__declspec(dllexport) void* VR_GetGenericInterface(const char* name, int* peError) {
    EnsureOrig();
    void* iface = pGipa ? pGipa(name, peError) : nullptr;
    Log("GetGenericInterface %s -> %p err=%d", name ? name : "(null)", iface, peError ? *peError : -1);
    if (iface && name && strncmp(name, "IVRCompositor", 13) == 0)
        HookCompositorIface(iface, name);
    if (iface && name && strncmp(name, "FnTable:IVRCompositor", 21) == 0)
        Log("FnTable compositor — flat exports only, no vtable hook");
    return iface;
}

__declspec(dllexport) void* VRSystem() {
    EnsureOrig();
    void* sys = pVRSystem ? pVRSystem() : nullptr;
    Log("VRSystem -> %p", sys);
    if (sys) WriteAlive(0, IdentityRigid());
    return sys;
}

__declspec(dllexport) int VR_IVRCompositor_WaitGetPoses(void* instance, TrackedDevicePose* render, uint32_t nRender,
                                                        TrackedDevicePose* game, uint32_t nGame) {
    EnsureOrig();
    if (gWaitLog < 3) { Log("flat WaitGetPoses nR=%u nG=%u", nRender, nGame); ++gWaitLog; }
    const int err = pWaitGetPoses ? pWaitGetPoses(instance, render, nRender, game, nGame) : 0;
    PatchPose(render, nRender, 1);
    if (game && nGame > 0) PatchPose(game, nGame, 1);
    return err;
}

__declspec(dllexport) int VR_IVRCompositor_GetLastPoses(void* instance, TrackedDevicePose* render, uint32_t nRender,
                                                        TrackedDevicePose* game, uint32_t nGame) {
    EnsureOrig();
    const int err = pGetLastPoses ? pGetLastPoses(instance, render, nRender, game, nGame) : 0;
    PatchPose(render, nRender, 1);
    if (game && nGame > 0) PatchPose(game, nGame, 1);
    return err;
}

__declspec(dllexport) void VR_IVRSystem_GetDeviceToAbsoluteTrackingPose(void* instance, int origin, float pred,
                                                                       TrackedDevicePose* poses, uint32_t count) {
    EnsureOrig();
    if (pGetDevicePose) pGetDevicePose(instance, origin, pred, poses, count);
    PatchPose(poses, count, origin);
}

__declspec(dllexport) void* VRCompositor() {
    EnsureOrig();
    void* iface = pVRCompositor ? pVRCompositor() : nullptr;
    Log("VRCompositor -> %p", iface);
    HookCompositorIface(iface, "VRCompositor");
    if (iface) WriteAlive(0, IdentityRigid());
    return iface;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        char exe[MAX_PATH]{};
        GetModuleFileNameA(nullptr, exe, MAX_PATH);
        Log("DllMain PROCESS_ATTACH pid=%lu exe=%s", GetCurrentProcessId(), exe);
    } else if (reason == DLL_PROCESS_DETACH) {
        Log("DllMain PROCESS_DETACH pid=%lu", GetCurrentProcessId());
    }
    return TRUE;
}

} // extern C

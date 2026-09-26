// SimSeatLock.OpenVR — drop-in openvr_api.dll for OpenVR titles (AMS2).
// Loads openvr_api.stock.dll from the same folder, forwards VR_* exports,
// vtable-hooks compositor poses, applies the same T_view product as the
// OpenXR layer, writes Local\\SimSeatLock.Game.v1.

#include "pose_math.h"
#include "shm.h"

#include <cmath>
#include <stdarg.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

namespace {

constexpr uint32_t kHmdIndex = 0;

#pragma pack(push, 8)
struct HmdMatrix34 {
    float m[3][4];
};
struct HmdVector3 {
    float v[3];
};
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

HMODULE gStock = nullptr;
SharedMemory gShm;
float gEyeX = 0.f, gEyeY = 1.10f, gEyeZ = 0.27f;
bool gGeom = false;

void* gOrigWaitGetPoses = nullptr;
void* gOrigGetLastPoses = nullptr;
void* gOrigGetDevicePose = nullptr;
void* gOrigGetLastPoseIndex = nullptr;

using Fn_Init = uint32_t (*)(int* peError, int eType);
using Fn_Init2 = uint32_t (*)(int* peError, int eType, const char* info);
using Fn_Shutdown = void (*)();
using Fn_Bool = bool (*)();
using Fn_GetInterface = void* (*)(const char* name, int* peError);
using Fn_IsVersion = bool (*)(const char* name);
using Fn_Token = uint32_t (*)();
using Fn_ErrStr = const char* (*)(int err);
using Fn_RuntimePathOld = const char* (*)();
using Fn_GetRuntimePath = bool (*)(char* buf, uint32_t size, uint32_t* required);

Fn_Init pInit = nullptr;
Fn_Init2 pInit2 = nullptr;
Fn_Shutdown pShutdown = nullptr;
Fn_Bool pIsHmd = nullptr;
Fn_Bool pIsRuntime = nullptr;
Fn_GetInterface pGetIface = nullptr;
Fn_IsVersion pIsVersion = nullptr;
Fn_Token pToken = nullptr;
Fn_ErrStr pErrSym = nullptr;
Fn_ErrStr pErrEng = nullptr;
Fn_RuntimePathOld pRuntimeOld = nullptr;
Fn_GetRuntimePath pRuntimePath = nullptr;

void Log(const char* fmt, ...) {
    char path[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&Log), &self);
    if (self) GetModuleFileNameA(self, path, MAX_PATH);
    char* slash = strrchr(path, '\\');
    if (slash) {
        slash[1] = 0;
        strcat_s(path, "simseatlock-openvr.log");
    } else {
        strcpy_s(path, "C:\\Users\\Bohnster\\sim-seat-lock\\publish\\openvr\\simseatlock-openvr.log");
    }
    FILE* f = nullptr;
    fopen_s(&f, path, "a");
    if (!f) return;
    SYSTEMTIME st{};
    GetLocalTime(&st);
    fprintf(f, "%04d-%02d-%02d %02d:%02d:%02d.%03d ", st.wYear, st.wMonth, st.wDay,
            st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    va_list ap;
    va_start(ap, fmt);
    vfprintf(f, fmt, ap);
    va_end(ap);
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
    r.px = m.m[0][3];
    r.py = m.m[1][3];
    r.pz = m.m[2][3];
    const float t = m.m[0][0] + m.m[1][1] + m.m[2][2];
    if (t > 0.f) {
        const float s = std::sqrt(t + 1.f) * 2.f;
        r.qw = 0.25f * s;
        r.qx = (m.m[2][1] - m.m[1][2]) / s;
        r.qy = (m.m[0][2] - m.m[2][0]) / s;
        r.qz = (m.m[1][0] - m.m[0][1]) / s;
    } else if (m.m[0][0] > m.m[1][1] && m.m[0][0] > m.m[2][2]) {
        const float s = std::sqrt(1.f + m.m[0][0] - m.m[1][1] - m.m[2][2]) * 2.f;
        r.qw = (m.m[2][1] - m.m[1][2]) / s;
        r.qx = 0.25f * s;
        r.qy = (m.m[0][1] + m.m[1][0]) / s;
        r.qz = (m.m[0][2] + m.m[2][0]) / s;
    } else if (m.m[1][1] > m.m[2][2]) {
        const float s = std::sqrt(1.f + m.m[1][1] - m.m[0][0] - m.m[2][2]) * 2.f;
        r.qw = (m.m[0][2] - m.m[2][0]) / s;
        r.qx = (m.m[0][1] + m.m[1][0]) / s;
        r.qy = 0.25f * s;
        r.qz = (m.m[1][2] + m.m[2][1]) / s;
    } else {
        const float s = std::sqrt(1.f + m.m[2][2] - m.m[0][0] - m.m[1][1]) * 2.f;
        r.qw = (m.m[1][0] - m.m[0][1]) / s;
        r.qx = (m.m[0][2] + m.m[2][0]) / s;
        r.qy = (m.m[1][2] + m.m[2][1]) / s;
        r.qz = 0.25f * s;
    }
    return r;
}

void RigidToMatrix(const Rigid& r, HmdMatrix34* m) {
    const float x = r.qx, y = r.qy, z = r.qz, w = r.qw;
    const float xx = x * x, yy = y * y, zz = z * z;
    const float xy = x * y, xz = x * z, yz = y * z;
    const float wx = w * x, wy = w * y, wz = w * z;
    m->m[0][0] = 1.f - 2.f * (yy + zz);
    m->m[0][1] = 2.f * (xy - wz);
    m->m[0][2] = 2.f * (xz + wy);
    m->m[0][3] = r.px;
    m->m[1][0] = 2.f * (xy + wz);
    m->m[1][1] = 1.f - 2.f * (xx + zz);
    m->m[1][2] = 2.f * (yz - wx);
    m->m[1][3] = r.py;
    m->m[2][0] = 2.f * (xz - wy);
    m->m[2][1] = 2.f * (yz + wx);
    m->m[2][2] = 1.f - 2.f * (xx + yy);
    m->m[2][3] = r.pz;
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
    if (!hmd.poseIsValid) {
        WriteAlive(space, IdentityRigid());
        return;
    }
    Rigid raw = MatrixToRigid(hmd.deviceToAbsolute);
    Rigid view = raw;
    if (armed) view = ApplyCompensateRigid(raw, rig, eyeX, eyeY, eyeZ);
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

using WaitGetPoses_t = int (*)(void* self, TrackedDevicePose* render, uint32_t nRender,
                               TrackedDevicePose* game, uint32_t nGame);
using GetDevicePose_t = void (*)(void* self, int origin, float pred,
                                 TrackedDevicePose* poses, uint32_t count);
using GetLastPoseIndex_t = int (*)(void* self, uint32_t index,
                                   TrackedDevicePose* render, TrackedDevicePose* game);

int HookedWaitGetPoses(void* self, TrackedDevicePose* render, uint32_t nRender,
                       TrackedDevicePose* game, uint32_t nGame) {
    auto fn = reinterpret_cast<WaitGetPoses_t>(gOrigWaitGetPoses);
    const int err = fn ? fn(self, render, nRender, game, nGame) : 0;
    PatchPose(render, nRender, 1);
    if (game && nGame > 0) PatchPose(game, nGame, 1);
    return err;
}

int HookedGetLastPoses(void* self, TrackedDevicePose* render, uint32_t nRender,
                       TrackedDevicePose* game, uint32_t nGame) {
    auto fn = reinterpret_cast<WaitGetPoses_t>(gOrigGetLastPoses);
    const int err = fn ? fn(self, render, nRender, game, nGame) : 0;
    PatchPose(render, nRender, 1);
    if (game && nGame > 0) PatchPose(game, nGame, 1);
    return err;
}

void HookedGetDevicePose(void* self, int origin, float pred, TrackedDevicePose* poses, uint32_t count) {
    auto fn = reinterpret_cast<GetDevicePose_t>(gOrigGetDevicePose);
    if (fn) fn(self, origin, pred, poses, count);
    PatchPose(poses, count, origin);
}

int HookedGetLastPoseIndex(void* self, uint32_t index, TrackedDevicePose* render, TrackedDevicePose* game) {
    auto fn = reinterpret_cast<GetLastPoseIndex_t>(gOrigGetLastPoseIndex);
    const int err = fn ? fn(self, index, render, game) : 0;
    if (index == kHmdIndex) {
        if (render) PatchPose(render, 1, 1);
        if (game) PatchPose(game, 1, 1);
    }
    return err;
}

int SystemPoseSlot(const char* name) {
    const char* us = strrchr(name, '_');
    const int ver = us ? atoi(us + 1) : 22;
    if (ver >= 26) return 12;
    if (ver >= 17) return 11;
    return 10;
}

void HookSystem(void* iface, const char* name) {
    const int slot = SystemPoseSlot(name);
    if (HookVtable(iface, slot, reinterpret_cast<void*>(&HookedGetDevicePose), &gOrigGetDevicePose))
        Log("hooked %s GetDeviceToAbsoluteTrackingPose slot %d", name, slot);
    else
        Log("FAILED hook %s slot %d", name, slot);
}

void HookCompositor(void* iface, const char* name) {
    if (HookVtable(iface, 2, reinterpret_cast<void*>(&HookedWaitGetPoses), &gOrigWaitGetPoses))
        Log("hooked %s WaitGetPoses slot 2", name);
    HookVtable(iface, 3, reinterpret_cast<void*>(&HookedGetLastPoses), &gOrigGetLastPoses);
    HookVtable(iface, 4, reinterpret_cast<void*>(&HookedGetLastPoseIndex), &gOrigGetLastPoseIndex);
}

HMODULE LoadStock() {
    char dir[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&LoadStock), &self);
    GetModuleFileNameA(self, dir, MAX_PATH);
    char* slash = strrchr(dir, '\\');
    if (slash) *slash = 0;
    char path[MAX_PATH]{};
    const char* names[] = {"openvr_api.stock.dll", "openvr_api.orig.dll"};
    for (const char* n : names) {
        sprintf_s(path, "%s\\%s", dir, n);
        HMODULE m = LoadLibraryA(path);
        if (m) { Log("stock %s", path); return m; }
    }
    Log("no stock openvr_api next to proxy (looked for openvr_api.stock.dll)");
    return nullptr;
}

void BindStock(HMODULE m) {
    pInit = reinterpret_cast<Fn_Init>(GetProcAddress(m, "VR_InitInternal"));
    pInit2 = reinterpret_cast<Fn_Init2>(GetProcAddress(m, "VR_InitInternal2"));
    pShutdown = reinterpret_cast<Fn_Shutdown>(GetProcAddress(m, "VR_ShutdownInternal"));
    pIsHmd = reinterpret_cast<Fn_Bool>(GetProcAddress(m, "VR_IsHmdPresent"));
    pIsRuntime = reinterpret_cast<Fn_Bool>(GetProcAddress(m, "VR_IsRuntimeInstalled"));
    pGetIface = reinterpret_cast<Fn_GetInterface>(GetProcAddress(m, "VR_GetGenericInterface"));
    pIsVersion = reinterpret_cast<Fn_IsVersion>(GetProcAddress(m, "VR_IsInterfaceVersionValid"));
    pToken = reinterpret_cast<Fn_Token>(GetProcAddress(m, "VR_GetInitToken"));
    pErrSym = reinterpret_cast<Fn_ErrStr>(GetProcAddress(m, "VR_GetVRInitErrorAsSymbol"));
    pErrEng = reinterpret_cast<Fn_ErrStr>(GetProcAddress(m, "VR_GetVRInitErrorAsEnglishDescription"));
    if (!pErrEng) pErrEng = reinterpret_cast<Fn_ErrStr>(GetProcAddress(m, "VR_GetStringForHmdError"));
    pRuntimeOld = reinterpret_cast<Fn_RuntimePathOld>(GetProcAddress(m, "VR_RuntimePath"));
    pRuntimePath = reinterpret_cast<Fn_GetRuntimePath>(GetProcAddress(m, "VR_GetRuntimePath"));
}

} // namespace

extern "C" {

__declspec(dllexport) int SSL_IsOpenVrProxy() { return 1; }

__declspec(dllexport) uint32_t VR_InitInternal(int* peError, int eType) {
    return pInit ? pInit(peError, eType) : 1;
}
__declspec(dllexport) uint32_t VR_InitInternal2(int* peError, int eType, const char* info) {
    if (pInit2) return pInit2(peError, eType, info);
    return pInit ? pInit(peError, eType) : 1;
}
__declspec(dllexport) void VR_ShutdownInternal() { if (pShutdown) pShutdown(); }
__declspec(dllexport) bool VR_IsHmdPresent() { return pIsHmd ? pIsHmd() : false; }
__declspec(dllexport) bool VR_IsRuntimeInstalled() { return pIsRuntime ? pIsRuntime() : false; }
__declspec(dllexport) const char* VR_RuntimePath() { return pRuntimeOld ? pRuntimeOld() : ""; }
__declspec(dllexport) bool VR_GetRuntimePath(char* buf, uint32_t size, uint32_t* required) {
    return pRuntimePath ? pRuntimePath(buf, size, required) : false;
}
__declspec(dllexport) bool VR_IsInterfaceVersionValid(const char* name) {
    return pIsVersion ? pIsVersion(name) : false;
}
__declspec(dllexport) uint32_t VR_GetInitToken() { return pToken ? pToken() : 0; }
__declspec(dllexport) const char* VR_GetVRInitErrorAsSymbol(int err) {
    return pErrSym ? pErrSym(err) : "Unknown";
}
__declspec(dllexport) const char* VR_GetVRInitErrorAsEnglishDescription(int err) {
    return pErrEng ? pErrEng(err) : "Unknown";
}
__declspec(dllexport) const char* VR_GetStringForHmdError(int err) {
    return VR_GetVRInitErrorAsEnglishDescription(err);
}

__declspec(dllexport) void* VR_GetGenericInterface(const char* name, int* peError) {
    void* iface = pGetIface ? pGetIface(name, peError) : nullptr;
    if (!iface || !name) return iface;
    Log("GetGenericInterface %s", name);
    if (strncmp(name, "IVRSystem_", 10) == 0) HookSystem(iface, name);
    if (strncmp(name, "IVRCompositor_", 14) == 0) HookCompositor(iface, name);
    WriteAlive(0, IdentityRigid());
    return iface;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        char exe[MAX_PATH]{};
        GetModuleFileNameA(nullptr, exe, MAX_PATH);
        Log("DllMain PROCESS_ATTACH pid=%lu exe=%s", GetCurrentProcessId(), exe);
        gStock = LoadStock();
        if (gStock) {
            BindStock(gStock);
            EnsureGeom();
            WriteAlive(0, IdentityRigid());
            Log("stock bound, Game.v1 heartbeat written");
        }
    } else if (reason == DLL_PROCESS_DETACH) {
        Log("DllMain PROCESS_DETACH pid=%lu", GetCurrentProcessId());
    }
    return TRUE;
}

} // extern C

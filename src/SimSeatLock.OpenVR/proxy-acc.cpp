// SimSeatLock.OpenVR ACC — OpenVR 1.5.17, 24 exports. Splash first, no vtable.
#include "shm.h"

#include <stdarg.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <windows.h>

namespace {

HMODULE gOrig = nullptr;
SharedMemory gShm;

using FnInit = uint32_t (*)(int* peError, int eType);
using FnInit2 = uint32_t (*)(int* peError, int eType, const char* info);
using FnShutdown = void (*)();
using FnGipa = void* (*)(const char* name, int* peError);
using FnBool = bool (*)();

FnInit pInit = nullptr;
FnInit2 pInit2 = nullptr;
FnShutdown pShutdown = nullptr;
FnGipa pGipa = nullptr;
FnBool pIsHmd = nullptr;
FnBool pIsRuntime = nullptr;

void Log(const char* fmt, ...) {
    char path[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&Log), &self);
    if (self) GetModuleFileNameA(self, path, MAX_PATH);
    char* slash = strrchr(path, '\\');
    if (slash) { slash[1] = 0; strcat_s(path, "simseatlock-openvr.log"); }
    else strcpy_s(path, "C:\\Users\\Bohnster\\sim-seat-lock\\publish\\openvr-acc\\simseatlock-openvr.log");
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

void Heartbeat() {
    GameBlock game{};
    game.Flags = RigFlags_GameLive;
    game.ViewCount = 1;
    game.Lqw = 1.f;
    game.Rqw = 1.f;
    gShm.WriteGame(game);
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
    pInit = reinterpret_cast<FnInit>(GetProcAddress(gOrig, "VR_InitInternal"));
    pInit2 = reinterpret_cast<FnInit2>(GetProcAddress(gOrig, "VR_InitInternal2"));
    pShutdown = reinterpret_cast<FnShutdown>(GetProcAddress(gOrig, "VR_ShutdownInternal"));
    pGipa = reinterpret_cast<FnGipa>(GetProcAddress(gOrig, "VR_GetGenericInterface"));
    pIsHmd = reinterpret_cast<FnBool>(GetProcAddress(gOrig, "VR_IsHmdPresent"));
    pIsRuntime = reinterpret_cast<FnBool>(GetProcAddress(gOrig, "VR_IsRuntimeInstalled"));
    Log("orig binds init=%d init2=%d shut=%d gipa=%d isHmd=%d", pInit != nullptr, pInit2 != nullptr,
        pShutdown != nullptr, pGipa != nullptr, pIsHmd != nullptr);
}

} // namespace

extern "C" {

__declspec(dllexport) int SSL_IsOpenVrProxy() { return 2; }

__declspec(dllexport) uint32_t VR_InitInternal(int* peError, int eType) {
    EnsureOrig();
    Log("VR_InitInternal type=%d orig=%p", eType, pInit);
    const uint32_t tok = pInit ? pInit(peError, eType) : 0;
    Log("VR_InitInternal -> %u err=%d", tok, peError ? *peError : -1);
    if (tok) Heartbeat();
    return tok;
}

__declspec(dllexport) uint32_t VR_InitInternal2(int* peError, int eType, const char* info) {
    EnsureOrig();
    Log("VR_InitInternal2 type=%d info=%s orig=%p", eType, info ? info : "(null)", pInit2);
    const uint32_t tok = pInit2 ? pInit2(peError, eType, info) : 0;
    Log("VR_InitInternal2 -> %u err=%d", tok, peError ? *peError : -1);
    if (tok) Heartbeat();
    return tok;
}

__declspec(dllexport) void VR_ShutdownInternal() {
    EnsureOrig();
    Log("VR_ShutdownInternal");
    if (pShutdown) pShutdown();
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
    if (iface) Heartbeat();
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

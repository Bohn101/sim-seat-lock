// SimSeatLock.Layer — thin OpenXR API layer (Khronos loader interface v1).
// Writes Local\\SimSeatLock.Game.v1 on negotiate + CreateInstance + xrLocateViews.
// Log: publish\\layer\\layer.log

#include "openxr_min.h"
#include "shm.h"
#include "math.h"

#include <stdarg.h>
#include <stdio.h>
#include <string.h>
#include <windows.h>

namespace {

constexpr char kLayerName[] = "XR_APILAYER_NOVENDOR_sim_seat_lock";

struct LayerState {
    PFN_xrGetInstanceProcAddr nextGipa = nullptr;
    PFN_xrCreateApiLayerInstance nextCreate = nullptr;
    PFN_xrLocateViews nextLocateViews = nullptr;
    PFN_xrDestroyInstance nextDestroyInstance = nullptr;
    SharedMemory shm;
    float eyeX = 0.f;
    float eyeY = 1.10f;
    float eyeZ = 0.27f;
    bool geometryLoaded = false;
};

LayerState g;

void Log(const char* fmt, ...) {
    char path[MAX_PATH]{};
    HMODULE self = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCSTR>(&Log), &self);
    if (self) GetModuleFileNameA(self, path, MAX_PATH);
    char* slash = strrchr(path, '\\');
    if (slash) {
        slash[1] = 0;
        strcat_s(path, "layer.log");
    } else {
        strcpy_s(path, "C:\\Users\\Bohnster\\sim-seat-lock\\publish\\layer\\layer.log");
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

void EnsureGeometry() {
    if (g.geometryLoaded) return;
    LoadGeometry(&g.eyeX, &g.eyeY, &g.eyeZ);
    g.geometryLoaded = true;
}

void WriteAlive(int32_t space, int32_t views, const XrView* xrViews, uint32_t n) {
    GameBlock game{};
    game.Flags = RigFlags_GameLive;
    game.SpaceType = space;
    game.ViewCount = views;
    game.Lqw = 1.f;
    game.Rqw = 1.f;
    if (xrViews && n >= 1) {
        game.Lpx = xrViews[0].pose.position.x;
        game.Lpy = xrViews[0].pose.position.y;
        game.Lpz = xrViews[0].pose.position.z;
        game.Lqx = xrViews[0].pose.orientation.x;
        game.Lqy = xrViews[0].pose.orientation.y;
        game.Lqz = xrViews[0].pose.orientation.z;
        game.Lqw = xrViews[0].pose.orientation.w;
    }
    if (xrViews && n >= 2) {
        game.Rpx = xrViews[1].pose.position.x;
        game.Rpy = xrViews[1].pose.position.y;
        game.Rpz = xrViews[1].pose.position.z;
        game.Rqx = xrViews[1].pose.orientation.x;
        game.Rqy = xrViews[1].pose.orientation.y;
        game.Rqz = xrViews[1].pose.orientation.z;
        game.Rqw = xrViews[1].pose.orientation.w;
    }
    g.shm.WriteGame(game);
}

XrResult XRAPI_CALL LayerLocateViews(XrSession session,
                                     const XrViewLocateInfo* viewLocateInfo,
                                     XrViewState* viewState,
                                     uint32_t viewCapacityInput,
                                     uint32_t* viewCountOutput,
                                     XrView* views) {
    if (!g.nextLocateViews) return XR_ERROR_FUNCTION_UNSUPPORTED;
    const XrResult result = g.nextLocateViews(session, viewLocateInfo, viewState, viewCapacityInput,
                                              viewCountOutput, views);
    if (result < 0) return result;
    if (!viewCountOutput || !views) return result;
    const uint32_t n = *viewCountOutput;
    if (viewCapacityInput == 0) return result;
    EnsureGeometry();
    RigBlock rig{};
    const bool haveRig = g.shm.TryReadRig(&rig);
    const bool armed = haveRig && rig.Armed != 0;
    const float eyeX = haveRig ? rig.EyeX : g.eyeX;
    const float eyeY = haveRig ? rig.EyeY : g.eyeY;
    const float eyeZ = haveRig ? rig.EyeZ : g.eyeZ;
    if (armed && n > 0) {
        for (uint32_t i = 0; i < n; ++i)
            views[i].pose = ApplyCompensate(views[i].pose, rig, eyeX, eyeY, eyeZ);
    }
    WriteAlive(viewLocateInfo ? static_cast<int32_t>(viewLocateInfo->viewConfigurationType) : 0,
               static_cast<int32_t>(n), views, n);
    return result;
}

XrResult XRAPI_CALL LayerDestroyInstance(XrInstance instance) {
    Log("xrDestroyInstance");
    if (g.nextDestroyInstance) return g.nextDestroyInstance(instance);
    return XR_SUCCESS;
}

XrResult XRAPI_CALL LayerGetInstanceProcAddr(XrInstance instance, const char* name, PFN_xrVoidFunction* function);

XrResult XRAPI_CALL LayerCreateApiLayerInstance(const XrInstanceCreateInfo* info,
                                                const XrApiLayerCreateInfo* apiLayerInfo,
                                                XrInstance* instance) {
    Log("CreateApiLayerInstance app=%s",
        (info && info->applicationInfo.applicationName[0]) ? info->applicationInfo.applicationName : "?");
    if (!info || !apiLayerInfo || !instance) return XR_ERROR_INITIALIZATION_FAILED;
    if (apiLayerInfo->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_CREATE_INFO)
        return XR_ERROR_INITIALIZATION_FAILED;
    if (!apiLayerInfo->nextInfo) {
        Log("nextInfo is null");
        return XR_ERROR_INITIALIZATION_FAILED;
    }
    XrApiLayerCreateInfo chain = *apiLayerInfo;
    chain.nextInfo = apiLayerInfo->nextInfo->next;
    g.nextGipa = apiLayerInfo->nextInfo->nextGetInstanceProcAddr;
    g.nextCreate = apiLayerInfo->nextInfo->nextCreateApiLayerInstance;
    if (!g.nextCreate || !g.nextGipa) {
        Log("missing nextCreate/nextGipa");
        return XR_ERROR_INITIALIZATION_FAILED;
    }
    const XrResult result = g.nextCreate(info, &chain, instance);
    Log("nextCreate -> %d", (int)result);
    if (result < 0) return result;
    g.nextGipa(*instance, "xrLocateViews", reinterpret_cast<PFN_xrVoidFunction*>(&g.nextLocateViews));
    g.nextGipa(*instance, "xrDestroyInstance", reinterpret_cast<PFN_xrVoidFunction*>(&g.nextDestroyInstance));
    EnsureGeometry();
    WriteAlive(0, 0, nullptr, 0);
    return result;
}

XrResult XRAPI_CALL LayerGetInstanceProcAddr(XrInstance instance, const char* name, PFN_xrVoidFunction* function) {
    if (!function) return XR_ERROR_FUNCTION_UNSUPPORTED;
    if (name && strcmp(name, "xrLocateViews") == 0) {
        *function = reinterpret_cast<PFN_xrVoidFunction>(LayerLocateViews);
        return XR_SUCCESS;
    }
    if (name && strcmp(name, "xrDestroyInstance") == 0) {
        *function = reinterpret_cast<PFN_xrVoidFunction>(LayerDestroyInstance);
        return XR_SUCCESS;
    }
    if (g.nextGipa) return g.nextGipa(instance, name, function);
    return XR_ERROR_FUNCTION_UNSUPPORTED;
}

} // namespace

extern "C" XRAPI_ATTR XrResult XRAPI_CALL xrNegotiateLoaderApiLayerInterface(
    const XrNegotiateLoaderInfo* loaderInfo, const char* layerName, XrNegotiateApiLayerRequest* apiLayerRequest) {
    Log("negotiate layerName=%s minIf=%u maxIf=%u",
        layerName ? layerName : "(null)",
        loaderInfo ? loaderInfo->minInterfaceVersion : 0,
        loaderInfo ? loaderInfo->maxInterfaceVersion : 0);
    if (!loaderInfo || !apiLayerRequest) return XR_ERROR_INITIALIZATION_FAILED;
    if (layerName && layerName[0] && strcmp(layerName, kLayerName) != 0) {
        Log("reject unexpected layerName");
        return XR_ERROR_INITIALIZATION_FAILED;
    }
    if (loaderInfo->structType != XR_LOADER_INTERFACE_STRUCT_LOADER_INFO) return XR_ERROR_INITIALIZATION_FAILED;
    if (apiLayerRequest->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_REQUEST)
        return XR_ERROR_INITIALIZATION_FAILED;
    if (loaderInfo->minInterfaceVersion > XR_CURRENT_LOADER_API_LAYER_VERSION ||
        loaderInfo->maxInterfaceVersion < XR_CURRENT_LOADER_API_LAYER_VERSION) {
        Log("interface version mismatch");
        return XR_ERROR_INITIALIZATION_FAILED;
    }
    apiLayerRequest->layerInterfaceVersion = XR_CURRENT_LOADER_API_LAYER_VERSION;
    apiLayerRequest->layerApiVersion = XR_CURRENT_API_VERSION;
    apiLayerRequest->getInstanceProcAddr = LayerGetInstanceProcAddr;
    apiLayerRequest->createApiLayerInstance = LayerCreateApiLayerInstance;
    WriteAlive(0, 0, nullptr, 0);
    Log("negotiate OK, Game.v1 heartbeat written");
    return XR_SUCCESS;
}

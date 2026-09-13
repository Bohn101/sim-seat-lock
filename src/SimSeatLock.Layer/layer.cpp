// SimSeatLock.Layer — thin OpenXR API layer (mbucchia template pattern,
// not OXRMC). Intercepts xrLocateViews only.
//
// Increment: identity passthrough + write Local\SimSeatLock.Game.v1.
// When Rig.v1 Armed != 0, apply T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd.

#include "openxr_min.h"
#include "shm.h"
#include "math.h"

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
    float eyeZ = 0.f;
    bool geometryLoaded = false;
};

LayerState g;

void EnsureGeometry() {
    if (g.geometryLoaded) return;
    LoadGeometry(&g.eyeX, &g.eyeY, &g.eyeZ);
    g.geometryLoaded = true;
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
        for (uint32_t i = 0; i < n; ++i) {
            views[i].pose = ApplyCompensate(views[i].pose, rig, eyeX, eyeY, eyeZ);
        }
    }

    GameBlock game{};
    game.Flags = RigFlags_GameLive;
    game.SpaceType = viewLocateInfo ? static_cast<int32_t>(viewLocateInfo->viewConfigurationType) : 0;
    game.ViewCount = static_cast<int32_t>(n);
    if (n >= 1) {
        game.Lpx = views[0].pose.position.x;
        game.Lpy = views[0].pose.position.y;
        game.Lpz = views[0].pose.position.z;
        game.Lqx = views[0].pose.orientation.x;
        game.Lqy = views[0].pose.orientation.y;
        game.Lqz = views[0].pose.orientation.z;
        game.Lqw = views[0].pose.orientation.w;
    } else {
        game.Lqw = 1.f;
    }
    if (n >= 2) {
        game.Rpx = views[1].pose.position.x;
        game.Rpy = views[1].pose.position.y;
        game.Rpz = views[1].pose.position.z;
        game.Rqx = views[1].pose.orientation.x;
        game.Rqy = views[1].pose.orientation.y;
        game.Rqz = views[1].pose.orientation.z;
        game.Rqw = views[1].pose.orientation.w;
    } else {
        game.Rqw = 1.f;
    }
    g.shm.WriteGame(game);
    return result;
}

XrResult XRAPI_CALL LayerDestroyInstance(XrInstance instance) {
    if (g.nextDestroyInstance) return g.nextDestroyInstance(instance);
    return XR_SUCCESS;
}

XrResult XRAPI_CALL LayerGetInstanceProcAddr(XrInstance instance, const char* name, PFN_xrVoidFunction* function);

XrResult XRAPI_CALL LayerCreateApiLayerInstance(const XrInstanceCreateInfo* info,
                                                const XrApiLayerCreateInfo* apiLayerInfo,
                                                XrInstance* instance) {
    if (!info || !apiLayerInfo || !instance) return XR_ERROR_INITIALIZATION_FAILED;
    if (apiLayerInfo->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_CREATE_INFO) {
        return XR_ERROR_INITIALIZATION_FAILED;
    }
    if (!apiLayerInfo->nextInfo) return XR_ERROR_INITIALIZATION_FAILED;

    XrApiLayerCreateInfo chain = *apiLayerInfo;
    chain.nextInfo = apiLayerInfo->nextInfo->next;
    g.nextGipa = apiLayerInfo->nextInfo->nextGetInstanceProcAddr;
    g.nextCreate = apiLayerInfo->nextInfo->nextCreateApiLayerInstance;
    if (!g.nextCreate || !g.nextGipa) return XR_ERROR_INITIALIZATION_FAILED;

    const XrResult result = g.nextCreate(info, &chain, instance);
    if (result < 0) return result;

    g.nextGipa(*instance, "xrLocateViews", reinterpret_cast<PFN_xrVoidFunction*>(&g.nextLocateViews));
    g.nextGipa(*instance, "xrDestroyInstance", reinterpret_cast<PFN_xrVoidFunction*>(&g.nextDestroyInstance));
    EnsureGeometry();
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
    if (!loaderInfo || !apiLayerRequest) return XR_ERROR_INITIALIZATION_FAILED;
    if (layerName && strcmp(layerName, kLayerName) != 0) return XR_ERROR_INITIALIZATION_FAILED;
    if (loaderInfo->structType != XR_LOADER_INTERFACE_STRUCT_LOADER_INFO) return XR_ERROR_INITIALIZATION_FAILED;
    if (apiLayerRequest->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_REQUEST) {
        return XR_ERROR_INITIALIZATION_FAILED;
    }
    if (loaderInfo->minInterfaceVersion > XR_CURRENT_LOADER_API_LAYER_VERSION ||
        loaderInfo->maxInterfaceVersion < XR_CURRENT_LOADER_API_LAYER_VERSION) {
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    apiLayerRequest->layerInterfaceVersion = XR_CURRENT_LOADER_API_LAYER_VERSION;
    apiLayerRequest->layerApiVersion = XR_CURRENT_API_VERSION;
    apiLayerRequest->getInstanceProcAddr = LayerGetInstanceProcAddr;
    apiLayerRequest->createApiLayerInstance = LayerCreateApiLayerInstance;
    return XR_SUCCESS;
}

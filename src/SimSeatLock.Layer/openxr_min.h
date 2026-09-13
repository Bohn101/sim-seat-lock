// Minimal OpenXR types for SimSeatLock.Layer.
// Values match Khronos OpenXR-SDK headers (Apache-2.0 OR MIT).
// Full SDK is not required to compile this increment.
#pragma once

#include <stdint.h>
#include <stddef.h>

#ifndef XRAPI_ATTR
#define XRAPI_ATTR
#endif
#ifndef XRAPI_CALL
#define XRAPI_CALL __stdcall
#endif
#ifndef XRAPI_PTR
#define XRAPI_PTR XRAPI_CALL
#endif
#ifndef XR_MAY_ALIAS
#define XR_MAY_ALIAS
#endif

#define XR_SUCCESS 0
#define XR_ERROR_INITIALIZATION_FAILED (-6)
#define XR_ERROR_FUNCTION_UNSUPPORTED (-7)

#define XR_TYPE_INSTANCE_CREATE_INFO 3
#define XR_TYPE_VIEW_LOCATE_INFO 6
#define XR_TYPE_VIEW 7
#define XR_TYPE_VIEW_STATE 11

#define XR_MAX_API_LAYER_NAME_SIZE 256
#define XR_MAX_APPLICATION_NAME_SIZE 128
#define XR_MAX_ENGINE_NAME_SIZE 128

#define XR_MAKE_VERSION(major, minor, patch) \
    ((((uint64_t)(major)) << 48) | (((uint64_t)(minor)) << 32) | ((uint64_t)(patch)))
#define XR_CURRENT_API_VERSION XR_MAKE_VERSION(1, 0, 0)

typedef uint64_t XrVersion;
typedef uint64_t XrFlags64;
typedef uint64_t XrInstance;
typedef uint64_t XrSession;
typedef uint64_t XrSpace;
typedef int64_t XrTime;
typedef int32_t XrResult;
typedef uint32_t XrStructureType;
typedef uint32_t XrViewConfigurationType;
typedef XrFlags64 XrViewStateFlags;
typedef XrFlags64 XrInstanceCreateFlags;
typedef void(XRAPI_PTR* PFN_xrVoidFunction)(void);

typedef struct XrQuaternionf {
    float x, y, z, w;
} XrQuaternionf;

typedef struct XrVector3f {
    float x, y, z;
} XrVector3f;

typedef struct XrPosef {
    XrQuaternionf orientation;
    XrVector3f position;
} XrPosef;

typedef struct XrFovf {
    float angleLeft, angleRight, angleUp, angleDown;
} XrFovf;

typedef struct XrView {
    XrStructureType type;
    void* XR_MAY_ALIAS next;
    XrPosef pose;
    XrFovf fov;
} XrView;

typedef struct XrViewLocateInfo {
    XrStructureType type;
    const void* XR_MAY_ALIAS next;
    XrViewConfigurationType viewConfigurationType;
    XrTime displayTime;
    XrSpace space;
} XrViewLocateInfo;

typedef struct XrViewState {
    XrStructureType type;
    void* XR_MAY_ALIAS next;
    XrViewStateFlags viewStateFlags;
} XrViewState;

typedef struct XrApplicationInfo {
    char applicationName[XR_MAX_APPLICATION_NAME_SIZE];
    uint32_t applicationVersion;
    char engineName[XR_MAX_ENGINE_NAME_SIZE];
    uint32_t engineVersion;
    XrVersion apiVersion;
} XrApplicationInfo;

typedef struct XrInstanceCreateInfo {
    XrStructureType type;
    const void* XR_MAY_ALIAS next;
    XrInstanceCreateFlags createFlags;
    XrApplicationInfo applicationInfo;
    uint32_t enabledApiLayerCount;
    const char* const* enabledApiLayerNames;
    uint32_t enabledExtensionCount;
    const char* const* enabledExtensionNames;
} XrInstanceCreateInfo;

#define XR_CURRENT_LOADER_API_LAYER_VERSION 1
#define XR_LOADER_INFO_STRUCT_VERSION 1
#define XR_API_LAYER_INFO_STRUCT_VERSION 1
#define XR_API_LAYER_NEXT_INFO_STRUCT_VERSION 1
#define XR_API_LAYER_CREATE_INFO_STRUCT_VERSION 1
#define XR_API_LAYER_MAX_SETTINGS_PATH_SIZE 512

typedef enum XrLoaderInterfaceStructs {
    XR_LOADER_INTERFACE_STRUCT_UNINTIALIZED = 0,
    XR_LOADER_INTERFACE_STRUCT_LOADER_INFO = 1,
    XR_LOADER_INTERFACE_STRUCT_API_LAYER_REQUEST = 2,
    XR_LOADER_INTERFACE_STRUCT_RUNTIME_REQUEST = 3,
    XR_LOADER_INTERFACE_STRUCT_API_LAYER_CREATE_INFO = 4,
    XR_LOADER_INTERFACE_STRUCT_API_LAYER_NEXT_INFO = 5
} XrLoaderInterfaceStructs;

typedef XrResult(XRAPI_PTR* PFN_xrGetInstanceProcAddr)(XrInstance instance, const char* name, PFN_xrVoidFunction* function);

typedef struct XrApiLayerCreateInfo XrApiLayerCreateInfo;

typedef XrResult(XRAPI_PTR* PFN_xrCreateApiLayerInstance)(
    const XrInstanceCreateInfo* info, const XrApiLayerCreateInfo* apiLayerInfo, XrInstance* instance);

typedef struct XrApiLayerNextInfo {
    XrLoaderInterfaceStructs structType;
    uint32_t structVersion;
    size_t structSize;
    char layerName[XR_MAX_API_LAYER_NAME_SIZE];
    PFN_xrGetInstanceProcAddr nextGetInstanceProcAddr;
    PFN_xrCreateApiLayerInstance nextCreateApiLayerInstance;
    struct XrApiLayerNextInfo* next;
} XrApiLayerNextInfo;

typedef struct XrApiLayerCreateInfo {
    XrLoaderInterfaceStructs structType;
    uint32_t structVersion;
    size_t structSize;
    void* XR_MAY_ALIAS loaderInstance;
    char settings_file_location[XR_API_LAYER_MAX_SETTINGS_PATH_SIZE];
    XrApiLayerNextInfo* nextInfo;
} XrApiLayerCreateInfo;

typedef struct XrNegotiateApiLayerRequest {
    XrLoaderInterfaceStructs structType;
    uint32_t structVersion;
    size_t structSize;
    uint32_t layerInterfaceVersion;
    XrVersion layerApiVersion;
    PFN_xrGetInstanceProcAddr getInstanceProcAddr;
    PFN_xrCreateApiLayerInstance createApiLayerInstance;
} XrNegotiateApiLayerRequest;

typedef struct XrNegotiateLoaderInfo {
    XrLoaderInterfaceStructs structType;
    uint32_t structVersion;
    size_t structSize;
    uint32_t minInterfaceVersion;
    uint32_t maxInterfaceVersion;
    XrVersion minApiVersion;
    XrVersion maxApiVersion;
} XrNegotiateLoaderInfo;

typedef XrResult(XRAPI_PTR* PFN_xrLocateViews)(XrSession session,
                                               const XrViewLocateInfo* viewLocateInfo,
                                               XrViewState* viewState,
                                               uint32_t viewCapacityInput,
                                               uint32_t* viewCountOutput,
                                               XrView* views);

typedef XrResult(XRAPI_PTR* PFN_xrDestroyInstance)(XrInstance instance);

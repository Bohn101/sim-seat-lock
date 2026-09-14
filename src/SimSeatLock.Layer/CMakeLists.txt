cmake_minimum_required(VERSION 3.20)
project(SimSeatLockLayer LANGUAGES CXX)
add_library(XR_APILAYER_NOVENDOR_sim_seat_lock SHARED
  layer.cpp
  math.cpp
  shm.cpp
  exports.def
)
target_compile_features(XR_APILAYER_NOVENDOR_sim_seat_lock PRIVATE cxx_std_17)
target_compile_definitions(XR_APILAYER_NOVENDOR_sim_seat_lock PRIVATE NOMINMAX _WINDOWS _USRDLL)
target_link_libraries(XR_APILAYER_NOVENDOR_sim_seat_lock PRIVATE kernel32 user32)
set_property(TARGET XR_APILAYER_NOVENDOR_sim_seat_lock PROPERTY
  MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
set_target_properties(XR_APILAYER_NOVENDOR_sim_seat_lock PROPERTIES
  OUTPUT_NAME XR_APILAYER_NOVENDOR_sim_seat_lock
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../../publish/layer"
)
add_custom_command(TARGET XR_APILAYER_NOVENDOR_sim_seat_lock POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    "${CMAKE_CURRENT_SOURCE_DIR}/XR_APILAYER_NOVENDOR_sim_seat_lock.json"
    "$<TARGET_FILE_DIR:XR_APILAYER_NOVENDOR_sim_seat_lock>/XR_APILAYER_NOVENDOR_sim_seat_lock.json"
)

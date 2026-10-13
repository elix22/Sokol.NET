cmake_minimum_required(VERSION 3.20)
project(sokol_appupdate C)

# sokol_appupdate — Google Play In-App Updates (Android only).
#   Android : sokol_appupdate_android.c (JNI -> com.sokol.appupdate.SokolAppUpdate) -> libs/android/<abi>/<config>/libsokol_appupdate.so
#   Others  : no native library — Play In-App Updates exist only on Android; the managed layer reports "unsupported".

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/AppUpdate/
set(LIB_NAME    "sokol_appupdate")

if(NOT ANDROID)
    message(STATUS "sokol_appupdate: Android only — nothing to build for ${CMAKE_SYSTEM_NAME}.")
    return()
endif()

add_library(${LIB_NAME} SHARED sokol_appupdate_android.c)
target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
target_compile_options(${LIB_NAME} PRIVATE -x c)
target_link_libraries(${LIB_NAME} PRIVATE android log)

# Link against the pre-built libsokol.so (for sapp_android_get_native_activity).
if(NOT DEFINED SOKOL_LIB_DIR)
    set(SOKOL_LIB_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../../libs/android/${ANDROID_ABI}/release")
endif()

set(SOKOL_SO "${SOKOL_LIB_DIR}/libsokol.so")
if(NOT EXISTS "${SOKOL_SO}")
    message(FATAL_ERROR
        "sokol_appupdate: libsokol.so not found at '${SOKOL_SO}'.\n"
        "Build the main sokol library first:\n"
        "  ./scripts/build-android-sokol-libraries.sh")
endif()

add_library(sokol SHARED IMPORTED)
set_target_properties(sokol PROPERTIES IMPORTED_LOCATION "${SOKOL_SO}")
target_link_libraries(${LIB_NAME} PRIVATE sokol)

set(ANDROID_OUT "${PLUGIN_ROOT}/libs/android/${ANDROID_ABI}/$<LOWER_CASE:$<CONFIG>>")
add_custom_command(TARGET ${LIB_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${ANDROID_OUT}"
    COMMAND ${CMAKE_COMMAND} -E copy
        "$<TARGET_FILE:${LIB_NAME}>"
        "${ANDROID_OUT}/$<TARGET_FILE_NAME:${LIB_NAME}>"
    COMMENT "Copying lib${LIB_NAME}.so → ${ANDROID_OUT}"
)

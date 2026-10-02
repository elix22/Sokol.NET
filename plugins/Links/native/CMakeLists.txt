cmake_minimum_required(VERSION 3.20)
project(sokol_links C)

# sokol_links — standalone native link-opening library.
#   Android : sokol_links_android.c (JNI: Intent ACTION_VIEW / ACTION_SENDTO)  -> libs/android/<abi>/<config>/libsokol_links.so
#   iOS     : sokol_links_apple.m   (UIApplication openURL)                   -> libs/ios/<target>/<config>/sokol_links.framework
#   macOS   : sokol_links_apple.m   (NSWorkspace openURL)                     -> libs/macos/<arch>/<config>/libsokol_links.dylib
#   Web     : sokol_links_web.c     (window.open / anchor click via EM_JS)    -> libs/emscripten/x86/<config>/sokol_links.a
#   Windows + Linux: no native library — the managed layer uses ShellExecute / xdg-open.

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Links/
set(LIB_NAME    "sokol_links")

# ── Windows / Linux early-out ─────────────────────────────────────────────────
if(NOT ANDROID AND NOT EMSCRIPTEN AND NOT APPLE)
    message(STATUS
        "sokol_links: ${CMAKE_SYSTEM_NAME} uses the managed backend (ShellExecute / xdg-open) — "
        "no native library needed. Nothing to build.")
    return()
endif()

# ── Android ───────────────────────────────────────────────────────────────────
if(ANDROID)
    add_library(${LIB_NAME} SHARED sokol_links_android.c)
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
            "sokol_links: libsokol.so not found at '${SOKOL_SO}'.\n"
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

# ── Web (Emscripten) ──────────────────────────────────────────────────────────
elseif(EMSCRIPTEN)
    add_library(${LIB_NAME} STATIC sokol_links_web.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

    set(WEB_OUT "${PLUGIN_ROOT}/libs/emscripten/x86/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${WEB_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${WEB_OUT}/${LIB_NAME}.a"
        COMMENT "Copying ${LIB_NAME}.a → ${WEB_OUT}"
    )

# ── macOS ─────────────────────────────────────────────────────────────────────
elseif(APPLE AND NOT CMAKE_SYSTEM_NAME STREQUAL "iOS")
    add_library(${LIB_NAME} SHARED sokol_links_apple.m)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    set_source_files_properties(sokol_links_apple.m PROPERTIES COMPILE_OPTIONS "-x;objective-c;-fobjc-arc")
    target_link_libraries(${LIB_NAME} PRIVATE "-framework Foundation" "-framework AppKit")

    if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64")
        set(MACOS_ARCH_DIR "X64")
    else()
        set(MACOS_ARCH_DIR "arm64")
    endif()

    set(MACOS_OUT "${PLUGIN_ROOT}/libs/macos/${MACOS_ARCH_DIR}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${MACOS_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${MACOS_OUT}/$<TARGET_FILE_NAME:${LIB_NAME}>"
        COMMENT "Copying lib${LIB_NAME}.dylib → ${MACOS_OUT}"
    )

# ── iOS ───────────────────────────────────────────────────────────────────────
elseif(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    add_library(${LIB_NAME} SHARED sokol_links_apple.m)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    set_source_files_properties(sokol_links_apple.m PROPERTIES COMPILE_OPTIONS "-x;objective-c;-fobjc-arc")
    target_link_libraries(${LIB_NAME} PRIVATE "-framework Foundation" "-framework UIKit")

    set_target_properties(${LIB_NAME} PROPERTIES
        FRAMEWORK                        TRUE
        MACOSX_FRAMEWORK_IDENTIFIER      com.sokolnet.plugins.links
        FRAMEWORK_VERSION                A
        XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"
    )

    if(CMAKE_OSX_SYSROOT MATCHES "iphonesimulator")
        if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64")
            set(IOS_ARCH_DIR "simulator-x64")
        else()
            set(IOS_ARCH_DIR "simulator-arm64")
        endif()
    else()
        set(IOS_ARCH_DIR "arm64")
    endif()

    set(IOS_OUT "${PLUGIN_ROOT}/libs/ios/${IOS_ARCH_DIR}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${IOS_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy_directory
            "$<TARGET_BUNDLE_DIR:${LIB_NAME}>"
            "${IOS_OUT}/${LIB_NAME}.framework"
        COMMENT "Copying ${LIB_NAME}.framework → ${IOS_OUT}"
    )
endif()

cmake_minimum_required(VERSION 3.20)
project(sokol_billing C)

# sokol_billing — standalone native in-app-purchase library.
# Android (Play Billing) and iOS (StoreKit 2) only; desktop/web builds have no
# store and the managed layer (Billing.cs) stubs everything out.
#
# Android build (output → plugins/Billing/libs/android/<abi>/<config>/):
#   cmake -S . -B build-android-<abi> \
#     -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
#     -DANDROID_ABI=arm64-v8a \
#     -DANDROID_PLATFORM=android-26
#
# iOS build (output → plugins/Billing/libs/ios/<target>/<config>/):
#   cmake -S . -B build-ios-device -G Xcode \
#     -DCMAKE_SYSTEM_NAME=iOS \
#     -DCMAKE_OSX_ARCHITECTURES=arm64 \
#     -DCMAKE_OSX_SYSROOT=iphoneos \
#     -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0     # StoreKit 2 floor

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Billing/
set(LIB_NAME    "sokol_billing")

# ── Desktop/web early-out ─────────────────────────────────────────────────────
if(NOT ANDROID AND NOT (CMAKE_SYSTEM_NAME STREQUAL "iOS"))
    message(STATUS
        "sokol_billing: no store on ${CMAKE_SYSTEM_NAME} — managed layer stubs "
        "billing out. Nothing to build.")
    return()
endif()

# ── Android ───────────────────────────────────────────────────────────────────
if(ANDROID)
    add_library(${LIB_NAME} SHARED
        sokol_billing_android.c
        sokol_billing_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    target_compile_options(${LIB_NAME} PRIVATE -x c)
    target_link_libraries(${LIB_NAME} PRIVATE android log)
    if(ANDROID_ABI STREQUAL "arm64-v8a" OR ANDROID_ABI STREQUAL "x86_64")
        target_link_options(${LIB_NAME} PRIVATE "-Wl,-z,max-page-size=16384")   # Google Play: 16 KB pages (whatever the NDK's default)
    endif()

    # Link against the pre-built libsokol.so (for sapp_android_get_native_activity).
    if(NOT DEFINED SOKOL_LIB_DIR)
        set(SOKOL_LIB_DIR
            "${CMAKE_CURRENT_SOURCE_DIR}/../../../libs/android/${ANDROID_ABI}/release")
    endif()

    set(SOKOL_SO "${SOKOL_LIB_DIR}/libsokol.so")
    if(NOT EXISTS "${SOKOL_SO}")
        message(FATAL_ERROR
            "sokol_billing: libsokol.so not found at '${SOKOL_SO}'.\n"
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

# ── iOS ───────────────────────────────────────────────────────────────────────
elseif(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    enable_language(Swift)

    # The event queue is plain C in its own static lib; the Swift framework
    # links it in, and @_silgen_name resolves sokolbilling__emit at link time
    # (avoids a mixed-language target, which not every generator supports).
    add_library(${LIB_NAME}_queue STATIC sokol_billing_queue.c)
    target_include_directories(${LIB_NAME}_queue PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

    add_library(${LIB_NAME} SHARED sokol_billing_ios.swift)
    target_link_libraries(${LIB_NAME} PRIVATE ${LIB_NAME}_queue "-framework StoreKit")

    set_target_properties(${LIB_NAME} PROPERTIES
        FRAMEWORK                        TRUE
        MACOSX_FRAMEWORK_IDENTIFIER      com.sokolnet.plugins.billing
        FRAMEWORK_VERSION                A
        XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"
        XCODE_ATTRIBUTE_SWIFT_VERSION    "5.0"
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

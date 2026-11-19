cmake_minimum_required(VERSION 3.20)
project(sokol_review C)

# sokol_review — the platform's own in-app rating prompt.
#   Android : sokol_review_android.c (JNI -> com.sokol.review.SokolReview, Play In-App Review) -> libs/android/<abi>/<config>/libsokol_review.so
#   iOS     : sokol_review_apple.swift (StoreKit AppStore.requestReview(in:), iOS 16+; none on iOS 15) -> libs/ios/<target>/<config>/sokol_review.framework
#   Others  : no native library — no in-app rating API; the managed layer reports "unsupported".

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Review/
set(LIB_NAME    "sokol_review")

if(ANDROID)
    add_library(${LIB_NAME} SHARED sokol_review_android.c)
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
            "sokol_review: libsokol.so not found at '${SOKOL_SO}'.\n"
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

elseif(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    # Swift: AppStore.requestReview(in:) — the current StoreKit API (iOS 16+) — is Swift-only.
    enable_language(Swift)
    add_library(${LIB_NAME} SHARED sokol_review_apple.swift)
    target_link_libraries(${LIB_NAME} PRIVATE "-framework Foundation" "-framework UIKit" "-framework StoreKit")

    set_target_properties(${LIB_NAME} PROPERTIES
        Swift_LANGUAGE_VERSION           5
        XCODE_ATTRIBUTE_SWIFT_VERSION    5.0
        FRAMEWORK                        TRUE
        MACOSX_FRAMEWORK_IDENTIFIER      com.sokolnet.plugins.review
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

else()
    message(STATUS "sokol_review: Android and iOS only — nothing to build for ${CMAKE_SYSTEM_NAME}.")
endif()

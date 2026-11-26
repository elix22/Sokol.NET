cmake_minimum_required(VERSION 3.20)
project(sokol_speech C)

# sokol_speech — standalone native text-to-speech library.
#   Android : sokol_speech_android.c (JNI -> com.sokol.speech.SokolSpeech)      -> libs/android/<abi>/<config>/libsokol_speech.so
#   iOS     : sokol_speech_apple.m   (AVSpeechSynthesizer)                       -> libs/ios/<target>/<config>/sokol_speech.framework
#   macOS   : sokol_speech_apple.m   (AVSpeechSynthesizer)                       -> libs/macos/<arch>/<config>/libsokol_speech.dylib
#   Windows : sokol_speech_winrt.cpp (Windows.Media.SpeechSynthesis, C++/WinRT)  -> libs/windows/<arch>/<config>/sokol_speech.dll
#   Web     : sokol_speech_web.c     (speechSynthesis via EM_JS)                 -> libs/emscripten/x86/<config>/sokol_speech.a
#   Linux   : no native library — the managed layer drives spd-say (speech-dispatcher) as a process.
#
# Every backend links sokol_speech_queue.c (the event ring) so sokolspeech_poll_event
# behaves identically everywhere.

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Speech/
set(LIB_NAME    "sokol_speech")

# ── Linux early-out ───────────────────────────────────────────────────────────
if(NOT ANDROID AND NOT EMSCRIPTEN AND NOT WIN32 AND NOT APPLE)
    message(STATUS
        "sokol_speech: ${CMAKE_SYSTEM_NAME} uses the managed spd-say backend — "
        "no native library needed. Nothing to build.")
    return()
endif()

# ── Android ───────────────────────────────────────────────────────────────────
if(ANDROID)
    add_library(${LIB_NAME} SHARED
        sokol_speech_android.c
        sokol_speech_queue.c)
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
            "sokol_speech: libsokol.so not found at '${SOKOL_SO}'.\n"
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
    add_library(${LIB_NAME} STATIC
        sokol_speech_web.c
        sokol_speech_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

    set(WEB_OUT "${PLUGIN_ROOT}/libs/emscripten/x86/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${WEB_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${WEB_OUT}/${LIB_NAME}.a"
        COMMENT "Copying ${LIB_NAME}.a → ${WEB_OUT}"
    )

# ── Windows (C++/WinRT) ───────────────────────────────────────────────────────
elseif(WIN32)
    enable_language(CXX)
    add_library(${LIB_NAME} SHARED
        sokol_speech_winrt.cpp
        sokol_speech_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    # C++/WinRT needs C++20 (native coroutines), exceptions, and bigobj for the
    # heavy template instantiations (same flags as nearnet's WinRT backend).
    set_source_files_properties(sokol_speech_winrt.cpp
        PROPERTIES COMPILE_FLAGS "/std:c++20 /EHsc /bigobj")
    target_link_libraries(${LIB_NAME} PRIVATE windowsapp shell32)
    # MSVC exports nothing from a DLL unless told to (clang/gcc export by default): the DLL had no sokolspeech_*
    # entry point, and every call from C# failed with EntryPointNotFoundException.
    set_target_properties(${LIB_NAME} PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)

    if(CMAKE_GENERATOR_PLATFORM MATCHES "ARM64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "ARM64")
        set(WIN_ARCH_DIR "arm64")
    else()
        set(WIN_ARCH_DIR "x64")
    endif()
    set(WIN_OUT "${PLUGIN_ROOT}/libs/windows/${WIN_ARCH_DIR}/$<LOWER_CASE:$<CONFIG>>")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${WIN_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy
            "$<TARGET_FILE:${LIB_NAME}>"
            "${WIN_OUT}/$<TARGET_FILE_NAME:${LIB_NAME}>"
        COMMENT "Copying ${LIB_NAME}.dll → ${WIN_OUT}"
    )

# ── macOS ─────────────────────────────────────────────────────────────────────
elseif(APPLE AND NOT CMAKE_SYSTEM_NAME STREQUAL "iOS")
    add_library(${LIB_NAME} SHARED
        sokol_speech_apple.m
        sokol_speech_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    set_source_files_properties(sokol_speech_apple.m PROPERTIES COMPILE_OPTIONS "-x;objective-c;-fobjc-arc")
    target_link_libraries(${LIB_NAME} PRIVATE "-framework AVFoundation" "-framework AppKit")

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
    add_library(${LIB_NAME} SHARED
        sokol_speech_apple.m
        sokol_speech_queue.c)
    target_include_directories(${LIB_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
    set_source_files_properties(sokol_speech_apple.m PROPERTIES COMPILE_OPTIONS "-x;objective-c;-fobjc-arc")
    target_link_libraries(${LIB_NAME} PRIVATE "-framework AVFoundation" "-framework UIKit")

    set_target_properties(${LIB_NAME} PROPERTIES
        FRAMEWORK                        TRUE
        MACOSX_FRAMEWORK_IDENTIFIER      com.sokolnet.plugins.speech
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

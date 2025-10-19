cmake_minimum_required(VERSION 3.16)
project(ozzutil VERSION 1.0.0 LANGUAGES CXX C)

# Platform detection
set(PLATFORM_WEB FALSE)
set(PLATFORM_IOS FALSE)
set(PLATFORM_ANDROID FALSE)
set(PLATFORM_MACOS FALSE)
set(PLATFORM_WINDOWS FALSE)
set(PLATFORM_LINUX FALSE)

if(EMSCRIPTEN)
    set(PLATFORM_WEB TRUE)
    message(STATUS "Building for Web/Emscripten")
elseif(APPLE)
    if(IOS)
        set(PLATFORM_IOS TRUE)
        message(STATUS "Building for iOS")
    else()
        set(PLATFORM_MACOS TRUE)
        message(STATUS "Building for macOS")
    endif()
elseif(ANDROID)
    set(PLATFORM_ANDROID TRUE)
    message(STATUS "Building for Android")
elseif(WIN32)
    set(PLATFORM_WINDOWS TRUE)
    message(STATUS "Building for Windows")
else()
    set(PLATFORM_LINUX TRUE)
    message(STATUS "Building for Linux")
endif()

# Determine library type based on platform
# Web uses static libraries
# iOS uses frameworks
# Desktop platforms (macOS, Windows, Linux) and Android use dynamic libraries
if(PLATFORM_WEB)
    set(OZZUTIL_LIBRARY_TYPE STATIC)
    message(STATUS "Building STATIC library for ${CMAKE_SYSTEM_NAME}")
elseif(PLATFORM_IOS)
    set(OZZUTIL_LIBRARY_TYPE SHARED)  # Framework will be created from dynamic lib
    message(STATUS "Building FRAMEWORK for ${CMAKE_SYSTEM_NAME}")
else()
    set(OZZUTIL_LIBRARY_TYPE SHARED)
    message(STATUS "Building SHARED library for ${CMAKE_SYSTEM_NAME}")
endif()

# Ensure static library has correct name without 'lib' prefix for Emscripten
if(PLATFORM_WEB)
    set(CMAKE_STATIC_LIBRARY_PREFIX "")
endif()

# Paths
set(EXT_ROOT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/..")
set(OZZUTIL_INCLUDE_DIR "${CMAKE_CURRENT_SOURCE_DIR}")
set(OZZ_ANIMATION_ROOT "${EXT_ROOT_DIR}/ozz-animation")

# Source files
set(OZZUTIL_SOURCES
    "${CMAKE_CURRENT_SOURCE_DIR}/ozzutil.cc"
    "${CMAKE_CURRENT_SOURCE_DIR}/ozzutil.c"
    "${OZZ_ANIMATION_ROOT}/samples/framework/mesh.cc"
)

set(OZZUTIL_HEADERS
    "${CMAKE_CURRENT_SOURCE_DIR}/ozzutil.h"
    "${OZZ_ANIMATION_ROOT}/samples/framework/mesh.h"
)

# Verify source files exist
foreach(SOURCE_FILE ${OZZUTIL_SOURCES})
    if(NOT EXISTS "${SOURCE_FILE}")
        message(FATAL_ERROR "Source file not found: ${SOURCE_FILE}")
    endif()
endforeach()

message(STATUS "Found ozzutil source files: ${OZZUTIL_SOURCES}")

# Create the library
add_library(ozzutil ${OZZUTIL_LIBRARY_TYPE} ${OZZUTIL_SOURCES} ${OZZUTIL_HEADERS})

# Set C++ standard
set_target_properties(ozzutil PROPERTIES
    CXX_STANDARD 17
    CXX_STANDARD_REQUIRED YES
)

# Ensure .c files are compiled as C
set_source_files_properties("${CMAKE_CURRENT_SOURCE_DIR}/ozzutil.c" PROPERTIES LANGUAGE C)

# Public include directories
target_include_directories(ozzutil PUBLIC
    ${OZZUTIL_INCLUDE_DIR}
    ${EXT_ROOT_DIR}  # For sokol_defines.h and sokol headers
    ${EXT_ROOT_DIR}/sokol  # For sokol_gfx.h
    ${OZZ_ANIMATION_ROOT}/include  # For ozz-animation headers
    ${OZZ_ANIMATION_ROOT}/samples  # For ozz-animation samples framework
)

# Find and link sokol library
set(LIBS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../libs")

# Determine sokol library path based on platform and architecture
if(PLATFORM_MACOS)
    if(CMAKE_OSX_ARCHITECTURES STREQUAL "arm64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64")
        set(SOKOL_LIB_PATH "${LIBS_DIR}/macos/arm64/release/libsokol.dylib")
    else()
        set(SOKOL_LIB_PATH "${LIBS_DIR}/macos/x86_64/release/libsokol.dylib")
    endif()
elseif(PLATFORM_WINDOWS)
    set(SOKOL_LIB_PATH "${LIBS_DIR}/windows/x64/release/sokol.lib")
elseif(PLATFORM_LINUX)
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
        set(SOKOL_LIB_PATH "${LIBS_DIR}/linux/arm64/release/libsokol.so")
    else()
        set(SOKOL_LIB_PATH "${LIBS_DIR}/linux/x86_64/release/libsokol.so")
    endif()
elseif(PLATFORM_WEB)
    set(SOKOL_LIB_PATH "${LIBS_DIR}/emscripten/x86/release/sokol.a")
elseif(PLATFORM_ANDROID)
    if(ANDROID_ABI STREQUAL "arm64-v8a")
        set(SOKOL_LIB_PATH "${LIBS_DIR}/android/arm64-v8a/release/libsokol.so")
    elseif(ANDROID_ABI STREQUAL "armeabi-v7a")
        set(SOKOL_LIB_PATH "${LIBS_DIR}/android/armeabi-v7a/release/libsokol.so")
    elseif(ANDROID_ABI STREQUAL "x86_64")
        set(SOKOL_LIB_PATH "${LIBS_DIR}/android/x86_64/release/libsokol.so")
    elseif(ANDROID_ABI STREQUAL "x86")
        set(SOKOL_LIB_PATH "${LIBS_DIR}/android/x86/release/libsokol.so")
    else()
        message(WARNING "Unsupported Android ABI: ${ANDROID_ABI}")
    endif()
elseif(PLATFORM_IOS)
    # iOS uses a universal framework
    set(SOKOL_LIB_PATH "${LIBS_DIR}/ios/arm64/sokol.framework")
endif()

# Link sokol library if found
if(SOKOL_LIB_PATH AND EXISTS "${SOKOL_LIB_PATH}")
    target_link_libraries(ozzutil PUBLIC "${SOKOL_LIB_PATH}")
    message(STATUS "Linking with sokol library: ${SOKOL_LIB_PATH}")
else()
    message(WARNING "Sokol library not found at: ${SOKOL_LIB_PATH}")
endif()

# Find and link ozz-animation libraries
set(OZZ_LIBS_DIR "${OZZ_ANIMATION_ROOT}/bin")

# Determine ozz-animation library paths based on platform and architecture
if(PLATFORM_MACOS)
    if(CMAKE_OSX_ARCHITECTURES STREQUAL "arm64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64")
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/macos/arm64/release")
    else()
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/macos/x86_64/release")
    endif()
elseif(PLATFORM_WINDOWS)
    set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/windows/x64/release")
elseif(PLATFORM_LINUX)
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/linux/arm64/release")
    else()
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/linux/x86_64/release")
    endif()
elseif(PLATFORM_WEB)
    set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/emscripten/wasm32/release")
elseif(PLATFORM_ANDROID)
    if(ANDROID_ABI STREQUAL "arm64-v8a")
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/android/arm64-v8a/release")
    elseif(ANDROID_ABI STREQUAL "armeabi-v7a")
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/android/armeabi-v7a/release")
    elseif(ANDROID_ABI STREQUAL "x86_64")
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/android/x86_64/release")
    elseif(ANDROID_ABI STREQUAL "x86")
        set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/android/x86/release")
    else()
        message(WARNING "Unsupported Android ABI: ${ANDROID_ABI}")
    endif()
elseif(PLATFORM_IOS)
    set(OZZ_PLATFORM_DIR "${OZZ_LIBS_DIR}/ios/arm64/release")
endif()

# Link ozz-animation libraries in correct order (dependencies first)
set(OZZ_REQUIRED_LIBS
    "libozz_base.a"
    "libozz_geometry.a" 
    "libozz_animation.a"
    "libozz_animation_offline.a"
    "libozz_options.a"
)

if(OZZ_PLATFORM_DIR)
    foreach(OZZ_LIB ${OZZ_REQUIRED_LIBS})
        set(OZZ_LIB_PATH "${OZZ_PLATFORM_DIR}/${OZZ_LIB}")
        if(EXISTS "${OZZ_LIB_PATH}")
            target_link_libraries(ozzutil PUBLIC "${OZZ_LIB_PATH}")
            message(STATUS "Linking with ozz library: ${OZZ_LIB_PATH}")
        else()
            message(WARNING "OZZ library not found: ${OZZ_LIB_PATH}")
        endif()
    endforeach()
else()
    message(FATAL_ERROR "Could not determine ozz-animation platform directory")
endif()

# Compiler flags
if(MSVC)
    # Windows-specific flags
    target_compile_options(ozzutil PRIVATE
        /W3
        /wd4244  # conversion from 'type1' to 'type2', possible loss of data
        /wd4267  # conversion from 'size_t' to 'type', possible loss of data
    )
else()
    # GCC/Clang flags for all other platforms
    target_compile_options(ozzutil PRIVATE
        -Wall
        -Wno-unused-function
        -Wno-unused-parameter
    )
    
    # Additional Apple-specific warnings
    if(APPLE)
        target_compile_options(ozzutil PRIVATE
            -Wno-shorten-64-to-32
        )
    endif()
endif()

# Platform-specific settings
if(PLATFORM_WEB)
    # Emscripten-specific settings
    target_compile_options(ozzutil PRIVATE
        -DSOKOL_GLES3
    )
    
elseif(PLATFORM_IOS)
    # iOS Framework creation
    set_target_properties(ozzutil PROPERTIES
        FRAMEWORK TRUE
        FRAMEWORK_VERSION A
        MACOSX_FRAMEWORK_IDENTIFIER com.sokol.ozzutil
        MACOSX_FRAMEWORK_INFO_PLIST "${CMAKE_CURRENT_SOURCE_DIR}/Info.plist"
        XCODE_ATTRIBUTE_IPHONEOS_DEPLOYMENT_TARGET "13.0"
        PUBLIC_HEADER "${OZZUTIL_HEADERS}"
    )
    
elseif(PLATFORM_MACOS)
    # macOS-specific settings for dynamic library
    set_target_properties(ozzutil PROPERTIES
        INSTALL_NAME_DIR "@rpath"
        BUILD_WITH_INSTALL_RPATH TRUE
        INSTALL_RPATH "@loader_path"
        MACOSX_RPATH TRUE
    )
    
elseif(PLATFORM_WINDOWS)
    # Windows-specific settings
    if(OZZUTIL_LIBRARY_TYPE STREQUAL "SHARED")
        # Export all symbols for DLL
        set_target_properties(ozzutil PROPERTIES
            WINDOWS_EXPORT_ALL_SYMBOLS TRUE
        )
    endif()
    
elseif(PLATFORM_ANDROID)
    # Android-specific settings
    target_compile_options(ozzutil PRIVATE
        -DSOKOL_GLES3
    )
    
elseif(PLATFORM_LINUX)
    # Linux-specific settings
    set_target_properties(ozzutil PROPERTIES
        INSTALL_RPATH "$ORIGIN"
    )
endif()

# Optional: Link to sokol if it's available as a target
if(TARGET sokol)
    target_link_libraries(ozzutil PUBLIC sokol)
    message(STATUS "Linking ozzutil with sokol target")
endif()

# Determine output library directory based on platform and architecture
set(LIBS_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/libs")
set(BUILD_TYPE_LOWER "release")
if(CMAKE_BUILD_TYPE)
    string(TOLOWER "${CMAKE_BUILD_TYPE}" BUILD_TYPE_LOWER)
endif()

if(PLATFORM_MACOS)
    if(CMAKE_OSX_ARCHITECTURES STREQUAL "arm64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/macos/arm64/${BUILD_TYPE_LOWER}")
    else()
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/macos/x86_64/${BUILD_TYPE_LOWER}")
    endif()
    set(LIBRARY_FILE_NAME "libozzutil.dylib")
elseif(PLATFORM_WINDOWS)
    set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/windows/x64/${BUILD_TYPE_LOWER}")
    if(OZZUTIL_LIBRARY_TYPE STREQUAL "SHARED")
        set(LIBRARY_FILE_NAME "ozzutil.dll")
    else()
        set(LIBRARY_FILE_NAME "ozzutil.lib")
    endif()
elseif(PLATFORM_LINUX)
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/linux/arm64/${BUILD_TYPE_LOWER}")
    else()
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/linux/x86_64/${BUILD_TYPE_LOWER}")
    endif()
    set(LIBRARY_FILE_NAME "libozzutil.so")
elseif(PLATFORM_WEB)
    set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/emscripten/x86/${BUILD_TYPE_LOWER}")
    set(LIBRARY_FILE_NAME "ozzutil.a")  # No 'lib' prefix for Emscripten
elseif(PLATFORM_ANDROID)
    if(ANDROID_ABI STREQUAL "arm64-v8a")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/arm64-v8a/${BUILD_TYPE_LOWER}")
    elseif(ANDROID_ABI STREQUAL "armeabi-v7a")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/armeabi-v7a/${BUILD_TYPE_LOWER}")
    elseif(ANDROID_ABI STREQUAL "x86_64")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/x86_64/${BUILD_TYPE_LOWER}")
    elseif(ANDROID_ABI STREQUAL "x86")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/x86/${BUILD_TYPE_LOWER}")
    else()
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/${ANDROID_ABI}/${BUILD_TYPE_LOWER}")
    endif()
    set(LIBRARY_FILE_NAME "libozzutil.so")
elseif(PLATFORM_IOS)
    set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/ios/${BUILD_TYPE_LOWER}")
    set(LIBRARY_FILE_NAME "ozzutil.framework")
endif()

# Add post-build command to copy library to libs folder
if(LIBS_PLATFORM_DIR)
    if(PLATFORM_MACOS)
        # macOS: Code sign the library before copying
        add_custom_command(TARGET ozzutil POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${LIBS_PLATFORM_DIR}"
            COMMAND codesign --force --sign - "$<TARGET_FILE:ozzutil>"
            COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:ozzutil>" "${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
            COMMENT "Signing and copying ozzutil library to ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
        )
    elseif(PLATFORM_IOS)
        # iOS: Copy the entire framework
        add_custom_command(TARGET ozzutil POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${LIBS_PLATFORM_DIR}"
            COMMAND ${CMAKE_COMMAND} -E remove_directory "${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
            COMMAND ${CMAKE_COMMAND} -E copy_directory "$<TARGET_BUNDLE_DIR:ozzutil>" "${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
            COMMENT "Copying ozzutil framework to ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
        )
    else()
        # Other platforms: Just copy the library
        add_custom_command(TARGET ozzutil POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${LIBS_PLATFORM_DIR}"
            COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:ozzutil>" "${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
            COMMENT "Copying ozzutil library to ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
        )
    endif()
    message(STATUS "Library will be copied to: ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}")
endif()

# Installation rules (optional)
if(NOT PLATFORM_ANDROID AND NOT PLATFORM_IOS)
    include(GNUInstallDirs)
    
    install(TARGETS ozzutil
        LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    )
    
    install(FILES ${OZZUTIL_HEADERS}
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/ozzutil
    )
endif()

# Debug output
message(STATUS "=== OzzUtil Build Configuration ===")
message(STATUS "Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "Library Type: ${OZZUTIL_LIBRARY_TYPE}")
message(STATUS "Source Directory: ${CMAKE_CURRENT_SOURCE_DIR}")
message(STATUS "OZZ Animation Root: ${OZZ_ANIMATION_ROOT}")
message(STATUS "OZZ Platform Dir: ${OZZ_PLATFORM_DIR}")
message(STATUS "Number of Sources: ${OZZUTIL_SOURCES}")
message(STATUS "=====================================")

cmake_minimum_required(VERSION 3.16)
project(spine-c VERSION 4.2.0 LANGUAGES C)

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
# Web, iOS, and Android typically use static libraries
# Desktop platforms (macOS, Windows, Linux) use dynamic libraries
if(PLATFORM_WEB OR PLATFORM_IOS)
    set(SPINE_LIBRARY_TYPE STATIC)
    message(STATUS "Building STATIC library for ${CMAKE_SYSTEM_NAME}")
else()
    set(SPINE_LIBRARY_TYPE SHARED)
    message(STATUS "Building SHARED library for ${CMAKE_SYSTEM_NAME}")
endif()

# Android can be either, but we'll default to SHARED
if(PLATFORM_ANDROID AND NOT DEFINED ANDROID_SPINE_STATIC)
    set(SPINE_LIBRARY_TYPE SHARED)
endif()

# Ensure static library has correct name without 'lib' prefix for Emscripten (like sokol.a)
if(PLATFORM_WEB)
    set(CMAKE_STATIC_LIBRARY_PREFIX "")
endif()

# Paths
set(EXT_ROOT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/..")
set(SPINE_INCLUDE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/include")
set(SPINE_SRC_DIR "${CMAKE_CURRENT_SOURCE_DIR}/src/spine")

# Collect all spine-c source files
file(GLOB SPINE_C_SOURCES
    "${SPINE_SRC_DIR}/*.c"
)

# Add sokol_spine_extension.c
set(SOKOL_SPINE_EXTENSION "${CMAKE_CURRENT_SOURCE_DIR}/sokol_spine_extension.c")
if(EXISTS "${SOKOL_SPINE_EXTENSION}")
    list(APPEND SPINE_C_SOURCES "${SOKOL_SPINE_EXTENSION}")
    message(STATUS "Including sokol_spine_extension.c")
else()
    message(WARNING "sokol_spine_extension.c not found at ${SOKOL_SPINE_EXTENSION}")
endif()

# Verify we found sources
list(LENGTH SPINE_C_SOURCES SPINE_SOURCE_COUNT)
if(SPINE_SOURCE_COUNT EQUAL 0)
    message(FATAL_ERROR "No spine-c source files found in ${SPINE_SRC_DIR}")
endif()

message(STATUS "Found ${SPINE_SOURCE_COUNT} spine-c source files")

# Create the library
add_library(spine-c ${SPINE_LIBRARY_TYPE} ${SPINE_C_SOURCES})

# Public include directories
target_include_directories(spine-c PUBLIC
    ${SPINE_INCLUDE_DIR}
    ${EXT_ROOT_DIR}  # For sokol_defines.h and sokol headers needed by sokol_spine_extension.c
    ${EXT_ROOT_DIR}/sokol  # For sokol_gfx.h
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
    else()
        message(WARNING "Unsupported Android ABI: ${ANDROID_ABI}")
    endif()
endif()

# Link sokol library if found
if(SOKOL_LIB_PATH AND EXISTS "${SOKOL_LIB_PATH}")
    target_link_libraries(spine-c PUBLIC "${SOKOL_LIB_PATH}")
    message(STATUS "Linking with sokol library: ${SOKOL_LIB_PATH}")
else()
    message(WARNING "Sokol library not found at: ${SOKOL_LIB_PATH}")
endif()

# Compiler flags - suppress warnings in spine-c code
if(MSVC)
    # Windows-specific flags
    target_compile_options(spine-c PRIVATE
        /W3
        /wd4244  # conversion from 'type1' to 'type2', possible loss of data
        /wd4267  # conversion from 'size_t' to 'type', possible loss of data
    )
else()
    # GCC/Clang flags for all other platforms
    target_compile_options(spine-c PRIVATE
        -Wall
        -Wno-unused-function
        -Wno-unused-parameter
        -Wno-implicit-const-int-float-conversion
    )
    
    # Additional Apple-specific warnings
    if(APPLE)
        target_compile_options(spine-c PRIVATE
            -Wno-shorten-64-to-32
        )
    endif()
endif()

# Platform-specific settings
if(PLATFORM_WEB)
    # Emscripten-specific settings
    target_compile_options(spine-c PRIVATE
        -DSOKOL_GLES3
    )
    
elseif(PLATFORM_IOS)
    # iOS-specific settings
    set_target_properties(spine-c PROPERTIES
        FRAMEWORK FALSE
        XCODE_ATTRIBUTE_IPHONEOS_DEPLOYMENT_TARGET "13.0"
    )
    
elseif(PLATFORM_MACOS)
    # macOS-specific settings for dynamic library
    set_target_properties(spine-c PROPERTIES
        INSTALL_NAME_DIR "@rpath"
        BUILD_WITH_INSTALL_RPATH TRUE
        INSTALL_RPATH "@loader_path"
        MACOSX_RPATH TRUE
    )
    
elseif(PLATFORM_WINDOWS)
    # Windows-specific settings
    if(SPINE_LIBRARY_TYPE STREQUAL "SHARED")
        # Export all symbols for DLL
        set_target_properties(spine-c PROPERTIES
            WINDOWS_EXPORT_ALL_SYMBOLS TRUE
        )
    endif()
    
elseif(PLATFORM_ANDROID)
    # Android-specific settings
    target_compile_options(spine-c PRIVATE
        -DSOKOL_GLES3
    )
    
elseif(PLATFORM_LINUX)
    # Linux-specific settings
    set_target_properties(spine-c PROPERTIES
        INSTALL_RPATH "$ORIGIN"
    )
endif()

# Set C standard
set_target_properties(spine-c PROPERTIES
    C_STANDARD 99
    C_STANDARD_REQUIRED YES
)

# Optional: Link to sokol if it's available as a target
# This is useful when spine-c is built as part of a larger project
if(TARGET sokol)
    target_link_libraries(spine-c PUBLIC sokol)
    message(STATUS "Linking spine-c with sokol target")
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
    set(LIBRARY_FILE_NAME "libspine-c.dylib")
elseif(PLATFORM_WINDOWS)
    set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/windows/x64/${BUILD_TYPE_LOWER}")
    if(SPINE_LIBRARY_TYPE STREQUAL "SHARED")
        set(LIBRARY_FILE_NAME "spine-c.dll")
    else()
        set(LIBRARY_FILE_NAME "spine-c.lib")
    endif()
elseif(PLATFORM_LINUX)
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/linux/arm64/${BUILD_TYPE_LOWER}")
    else()
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/linux/x86_64/${BUILD_TYPE_LOWER}")
    endif()
    set(LIBRARY_FILE_NAME "libspine-c.so")
elseif(PLATFORM_WEB)
    set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/emscripten/x86/${BUILD_TYPE_LOWER}")
    set(LIBRARY_FILE_NAME "spine-c.a")  # No 'lib' prefix for Emscripten (matches sokol.a naming)
elseif(PLATFORM_ANDROID)
    if(ANDROID_ABI STREQUAL "arm64-v8a")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/arm64-v8a/${BUILD_TYPE_LOWER}")
    elseif(ANDROID_ABI STREQUAL "armeabi-v7a")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/armeabi-v7a/${BUILD_TYPE_LOWER}")
    elseif(ANDROID_ABI STREQUAL "x86_64")
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/x86_64/${BUILD_TYPE_LOWER}")
    else()
        set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/android/${ANDROID_ABI}/${BUILD_TYPE_LOWER}")
    endif()
    set(LIBRARY_FILE_NAME "libspine-c.so")
elseif(PLATFORM_IOS)
    set(LIBS_PLATFORM_DIR "${LIBS_OUTPUT_DIR}/ios/${BUILD_TYPE_LOWER}")
    set(LIBRARY_FILE_NAME "libspine-c.a")
endif()

# Add post-build command to copy library to libs folder
if(LIBS_PLATFORM_DIR)
    if(PLATFORM_MACOS)
        # macOS: Code sign the library before copying (required for macOS to load it)
        add_custom_command(TARGET spine-c POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${LIBS_PLATFORM_DIR}"
            COMMAND codesign --force --sign - "$<TARGET_FILE:spine-c>"
            COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:spine-c>" "${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
            COMMENT "Signing and copying spine-c library to ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
        )
    else()
        # Other platforms: Just copy the library
        add_custom_command(TARGET spine-c POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${LIBS_PLATFORM_DIR}"
            COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:spine-c>" "${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
            COMMENT "Copying spine-c library to ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}"
        )
    endif()
    message(STATUS "Library will be copied to: ${LIBS_PLATFORM_DIR}/${LIBRARY_FILE_NAME}")
endif()

# Installation rules (optional)
if(NOT PLATFORM_ANDROID AND NOT PLATFORM_IOS)
    include(GNUInstallDirs)
    
    install(TARGETS spine-c
        LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    )
    
    install(DIRECTORY ${SPINE_INCLUDE_DIR}/
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/spine-c
        FILES_MATCHING PATTERN "*.h"
    )
endif()

# Debug output
message(STATUS "=== Spine-C Build Configuration ===")
message(STATUS "Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "Library Type: ${SPINE_LIBRARY_TYPE}")
message(STATUS "Source Directory: ${SPINE_SRC_DIR}")
message(STATUS "Include Directory: ${SPINE_INCLUDE_DIR}")
message(STATUS "Number of Sources: ${SPINE_SOURCE_COUNT}")
if(EXISTS "${SPINE_EXTENSION_PATH}")
    message(STATUS "Extension: ${SPINE_EXTENSION_PATH}")
endif()
message(STATUS "===================================")

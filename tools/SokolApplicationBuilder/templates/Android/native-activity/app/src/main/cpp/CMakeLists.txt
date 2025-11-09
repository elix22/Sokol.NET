#
# Copyright (C) The Android Open Source Project
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

cmake_minimum_required(VERSION 3.22.1)
project(sokol)

# build native_app_glue as a static lib
set(${CMAKE_C_FLAGS}, "${CMAKE_C_FLAGS}")

# Extract NDK major version for conditional compilation
if(DEFINED ANDROID_NDK)
    file(READ "${ANDROID_NDK}/source.properties" NDK_SOURCE_PROPERTIES)
    string(REGEX MATCH "Pkg\\.Revision = ([0-9]+)" _ ${NDK_SOURCE_PROPERTIES})
    set(ANDROID_NDK_MAJOR_VERSION ${CMAKE_MATCH_1})
    message(STATUS "Detected Android NDK major version: ${ANDROID_NDK_MAJOR_VERSION}")
endif()

add_definitions(-DCIMGUI_VARGS0)

set(EXT_ROOT_DIR "../../../../../../../../ext")

set(IMGUI_SOURCE_FILES
        ${EXT_ROOT_DIR}/cimgui/cimgui.cpp
        ${EXT_ROOT_DIR}/cimgui/imgui/imgui_demo.cpp
        ${EXT_ROOT_DIR}/cimgui/imgui/imgui_draw.cpp
        ${EXT_ROOT_DIR}/cimgui/imgui/imgui_tables.cpp
        ${EXT_ROOT_DIR}/cimgui/imgui/imgui_widgets.cpp
        ${EXT_ROOT_DIR}/cimgui/imgui/imgui.cpp
)

set(TINYEXR_SOURCE_FILES
        ${EXT_ROOT_DIR}/tinyexr_wrapper.cpp
)

 set(FILEUTIL_SRCS ${EXT_ROOT_DIR}/util/fileutil.c ${EXT_ROOT_DIR}/util/fileutil.h)

set_source_files_properties(${IMGUI_SOURCE_FILES}
    PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++ -Wno-unused-function -Wincompatible-pointer-types-discards-qualifiers"
)

# Suppress deprecated builtins warning in basisu for NDK 25+ (required for NDK 29+)
# This warning flag is not recognized by older NDK versions
if(ANDROID_NDK_MAJOR_VERSION GREATER_EQUAL 25)
    set_source_files_properties(${EXT_ROOT_DIR}/basisu/sokol_basisu.cpp
        PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-Wno-deprecated-builtins"
    )
endif()

add_library(native_app_glue STATIC
    ${ANDROID_NDK}/sources/android/native_app_glue/android_native_app_glue.c)

# now build app's shared lib
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall")

# Export ANativeActivity_onCreate(),
# Refer to: https://github.com/android-ndk/ndk/issues/381.
set(CMAKE_SHARED_LINKER_FLAGS
    "${CMAKE_SHARED_LINKER_FLAGS} -u ANativeActivity_onCreate")

add_library(sokol SHARED  ${EXT_ROOT_DIR}/sokol.c ${EXT_ROOT_DIR}/basisu/sokol_basisu.cpp ${IMGUI_SOURCE_FILES} ${FILEUTIL_SRCS} ${TINYEXR_SOURCE_FILES})

target_include_directories(sokol PRIVATE
    ${ANDROID_NDK}/sources/android/native_app_glue ${EXT_ROOT_DIR} ${EXT_ROOT_DIR}/cimgui ${EXT_ROOT_DIR}/sokol ${EXT_ROOT_DIR}/sokol/util ${EXT_ROOT_DIR}/pl_mpeg ${EXT_ROOT_DIR}/cgltf ${EXT_ROOT_DIR}/tinyexr ${EXT_ROOT_DIR}/tinyexr/deps/nanozlib .)

target_compile_options(sokol PRIVATE
    -Wunused-function
    -Wno-incompatible-pointer-types-discards-qualifiers
    -Wno-error=implicit-const-int-float-conversion
)

set_target_properties(sokol PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED YES)

# Google new requirement , Apply max-page-size linker option only for 64-bit architectures
if(ANDROID_ABI STREQUAL "arm64-v8a" OR ANDROID_ABI STREQUAL "x86_64")
    target_link_options(${CMAKE_PROJECT_NAME} PRIVATE "-Wl,-z,max-page-size=16384")
endif()

set(PREBUILT_LIB_PATH ${CMAKE_CURRENT_SOURCE_DIR}/../../../libs)

add_library(${APP_NAME} SHARED IMPORTED)
set_target_properties(${APP_NAME} PROPERTIES 
    IMPORTED_LOCATION ${PREBUILT_LIB_PATH}/${ANDROID_ABI}/lib${APP_NAME}.so
     # Ensure library name matches what Android expects
    IMPORTED_SONAME "lib${APP_NAME}.so")

# add lib dependencies
target_link_libraries(sokol
    android
    native_app_glue
    EGL
    GLESv1_CM
    OpenSLES
    GLESv3
    aaudio
    log
    ${APP_NAME}
)
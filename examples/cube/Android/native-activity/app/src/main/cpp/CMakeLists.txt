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

set(EXT_ROOT_DIR "../../../../../../../../ext")

set(IMGUI_SOURCE_FILES
        ${EXT_ROOT_DIR}/dcimgui/src/cimgui.cpp
        ${EXT_ROOT_DIR}/dcimgui/src/imgui_demo.cpp
        ${EXT_ROOT_DIR}/dcimgui/src/imgui_draw.cpp
        ${EXT_ROOT_DIR}/dcimgui/src/imgui_tables.cpp
        ${EXT_ROOT_DIR}/dcimgui/src/imgui_widgets.cpp
        ${EXT_ROOT_DIR}/dcimgui/src/imgui.cpp
)

set_source_files_properties(${IMGUI_SOURCE_FILES}
    PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++ -Wno-unused-function -Wincompatible-pointer-types-discards-qualifiers"
)

add_library(native_app_glue STATIC
    ${ANDROID_NDK}/sources/android/native_app_glue/android_native_app_glue.c)

# now build app's shared lib
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -Werror")

# Export ANativeActivity_onCreate(),
# Refer to: https://github.com/android-ndk/ndk/issues/381.
set(CMAKE_SHARED_LINKER_FLAGS
    "${CMAKE_SHARED_LINKER_FLAGS} -u ANativeActivity_onCreate")

add_library(sokol SHARED sokol_android_main.c util/fileutil.c ${IMGUI_SOURCE_FILES})

target_include_directories(sokol PRIVATE
    ${ANDROID_NDK}/sources/android/native_app_glue ${EXT_ROOT_DIR}/dcimgui/src .)

target_compile_options(sokol PRIVATE
    -Werror
    -Wunused-function
    -Wno-incompatible-pointer-types-discards-qualifiers
)

set_target_properties(sokol PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED YES)

set(PREBUILT_LIB_PATH ${CMAKE_CURRENT_SOURCE_DIR}/../../../libs)

add_library(RotatingCubeExample SHARED IMPORTED)
set_target_properties(RotatingCubeExample PROPERTIES 
    IMPORTED_LOCATION ${PREBUILT_LIB_PATH}/${ANDROID_ABI}/libRotatingCubeExample.so
     # Ensure library name matches what Android expects
    IMPORTED_SONAME "libRotatingCubeExample.so")

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
    RotatingCubeExample
)
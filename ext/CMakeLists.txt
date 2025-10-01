cmake_minimum_required(VERSION 3.16)

project(sokol)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 11)

if (MSVC)
    set(CMAKE_C_FLAGS "/W3")
    set(CMAKE_CXX_FLAGS "/W3")
endif()

if (CMAKE_SYSTEM_NAME STREQUAL Linux)
    set(THREADS_PREFER_PTHREAD_FLAG ON)
    find_package(Threads REQUIRED)
endif()

if (WIN32)
    add_definitions(-DSOKOL_D3D11)
    set(CMAKE_SHARED_LIBRARY_PREFIX "")
elseif (APPLE)
    add_definitions(-DSOKOL_METAL)
elseif (ANDROID)
    add_definitions(-DSOKOL_GLES3)
    set(CMAKE_SHARED_LIBRARY_PREFIX "lib")
elseif (CMAKE_SYSTEM_NAME STREQUAL "Emscripten")
    add_definitions(-DSOKOL_GLES3)
    set(CMAKE_SHARED_LIBRARY_PREFIX "")
else()
    add_definitions(-DSOKOL_GLCORE)
endif()

add_definitions(-DCIMGUI_VARGS0)


set(IMGUI_SOURCE_FILES 
        cimgui/cimgui.cpp
        cimgui/imgui/imgui_demo.cpp
        cimgui/imgui/imgui_draw.cpp
        cimgui/imgui/imgui_tables.cpp
        cimgui/imgui/imgui_widgets.cpp
        cimgui/imgui/imgui.cpp
)

if (NOT MSVC)
    set_source_files_properties(basisu/sokol_basisu.cpp PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++")
    set_source_files_properties(${IMGUI_SOURCE_FILES} PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++")
endif()
if (APPLE AND CMAKE_SYSTEM_NAME STREQUAL "iOS")
    set(FILEUTIL_SRCS "util/fileutil_osx.m")
else()
    set(FILEUTIL_SRCS "util/fileutil.c" "util/fileutil.h")
endif()
# Determine library type based on platform
if (CMAKE_SYSTEM_NAME STREQUAL "Emscripten")
    set(SOKOL_LIBRARY_TYPE STATIC)
else()
    set(SOKOL_LIBRARY_TYPE SHARED)
endif()

add_library(sokol ${SOKOL_LIBRARY_TYPE}
"sokol.c" 
${IMGUI_SOURCE_FILES} 
${FILEUTIL_SRCS}
"basisu/sokol_basisu.cpp")



if (APPLE)
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -stdlib=libc++")
endif()

set_target_properties(sokol PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED YES)

if (NOT MSVC)
    target_compile_options(sokol PRIVATE -Wincompatible-pointer-types-discards-qualifiers)
endif()

target_link_libraries(sokol)

target_include_directories(sokol PRIVATE sokol )

# Add public include directory for consumers
target_include_directories(sokol PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/sokol ${CMAKE_CURRENT_SOURCE_DIR}/sokol/util ${CMAKE_CURRENT_SOURCE_DIR}/sokol_gp ${CMAKE_CURRENT_SOURCE_DIR}/cgltf ${CMAKE_CURRENT_SOURCE_DIR}/../bindgen/c)


if (WIN32)
    add_definitions(-DSOKOL_D3D11)
    set(CMAKE_SHARED_LIBRARY_PREFIX "")
    
    # Set Windows-specific properties
    set_target_properties(sokol PROPERTIES 
        WINDOWS_EXPORT_ALL_SYMBOLS TRUE
        VS_DEBUGGER_WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
    )
    
    # Add Windows dependencies
    target_link_libraries(sokol
        d3d11
        dxgi
        d3dcompiler
    )
    
    # Windows post-build commands
    # add_custom_command(TARGET sokol POST_BUILD
    #     # Log source file details
    #     COMMAND ${CMAKE_COMMAND} -E echo "Source file info:"
    #     COMMAND dir /B /S $<TARGET_FILE:sokol>
    #     
    #     # Create directory and copy
    #     COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}
    #     COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}/
    #     
    #     # Verify destination file
    #     COMMAND ${CMAKE_COMMAND} -E echo "Destination file info:"
    #     COMMAND dir /B /S ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}/sokol.dll
    #     
    #     # Verify files are identical
    #     COMMAND ${CMAKE_COMMAND} -E compare_files $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}/sokol.dll
    #     
    #     COMMENT "Installing and verifying library for Windows"
    # )
elseif (APPLE)
    if (CMAKE_SYSTEM_NAME STREQUAL "iOS")
        target_compile_options(sokol PRIVATE -x objective-c)
        target_link_libraries(sokol
            "-framework UIKit"
            "-framework QuartzCore"
            "-framework Metal"
            "-framework MetalKit"
            "-framework AudioToolbox"
            "-framework AVFoundation"
            "-framework Foundation")
        set_target_properties(sokol PROPERTIES 
            FRAMEWORK TRUE
            FRAMEWORK_VERSION A
            MACOSX_FRAMEWORK_IDENTIFIER com.elix22.sokol
            MACOSX_FRAMEWORK_INFO_PLIST ${CMAKE_CURRENT_SOURCE_DIR}/Info.plist
            BUILD_WITH_INSTALL_RPATH ON
            INSTALL_RPATH "@executable_path/Frameworks"
            XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED "NO"
            XCODE_ATTRIBUTE_CODE_SIGNING_REQUIRED "NO"
            XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY ""
        )
        # Ensure Info.plist is copied to the framework bundle
        add_custom_command(TARGET sokol POST_BUILD
              COMMAND ${CMAKE_COMMAND} -E copy
              ${CMAKE_CURRENT_SOURCE_DIR}/Info.plist
              $<TARGET_FILE_DIR:sokol>/Info.plist
          )
    else()
        target_compile_options(sokol PRIVATE -x objective-c)
        target_link_libraries(sokol
            "-framework Cocoa"
            "-framework QuartzCore"
            "-framework Metal"
            "-framework MetalKit"
            "-framework AudioToolbox"
            "-framework AVFoundation"
            "-framework Foundation")
        set_target_properties(sokol PROPERTIES LINK_FLAGS "-Wl,-F/Library/Frameworks")
    endif()
elseif (ANDROID)
    target_compile_options(sokol PRIVATE -x c)
    target_link_libraries(sokol
        android
        log
        GLESv3
        OpenSLES
        EGL
        aaudio)
elseif (CMAKE_SYSTEM_NAME STREQUAL "Emscripten")
    # Ensure static library has correct name without 'lib' prefix for Emscripten
    set(CMAKE_STATIC_LIBRARY_PREFIX "")
    # Emscripten-specific compile and link options for static library
    target_compile_options(sokol PRIVATE -x c -s ASSERTIONS=0)
    
    # For static libraries, EXPORTED_FUNCTIONS and --js-library don't apply - they apply to the final executable
    # The JavaScript library will be included at the application level
    set_target_properties(sokol PROPERTIES 
        PREFIX ""
        LINK_FLAGS "-s WASM=1"
    )
    
    # Post-build commands for Emscripten
    add_custom_command(TARGET sokol POST_BUILD
        # Log source file info
        COMMAND ${CMAKE_COMMAND} -E echo "Source file info:"
        COMMAND ls -l $<TARGET_FILE:sokol>
        
        # Create directory and copy
        COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}
        COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}/sokol.a
        
        # Verify destination file
        COMMAND ${CMAKE_COMMAND} -E echo "Destination file info:"
        COMMAND ls -l ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}/sokol.a
        
        COMMENT "Installing and verifying static library for Emscripten"
    )
elseif (LINUX)
            # Add Linux-specific compile options
            target_compile_options(sokol PRIVATE -fPIC)
            
            # Find required packages
            find_package(X11 REQUIRED)
            find_package(OpenGL REQUIRED)
            find_package(Threads REQUIRED)
            
            # Link dependencies
            target_link_libraries(sokol 
                X11 
                Xi 
                Xcursor 
                ${OPENGL_LIBRARIES}
                dl 
                m
                Threads::Threads
            )
            
            # Post-build commands for Linux
            add_custom_command(TARGET sokol POST_BUILD
                # Log source file info
                COMMAND ${CMAKE_COMMAND} -E echo "Source file info:"
                COMMAND ls -l $<TARGET_FILE:sokol>
                
                # Create directory and copy
                COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_SOURCE_DIR}/../libs/linux/${CMAKE_SYSTEM_PROCESSOR}
                COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/linux/${CMAKE_SYSTEM_PROCESSOR}/
                
                # Verify destination file
                COMMAND ${CMAKE_COMMAND} -E echo "Destination file info:"
                COMMAND ls -l ${CMAKE_SOURCE_DIR}/../libs/linux/${CMAKE_SYSTEM_PROCESSOR}/libsokol.so
                
                # Verify files are identical
                COMMAND ${CMAKE_COMMAND} -E compare_files $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/linux/${CMAKE_SYSTEM_PROCESSOR}/libsokol.so
                
                COMMENT "Installing and verifying library for Linux"
            )
endif()
cmake_minimum_required(VERSION 3.16)

project(sokol)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 11)

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
    set(CMAKE_SHARED_LIBRARY_PREFIX "lib")
else()
    add_definitions(-DSOKOL_GLCORE)
endif()


set(IMGUI_SOURCE_FILES 
        dcimgui/src/cimgui.cpp
        dcimgui/src/imgui_demo.cpp
        dcimgui/src/imgui_draw.cpp
        dcimgui/src/imgui_tables.cpp
        dcimgui/src/imgui_widgets.cpp
        dcimgui/src/imgui.cpp
)

# set_source_files_properties(basisu/basisu_transcoder.cpp PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++")
set_source_files_properties(basisu/sokol_basisu.cpp PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++")
set_source_files_properties(${IMGUI_SOURCE_FILES} PROPERTIES LANGUAGE CXX COMPILE_FLAGS "-x c++")
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

target_compile_options(sokol PRIVATE -Wincompatible-pointer-types-discards-qualifiers)

target_link_libraries(sokol)

target_include_directories(sokol PRIVATE sokol)

# Add public include directory for consumers
target_include_directories(sokol PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/sokol ${CMAKE_CURRENT_SOURCE_DIR}/sokol_gp ${CMAKE_CURRENT_SOURCE_DIR}/cgltf ${CMAKE_CURRENT_SOURCE_DIR}/../bindgen/c)


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
    add_custom_command(TARGET sokol POST_BUILD
        # Log source file details
        COMMAND ${CMAKE_COMMAND} -E echo "Source file info:"
        COMMAND dir /B /S $<TARGET_FILE:sokol>
        
        # Create directory and copy
        COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}
        COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}/
        
        # Verify destination file
        COMMAND ${CMAKE_COMMAND} -E echo "Destination file info:"
        COMMAND dir /B /S ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}/sokol.dll
        
        # Verify files are identical
        COMMAND ${CMAKE_COMMAND} -E compare_files $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/windows/${CMAKE_SYSTEM_PROCESSOR}/sokol.dll
        
        COMMENT "Installing and verifying library for Windows"
    )
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
    # Emscripten-specific compile and link options for static library
    target_compile_options(sokol PRIVATE -x c)
    
    # List of sokol functions to export for WebAssembly
    # set(SOKOL_EXPORTED_FUNCTIONS
    #     "_sg_setup,_sg_shutdown,_sg_query_backend,_sg_begin_pass,_sg_apply_pipeline,_sg_apply_bindings,_sg_apply_uniforms,_sg_draw,_sg_end_pass,_sg_commit,_sg_make_buffer,_sg_make_image,_sg_make_sampler,_sg_make_shader,_sg_make_pipeline,_sg_destroy_buffer,_sg_destroy_image,_sg_destroy_sampler,_sg_destroy_shader,_sg_destroy_pipeline,_sg_update_buffer,_sg_update_image,_sg_append_buffer,_sg_query_buffer_info,_sg_query_image_info,_sg_query_shader_info,_sg_query_pipeline_info,_sg_query_buffer_defaults,_sg_query_image_defaults,_sg_query_shader_defaults,_sg_query_pipeline_defaults,_sg_enable_frame_stats,_sg_disable_frame_stats,_sg_frame_stats_enabled,_sg_add_commit_listener,_sg_remove_commit_listener,_sapp_run,_sapp_shutdown,_sapp_isvalid,_sapp_width,_sapp_height,_sapp_widthf,_sapp_heightf,_sapp_dpi_scale,_sapp_show_keyboard,_sapp_keyboard_shown,_sapp_show_mouse,_sapp_mouse_shown,_sapp_request_quit,_sapp_cancel_quit,_sapp_quit,_sapp_query_desc,_sapp_frame_duration,_sapp_set_clipboard_string,_sapp_get_clipboard_string,_sapp_get_num_dropped_files,_sapp_get_dropped_file_path,_sapp_set_window_title,_sapp_set_icon,_sapp_get_mouse_x,_sapp_get_mouse_y,_sapp_get_mouse_dx,_sapp_get_mouse_dy,_sapp_userdata,_sapp_html5_ask_leave_site,_sapp_html5_get_dropped_file_size,_sapp_html5_fetch_dropped_file,_sapp_color_format,_sapp_depth_format,_sapp_sample_count,_sglue_environment,_sglue_swapchain,_glBindSampler"
    # )
    
    # set_target_properties(sokol PROPERTIES 
    #     LINK_FLAGS "-s WASM=1 -s USE_WEBGL2=1 -s FULL_ES3=1 -s ALLOW_MEMORY_GROWTH=1 -s MIN_WEBGL_VERSION=1 -s MAX_WEBGL_VERSION=2 -s GL_ENABLE_GET_PROC_ADDRESS=1 -s WEBGL2_BACKWARDS_COMPATIBILITY_EMULATION=1 -s LEGACY_GL_EMULATION=1 -s EXPORTED_FUNCTIONS=\"[${SOKOL_EXPORTED_FUNCTIONS}]\""
    # )

    set_target_properties(sokol PROPERTIES 
        LINK_FLAGS "-s WASM=1"
    )
    
    # Post-build commands for Emscripten
    add_custom_command(TARGET sokol POST_BUILD
        # Log source file info
        COMMAND ${CMAKE_COMMAND} -E echo "Source file info:"
        COMMAND ls -l $<TARGET_FILE:sokol>
        
        # Create directory and copy
        COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}
        COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}/
        
        # Verify destination file
        COMMAND ${CMAKE_COMMAND} -E echo "Destination file info:"
        COMMAND ls -l ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}/libsokol.a
        
        # Verify files are identical
        COMMAND ${CMAKE_COMMAND} -E compare_files $<TARGET_FILE:sokol> ${CMAKE_SOURCE_DIR}/../libs/emscripten/${CMAKE_SYSTEM_PROCESSOR}/libsokol.a
        
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
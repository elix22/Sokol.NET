cmake_minimum_required(VERSION 3.20)
project(sokol_net C CXX)

# sokol_net — WebSocket (ws/wss) + WebRTC data channels for Sokol.NET apps.
# Final link stage: one self-contained library from the static archives the build scripts produce
# (libdatachannel + libjuice + usrsctp + Mbed TLS, see scripts/common.sh) plus sokol_net.c and the
# bundled CA certificates. Exports only libdatachannel's C API (rtc*) and sokolnet_*.
#   Android : libs/android/<abi>/release/libsokol_net.so    (16 KB aligned on 64-bit ABIs)
#   iOS     : libs/ios/<target>/release/sokol_net.framework
#   macOS   : libs/macos/<arch>/release/libsokol_net.dylib
#
# Required: -DSOKOLNET_DEPS_DIR=<dir with libdatachannel-static.a libjuice-static.a libusrsctp.a
#           libmbedtls.a libmbedx509.a libmbedcrypto.a>

set(PLUGIN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/..")   # plugins/Net/
set(LIB_NAME    "sokol_net")

if(NOT SOKOLNET_DEPS_DIR)
    message(FATAL_ERROR "sokol_net: set SOKOLNET_DEPS_DIR (run plugins/Net/scripts/build-<platform>.sh)")
endif()
set(D "${SOKOLNET_DEPS_DIR}")
foreach(lib datachannel-static juice-static usrsctp mbedtls mbedx509 mbedcrypto)
    if(NOT EXISTS "${D}/lib${lib}.a")
        message(FATAL_ERROR "sokol_net: missing ${D}/lib${lib}.a")
    endif()
endforeach()

# ── The CA bundle, embedded as a NUL-terminated byte array (portable: no string-literal size limits) ─────
set(CA_PEM "${PLUGIN_ROOT}/certs/cacert.pem")
set(CA_C   "${CMAKE_CURRENT_BINARY_DIR}/sokol_net_cacert.c")
file(READ "${CA_PEM}" CA_HEX HEX)
string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1," CA_HEX "${CA_HEX}")
file(WRITE "${CA_C}" "const unsigned char g_sokol_net_cacert[] = {${CA_HEX}0x00};\n")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${CA_PEM}")

add_library(${LIB_NAME} SHARED sokol_net.c "${CA_C}")
target_include_directories(${LIB_NAME} PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}")
set_target_properties(${LIB_NAME} PROPERTIES LINKER_LANGUAGE CXX)   # pulls in the C++ runtime libdatachannel needs

# Order matters for static archives: each library before the ones it depends on.
set(DEPS "${D}/libjuice-static.a" "${D}/libusrsctp.a" "${D}/libmbedtls.a" "${D}/libmbedx509.a" "${D}/libmbedcrypto.a")

if(ANDROID)
    target_link_libraries(${LIB_NAME} PRIVATE
        "-Wl,--whole-archive" "${D}/libdatachannel-static.a" "-Wl,--no-whole-archive"
        ${DEPS} log
        "-Wl,--version-script=${CMAKE_CURRENT_SOURCE_DIR}/exports.map"
        "-Wl,--gc-sections")
    if(ANDROID_ABI STREQUAL "arm64-v8a" OR ANDROID_ABI STREQUAL "x86_64")
        target_link_options(${LIB_NAME} PRIVATE "-Wl,-z,max-page-size=16384")   # Google Play: 16 KB pages
    endif()
    set(OUT_DIR "${PLUGIN_ROOT}/libs/android/${ANDROID_ABI}/release")
    add_custom_command(TARGET ${LIB_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "${OUT_DIR}"
        COMMAND ${CMAKE_STRIP} --strip-unneeded "$<TARGET_FILE:${LIB_NAME}>" -o "${OUT_DIR}/lib${LIB_NAME}.so"
        COMMENT "lib${LIB_NAME}.so → ${OUT_DIR}")

elseif(APPLE)
    target_link_libraries(${LIB_NAME} PRIVATE
        "-Wl,-force_load,${D}/libdatachannel-static.a"
        ${DEPS}
        "-Wl,-exported_symbols_list,${CMAKE_CURRENT_SOURCE_DIR}/exports.txt"
        "-Wl,-dead_strip")

    if(CMAKE_SYSTEM_NAME STREQUAL "iOS")
        set_target_properties(${LIB_NAME} PROPERTIES
            FRAMEWORK                   TRUE
            FRAMEWORK_VERSION           A
            MACOSX_FRAMEWORK_IDENTIFIER com.sokolnet.plugins.net
            MACOSX_FRAMEWORK_SHORT_VERSION_STRING "1.0"
            MACOSX_FRAMEWORK_BUNDLE_VERSION       "1"
            INSTALL_NAME_DIR            "@rpath")
        # CMake resolves "iphonesimulator" to the SDK path (…/iPhoneSimulator.sdk) with the Makefile generator → compare lower-case.
        string(TOLOWER "${CMAKE_OSX_SYSROOT}" SYSROOT_LC)
        if(SYSROOT_LC MATCHES "iphonesimulator")
            if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64")
                set(ARCH_DIR "simulator-x64")
            else()
                set(ARCH_DIR "simulator-arm64")
            endif()
        else()
            set(ARCH_DIR "arm64")
        endif()
        set(OUT_DIR "${PLUGIN_ROOT}/libs/ios/${ARCH_DIR}/release")
        add_custom_command(TARGET ${LIB_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E rm -rf "${OUT_DIR}/${LIB_NAME}.framework"
            COMMAND ${CMAKE_COMMAND} -E make_directory "${OUT_DIR}"
            COMMAND ${CMAKE_COMMAND} -E copy_directory "$<TARGET_BUNDLE_DIR:${LIB_NAME}>" "${OUT_DIR}/${LIB_NAME}.framework"
            COMMAND strip -x "${OUT_DIR}/${LIB_NAME}.framework/${LIB_NAME}"
            COMMENT "${LIB_NAME}.framework → ${OUT_DIR}")
    else()
        set_target_properties(${LIB_NAME} PROPERTIES INSTALL_NAME_DIR "@rpath")
        if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64")
            set(ARCH_DIR "X64")
        else()
            set(ARCH_DIR "arm64")
        endif()
        set(OUT_DIR "${PLUGIN_ROOT}/libs/macos/${ARCH_DIR}/release")
        add_custom_command(TARGET ${LIB_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${OUT_DIR}"
            COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:${LIB_NAME}>" "${OUT_DIR}/lib${LIB_NAME}.dylib"
            COMMAND strip -x "${OUT_DIR}/lib${LIB_NAME}.dylib"
            COMMAND codesign -f -s - "${OUT_DIR}/lib${LIB_NAME}.dylib"
            COMMENT "lib${LIB_NAME}.dylib → ${OUT_DIR}")
    endif()

else()
    message(FATAL_ERROR "sokol_net: ${CMAKE_SYSTEM_NAME} is not supported yet (Windows and Linux come next)")
endif()

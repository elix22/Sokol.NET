# Injected into every project() of the libdatachannel build (CMAKE_PROJECT_INCLUDE) so its sources compile
# against the same Mbed TLS configuration the Mbed TLS libraries were built with.
if(SOKOLNET_MBEDTLS_USER_CONFIG)
    add_compile_definitions(MBEDTLS_USER_CONFIG_FILE="${SOKOLNET_MBEDTLS_USER_CONFIG}")
endif()

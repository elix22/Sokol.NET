// sokol_net — Mbed TLS options libdatachannel needs, appended to Mbed TLS's default config
// (MBEDTLS_USER_CONFIG_FILE). Used for BOTH the Mbed TLS build and libdatachannel's compile, so both
// see the same configuration (struct layouts depend on it).
//  - MBEDTLS_SSL_DTLS_SRTP: libdatachannel's DTLS transport uses the SRTP profile API unconditionally,
//    even when built with NO_MEDIA.
//  - MBEDTLS_THREADING_C + _PTHREAD: TLS/DTLS run on libdatachannel's thread pool and initialise PSA
//    crypto from several threads, so Mbed TLS needs its mutexes. (Windows will need MBEDTLS_THREADING_ALT.)
#define MBEDTLS_SSL_DTLS_SRTP
#define MBEDTLS_THREADING_C
#define MBEDTLS_THREADING_PTHREAD

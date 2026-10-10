// sokol_net — Mbed TLS options libdatachannel needs, appended to Mbed TLS's default config
// (MBEDTLS_USER_CONFIG_FILE). Used for BOTH the Mbed TLS build and libdatachannel's compile, so both
// see the same configuration (struct layouts depend on it).
//  - MBEDTLS_SSL_DTLS_SRTP: libdatachannel's DTLS transport uses the SRTP profile API unconditionally,
//    even when built with NO_MEDIA.
//  - MBEDTLS_THREADING_C + _PTHREAD: TLS/DTLS run on libdatachannel's thread pool and initialise PSA
//    crypto from several threads, so Mbed TLS needs its mutexes. (Windows will need MBEDTLS_THREADING_ALT.)
//  - MBEDTLS_PLATFORM_DEV_RANDOM (Android): Mbed TLS uses getrandom() only with glibc, so on bionic it reads
//    /dev/random — which BLOCKS on kernels older than 5.6 when the entropy estimate is low. libdatachannel seeds
//    its TLS transport on the one poll thread, so every socket stalled and timed out (Galaxy Tab A8 kernel 4.14,
//    Redmi 6A 4.9). /dev/urandom never blocks and is what getrandom() returns once the pool is initialised —
//    long before any app runs on a phone.
#define MBEDTLS_SSL_DTLS_SRTP
#define MBEDTLS_THREADING_C
#define MBEDTLS_THREADING_PTHREAD
#if defined(__ANDROID__)
#define MBEDTLS_PLATFORM_DEV_RANDOM "/dev/urandom"
#endif

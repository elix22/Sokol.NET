// sokol_net — WebSocket + WebRTC data channels for Sokol.NET apps (libdatachannel's C API, rtc.h,
// plus the helpers below). See plugins/Net/README.md.
#pragma once
#ifdef __cplusplus
extern "C" {
#endif

// The bundled CA certificates (Mozilla's root store, PEM, NUL-terminated). Pass it as
// rtcWsConfiguration.caCertificatePemFile to verify wss:// servers: with the Mbed TLS backend
// libdatachannel has no default roots of its own.
const char* sokolnet_ca_bundle(void);

// Plugin ABI version (bumped when this header or the patched rtc.h changes).
int sokolnet_version(void);

#ifdef __cplusplus
}
#endif

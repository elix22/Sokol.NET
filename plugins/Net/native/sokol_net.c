#include "sokol_net.h"

extern const unsigned char g_sokol_net_cacert[];   // generated from certs/cacert.pem by CMakeLists.txt

const char* sokolnet_ca_bundle(void) { return (const char*)g_sokol_net_cacert; }
int sokolnet_version(void) { return 1; }

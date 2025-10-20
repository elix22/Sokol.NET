// fontstash C wrapper for C# bindings
#include <stddef.h>
#include <stdlib.h>
#include "ext/fontstash/fontstash.h"

// Additional API functions that are in the implementation but should be exposed
#ifdef __cplusplus
extern "C" {
#endif

FONS_DEF int fonsAddFontMem(FONScontext* stash, const char* name, unsigned char* data, int dataSize, int freeData);

#ifdef __cplusplus
}
#endif

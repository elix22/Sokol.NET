/* Suppress visibility attribute so clang AST only emits ParmVarDecl nodes
 * inside FunctionDecl - Attr nodes would cause gen_ir.py to drop the func. */
#ifdef NN_API
#undef NN_API
#endif
#define NN_API
#include "ext/nearnet/include/nearnet.h"

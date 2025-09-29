#!/bin/bash 
rawArgs="$@"
if [[ -z "${RealCppCompilerAndLinker}" ]]; then
	RealCppCompilerAndLinker=$ANDROID_NDK_ROOT/toolchains/llvm/prebuilt/darwin-x86_64/bin/clang
fi
$RealCppCompilerAndLinker $rawArgs 
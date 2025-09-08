@ECHO OFF
SETLOCAL ENABLEDELAYEDEXPANSION

SET "rawArgs=%*"
IF NOT DEFINED RealCppCompilerAndLinker (
    SET "RealCppCompilerAndLinker=%ANDROID_NDK_ROOT%\toolchains\llvm\prebuilt\windows-x86_64\bin\clang.exe"
)

"%RealCppCompilerAndLinker%" !rawArgs!
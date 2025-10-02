@echo off

REM   Copyright (c) 2020-2021 Eli Aloni a.k.a elix22.
REM  
REM   Permission is hereby granted, free of charge, to any person obtaining a copy
REM   of this software and associated documentation files (the "Software"), to deal
REM   in the Software without restriction, including without limitation the rights
REM   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
REM   copies of the Software, and to permit persons to whom the Software is
REM   furnished to do so, subject to the following conditions:
REM  
REM   The above copyright notice and this permission notice shall be included in
REM   all copies or substantial portions of the Software.
REM  
REM   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
REM   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
REM   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
REM   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
REM   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
REM   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
REM   THE SOFTWARE.

REM  This script will create an folder called '.sokolnet_config' in the home folder
REM  It will create configuration files in that folder , to allow  proper functionality for Sokol.Net
REM  Each time that the Sokol.Net folder is moved to a different folder , this script must be called 

set SOKOLNET_CONFIG_FOLDER=.sokolnet_config
set HOME=%homedrive%%homepath%
set currPwd=%~dp0
REM Remove trailing backslash if present
if "%currPwd:~-1%"=="\" set "currPwd=%currPwd:~0,-1%"
REM Convert backslashes to forward slashes for consistency
set "currPwd=%currPwd:\=/%"

echo %currPwd%
echo %HOME%

if NOT "%currPwd%"=="%currPwd: =%" (
    echo ERROR: PATH %currPwd%  contains whitespace characters !!
    echo Make sure that Sokol.Net installation path doesn't contain whitespace characters
    goto done
)


@RD /S /Q %HOME%\%SOKOLNET_CONFIG_FOLDER%
mkdir %HOME%\%SOKOLNET_CONFIG_FOLDER%

REM Copy template file and replace placeholder
copy /Y templates\SokolNetHome.config %HOME%\%SOKOLNET_CONFIG_FOLDER%\SokolNetHome.config >nul

REM Replace TEMPLATE_SOKOLNET_HOME with actual path using PowerShell
powershell -Command "(Get-Content '%HOME%\%SOKOLNET_CONFIG_FOLDER%\SokolNetHome.config') -replace 'TEMPLATE_SOKOLNET_HOME', '%currPwd%' | Set-Content '%HOME%\%SOKOLNET_CONFIG_FOLDER%\SokolNetHome.config'"

REM Create sokolnet_home file
echo %currPwd%>%HOME%\%SOKOLNET_CONFIG_FOLDER%\sokolnet_home

if NOT exist %HOME%\.sokolnet_config\sokolnet_home (
    echo ERROR Sokol.Net configuration failed
    goto done
)

if NOT exist %HOME%\%SOKOLNET_CONFIG_FOLDER%\SokolNetHome.config (
    echo Sokol.Net configuration failure!
    goto done
)

echo.
echo Sokol.Net configured!
echo.
echo cat %HOME%\.sokolnet_config\sokolnet_home
type %HOME%\.sokolnet_config\sokolnet_home
echo.
echo cat %HOME%\%SOKOLNET_CONFIG_FOLDER%\SokolNetHome.config
type %HOME%\%SOKOLNET_CONFIG_FOLDER%\SokolNetHome.config
echo.


:done
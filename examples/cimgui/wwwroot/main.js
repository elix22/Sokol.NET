// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './_framework/dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

var canvas = document.getElementById('canvas');
dotnet.instance.Module["canvas"] = canvas;

// Make Module globally available for compatibility
globalThis.Module = dotnet.instance.Module;
window.Module = dotnet.instance.Module;

console.log('Module initialized and made globally available');

function SetRendererSize (width, height) {
    console.log('Engine renderer size changed to', width, height);
}
function GetUserAgent()
{
    return navigator.userAgent;
}

dotnet.instance.Module["SetRendererSize"] = SetRendererSize;
dotnet.instance.Module["GetUserAgent"]=GetUserAgent;

// We're ready to dotnet.run, so let's remove the spinner
const loading_div = document.getElementById('spinner');
loading_div.remove();

const downloading = document.getElementById('Downloading');
downloading.remove();

await dotnet.run();
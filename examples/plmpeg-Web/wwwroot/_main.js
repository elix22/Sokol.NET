// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './_framework/dotnet.js'

var canvas = document.getElementById('canvas');

// Set up canvas for Emscripten/Sokol
canvas.width = 800;
canvas.height = 600;

// Ensure Module object exists
var Module = Module || {};

// Module configuration - let sokol handle WebGL context creation
Module.canvas = document.getElementById('canvas');

Module.print = function(text) {
    console.log(text);
};

Module.printErr = function(text) {
    console.error(text);
};

// Set up Module for Emscripten BEFORE creating dotnet instance
// This follows the pattern from sokol-samples - let sokol handle WebGL completely
window.Module = {
    canvas: canvas,
    print: function(text) {
        console.log(text);
    },
    printErr: function(text) {
        console.error(text);
    },
    // Set up callbacks for WebGL context initialization
    preRun: [function() {
        console.log('Module preRun: Canvas element ready for sokol');
        console.log('Canvas element:', canvas);
        
        // Direct override of GL functions to fix OpenGL ES 3.0 compatibility
        // This must happen before any GL calls are made
        Module.GL = Module.GL || {};
        
        // Hook into Emscripten's GL initialization to override glGetIntegerv
        const originalMakeContextCurrent = Module.GL.makeContextCurrent;
        Module.GL.makeContextCurrent = function(contextHandle) {
            console.log('GL.makeContextCurrent called with handle:', contextHandle);
            
            if (originalMakeContextCurrent) {
                const result = originalMakeContextCurrent.call(this, contextHandle);
                
                // Override glGetIntegerv after context is made current
                if (contextHandle && Module.GL.currentContext) {
                    const gl = Module.GL.currentContext.GLctx;
                    if (gl && !gl._sokolPatched) {
                        console.log('Patching WebGL context for sokol compatibility...');
                        
                        const originalGetParameter = gl.getParameter.bind(gl);
                        gl.getParameter = function(pname) {
                            // OpenGL ES 3.0 constants missing in WebGL2
                            const GL_MAJOR_VERSION = 0x821B;
                            const GL_MINOR_VERSION = 0x821C;
                            const GL_NUM_EXTENSIONS = 0x821D;
                            
                            switch (pname) {
                                case GL_MAJOR_VERSION:
                                    console.log('Intercepted GL_MAJOR_VERSION -> 3');
                                    return 3;
                                case GL_MINOR_VERSION:
                                    console.log('Intercepted GL_MINOR_VERSION -> 0'); 
                                    return 0;
                                case GL_NUM_EXTENSIONS:
                                    console.log('Intercepted GL_NUM_EXTENSIONS -> 0');
                                    return 0;
                                default:
                                    try {
                                        return originalGetParameter(pname);
                                    } catch (error) {
                                        console.warn('getParameter failed for', pname.toString(16), '- returning 0');
                                        return 0;
                                    }
                            }
                        };
                        
                        gl._sokolPatched = true;
                        console.log('WebGL context patched successfully');
                    }
                }
                
                return result;
            }
        };
        
        console.log('GL context override installed');
    }],
    postRun: [function() {
        console.log('Module postRun: Sokol should have initialized WebGL');
    }]
};

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .withModuleConfig(window.Module)
    .create();

// Log the dotnet instance Module to verify it was set correctly
console.log('dotnet.instance.Module:', dotnet.instance.Module);

// Expose functions to .NET
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
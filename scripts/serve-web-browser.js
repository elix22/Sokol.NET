#!/usr/bin/env node

/**
 * Node.js web server for serving WebAssembly examples in browser
 * Compatible with VS Code launch configurations
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

// Get example name from command line argument
const exampleName = process.argv[2];
if (!exampleName) {
    console.error('Usage: node serve-web-browser.js <example-name>');
    process.exit(1);
}

const BASE_PORT = 8000;
const WORKSPACE_ROOT = path.resolve(__dirname, '..');
const WWWROOT_PATH = path.join(WORKSPACE_ROOT, 'examples', exampleName, 'bin', 'Debug', 'net8.0', 'wwwroot');

// MIME types for common file extensions
const MIME_TYPES = {
    '.html': 'text/html',
    '.js': 'application/javascript',
    '.wasm': 'application/wasm',
    '.css': 'text/css',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.gif': 'image/gif',
    '.ico': 'image/x-icon',
    '.svg': 'image/svg+xml',
    '.json': 'application/json',
    '.txt': 'text/plain',
    '.mpg': 'video/mpeg',
    '.mpeg': 'video/mpeg',
    '.mp4': 'video/mp4',
    '.webm': 'video/webm',
    '.ogg': 'video/ogg',
    '.avi': 'video/x-msvideo',
    '.mov': 'video/quicktime',
    '.wmv': 'video/x-ms-wmv',
    '.mp3': 'audio/mpeg',
    '.wav': 'audio/wav',
    '.ogg': 'audio/ogg',
    '.flac': 'audio/flac'
};

function getMimeType(filePath) {
    const ext = path.extname(filePath).toLowerCase();
    return MIME_TYPES[ext] || 'application/octet-stream';
}

function serveFile(res, filePath, req) {
    fs.stat(filePath, (err, stats) => {
        if (err) {
            res.writeHead(404, { 'Content-Type': 'text/plain' });
            res.end('File not found');
            return;
        }
        
        const mimeType = getMimeType(filePath);
        const fileSize = stats.size;
        
        // Handle range requests for video files
        const range = req.headers.range;
        if (range && filePath.match(/\.(mpg|mpeg|mp4|webm|ogg|avi|mov|wmv)$/i)) {
            const parts = range.replace(/bytes=/, "").split("-");
            const start = parseInt(parts[0], 10);
            const end = parts[1] ? parseInt(parts[1], 10) : fileSize - 1;
            const chunksize = (end - start) + 1;
            
            const headers = {
                'Content-Range': `bytes ${start}-${end}/${fileSize}`,
                'Accept-Ranges': 'bytes',
                'Content-Length': chunksize,
                'Content-Type': mimeType,
                'Cross-Origin-Embedder-Policy': 'require-corp',
                'Cross-Origin-Opener-Policy': 'same-origin'
            };
            
            res.writeHead(206, headers);
            fs.createReadStream(filePath, { start, end }).pipe(res);
            return;
        }
        
        // Normal file serving
        const headers = { 
            'Content-Type': mimeType,
            'Content-Length': fileSize,
            'Cross-Origin-Embedder-Policy': 'require-corp',
            'Cross-Origin-Opener-Policy': 'same-origin',
            'Accept-Ranges': 'bytes'
        };
        
        // Add cache headers for static assets
        if (filePath.match(/\.(js|wasm|css|png|jpg|jpeg|gif|ico|svg|mpg|mp4|webm|ogg)$/i)) {
            headers['Cache-Control'] = 'public, max-age=3600';
        }
        
        fs.readFile(filePath, (readErr, data) => {
            if (readErr) {
                res.writeHead(500, { 'Content-Type': 'text/plain' });
                res.end('Internal Server Error');
                return;
            }
            
            res.writeHead(200, headers);
            res.end(data);
        });
    });
}

// Check if wwwroot directory exists
if (!fs.existsSync(WWWROOT_PATH)) {
    console.error(`❌ WebAssembly build not found: ${WWWROOT_PATH}`);
    console.error(`   Run the prepare-${exampleName}-web task first to build the WebAssembly version.`);
    process.exit(1);
}

// Function to find an available port
function findAvailablePort(startPort, callback) {
    const port = startPort;
    const server = require('net').createServer();
    
    server.listen(port, (err) => {
        server.once('close', () => {
            callback(port);
        });
        server.close();
    });
    
    server.on('error', (err) => {
        if (err.code === 'EADDRINUSE') {
            findAvailablePort(startPort + 1, callback);
        } else {
            callback(null, err);
        }
    });
}

// Create HTTP server
const server = http.createServer((req, res) => {
    let urlPath = req.url;
    
    // Default to index.html for root requests
    if (urlPath === '/') {
        urlPath = '/index.html';
    }
    
    // Remove query parameters
    urlPath = urlPath.split('?')[0];
    
    // Security: prevent directory traversal
    if (urlPath.includes('..')) {
        res.writeHead(400, { 'Content-Type': 'text/plain' });
        res.end('Bad Request');
        return;
    }
    
    const filePath = path.join(WWWROOT_PATH, urlPath);
    
    // Check if file exists
    fs.access(filePath, fs.constants.F_OK, (err) => {
        if (err) {
            res.writeHead(404, { 'Content-Type': 'text/plain' });
            res.end('File not found');
            return;
        }
        
        serveFile(res, filePath, req);
    });
});

// Find available port and start server
findAvailablePort(BASE_PORT, (port, err) => {
    if (err) {
        console.error(`❌ Failed to find available port: ${err.message}`);
        process.exit(1);
    }
    
    if (!port) {
        console.error('❌ No available ports found');
        process.exit(1);
    }
    
    // Start server on available port
    server.listen(port, () => {
        const url = `http://localhost:${port}`;
        console.log(`🚀 Serving ${exampleName} WebAssembly example at ${url}`);
        console.log(`📁 Serving from: ${WWWROOT_PATH}`);
        
        if (port !== BASE_PORT) {
            console.log(`⚠️  Port ${BASE_PORT} was in use, using port ${port} instead`);
        }
        
        console.log('');
        console.log('🌐 Opening browser...');
        
        // Open browser automatically based on platform
        const isWindows = process.platform === 'win32';
        const isMac = process.platform === 'darwin';
        
        let openCommand;
        if (isWindows) {
            openCommand = 'start';
        } else if (isMac) {
            openCommand = 'open';
        } else {
            openCommand = 'xdg-open';
        }
        
        spawn(openCommand, [url], { 
            detached: true, 
            stdio: 'ignore',
            shell: isWindows 
        });
        
        console.log('💡 Press Ctrl+C to stop the server');
    });
    
    // Error handling for server
    server.on('error', (err) => {
        if (err.code === 'EADDRINUSE') {
            console.error(`❌ Port ${port} is already in use. Trying to find another port...`);
            findAvailablePort(port + 1, (newPort, newErr) => {
                if (newErr || !newPort) {
                    console.error('❌ Failed to find an available port');
                    process.exit(1);
                }
            });
        } else {
            console.error(`❌ Server error: ${err.message}`);
            process.exit(1);
        }
    });
});

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n🛑 Shutting down server...');
    server.close(() => {
        console.log('✅ Server stopped');
        process.exit(0);
    });
});

process.on('SIGTERM', () => {
    console.log('\n🛑 Shutting down server...');
    server.close(() => {
        console.log('✅ Server stopped');
        process.exit(0);
    });
});
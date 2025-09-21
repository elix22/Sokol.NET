#!/usr/bin/env python3
"""
Simple HTTP Server for WebAssembly Examples
Serves the wwwroot directory of built WebAssembly projects
"""

import http.server
import socketserver
import os
import sys
import webbrowser
from pathlib import Path

def main():
    if len(sys.argv) != 2:
        print("Usage: serve-web-example.py <example_path>")
        print("Example: serve-web-example.py examples/cube")
        sys.exit(1)
    
    example_path = sys.argv[1]
    wwwroot_path = Path(example_path) / "bin" / "Debug" / "net8.0" / "wwwroot"
    
    if not wwwroot_path.exists():
        print(f"❌ Error: wwwroot not found at {wwwroot_path}")
        print("Please build the web project first using the appropriate build task.")
        sys.exit(1)
    
    # Change to the wwwroot directory
    os.chdir(wwwroot_path)
    
    # Start server
    PORT = 8000
    
    try:
        with socketserver.TCPServer(("", PORT), http.server.SimpleHTTPRequestHandler) as httpd:
            print(f"🚀 Serving {example_path} at http://localhost:{PORT}")
            print(f"📁 Serving directory: {wwwroot_path}")
            print("Press Ctrl+C to stop the server")
            
            # Open browser automatically
            webbrowser.open(f"http://localhost:{PORT}")
            
            httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n🛑 Server stopped by user")
    except OSError as e:
        if "Address already in use" in str(e):
            print(f"❌ Error: Port {PORT} is already in use")
            print("Please stop any other web servers or use a different port")
        else:
            print(f"❌ Error starting server: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
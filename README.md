# Sokol.NET Documentation

This folder contains guides and reference documentation for building, running, and debugging Sokol.NET projects.

## 🌐 Live Examples Showcase

This directory also hosts the **interactive WebAssembly examples showcase** for GitHub Pages:

- **[Live Showcase](https://elix22.github.io/Sokol.NET/)** - Try all 36 examples in your browser
- **[GitHub Pages Setup Guide](GITHUB_PAGES_SETUP.md)** - Deploy the showcase
- **[Build Script](../scripts/build-all-web-examples.sh)** - Build all WebAssembly examples

### Showcase Files
- `index.html` - Main showcase page
- `examples-data.json` - Example metadata
- `css/showcase.css` - Styles
- `js/showcase.js` - Interactive functionality
- `examples/` - WebAssembly builds (generated)
- `thumbnails/` - Preview images

## Getting Started

- **[Visual Studio Code Run Guide](VSCODE_RUN_GUIDE.md)** ⭐ - Complete step-by-step guide with screenshots showing how to run applications on Desktop, Web, Android, and iOS using VS Code
- **[Shader Programming Guide](SHADER_GUIDE.md)** 🎨 - Complete guide to writing cross-platform shaders with sokol-shdc
- **[Creating New Examples](CREATE_EXAMPLE.md)** 🆕 - How to create new example projects from template

## Key Guides

### Android
- [Android Properties Configuration](./ANDROID_PROPERTIES.md) - Configure Android builds with Directory.Build.props (permissions, SDK versions, fullscreen, orientation)
- [Android Screen Orientation](./ANDROID_SCREEN_ORIENTATION.md) - Quick reference for screen orientation configuration
- [Android Device Selection & Installation](./ANDROID_DEVICE_SELECTION.md) - Multi-device installation guide
- [AAB Build Guide](./AAB_BUILD_GUIDE.md) - Android App Bundle build guide

### iOS
- [iOS Device Selection & Installation](./ios-device-selection.md)

### Web/WebAssembly
- [WebAssembly Local Server & VS Code Integration](./web-server-setup.md)
- [WebAssembly Browser Guide](./WEBASSEMBLY_BROWSER_GUIDE.md)

### Build System & CI/CD
- [Build System Documentation](./BUILD_SYSTEM.md) - Comprehensive guide to building sokol libraries for all platforms
- [Quick Build Reference](./QUICK_BUILD.md) - Quick reference for local builds and common commands

### General
- [Multi-Device Install](./MULTI_DEVICE_INSTALL.md)
- [Android Keyboard Implementation](./ANDROID_KEYBOARD_IMPLEMENTATION.md) - Android soft keyboard and clipboard support
- Desktop, and other platform guides (see other files in this folder)

## Quick Start

For most users, see the main README in the root of the repository for project setup and usage. For browser/WebAssembly development, see:

- [docs/web-server-setup.md](./web-server-setup.md)

## How to Use

- Open any guide in this folder for platform-specific instructions
- Use the VS Code launch configurations for easy debugging and browser integration

---
**Back to project root:** [../](../)

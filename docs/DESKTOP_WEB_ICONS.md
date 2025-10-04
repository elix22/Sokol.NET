# Desktop and Web Icon Configuration Guide

This guide covers three different approaches to configure application icons for Desktop (Windows/macOS/Linux) and Web deployments.

## Overview

You have **three options** for handling icons in desktop and web applications:

1. **Automatic Generation** - Let SokolApplicationBuilder generate all icon formats from a source image
2. **Manual .csproj Configuration** - Use .NET's built-in icon handling
3. **Pre-generated Icons** - Provide ready-made icon files

Choose the approach that best fits your workflow!

---

## Option 1: Automatic Icon Generation (Recommended)

This is the easiest approach - specify one source image and let the build system generate all required formats automatically.

### Desktop Icons (Automatic)

Add to your `Directory.Build.props`:

```xml
<PropertyGroup>
    <!-- Desktop app icon (single source image) -->
    <DesktopIcon>logo_full_large.png</DesktopIcon>
</PropertyGroup>
```

**What gets generated:**

| Platform | Output Files | Sizes |
|----------|-------------|-------|
| **Windows** | `app.ico` | Multi-size ICO (16, 32, 48, 64, 128, 256px) |
| **macOS** | `app.icns` | ICNS bundle (16-1024px @1x/@2x) |
| **Linux** | `icon_*.png` | Multiple PNG files (16, 22, 24, 32, 48, 64, 128, 256, 512px) |

**Build command:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid osx-arm64 \
  --path /path/to/your/project
```

### Web Icons (Automatic)

Add to your `Directory.Build.props`:

```xml
<PropertyGroup>
    <!-- Web app icon (single source image) -->
    <WebIcon>logo_full_large.png</WebIcon>
</PropertyGroup>
```

**What gets generated:**

| Purpose | Files | Sizes |
|---------|-------|-------|
| **Favicon** | `favicon.ico` or `favicon.png` | 48, 32, 16px |
| **Apple Touch** | `apple-touch-icon-*.png` | 180, 167, 152, 120px |
| **PWA Manifest** | `icon-*.png` | 192, 512px |
| **Manifest** | `manifest.json` | PWA configuration with icon references |

**Build command:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture web \
  --path /path/to/your/project
```

### Requirements for Automatic Generation

**Image Processing Tools:**
- **ImageMagick** (recommended - cross-platform)
- **sips** (macOS built-in)
- **iconutil** (macOS - for .icns generation)

**Install ImageMagick:**
```bash
# macOS
brew install imagemagick

# Linux (Ubuntu/Debian)
sudo apt-get install imagemagick

# Windows (Chocolatey)
choco install imagemagick
```

**Recommended Source Image:**
- Format: PNG with transparency
- Size: 1024x1024 or larger
- Aspect Ratio: Square (1:1)
- Location: `Assets/` folder or specify path

---

## Option 2: Manual .csproj Configuration

Use .NET's built-in icon handling by configuring your `.csproj` file directly.

### Windows Desktop Icon

Add to your `.csproj`:

```xml
<PropertyGroup>
  <!-- Windows application icon -->
  <ApplicationIcon>Assets\app.ico</ApplicationIcon>
  
  <!-- Or conditionally based on runtime -->
  <ApplicationIcon Condition="'$(RuntimeIdentifier)' == 'win-x64' Or '$(RuntimeIdentifier)' == 'win-arm64'">
    Assets\windows_app.ico
  </ApplicationIcon>
</PropertyGroup>
```

**What you need to provide:**
- Create `Assets/app.ico` with multiple sizes (16, 32, 48, 256px minimum)
- You can use online tools or ImageMagick to create .ico files

**Create .ico manually:**
```bash
# Using ImageMagick
magick convert icon-256.png icon-128.png icon-48.png icon-32.png icon-16.png app.ico
```

### macOS Desktop Icon

For macOS, icons are typically embedded in the app bundle's `Info.plist` and require more setup:

```xml
<PropertyGroup>
  <!-- macOS bundle configuration -->
  <CFBundleIconFile>app</CFBundleIconFile>
</PropertyGroup>

<ItemGroup>
  <!-- Include .icns file in bundle -->
  <BundleResource Include="Assets\app.icns">
    <Link>Resources\app.icns</Link>
  </BundleResource>
</ItemGroup>
```

**What you need to provide:**
- Create `Assets/app.icns` file
- On macOS, you can create this with:

```bash
# 1. Create iconset directory
mkdir app.iconset

# 2. Generate all required sizes (using ImageMagick or sips)
sips -z 16 16     source.png --out app.iconset/icon_16x16.png
sips -z 32 32     source.png --out app.iconset/icon_16x16@2x.png
sips -z 32 32     source.png --out app.iconset/icon_32x32.png
sips -z 64 64     source.png --out app.iconset/icon_32x32@2x.png
sips -z 128 128   source.png --out app.iconset/icon_128x128.png
sips -z 256 256   source.png --out app.iconset/icon_128x128@2x.png
sips -z 256 256   source.png --out app.iconset/icon_256x256.png
sips -z 512 512   source.png --out app.iconset/icon_256x256@2x.png
sips -z 512 512   source.png --out app.iconset/icon_512x512.png
sips -z 1024 1024 source.png --out app.iconset/icon_512x512@2x.png

# 3. Convert to .icns
iconutil -c icns app.iconset -o app.icns
```

### Linux Desktop Icon

Linux desktop icons are typically installed system-wide following freedesktop.org standards:

```xml
<ItemGroup>
  <!-- Include icon files -->
  <None Include="Assets\icons\*.png" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**What you need to provide:**
- Multiple PNG files in standard sizes: 16, 22, 24, 32, 48, 64, 128, 256, 512px
- Typically organized in `hicolor` theme structure
- Desktop entry file (.desktop) references the icon

**Example .desktop file:**
```desktop
[Desktop Entry]
Type=Application
Name=MyApp
Icon=myapp
Exec=/path/to/myapp
Categories=Game;
```

### Web Icons (Manual)

For web applications, add icons to your wwwroot and reference them in HTML:

**Create icon files:**
```
wwwroot/
  ├── favicon.ico
  ├── apple-touch-icon.png (180x180)
  ├── icon-192.png
  ├── icon-512.png
  └── manifest.json
```

**Reference in HTML (`index.html`):**
```html
<head>
  <link rel="icon" href="favicon.ico" sizes="any">
  <link rel="icon" href="icon.svg" type="image/svg+xml">
  <link rel="apple-touch-icon" href="apple-touch-icon.png">
  <link rel="manifest" href="manifest.json">
</head>
```

**Create manifest.json:**
```json
{
  "name": "My Sokol App",
  "short_name": "MySokolApp",
  "start_url": "./",
  "display": "standalone",
  "background_color": "#ffffff",
  "theme_color": "#000000",
  "icons": [
    {
      "src": "icon-192.png",
      "sizes": "192x192",
      "type": "image/png"
    },
    {
      "src": "icon-512.png",
      "sizes": "512x512",
      "type": "image/png"
    }
  ]
}
```

**Build with standard dotnet:**
```bash
dotnet publish -c Release -r browser-wasm
```

---

## Option 3: Pre-generated Icons

If you have pre-generated icon files in the correct formats, simply place them in the output directory.

### Desktop (Pre-generated)

**1. Generate icons externally** (using Photoshop, GIMP, online tools, etc.)

**2. Place in your project:**
```
Assets/
  ├── app.ico      (Windows)
  ├── app.icns     (macOS)
  └── icons/       (Linux)
      ├── icon_16.png
      ├── icon_32.png
      ├── icon_48.png
      └── ...
```

**3. Copy to output in .csproj:**
```xml
<ItemGroup>
  <None Include="Assets\app.ico" CopyToOutputDirectory="PreserveNewest" 
        Condition="'$(RuntimeIdentifier)' == 'win-x64' Or '$(RuntimeIdentifier)' == 'win-arm64'" />
  <None Include="Assets\app.icns" CopyToOutputDirectory="PreserveNewest" 
        Condition="'$(RuntimeIdentifier)' == 'osx-arm64' Or '$(RuntimeIdentifier)' == 'osx-x64'" />
  <None Include="Assets\icons\**\*.png" CopyToOutputDirectory="PreserveNewest" 
        Condition="'$(RuntimeIdentifier)' == 'linux-x64' Or '$(RuntimeIdentifier)' == 'linux-arm64'" />
</ItemGroup>
```

**4. Build normally:**
```bash
dotnet publish -c Release -r win-x64
```

### Web (Pre-generated)

**1. Generate web icons** using online tools or design software

**2. Place in wwwroot:**
```
wwwroot/
  ├── favicon.ico
  ├── favicon.png
  ├── apple-touch-icon-180x180.png
  ├── apple-touch-icon-167x167.png
  ├── apple-touch-icon-152x152.png
  ├── apple-touch-icon-120x120.png
  ├── icon-192x192.png
  ├── icon-512x512.png
  └── manifest.json
```

**3. Build normally** - files are copied automatically

---

## Comparison of Approaches

| Aspect | Automatic Generation | Manual .csproj | Pre-generated |
|--------|---------------------|----------------|---------------|
| **Ease of Use** | ⭐⭐⭐⭐⭐ Easiest | ⭐⭐⭐ Moderate | ⭐⭐⭐⭐ Easy |
| **Flexibility** | ⭐⭐⭐ Good | ⭐⭐⭐⭐⭐ Full control | ⭐⭐⭐⭐⭐ Full control |
| **Cross-platform** | ⭐⭐⭐⭐⭐ All platforms | ⭐⭐⭐ Platform-specific | ⭐⭐⭐⭐ All platforms |
| **Requirements** | ImageMagick/sips | Icon creation tools | Icon creation tools |
| **Build Integration** | SokolApplicationBuilder | dotnet publish | dotnet publish |
| **Icon Quality** | ⭐⭐⭐⭐ Very good | ⭐⭐⭐⭐⭐ Depends on source | ⭐⭐⭐⭐⭐ Depends on source |
| **Best For** | Quick setup, consistency | Native .NET integration | Design-heavy projects |

---

## Quick Start Examples

### Example 1: Quick Automatic Setup (All Platforms)

**Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <AndroidIcon>myicon.png</AndroidIcon>
    <IOSIcon>myicon.png</IOSIcon>
    <DesktopIcon>myicon.png</DesktopIcon>
    <WebIcon>myicon.png</WebIcon>
  </PropertyGroup>
</Project>
```

Place `myicon.png` (1024x1024) in `Assets/` folder. Done!

### Example 2: Platform-Specific Icons

**Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <AndroidIcon>icons/android_icon.png</AndroidIcon>
    <IOSIcon>icons/ios_icon.png</IOSIcon>
    <DesktopIcon>icons/desktop_icon.png</DesktopIcon>
    <WebIcon>icons/web_icon.png</WebIcon>
  </PropertyGroup>
</Project>
```

### Example 3: Mixed Approach

Use automatic for mobile, manual for desktop/web:

**Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <!-- Automatic for mobile -->
    <AndroidIcon>mobile_icon.png</AndroidIcon>
    <IOSIcon>mobile_icon.png</IOSIcon>
  </PropertyGroup>
</Project>
```

**.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Manual for desktop -->
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
  </PropertyGroup>
</Project>
```

---

## Troubleshooting

### Desktop Icons Not Showing

**Windows:**
- Ensure `app.ico` is in the output directory
- Icon cache may need clearing: `ie4uinit.exe -show`
- Check file association in Registry

**macOS:**
- Verify `app.icns` is in `MyApp.app/Contents/Resources/`
- Check `Info.plist` has `CFBundleIconFile`
- Clear icon cache: `sudo rm -rf /Library/Caches/com.apple.iconservices.store`

**Linux:**
- Verify `.desktop` file has correct `Icon=` path
- Update icon cache: `gtk-update-icon-cache`
- Check XDG_DATA_DIRS environment variable

### Web Icons Not Loading

- Check browser console for 404 errors
- Verify paths in `manifest.json` are relative to manifest location
- Clear browser cache
- Check MIME types are correct for icon files

### ImageMagick Not Found

If automatic generation fails:
```bash
# Check if installed
which magick
which convert

# Install if missing
brew install imagemagick  # macOS
```

### Icon Looks Pixelated

- Use higher resolution source image (1024x1024 minimum)
- Ensure source is PNG with transparency
- For best results, design icons at each target size

---

## Best Practices

1. **Source Image Quality**
   - Use 1024x1024 or larger PNG
   - Design with simplicity - complex details don't scale well
   - Test at smallest size (16x16) to ensure clarity

2. **Choose the Right Approach**
   - **Automatic**: For rapid prototyping, consistent builds
   - **Manual**: For production apps needing fine-tuned icons
   - **Pre-generated**: When designers provide specific icon assets

3. **Platform-Specific Considerations**
   - **Windows**: Test with light/dark Windows themes
   - **macOS**: Ensure transparency works with dark mode
   - **Linux**: Follow freedesktop.org specifications
   - **Web**: Provide both light and dark mode variants

4. **Version Control**
   - Commit source images (PNG) to version control
   - Add generated icons to `.gitignore` if using automatic generation
   - Commit pre-generated icons if using manual approach

5. **Testing**
   - Test icons on actual devices, not just simulators
   - Check appearance in all contexts (taskbar, alt-tab, about dialog)
   - Verify PWA "Add to Home Screen" shows correct icon

---

## See Also

- [Android & iOS Icon Configuration](APP_ICON.md)
- [Directory.Build.props Examples](../examples/cube/Directory.Build.props)
- [Building Desktop Applications](DESKTOP_BUILD.md)
- [Building Web Applications](WEB_BUILD.md)

# Quick Start: Application Icons

Get your custom icons working across all platforms in 5 minutes!

## Step 1: Prepare Your Icon

Create or find a high-quality icon:
- **Format**: PNG with transparency
- **Size**: 1024x1024 pixels (minimum)
- **Aspect Ratio**: Square (1:1)

**Tip**: Design simply - complex details don't scale well to 16x16px

## Step 2: Add Icon to Your Project

Place your icon in the Assets folder:
```
your-project/
  Assets/
    myicon.png  ← Your 1024x1024 PNG icon here
  Source/
  Directory.Build.props
```

## Step 3: Configure Directory.Build.props

Open `Directory.Build.props` and add icon properties:

```xml
<Project>
  <PropertyGroup>
    <!-- Your existing properties... -->
  </PropertyGroup>

  <!-- Android Configuration -->
  <PropertyGroup>
    <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
    <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
    
    <!-- 📱 Add this line -->
    <AndroidIcon>myicon.png</AndroidIcon>
  </PropertyGroup>

  <!-- iOS Configuration -->
  <PropertyGroup>
    <IOSMinVersion>14.0</IOSMinVersion>
    
    <!-- 📱 Add this line -->
    <IOSIcon>myicon.png</IOSIcon>
  </PropertyGroup>

  <!-- Desktop Configuration -->
  <PropertyGroup>
    <!-- 🖥️ Add this line -->
    <DesktopIcon>myicon.png</DesktopIcon>
  </PropertyGroup>

  <!-- Web Configuration -->
  <PropertyGroup>
    <!-- 🌐 Add this line -->
    <WebIcon>myicon.png</WebIcon>
  </PropertyGroup>
</Project>
```

## Step 4: Install ImageMagick (Optional but Recommended)

For best results, install ImageMagick for automatic icon resizing:

```bash
# macOS
brew install imagemagick

# Linux (Ubuntu/Debian)
sudo apt-get install imagemagick

# Windows (Chocolatey)
choco install imagemagick
```

**Without ImageMagick**: The build will copy your original icon (not ideal, but works).

## Step 5: Build Your App

Build for your target platform:

### Android
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture android \
  --path /path/to/your/project
```

### iOS
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture ios \
  --path /path/to/your/project
```

### Desktop (macOS)
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid osx-arm64 \
  --path /path/to/your/project
```

### Desktop (Windows)
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid win-x64 \
  --path /path/to/your/project
```

### Desktop (Linux)
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid linux-x64 \
  --path /path/to/your/project
```

### Web
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture web \
  --path /path/to/your/project
```

## Step 6: Verify Your Icons

Check the build output for icon generation messages:

### Android
```
📱 Processing Android icon: myicon.png
   ✅ Created mipmap-mdpi/ic_launcher.png (48x48)
   ✅ Created mipmap-hdpi/ic_launcher.png (72x72)
   ✅ Created mipmap-xhdpi/ic_launcher.png (96x96)
   ✅ Created mipmap-xxhdpi/ic_launcher.png (144x144)
   ✅ Created mipmap-xxxhdpi/ic_launcher.png (192x192)
✅ Android icon processed successfully
```

### iOS
```
📱 Processing iOS icon: myicon.png
   ✅ Created icon-20@2x.png (40x40)
   ✅ Created icon-20@3x.png (60x60)
   ... (18 sizes total)
   ✅ Created icon-1024.png (1024x1024)
✅ iOS icon processed successfully
```

### Desktop
```
🖥️  Processing Desktop icon: myicon.png
   ✅ Created app.ico            (Windows)
   ✅ Created app.icns           (macOS)
   ✅ Created icon_16.png        (Linux)
   ... (9 sizes total for Linux)
✅ Desktop icon processed successfully
```

### Web
```
🌐 Processing Web icon: myicon.png
   ✅ Created favicon.ico
   ✅ Created apple-touch-icon-180x180.png
   ✅ Created apple-touch-icon-167x167.png
   ✅ Created apple-touch-icon-152x152.png
   ✅ Created apple-touch-icon-120x120.png
   ✅ Created icon-192x192.png
   ✅ Created icon-512x512.png
   ✅ Created manifest.json
✅ Web icon processed successfully
```

## Done! 🎉

Your app now has custom icons on all platforms!

---

## Troubleshooting

### "No AndroidIcon specified"
- Check that `<AndroidIcon>myicon.png</AndroidIcon>` is in Directory.Build.props
- Verify the icon file exists in the Assets folder
- File names are case-sensitive on Linux/macOS

### "Icon not found: myicon.png"
- Ensure the icon is in `Assets/myicon.png`
- Try using an absolute path: `<AndroidIcon>/full/path/to/myicon.png</AndroidIcon>`
- Check file permissions

### "Image resizing tools not found"
- Install ImageMagick (see Step 4)
- The build will continue but copy the original icon
- Icons may not look good at small sizes

### Icons don't appear in app
- **Android**: Uninstall the old APK first, then reinstall
- **iOS**: Delete app from device, then reinstall
- **Desktop**: Clear icon cache (platform-specific)
- **Web**: Clear browser cache

### ImageMagick not found on macOS
```bash
# Check if installed
which magick

# If not found, install via Homebrew
brew install imagemagick
```

---

## Next Steps

### Use Different Icons for Each Platform
```xml
<PropertyGroup>
  <AndroidIcon>android_icon.png</AndroidIcon>
  <IOSIcon>ios_icon.png</IOSIcon>
  <DesktopIcon>desktop_icon.png</DesktopIcon>
  <WebIcon>web_icon.png</WebIcon>
</PropertyGroup>
```

### Icons in Subdirectories
```xml
<AndroidIcon>icons/android/launcher.png</AndroidIcon>
```

### Advanced Configuration

📖 **See full documentation:**
- [Android & iOS Icons](APP_ICON.md)
- [Desktop & Web Icons (All Approaches)](DESKTOP_WEB_ICONS.md)
- [Build Configuration](../examples/cube/Directory.Build.props)

---

## Full Example

Here's a complete minimal Directory.Build.props with icons:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <Platforms>AnyCPU</Platforms>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <!-- Android Configuration -->
  <PropertyGroup>
    <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
    <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
    <AndroidFullscreen>true</AndroidFullscreen>
    <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
    <AndroidIcon>myicon.png</AndroidIcon>
  </PropertyGroup>

  <!-- iOS Configuration -->
  <PropertyGroup>
    <IOSMinVersion>14.0</IOSMinVersion>
    <IOSScreenOrientation>landscape</IOSScreenOrientation>
    <IOSStatusBarHidden>true</IOSStatusBarHidden>
    <IOSIcon>myicon.png</IOSIcon>
  </PropertyGroup>

  <!-- Desktop Configuration -->
  <PropertyGroup>
    <DesktopIcon>myicon.png</DesktopIcon>
  </PropertyGroup>

  <!-- Web Configuration -->
  <PropertyGroup>
    <WebIcon>myicon.png</WebIcon>
  </PropertyGroup>
</Project>
```

**That's it!** Place `myicon.png` in your `Assets/` folder and build. 🚀

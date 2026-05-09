#!/bin/sh
set -e
if test "$CONFIGURATION" = "Debug"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  /Applications/CMake.app/Contents/bin/cmake -E copy /Users/elialoni/Development/Graphics/Sokol.NET/ext/Info.plist /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/Debug${EFFECTIVE_PLATFORM_NAME}/sokol.framework/Info.plist
fi
if test "$CONFIGURATION" = "Release"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  /Applications/CMake.app/Contents/bin/cmake -E copy /Users/elialoni/Development/Graphics/Sokol.NET/ext/Info.plist /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/Release${EFFECTIVE_PLATFORM_NAME}/sokol.framework/Info.plist
fi
if test "$CONFIGURATION" = "MinSizeRel"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  /Applications/CMake.app/Contents/bin/cmake -E copy /Users/elialoni/Development/Graphics/Sokol.NET/ext/Info.plist /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/MinSizeRel${EFFECTIVE_PLATFORM_NAME}/sokol.framework/Info.plist
fi
if test "$CONFIGURATION" = "RelWithDebInfo"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  /Applications/CMake.app/Contents/bin/cmake -E copy /Users/elialoni/Development/Graphics/Sokol.NET/ext/Info.plist /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/RelWithDebInfo${EFFECTIVE_PLATFORM_NAME}/sokol.framework/Info.plist
fi


#!/bin/sh
set -e
if test "$CONFIGURATION" = "Debug"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  make -f /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/CMakeScripts/ReRunCMake.make
fi
if test "$CONFIGURATION" = "Release"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  make -f /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/CMakeScripts/ReRunCMake.make
fi
if test "$CONFIGURATION" = "MinSizeRel"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  make -f /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/CMakeScripts/ReRunCMake.make
fi
if test "$CONFIGURATION" = "RelWithDebInfo"; then :
  cd /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios
  make -f /Users/elialoni/Development/Graphics/Sokol.NET/examples/JoltPhysicsDemo/ios/sokol-ios/CMakeScripts/ReRunCMake.make
fi


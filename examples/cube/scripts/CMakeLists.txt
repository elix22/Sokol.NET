cmake_minimum_required(VERSION 3.16)

project(cube-ios-app)

# Set iOS deployment target
set(CMAKE_OSX_DEPLOYMENT_TARGET "12.0" CACHE STRING "Minimum iOS deployment version")

# Configure for iOS
set(CMAKE_SYSTEM_NAME iOS)
set(CMAKE_OSX_SYSROOT iphoneos)

# Enable Objective-C
enable_language(OBJC)

# Add the main.m source file
set(SOURCES
    main.m
)

# Create the iOS application executable
add_executable(cube-ios-app ${SOURCES})

# Set iOS-specific properties
set_target_properties(cube-ios-app PROPERTIES
    MACOSX_BUNDLE TRUE
    MACOSX_BUNDLE_GUI_IDENTIFIER "com.elix22.cube-ios-app"
    MACOSX_BUNDLE_BUNDLE_NAME "Cube iOS App"
    MACOSX_BUNDLE_BUNDLE_VERSION "1.0"
    MACOSX_BUNDLE_SHORT_VERSION_STRING "1.0"
    XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED "YES"
    XCODE_ATTRIBUTE_CODE_SIGNING_REQUIRED "YES"
    XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY "iPhone Developer"
    XCODE_ATTRIBUTE_DEVELOPMENT_TEAM ""
    XCODE_ATTRIBUTE_PROVISIONING_PROFILE_SPECIFIER ""
    XCODE_ATTRIBUTE_LD_RUNPATH_SEARCH_PATHS "@executable_path/Frameworks"
    XCODE_ATTRIBUTE_DYLIB_INSTALL_NAME_BASE "@rpath"
    # iPad support configurations
    XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"  # 1=iPhone, 2=iPad (Universal)
    XCODE_ATTRIBUTE_IPAD_PROVISIONING_PROFILE_SPECIFIER ""
    XCODE_ATTRIBUTE_SUPPORTS_MACCATALYST "NO"
    # Screen orientations
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations "UIInterfaceOrientationPortrait UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight"
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations~ipad "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight"
    # Launch screen
    XCODE_ATTRIBUTE_UILaunchScreen~ipad "{}"
)

# Embed the frameworks
set(FRAMEWORK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/frameworks")

# Embed sokol.framework
set_source_files_properties(
    "${FRAMEWORK_DIR}/sokol.framework"
    PROPERTIES
    MACOSX_PACKAGE_LOCATION Frameworks
    XCODE_FILE_ATTRIBUTES "CodeSignOnCopy;RemoveHeadersOnCopy"
)
target_sources(cube-ios-app PRIVATE "${FRAMEWORK_DIR}/sokol.framework")

# Embed cube.framework
set_source_files_properties(
    "${FRAMEWORK_DIR}/cube.framework"
    PROPERTIES
    MACOSX_PACKAGE_LOCATION Frameworks
    XCODE_FILE_ATTRIBUTES "CodeSignOnCopy;RemoveHeadersOnCopy"
)
target_sources(cube-ios-app PRIVATE "${FRAMEWORK_DIR}/cube.framework")

# Link against the frameworks
target_link_libraries(cube-ios-app
    "-F${FRAMEWORK_DIR}"
    "-framework sokol"
    "-framework cube"
    "-framework UIKit"
    "-framework Foundation"
)

# Set the output directory for the app bundle
set_target_properties(cube-ios-app PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

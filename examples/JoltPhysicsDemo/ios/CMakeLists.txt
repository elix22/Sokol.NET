cmake_minimum_required(VERSION 3.16)

project(JoltPhysicsDemo-ios-app)

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

# Include assets in the bundle
set(ASSETS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../Assets")
if(EXISTS ${ASSETS_DIR})
    # Find all files directly in Assets directory (not in subdirectories)
    file(GLOB ASSETS_FILES "${ASSETS_DIR}/*")
    foreach(ASSET_ITEM ${ASSETS_FILES})
        if(NOT IS_DIRECTORY ${ASSET_ITEM})
            # This is a file directly in Assets - add to Resources
            list(APPEND SOURCES ${ASSET_ITEM})
            set_source_files_properties(${ASSET_ITEM} PROPERTIES
                MACOSX_PACKAGE_LOCATION "Resources"
            )
        else()
            # This is a subdirectory - add the entire directory to Resources
            list(APPEND SOURCES ${ASSET_ITEM})
            set_source_files_properties(${ASSET_ITEM} PROPERTIES
                MACOSX_PACKAGE_LOCATION "Resources"
            )
        endif()
    endforeach()
endif()

# Create the iOS application executable
add_executable(JoltPhysicsDemo-ios-app ${SOURCES})

# Set iOS-specific properties
set_target_properties(JoltPhysicsDemo-ios-app PROPERTIES
    MACOSX_BUNDLE TRUE
    MACOSX_BUNDLE_GUI_IDENTIFIER "com.sokol.JoltPhysicsDemo-ios-app"
    MACOSX_BUNDLE_BUNDLE_NAME "JoltPhysicsDemo iOS App"
    MACOSX_BUNDLE_BUNDLE_VERSION "1.0"
    MACOSX_BUNDLE_SHORT_VERSION_STRING "1.0"
    XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED "YES"
    XCODE_ATTRIBUTE_CODE_SIGNING_REQUIRED "YES"
    XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY "iPhone Developer"
    XCODE_ATTRIBUTE_LD_RUNPATH_SEARCH_PATHS "@executable_path/Frameworks"
    XCODE_ATTRIBUTE_DYLIB_INSTALL_NAME_BASE "@rpath"
    # iPad support configurations
    XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"  # 1=iPhone, 2=iPad (Universal)
    XCODE_ATTRIBUTE_IPAD_PROVISIONING_PROFILE_SPECIFIER ""
    XCODE_ATTRIBUTE_SUPPORTS_MACCATALYST "NO"
    # Screen orientations
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations "UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight"
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations~ipad "UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight"
    # Launch screen
    XCODE_ATTRIBUTE_UILaunchScreen~ipad "{}"
    # App icon from asset catalog
    XCODE_ATTRIBUTE_ASSETCATALOG_COMPILER_APPICON_NAME "AppIcon"
    # Info.plist entries for orientation
    MACOSX_BUNDLE_INFO_PLIST "${CMAKE_CURRENT_SOURCE_DIR}/Info.plist.in"
)

# Include app icon assets if they exist - must be added AFTER target creation
set(ASSETS_XCASSETS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Assets.xcassets")
if(EXISTS ${ASSETS_XCASSETS_DIR})
    # Add the asset catalog to the target's resources
    target_sources(JoltPhysicsDemo-ios-app PRIVATE "${ASSETS_XCASSETS_DIR}")
    set_source_files_properties("${ASSETS_XCASSETS_DIR}" PROPERTIES
        MACOSX_PACKAGE_LOCATION "Resources"
    )
endif()

# Set development team if provided
if(DEFINED DEVELOPMENT_TEAM)
    set_target_properties(JoltPhysicsDemo-ios-app PROPERTIES
        XCODE_ATTRIBUTE_DEVELOPMENT_TEAM "${DEVELOPMENT_TEAM}"
    )
endif()

# Embed the frameworks
set(FRAMEWORK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/frameworks")


# Embed frameworks (dynamically configured based on detected native libraries)
set(CMAKE_XCODE_ATTRIBUTE_EMBED_FRAMEWORKS "${FRAMEWORK_DIR}/sokol.framework;${FRAMEWORK_DIR}/JoltPhysicsDemo.framework;${FRAMEWORK_DIR}/cjolt.framework")
set_target_properties(JoltPhysicsDemo-ios-app PROPERTIES
    XCODE_EMBED_FRAMEWORKS "${FRAMEWORK_DIR}/sokol.framework;${FRAMEWORK_DIR}/JoltPhysicsDemo.framework;${FRAMEWORK_DIR}/cjolt.framework"
    XCODE_EMBED_FRAMEWORKS_CODE_SIGN_ON_COPY ON
    XCODE_ATTRIBUTE_STRIP_STYLE "non-global"
    XCODE_ATTRIBUTE_DEPLOYMENT_POSTPROCESSING "NO"
    XCODE_ATTRIBUTE_STRIP_INSTALLED_PRODUCT "NO"
)

# Link against the frameworks (dynamically configured based on detected native libraries)
target_link_libraries(JoltPhysicsDemo-ios-app
    "-F${FRAMEWORK_DIR}"
    "-framework sokol"
    "-framework JoltPhysicsDemo"
    "-framework cjolt"
    "-framework UIKit"
    "-framework Foundation"
)

# Set the output directory for the app bundle
set_target_properties(JoltPhysicsDemo-ios-app PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

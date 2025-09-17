cmake_minimum_required(VERSION 3.16)

project(TEMPLATE_PROJECT_NAME-ios-app)

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
add_executable(TEMPLATE_PROJECT_NAME-ios-app ${SOURCES})

# Set iOS-specific properties
set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
    MACOSX_BUNDLE TRUE
    MACOSX_BUNDLE_GUI_IDENTIFIER "com.elix22.TEMPLATE_PROJECT_NAME-ios-app"
    MACOSX_BUNDLE_BUNDLE_NAME "TEMPLATE_PROJECT_NAME iOS App"
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
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations "UIInterfaceOrientationPortrait UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight"
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations~ipad "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight"
    # Launch screen
    XCODE_ATTRIBUTE_UILaunchScreen~ipad "{}"
)

# Set development team if provided
if(DEFINED DEVELOPMENT_TEAM)
    set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
        XCODE_ATTRIBUTE_DEVELOPMENT_TEAM "${DEVELOPMENT_TEAM}"
    )
endif()

# Embed the frameworks
set(FRAMEWORK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/frameworks")


# Embed sokol.framework
set(CMAKE_XCODE_ATTRIBUTE_EMBED_FRAMEWORKS "${FRAMEWORK_DIR}/sokol.framework;${FRAMEWORK_DIR}/TEMPLATE_PROJECT_NAME.framework")
set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
    XCODE_EMBED_FRAMEWORKS "${FRAMEWORK_DIR}/sokol.framework;${FRAMEWORK_DIR}/TEMPLATE_PROJECT_NAME.framework"
    XCODE_EMBED_FRAMEWORKS_CODE_SIGN_ON_COPY ON
    XCODE_ATTRIBUTE_STRIP_STYLE "non-global"
    XCODE_ATTRIBUTE_DEPLOYMENT_POSTPROCESSING "NO"
    XCODE_ATTRIBUTE_STRIP_INSTALLED_PRODUCT "NO"
)


# Embed sokol.framework

# Link against the frameworks
target_link_libraries(TEMPLATE_PROJECT_NAME-ios-app
    "-F${FRAMEWORK_DIR}"
    "-framework sokol"
    "-framework TEMPLATE_PROJECT_NAME"
    "-framework UIKit"
    "-framework Foundation"
)

# Set the output directory for the app bundle
set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

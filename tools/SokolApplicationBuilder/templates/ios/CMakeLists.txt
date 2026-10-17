cmake_minimum_required(VERSION 3.16)

project(TEMPLATE_PROJECT_NAME-ios-app)

# Set iOS deployment target
set(CMAKE_OSX_DEPLOYMENT_TARGET "TEMPLATE_IOS_MIN_VERSION" CACHE STRING "Minimum iOS deployment version")

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
    # Pin the app target's minimum iOS version. Xcode stamps both the Mach-O
    # min-OS load command and Info.plist MinimumOSVersion from this build setting;
    # without it, Xcode defaults to the (current SDK) version and the bundle is
    # rejected on older devices ("DeviceOSVersionTooLow").
    XCODE_ATTRIBUTE_IPHONEOS_DEPLOYMENT_TARGET "TEMPLATE_IOS_MIN_VERSION"
    XCODE_ATTRIBUTE_CLANG_ENABLE_OBJC_ARC "YES"   # iOS builds with ARC, like the sokol framework the app embeds (ext/CMakeLists.txt)
    MACOSX_BUNDLE_GUI_IDENTIFIER "TEMPLATE_BUNDLE_ID"
    MACOSX_BUNDLE_BUNDLE_NAME "TEMPLATE_BUNDLE_DISPLAY_NAME"
    MACOSX_BUNDLE_BUNDLE_VERSION "TEMPLATE_APP_VERSION"
    MACOSX_BUNDLE_SHORT_VERSION_STRING "TEMPLATE_APP_VERSION"
    XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED "YES"
    XCODE_ATTRIBUTE_CODE_SIGNING_REQUIRED "YES"
    XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY "iPhone Developer"
    # /usr/lib/swift first — the OS Swift runtime dylibs (e.g. weak-linked
    # libswift_Concurrency) carry @rpath install names; Xcode's own app templates
    # always search there before the bundle
    XCODE_ATTRIBUTE_LD_RUNPATH_SEARCH_PATHS "/usr/lib/swift @executable_path/Frameworks"
    XCODE_ATTRIBUTE_DYLIB_INSTALL_NAME_BASE "@rpath"
    # iPad support configurations
    XCODE_ATTRIBUTE_TARGETED_DEVICE_FAMILY "1,2"  # 1=iPhone, 2=iPad (Universal)
    XCODE_ATTRIBUTE_IPAD_PROVISIONING_PROFILE_SPECIFIER ""
    XCODE_ATTRIBUTE_SUPPORTS_MACCATALYST "NO"
    # Screen orientations
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations "TEMPLATE_IOS_ORIENTATIONS"
    XCODE_ATTRIBUTE_UISupportedInterfaceOrientations~ipad "TEMPLATE_IPAD_ORIENTATIONS"
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
    target_sources(TEMPLATE_PROJECT_NAME-ios-app PRIVATE "${ASSETS_XCASSETS_DIR}")
    set_source_files_properties("${ASSETS_XCASSETS_DIR}" PROPERTIES
        MACOSX_PACKAGE_LOCATION "Resources"
    )
endif()

# Privacy manifest at the bundle root, if the project has one (IOSAppBuilder.CopyPrivacyManifest)
set(PRIVACY_MANIFEST "${CMAKE_CURRENT_SOURCE_DIR}/PrivacyInfo.xcprivacy")
if(EXISTS ${PRIVACY_MANIFEST})
    target_sources(TEMPLATE_PROJECT_NAME-ios-app PRIVATE "${PRIVACY_MANIFEST}")
    set_source_files_properties("${PRIVACY_MANIFEST}" PROPERTIES
        MACOSX_PACKAGE_LOCATION "Resources"
    )
endif()

# <lang>.lproj folders at the bundle root, if the project has them (IOSAppBuilder.CopyLocalizations)
file(GLOB LOCALIZATION_DIRS "${CMAKE_CURRENT_SOURCE_DIR}/Localizations/*.lproj")
foreach(LPROJ ${LOCALIZATION_DIRS})
    target_sources(TEMPLATE_PROJECT_NAME-ios-app PRIVATE "${LPROJ}")
    set_source_files_properties("${LPROJ}" PROPERTIES
        MACOSX_PACKAGE_LOCATION "Resources"
    )
endforeach()

# Set development team if provided
if(DEFINED DEVELOPMENT_TEAM)
    set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
        XCODE_ATTRIBUTE_DEVELOPMENT_TEAM "${DEVELOPMENT_TEAM}"
    )
endif()

# Embed the frameworks
set(FRAMEWORK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/frameworks")


# Embed frameworks (dynamically configured based on detected native libraries)
set(CMAKE_XCODE_ATTRIBUTE_EMBED_FRAMEWORKS "TEMPLATE_EMBED_FRAMEWORKS_LIST")
set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
    XCODE_EMBED_FRAMEWORKS "TEMPLATE_EMBED_FRAMEWORKS_LIST"
    XCODE_EMBED_FRAMEWORKS_CODE_SIGN_ON_COPY ON
    XCODE_ATTRIBUTE_STRIP_STYLE "non-global"
    XCODE_ATTRIBUTE_DEPLOYMENT_POSTPROCESSING "NO"
    XCODE_ATTRIBUTE_STRIP_INSTALLED_PRODUCT "NO"
    # Swift lib search paths — needed when a static Swift vendor SDK is linked into the
    # app executable (IOSStaticFrameworks_*); "$(inherited)" otherwise.
    XCODE_ATTRIBUTE_LIBRARY_SEARCH_PATHS "TEMPLATE_LIBRARY_SEARCH_PATHS"
)

# Link against the frameworks (dynamically configured based on detected native libraries)
target_link_libraries(TEMPLATE_PROJECT_NAME-ios-app
    "-F${FRAMEWORK_DIR}"
    TEMPLATE_FRAMEWORK_LINKS
    "-framework UIKit"
    "-framework Foundation"
)

# Set the output directory for the app bundle
set_target_properties(TEMPLATE_PROJECT_NAME-ios-app PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

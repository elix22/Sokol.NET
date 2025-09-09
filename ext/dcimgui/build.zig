const std = @import("std");
const Build = std.Build;

const imgui_sources = [_][]const u8{
    "cimgui.cpp",
    "cimgui_internal.cpp",
    "imgui_demo.cpp",
    "imgui_draw.cpp",
    "imgui_tables.cpp",
    "imgui_widgets.cpp",
    "imgui.cpp",
};

// returned by the getConfig() helper function to get a matching
// set of module name, C header path and C library name for
// vanilla imgui vs imgui docking branch (because mismatches
// may appear to build but then cause hilarious runtime bugs)
pub const Config = struct {
    module_name: []const u8, // cimgui or cimgui_docking
    include_dir: []const u8, // src or src-docking
    clib_name: []const u8, // cimgui_clib or cimgui_docking_clib
};

// helper function to return a matching set of Zig module name,
// C header search path and C library name for docking vs non-docking
pub fn getConfig(docking: bool) Config {
    if (docking) {
        return .{
            .module_name = "cimgui_docking",
            .include_dir = "src-docking",
            .clib_name = "cimgui_docking_clib",
        };
    } else {
        return .{
            .module_name = "cimgui",
            .include_dir = "src",
            .clib_name = "cimgui_clib",
        };
    }
}

pub fn build(b: *std.Build) !void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});
    const opt_dynamic_linkage = b.option(bool, "dynamic_linkage", "Builds cimgui_clib artifact with dynamic linkage.") orelse false;

    // the regular imgui module
    try buildModule(b, .{
        .modname = "cimgui",
        .subdir = "src",
        .sources = &imgui_sources,
        .target = target,
        .optimize = optimize,
        .linkage = if (opt_dynamic_linkage) .dynamic else .static,
    });

    // ...and the imgui_docking module
    try buildModule(b, .{
        .modname = "cimgui_docking",
        .subdir = "src-docking",
        .sources = &imgui_sources,
        .target = target,
        .optimize = optimize,
        .linkage = if (opt_dynamic_linkage) .dynamic else .static,
    });
}

const BuildModuleOptions = struct {
    modname: []const u8,
    subdir: []const u8,
    sources: []const []const u8,
    target: Build.ResolvedTarget,
    optimize: std.builtin.OptimizeMode,
    linkage: std.builtin.LinkMode,
};

fn buildModule(b: *std.Build, opts: BuildModuleOptions) !void {
    var cflags_buf: [16][]const u8 = undefined;
    var cflags = std.ArrayListUnmanaged([]const u8).initBuffer(&cflags_buf);
    if (opts.target.result.cpu.arch.isWasm()) {
        // on WASM, switch off UBSAN (zig-cc enables this by default in debug mode)
        // but it requires linking with an ubsan runtime)
        try cflags.appendBounded("-fno-sanitize=undefined");
    }

    // build imgui into a C library
    const mod_clib = b.addModule(b.fmt("mod_{s}_clib", .{opts.modname}), .{
        .target = opts.target,
        .optimize = opts.optimize,
        .link_libc = true,
        .link_libcpp = true,
    });
    for (imgui_sources) |src| {
        mod_clib.addCSourceFile(.{
            .file = b.path(b.fmt("{s}/{s}", .{ opts.subdir, src })),
            .flags = cflags.items,
        });
    }
    const clib = b.addLibrary(.{
        .name = b.fmt("{s}_clib", .{opts.modname}),
        .root_module = mod_clib,
        .linkage = opts.linkage,
    });
    // make the C library available as artifact, this allows to inject
    // the Emscripten sysroot include path in the upstream project
    b.installArtifact(clib);

    // translate-c the cimgui.h file
    // NOTE: running this step with the host target is intended to avoid
    // any Emscripten header search path shenanigans
    const translateC = b.addTranslateC(.{
        .root_source_file = b.path(b.fmt("{s}/cimgui_all.h", .{opts.subdir})),
        .target = b.graph.host,
        .optimize = opts.optimize,
    });

    // ...and the Zig module for the generated bindings
    const mod = b.addModule(opts.modname, .{
        .root_source_file = translateC.getOutput(),
        .target = opts.target,
        .optimize = opts.optimize,
        .link_libc = true,
        .link_libcpp = true,
    });
    mod.linkLibrary(clib);
}

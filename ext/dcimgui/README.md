[![Build](https://github.com/floooh/dcimgui/actions/workflows/build.yml/badge.svg)](https://github.com/floooh/dcimgui/actions/workflows/build.yml)

A version-tagged all-in-one [Dear ImGui](https://github.com/ocornut/imgui)
source distribution repository for C, C++ and Zig coding with:

- regular and docking flavours of Dear ImGui
- C bindings for both flavours (generated with the
  new [dear_bindings](https://github.com/dearimgui/dear_bindings) approach.

The C bindings use the `ig` prefix and cimgui.h/cimgui.cpp filenames to be as
compatible as possible with the 'legacy' cimgui bindings (but please be aware
that there are still significant differences to the legacy cimgui bindings).

The CMakeLists.txt file can be used both from regular cmake projects and
from fips projects (https://floooh.github.io/fips/) and defines two
static link libraries (`imgui` and `imgui-docking`).

NOTE: do not use the `imgui` and `imgui-docking` libraries together in the
same project since this will confuse header search paths.

To use the C API:

- for the regular version: link with `imgui` and include `cimgui.h`
- for the docking version: link with `imgui-docking` and include `cimgui.h`
- NOTE: the cimgui.h header contains duplicate symbol definitions and
  must be compiled in C11 mode.

To use the C++ API:

- for regular version: link with `imgui` and include `imgui.h`
- for the docking version: link with `imgui-docking` and include `imgui.h`

To use the Zig module:

- add a dependency to your build.zig.zon:
    ```
    zig fetch --save=cimgui git+https://github.com/floooh/dcimgui.git
    ```
- ...and see https://github.com/floooh/sokol-zig-imgui-sample/blob/main/build.zig
  for how to integrate the `cimgui` module with your Zig project

# plmpeg Example

This example demonstrates video playback using the pl_mpeg library to decode and render MPEG-1 video files.

## Video Asset

### BigBuckBunny.mpg

**Source:** [Big Buck Bunny - Blender Peach Project](https://peach.blender.org/download/)

**About:** Big Buck Bunny is an open-source animated short film created by the Blender Foundation as part of the Peach open movie project.

**License:** [Creative Commons Attribution 3.0](https://creativecommons.org/licenses/by/3.0/)

The results of the Peach open movie project have been licensed under the Creative Commons Attribution 3.0 license. This includes all the data published online and on the DVDs, and all of the contents on the website. 

In short, this means you can freely reuse and distribute this content, also commercially, for as long as you provide proper attribution.

**License Information:** https://peach.blender.org/about/

**Attribution:**
- **Title:** Big Buck Bunny
- **Creator:** Blender Foundation
- **Source:** https://peach.blender.org/
- **Year:** 2008

## Usage

The example loads and plays the MPEG-1 video file, demonstrating:
- Video decoding using pl_mpeg
- YUV to RGB conversion in shaders
- Audio playback synchronization
- Video texture updates

## Supported Platforms

- Desktop (Windows, macOS, Linux)
- Web (WebAssembly)
- iOS
- Android

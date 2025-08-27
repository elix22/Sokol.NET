#!/bin/bash

# Check if the input file name is provided as a command-line argument
if [ -z "$1" ]; then
    echo "Usage: $0 <input_file>"
    exit 1
fi

input_file="$1"

# Extract the base name of the input file (without extension)
base_name=$(basename "$input_file" | cut -d. -f1)

# Set the output file name with .cs extension
output_file="${base_name}-shader.cs"
# --reflection
#  sokol-shdc --input "$input_file" --output shaders/compiled/"$output_file" --slang metal_macos --genver 5 -f sokol_csharp --bytecode
 sokol-shdc --input "$input_file" --output shaders/compiled/"$output_file" --slang glsl300es:glsl430:hlsl5:metal_macos:metal_ios -f sokol_csharp --bytecode

#OSX
# sokol-shdc --input "$input_file" --output shaders/compiled/osx/"$output_file" --slang metal_macos -f sokol_csharp --bytecode
#Windows
# sokol-shdc --input "$input_file" --output shaders/compiled/windows/"$output_file" --slang hlsl5 -f sokol_csharp --bytecode
#Linux
# sokol-shdc --input "$input_file" --output shaders/compiled/linux/"$output_file" --slang glsl430 -f sokol_csharp 
#iOS
# sokol-shdc --input "$input_file" --output shaders/compiled/ios/"$output_file" --slang metal_ios -f sokol_csharp --bytecode
#Android
#sokol-shdc --input "$input_file" --output shaders/compiled/android/"$output_file" --slang glsl300es -f sokol_csharp 


# sokol-shdc --input "$input_file" --output shaders/compiled/"$output_file.h" --slang glsl410::metal_macos -f sokol
# sokol-shdc --input "$input_file" --output shaders/compiled/"$output_file.h" --slang glsl300es -f sokol

rm -rf shaders/compiled/*.air
rm -rf shaders/compiled/*.dia
rm -rf shaders/compiled/*.metal
rm -rf shaders/compiled/*.metallib
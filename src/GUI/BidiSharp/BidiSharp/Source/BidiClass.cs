/*
    BidiSharp: Bidirectional algorithm C# implementation

    Copyright (c) 2019 Fayyad Sufyan
    
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */

namespace BidiSharp
{
    internal enum BidiClass : byte
    {
        L,              // Left-to-Right
        LRE,            // Left-to-Right Embedding
        LRO,            // Left-to-Right Override
        R,              // Right-to-Left
        AL,             // Right-to-Left Arabic
        RLE,            // Right-to-Left Embedding
        RLO,            // Right-to-Left Override
        PDF,            // Pop Directional Format
        EN,             // European Number
        ES,             // European Number Separator
        ET,             // European Number Terminator
        AN,             // Arabic Number
        CS,             // Common Number Separator
        NSM,            // Nonspacing Mark
        BN,             // Boundary Neutral
        B,              // Paragraph Separator
        S,              // Segment Separator
        WS,             // Whitespace
        ON,             // Other Neutrals
        LRI,            // Left-to-Right Isolate
        RLI,            // Right-to-left Isolate
        FSI,            // First Strong Isolate
        PDI             // Pop Directional Isolate
    }
}

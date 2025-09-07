
// Copyright (c) 2022 Eli Aloni (a.k.a  elix22)
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security;
using CommandLine;
using Microsoft.Build.Framework;

namespace SokolApplicationBuilder
{

    public class SokolBuildEngine : IBuildEngine
    {

        const string RedColorBackground = "\u001b[41m";
        const string GreenColorBackground = "\u001b[42m";
        const string YellowColorBackground = "\u001b[43m";
        const string BlueColorBackground = "\u001b[44m";
        const string PurpleColorBackground = "\u001b[45m";
        const string CyanColorBackground = "\u001b[46m";

        const string RedColorText = "\u001b[31m";
        const string GreenColorText = "\u001b[32m";
        const string YellowColorText = "\u001b[33m";
        const string BlueColorText = "\u001b[34m";
        const string PurpleColorText = "\u001b[35m";
        const string CyanColorText = "\u001b[36m";
        const  String WhiteColorText = "\u001B[37m";

        // It's just a test helper so public fields is fine.
        public List<BuildErrorEventArgs> LogErrorEvents = new List<BuildErrorEventArgs>();

        public List<BuildMessageEventArgs> LogMessageEvents =
            new List<BuildMessageEventArgs>();

        public List<CustomBuildEventArgs> LogCustomEvents =
            new List<CustomBuildEventArgs>();

        public List<BuildWarningEventArgs> LogWarningEvents =
            new List<BuildWarningEventArgs>();

        public bool BuildProjectFile(
            string projectFileName, string[] targetNames,
            System.Collections.IDictionary globalProperties,
            System.Collections.IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public int ColumnNumberOfTaskNode
        {
            get { return 0; }
        }

        public bool ContinueOnError
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int LineNumberOfTaskNode
        {
            get { return 0; }
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            // LogCustomEvents.Add(e);
            Console.WriteLine(WhiteColorText + e.Message);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            // LogErrorEvents.Add(e);
            Console.WriteLine(RedColorText + e.File + " : " + e.Message + WhiteColorText);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            // LogMessageEvents.Add(e);
            Console.WriteLine(WhiteColorText + e.Message);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            // LogWarningEvents.Add(e);
            Console.WriteLine(YellowColorText + e.File + " : "  + e.Message + WhiteColorText);
        }

        public string ProjectFileOfTaskNode
        {
            get { return "Sokol BuildEngine"; }
        }

    }

}

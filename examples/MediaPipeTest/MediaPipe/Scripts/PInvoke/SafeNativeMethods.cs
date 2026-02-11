// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Security;

namespace Mediapipe
{
  [SuppressUnmanagedCodeSecurity]
  internal static partial class SafeNativeMethods
  {
    internal const string MediaPipeLibrary =
#if __IOS__
      "@rpath/MediaPipeUnity.framework/MediaPipeUnity";
#elif __ANDROID__
      "mediapipe_jni";
#else
      "mediapipe_c";
#endif
  }
}

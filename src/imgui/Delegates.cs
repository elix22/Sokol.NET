using System;
using System.Collections.Generic;
using System.Text;

namespace Imgui
{
    public unsafe delegate void ImGuiSizeCallback(ImGuiSizeCallbackData* data);
    public unsafe delegate int ImGuiInputTextCallback(ImGuiInputTextCallbackData* data);
}

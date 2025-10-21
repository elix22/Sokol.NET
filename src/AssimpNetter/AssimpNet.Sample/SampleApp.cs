/*
* Copyright (c) 2012-2020 AssimpNet - Nicholas Woodfield
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

using Assimp.Configs;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using SN = System.Numerics;

namespace Assimp.Sample
{
    public class SampleApp
    {
        private GraphicsDevice m_graphicsDevice;
        private CommandList m_cmdList;
        private Camera m_cam;
        private SimpleModel m_targetModel;
        private float m_currRotAngle;
        private Sdl2Window m_window;
        private bool m_resizeRequested;

        public SampleApp() { }

        public void Run()
        {
            m_cam = new Camera();

            WindowCreateInfo windowInfo = new WindowCreateInfo(100, 100, 640, 480, WindowState.Normal, "Quack! - AssimpNet Sample");
            GraphicsDeviceOptions options = new GraphicsDeviceOptions(false, PixelFormat.B8_G8_R8_A8_UNorm, true, ResourceBindingModel.Default, true, true);
            options.SwapchainDepthFormat = PixelFormat.D24_UNorm_S8_UInt;

            m_window = VeldridStartup.CreateWindow(ref windowInfo);

            //If on windows...use D3D11...if not...use OpenGL. Vulkan did not seem to play nice and haven't tested Metal
            m_graphicsDevice = VeldridStartup.CreateGraphicsDevice(m_window, options, IsWindows() ? GraphicsBackend.Direct3D11 : GraphicsBackend.OpenGL);

            m_cmdList = m_graphicsDevice.ResourceFactory.CreateCommandList();

            //NOTICE: This is the duck model we load for the sample, replace this line with a path to your own model to see it imported!
            string fileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assets", "duck.dae");

            //Veldrid defaults to Clockwise winding order, assimp defaults to CCW. We also do some processing + generate normals if missing.
            SimpleModel model = SimpleModel.LoadFromFile(fileName, m_graphicsDevice, PostProcessPreset.TargetRealTimeQuality | PostProcessSteps.FlipWindingOrder,
                new NormalSmoothingAngleConfig(66f));

            if(model == null)
                return;

            m_targetModel = model;
            SetupCamera(m_targetModel);
            m_window.Resized += Window_Resized;

            RenderLoop();

            m_graphicsDevice.WaitForIdle();

            m_targetModel.Dispose();
            m_graphicsDevice.Dispose();
        }

        private bool IsWindows()
        {
            switch(Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    return false;
                default:
                    return true;
            }
        }

        private void RenderLoop()
        {
            Stopwatch clock = Stopwatch.StartNew();
            double prevElapsed = clock.Elapsed.TotalSeconds;

            while(m_window.Exists)
            {
                if(m_resizeRequested)
                {
                    m_graphicsDevice.ResizeMainWindow((uint) m_window.Width, (uint) m_window.Height);
                    SetupCamera(m_targetModel);
                    m_resizeRequested = false;
                }

                double newElapsed = clock.Elapsed.TotalSeconds;
                float deltaTime = (float) (newElapsed - prevElapsed);
                prevElapsed = newElapsed;

                DoRender(deltaTime);
                m_window.PumpEvents();
            }
        }

        private void DoRender(float elapsedTime)
        {
            //Rotate the model by a small amount every frame
            m_currRotAngle += (MathF.PI / 180f) * 35.0f * elapsedTime;
            if(m_currRotAngle >= (Math.PI * 2.0f))
                m_currRotAngle = 0.0f;

            m_targetModel.WorldMatrix = SN.Matrix4x4.CreateRotationY(m_currRotAngle);

            m_cmdList.Begin();

            m_cmdList.SetFramebuffer(m_graphicsDevice.MainSwapchain.Framebuffer);
            m_cmdList.ClearColorTarget(0, RgbaFloat.CornflowerBlue);
            m_cmdList.ClearDepthStencil(1.0f);
            m_targetModel.Draw(m_cmdList, m_cam);

            m_cmdList.End();
            m_graphicsDevice.SubmitCommands(m_cmdList);
            m_graphicsDevice.SwapBuffers();
            m_graphicsDevice.WaitForIdle();
        }

        private void Window_Resized()
        {
            m_resizeRequested = true;
        }

        private void SetupCamera(SimpleModel modelToLookAt)
        {
            float aspectRatio = (float)m_window.Width / (float)m_window.Height;

            //Using the imported scene's bounding volume, we place the camera at the maximum point looking down at the scene center. The clip planes are sized so it fits the scene completely.
            //The model gets scaled down when its imported so we shouldn't run into precision issues with the clip planes being too wide apart.

            SN.Vector3 min = modelToLookAt.SceneMin;
            SN.Vector3 max = modelToLookAt.SceneMax;
            SN.Vector3 center = modelToLookAt.SceneCenter;
            float diagonal = SN.Vector3.Distance(center, max) * 3.5f;
            SN.Vector3 dir = max - center;
            SN.Vector3 camPos = SN.Vector3.Normalize(dir) * diagonal;

            diagonal = SN.Vector3.Distance(camPos, min);

            SN.Matrix4x4 proj = SN.Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, aspectRatio, 0.5f, diagonal);
            SN.Matrix4x4 view = SN.Matrix4x4.CreateLookAt(camPos, center, SN.Vector3.UnitY);

            m_cam.ViewProjection = view * proj;
            m_cam.Position = camPos;
            m_targetModel.LightPosition = camPos;
        }
    }
}

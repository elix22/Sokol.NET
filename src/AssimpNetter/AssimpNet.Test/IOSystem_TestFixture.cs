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

using System.IO;
using NUnit.Framework;

namespace Assimp.Test
{
    [TestFixture]
    public class IOSystem_TestFixture
    {
        [Test]
        public void TestMultiSearchDirectoryLoad()
        {
            string fileName = "fenris.lws";
            string[] searchPaths = { Path.Combine(TestHelper.RootPath, "TestFiles/fenris/scenes"), Path.Combine(TestHelper.RootPath, "TestFiles/fenris/objects") };
            FileIOSystem ioSystem = new FileIOSystem(searchPaths);

            AssimpContext importer = new AssimpContext();
            importer.SetIOSystem(ioSystem);

            //None, using the "target high quality flags caused a crash with this model.
            Scene scene = importer.ImportFile(fileName, PostProcessSteps.None);
            Assert.That(scene, Is.Not.Null);
        }

        [Test]
        public void TestMultiSearchDirectoryConvert()
        {
            string fileName = Path.Combine(TestHelper.RootPath, "TestFiles/fenris/scenes/fenris.lws");
            string[] searchPaths = { Path.Combine(TestHelper.RootPath, "TestFiles/fenris/objects") };
            FileIOSystem ioSystem = new FileIOSystem(searchPaths);

            AssimpContext importer = new AssimpContext();
            importer.SetIOSystem(ioSystem);

            //Output path has to be specified fully, since we may be creating the file
            string outputPath = Path.Combine(TestHelper.RootPath, "TestFiles/fenris/fenris2.obj");
            importer.ConvertFromFileToFile(fileName, PostProcessSteps.None, outputPath, "obj", PostProcessSteps.None);
        }

        [Test]
        public void TestIOSystemError()
        {
            string fileName = "duckduck.dae"; //GOOSE!
            string[] searchPaths = { Path.Combine(TestHelper.RootPath, "TestFiles") };
            FileIOSystem ioSystem = new FileIOSystem(searchPaths);

            AssimpContext importer = new AssimpContext();
            importer.SetIOSystem(ioSystem);
            Assert.Throws<AssimpException>(delegate()
            {
                importer.ImportFile(fileName, PostProcessSteps.None);
            });
        }

        [Test]
        public void TestIOSystem_ImportObj()
        {
            string dir = Path.Combine(TestHelper.RootPath, "TestFiles");
            LogStream.IsVerboseLoggingEnabled = true;
            ConsoleLogStream log = new ConsoleLogStream();
            log.Attach();

            using(AssimpContext importer = new AssimpContext())
            {
                FileIOSystem iOSystem = new FileIOSystem(dir);
                importer.SetIOSystem(iOSystem);

                //Using stream does not use the IO system...
                using(Stream fs = File.OpenRead(Path.Combine(dir, "sphere.obj")))
                {
                    Scene scene = importer.ImportFileFromStream(fs, "obj");
                    Assert.That(scene, Is.Not.Null);
                    Assert.That(scene.HasMeshes, Is.True);
                    Assert.That(scene.HasMaterials, Is.True);

                    //No material file, so the mesh will always use the default material
                    Assert.That(scene.Materials[scene.Meshes[0].MaterialIndex].Name, Is.EqualTo("DefaultMaterial"));
                }

                //Using custom IO system requires us to pass in the file name, assimp will ask the io system to get a stream
                Scene scene2 = importer.ImportFile("sphere.obj");
                Assert.That(scene2, Is.Not.Null);
                Assert.That(scene2.HasMeshes, Is.True);
                Assert.That(scene2.HasMaterials, Is.True);

                //Should have found a material with the name "SphereMaterial" in the mtl file
                Assert.That(scene2.Materials[scene2.Meshes[0].MaterialIndex].Name, Is.EqualTo("SphereMaterial"));
            }
        }
    }
}

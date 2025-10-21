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

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Assimp.Configs;
using Assimp.Unmanaged;
using NUnit.Framework;

namespace Assimp.Test
{
    [TestFixture]
    public class AssimpContextTestFixture
    {
        [OneTimeSetUp]
        public void Setup()
        {
            string outputPath = Path.Combine(TestHelper.RootPath, "TestFiles/output");

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            IEnumerable<string> filePaths = Directory.GetFiles(outputPath);

            foreach(string filePath in filePaths)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [Test]
        public void TestExportBadFormatId()
        {
            AssimpContext importer = new AssimpContext();
            NormalSmoothingAngleConfig config = new NormalSmoothingAngleConfig(66.0f);
            importer.SetConfig(config);

            LogStream logStream = new LogStream(delegate (string msg, string userData)
            {
                Console.WriteLine(msg);
            });
            logStream.Attach();

            Scene collada = importer.ImportFile(Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae"));

            bool success = importer.ExportFile(collada, Path.Combine(TestHelper.RootPath, "TestFiles/output/exportedCollada.dae"), "dae");

            Assert.That(success, Is.False);

            success = importer.ExportFile(collada, Path.Combine(TestHelper.RootPath, "TestFiles/output/exportedCollada.dae"), "collada");

            Assert.That(success, Is.True);
        }

        [Test]
        public void TestExportToBlob()
        {
            string colladaPath = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");

            AssimpContext context = new AssimpContext();
            Scene ducky = context.ImportFile(colladaPath);
            ExportDataBlob blob = context.ExportToBlob(ducky, "obj");

            Assert.That(blob.HasData, Is.True);
            Assert.That(blob.NextBlob, Is.Not.Null);
            Assert.That(blob.NextBlob.Name, Is.EqualTo("mtl"));
        }

        [Test]
        public void TestImportExportFile()
        {
            string colladaPath = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");
            string plyPath = Path.Combine(TestHelper.RootPath, "TestFiles/output/duck.ply");

            AssimpContext context = new AssimpContext();
            Scene ducky = context.ImportFile(colladaPath);
            context.ExportFile(ducky, plyPath, "ply");
        }

        [Test]
        public void TestImportExportImportFile()
        {
            string colladaPath = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");
            string plyPath = Path.Combine(TestHelper.RootPath, "TestFiles/output/duck2.dae");

            AssimpContext context = new AssimpContext();
            Scene ducky = context.ImportFile(colladaPath);
            context.ExportFile(ducky, plyPath, "collada");

            Scene ducky2 = context.ImportFile(plyPath);
            Assert.That(ducky2, Is.Not.Null);
        }

        [Test]
        public void TestExportToFile()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/ExportedTriangle.obj");

            //Create a very simple scene a single node with a mesh that has a single face, a triangle and a default material
            Scene scene = new Scene();
            scene.RootNode = new Node("Root");

            Mesh triangle = new Mesh("", PrimitiveType.Triangle);
            triangle.Vertices.Add(new Vector3(1, 0, 0));
            triangle.Vertices.Add(new Vector3(5, 5, 0));
            triangle.Vertices.Add(new Vector3(10, 0, 0));
            triangle.Faces.Add(new Face(new int[] { 0, 1, 2 }));
            triangle.MaterialIndex = 0;

            scene.Meshes.Add(triangle);
            scene.RootNode.MeshIndices.Add(0);

            Material mat = new Material();
            mat.Name = "MyMaterial";
            scene.Materials.Add(mat);

            //Export the scene then read it in and compare!

            AssimpContext context = new AssimpContext();
            Assert.That(context.ExportFile(scene, path, "obj"), Is.True);

            Scene importedScene = context.ImportFile(path);
            Assert.That(importedScene.MeshCount, Is.EqualTo(scene.MeshCount));
            Assert.That(importedScene.MaterialCount, Is.EqualTo(2)); //Always has the default material, should also have our material

            //Compare the meshes
            Mesh importedTriangle = importedScene.Meshes[0];

            Assert.That(importedTriangle.VertexCount, Is.EqualTo(triangle.VertexCount));
            for(int i = 0; i < importedTriangle.VertexCount; i++)
            {
                Assert.That(importedTriangle.Vertices[i].Equals(triangle.Vertices[i]), Is.True);
            }

            Assert.That(importedTriangle.FaceCount, Is.EqualTo(triangle.FaceCount));
            for(int i = 0; i < importedTriangle.FaceCount; i++)
            {
                Face importedFace = importedTriangle.Faces[i];
                Face face = triangle.Faces[i];

                for(int j = 0; j < importedFace.IndexCount; j++)
                {
                    Assert.That(importedFace.Indices[j], Is.EqualTo(face.Indices[j]));
                }
            }
        }

        [Test]
        public void TestFreeLogStreams()
        {
            ConsoleLogStream console1 = new ConsoleLogStream();
            ConsoleLogStream console2 = new ConsoleLogStream();
            ConsoleLogStream console3 = new ConsoleLogStream();

            console1.Attach();
            console2.Attach();
            console3.Attach();

            AssimpLibrary.Instance.FreeLibrary();

            IEnumerable<LogStream> logs = LogStream.GetAttachedLogStreams();

            Assert.That(logs, Is.Empty);
            Assert.That(console1.IsAttached, Is.False);
            Assert.That(console2.IsAttached, Is.False);
            Assert.That(console3.IsAttached, Is.False);
        }

        [Test]
        public void TestImportFromFile()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/sphere.obj");

            AssimpContext importer = new AssimpContext();

            importer.SetConfig(new NormalSmoothingAngleConfig(55.0f));
            importer.Scale = .5f;
            importer.XAxisRotation = 25.0f;
            importer.YAxisRotation = 50.0f;
            LogStream.IsVerboseLoggingEnabled = true;

            Assert.That(importer.ContainsConfig(NormalSmoothingAngleConfig.NormalSmoothingAngleConfigName), Is.True);

            importer.RemoveConfigs();

            Assert.That(importer.ContainsConfig(NormalSmoothingAngleConfig.NormalSmoothingAngleConfigName), Is.False);

            importer.SetConfig(new NormalSmoothingAngleConfig(65.0f));
            importer.SetConfig(new NormalSmoothingAngleConfig(22.5f));
            importer.RemoveConfig(NormalSmoothingAngleConfig.NormalSmoothingAngleConfigName);

            Assert.That(importer.ContainsConfig(NormalSmoothingAngleConfig.NormalSmoothingAngleConfigName), Is.False);

            importer.SetConfig(new NormalSmoothingAngleConfig(65.0f));

            Scene scene = importer.ImportFile(path, PostProcessPreset.TargetRealTimeMaximumQuality);

            Assert.That(scene, Is.Not.Null);
            Assert.That((scene.SceneFlags & SceneFlags.Incomplete), Is.Not.EqualTo(SceneFlags.Incomplete));
        }

        [Test]
        public void TestImportFromStream()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");

            FileStream fs = File.OpenRead(path);

            AssimpContext importer = new AssimpContext();
            LogStream.IsVerboseLoggingEnabled = true;

            LogStream logstream = new LogStream(delegate(string msg, string userData)
            {
                Console.WriteLine(msg);
            });

            logstream.Attach();

            Scene scene = importer.ImportFileFromStream(fs, ".dae");

            fs.Close();

            Assert.That(scene, Is.Not.Null);
            Assert.That((scene.SceneFlags & SceneFlags.Incomplete), Is.Not.EqualTo(SceneFlags.Incomplete));
        }

        [Test]
        public void TestImportFromStreamNoFormatHint()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");

            FileStream fs = File.OpenRead(path);

            AssimpContext importer = new AssimpContext();
            LogStream.IsVerboseLoggingEnabled = true;

            LogStream logstream = new LogStream(delegate (string msg, string userData)
            {
                Console.WriteLine(msg);
            });

            logstream.Attach();

            Scene scene = importer.ImportFileFromStream(fs, string.Empty); //null also seems to work well

            fs.Close();

            Assert.That(scene, Is.Not.Null);
            Assert.That((scene.SceneFlags & SceneFlags.Incomplete), Is.Not.EqualTo(SceneFlags.Incomplete));
        }

        [Test]
        public void TestImporterDescriptions()
        {
            AssimpContext importer = new AssimpContext();
            ImporterDescription[] importerDescrs = importer.GetImporterDescriptions();

            Assert.That(importerDescrs, Is.Not.Null);
            Assert.That(importerDescrs.Length, Is.GreaterThan(0));

            ImporterDescription descr = importer.GetImporterDescriptionFor("obj");
            ImporterDescription descr2 = importer.GetImporterDescriptionFor(".obj");

            Assert.That(descr, Is.Not.Null);
            Assert.That(descr2, Is.Not.Null);
            Assert.That(descr.Name, Is.EqualTo(descr2.Name));
        }

        [Test]
        public void TestSupportedFormats()
        {
            AssimpContext importer = new AssimpContext();
            ExportFormatDescription[] exportDescs = importer.GetSupportedExportFormats();

            string[] importFormats = importer.GetSupportedImportFormats();

            Assert.That(exportDescs, Is.Not.Null);
            Assert.That(importFormats, Is.Not.Null);
            Assert.That(exportDescs.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(importFormats.Length, Is.GreaterThanOrEqualTo(1));

            Assert.That(importer.IsExportFormatSupported(exportDescs[0].FileExtension), Is.True);
            Assert.That(importer.IsImportFormatSupported(importFormats[0]), Is.True);

            Assert.That(importer.IsExportFormatSupported("obj"), Is.True);
            Assert.That(importer.IsExportFormatSupported(".obj"), Is.True);
        }

        [Test]
        public void TestConvertFromFile()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/Bob.md5mesh");
            string outputPath = Path.Combine(TestHelper.RootPath, "TestFiles/output/Bob.dae");

            AssimpContext importer = new AssimpContext();
            importer.ConvertFromFileToFile(path, outputPath, "collada");

            ExportDataBlob blob = importer.ConvertFromFileToBlob(path, "collada");
        }

        [Test]
        public void TestConvertFromStreamNoFormatHint()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");
            string outputPath = Path.Combine(TestHelper.RootPath, "TestFiles/output/duckNoHint.obj");

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            FileStream fs = File.OpenRead(path);

            new ConsoleLogStream().Attach();

            AssimpContext importer = new AssimpContext();
            bool success = importer.ConvertFromStreamToFile(fs, ".dae", outputPath, "obj");
            Assert.That(success, Is.True);

            Assert.That(File.Exists(outputPath), Is.True);
        }

        [Test]
        public void TestConvertFromStream()
        {
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");
            string outputPath = Path.Combine(TestHelper.RootPath, "TestFiles/output/duck.obj");
            string outputPath2 = Path.Combine(TestHelper.RootPath, "TestFiles/output/duck-fromBlob.obj");

            FileStream fs = File.OpenRead(path);

            new ConsoleLogStream().Attach();

            AssimpContext importer = new AssimpContext();
            bool success = importer.ConvertFromStreamToFile(fs, ".dae", outputPath, "obj");
            Assert.That(success, Is.True);

            fs.Position = 0;

            ExportDataBlob blob = importer.ConvertFromStreamToBlob(fs, ".dae", "collada");
            Assert.That(blob, Is.Not.Null);

            fs.Close();

            //Take ExportDataBlob's data, write it to a memory stream and export that back to an obj and write it

            MemoryStream memStream = new MemoryStream();
            memStream.Write(blob.Data, 0, blob.Data.Length);

            memStream.Position = 0;

            success = importer.ConvertFromStreamToFile(memStream, ".dae", outputPath2, "obj");

            memStream.Close();

            LogStream.DetachAllLogstreams();

            Assert.That(success, Is.True);
        }

        [Test, Repeat(100), Ignore("Upstream issue: https://github.com/assimp/assimp/issues/5896")]
        public void TestMultipleImportersMultipleThreads()
        {
            LogStream.IsVerboseLoggingEnabled = true;

            ThreadStart[] actions = new ThreadStart[]
            {
                LoadSceneA,
                LoadSceneB,
                ConvertSceneC,
            };

            var threads = Enumerable.Repeat(actions, 25)
                .SelectMany(x => x)
                .OrderBy(_ => Guid.NewGuid())
                .Select(x => new Thread(x))
                .ToArray();

            foreach (var thread in threads)
                thread.Start();

            foreach(var thread in threads)
                thread.Join();

            LogStream.DetachAllLogstreams();
        }

        private void LoadSceneA()
        {
            Console.WriteLine("Thread A: Starting import.");
            AssimpContext importer = new AssimpContext();
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/Bob.md5mesh");

            new ConsoleLogStream("Thread A:").Attach();
            Console.WriteLine("Thread A: Importing");
            Scene scene = importer.ImportFile(path);
            Console.WriteLine("Thread A: Done importing");
        }

        private void LoadSceneB()
        {
            Console.WriteLine("Thread B: Starting import.");
            AssimpContext importer = new AssimpContext();
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");

            new ConsoleLogStream("Thread B:").Attach();
            importer.SetConfig(new NormalSmoothingAngleConfig(55.0f));
            Console.WriteLine("Thread B: Importing");
            Scene scene = importer.ImportFile(path);
            Console.WriteLine("Thread B: Done importing");
        }

        private void ConvertSceneC()
        {
            Console.WriteLine("Thread C: Starting convert.");
            AssimpContext importer = new AssimpContext();
            string path = Path.Combine(TestHelper.RootPath, "TestFiles/duck.dae");
            string outputPath = Path.Combine(TestHelper.RootPath, "TestFiles/duck2.obj");

            new ConsoleLogStream("Thread C:").Attach();
            importer.SetConfig(new NormalSmoothingAngleConfig(55.0f));
            importer.SetConfig(new FavorSpeedConfig(true));

            Console.WriteLine("Thread C: Converting");
            ExportDataBlob blob = importer.ConvertFromFileToBlob(path, "obj");

            Console.WriteLine("Thread C: Done converting");
        }
    }
}

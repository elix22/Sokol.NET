using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Materials;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// Asset ids: the <c>.meta</c> files and the database made from them (AssetDatabase), and the table of ids a
    /// scene, a prefab, a material and the project settings carry, written on save and read before the file is
    /// (AssetRefs). A file that moved is found by its id; a file with no table reads as it always did.
    /// </summary>
    public sealed class AssetDatabaseTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        const string Duck = "Models/duck.obj", Yellow = "Materials/Yellow.pbrmat", Robot = "Models/robot.glb",
                     Bullet = "Prefabs/Bullet.prefab", Enemy = "Prefabs/Enemy.prefab", Squad = "Prefabs/Squad.prefab";

        readonly Dictionary<string, string> _files = new();
        readonly List<string> _folders = new();

        public AssetDatabaseTests()
        {
            World.Clear();
            AssetDatabase.Clear();
            PrefabLibrary.Clear();
            PrefabLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
        }

        public void Dispose()
        {
            PrefabLibrary.Reader = null;
            PrefabLibrary.Clear();
            AssetDatabase.Clear();
            World.Clear();
            foreach (string folder in _folders)
                try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        static string Id(int n) => n.ToString("x32");

        /// <summary>Gives each key an id (1, 2, …) as if its file were in the project.</summary>
        static void Known(params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++) AssetDatabase.Set(Id(AssetDatabase.Count + 1), "Assets/" + keys[i]);
        }

        static void MoveKey(string from, string to) => AssetDatabase.Rename("Assets/" + from, "Assets/" + to);

        static GameEditor.Framework.Scene.Scene NewScene()
        {
            World.Clear();
            return new GameEditor.Framework.Scene.Scene("t");
        }

        static Entity Mesh(GameEditor.Framework.Scene.Scene s, string name, string mesh, string material = "")
        {
            Entity e = s.CreateEntity(name);
            World.AddComponent(e, new MeshRenderer { MeshPath = mesh, MaterialPath = material, Visible = true, ReceivesShadows = true, CastsShadows = true });
            return e;
        }

        /// <summary>The scene as its file has it, before it is indented: with the table of the files it names.</summary>
        static string Save(GameEditor.Framework.Scene.Scene s) => AssetRefs.Stamp(SceneSerializer.Serialize(s));

        static GameEditor.Framework.Scene.Scene Load(string json)
        {
            var s = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, s);
            return s;
        }

        static Entity Find(string name)
        {
            foreach (Entity e in World.Entities)
                if (World.GetComponent<NameTag>(e).Name == name) return e;
            throw new Xunit.Sdk.XunitException($"entity '{name}' not found");
        }

        static MeshRenderer MeshOf(string name) => World.GetComponent<MeshRenderer>(Find(name));

        static string[] TableKeys(string json)
        {
            var table = AssetRefs.ReadTable(json);
            if (table == null) return [];
            var keys = new List<string>(table.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys.ToArray();
        }

        /// <summary>The world as a prefab file, then an empty world.</summary>
        static string AsPrefab(GameEditor.Framework.Scene.Scene s, string name)
        {
            s.Name = name;
            s.Blocks["prefab"] = "1";
            string text = Save(s);
            World.Clear();
            return text;
        }

        static Entity Child(GameEditor.Framework.Scene.Scene s, string name, Entity parent)
        {
            Entity e = s.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Parent = parent;
            World.AddComponent(e, tr);
            return e;
        }

        // Enemy ─ Body (the duck mesh, yellow)
        string EnemyPrefab()
        {
            var s = NewScene();
            Entity root = s.CreateEntity("Enemy");
            Entity body = Child(s, "Body", root);
            World.AddComponent(body, new MeshRenderer { MeshPath = Duck, MaterialPath = Yellow, Visible = true, ReceivesShadows = true, CastsShadows = true });
            return _files[Enemy] = AsPrefab(s, "Enemy");
        }

        string Project(params string[] files)
        {
            string folder = Path.Combine(Path.GetTempPath(), "assetdb_" + Guid.NewGuid().ToString("N"));
            _folders.Add(folder);
            foreach (string file in files) Write(folder, file, "x");
            return folder;
        }

        static void Write(string folder, string file, string text)
        {
            string abs = Path.Combine(folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, text);
        }

        static bool Exists(string folder, string file) => File.Exists(Path.Combine(folder, file));
        static string Read(string folder, string file) => File.ReadAllText(Path.Combine(folder, file));

        // ── The table a file carries ────────────────────────────────────────────────────

        [Fact]
        public void A_scene_gets_a_line_for_each_file_it_names()
        {
            Known(Duck, Yellow, Robot, Bullet, "Models/unused.obj");
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            Mesh(s, "Arm", Robot + "#m0p1", Robot + "#mat2");
            Mesh(s, "Box", "prim:box");
            Entity gun = s.CreateEntity("Gun");
            World.AddComponent(gun, new ScriptComponent { TypeName = "Cannon", Properties = new() { ["Bullet"] = Bullet, ["Speed"] = "3" } });

            string plain = SceneSerializer.Serialize(s);
            string saved = AssetRefs.Stamp(plain);

            Assert.Equal(new[] { Yellow, Duck, Robot, Bullet }, TableKeys(saved));   // sorted, a glTF's parts under the file's key, no line for a file not named
            Assert.Equal(AssetDatabase.IdOfKey(Robot), AssetRefs.ReadTable(saved)![Robot]);
            // Nothing else in the text changed: the table is the last member.
            Assert.StartsWith(plain.Substring(0, plain.Length - 1) + ",\"assets\":{", saved);
            Assert.Same(saved, AssetRefs.Stamp(saved));   // right already
        }

        [Fact]
        public void A_scene_that_names_no_file_is_written_as_before()
        {
            Known(Duck, Yellow);
            var s = NewScene();
            Mesh(s, "Box", "prim:box");
            Mesh(s, "Other", "Models/not_in_the_project.obj");
            s.CreateEntity("Empty");
            string plain = SceneSerializer.Serialize(s);
            Assert.Same(plain, AssetRefs.Stamp(plain));
            Assert.DoesNotContain("\"assets\"", plain);
        }

        [Fact]
        public void With_no_database_a_text_is_left_alone()
        {
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            string plain = SceneSerializer.Serialize(s);
            Assert.Same(plain, AssetRefs.Stamp(plain));
            Assert.Same(plain, AssetRefs.Resolve(plain));
        }

        [Fact]
        public void A_moved_file_is_found_and_the_new_key_is_saved()
        {
            Known(Duck, Yellow, Robot, Bullet);
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            Mesh(s, "Arm", Robot + "#m0p1", Robot + "#mat2");
            Entity gun = s.CreateEntity("Gun");
            World.AddComponent(gun, new ScriptComponent { TypeName = "Cannon", Properties = new() { ["Bullet"] = Bullet } });
            string saved = Save(s);

            MoveKey(Duck, "Props/bird.obj");
            MoveKey(Robot, "Models/Robots/arm.glb");
            MoveKey(Bullet, "Prefabs/Shots/Bullet.prefab");

            var loaded = Load(saved);
            Assert.Equal("Props/bird.obj", MeshOf("Duck").MeshPath);
            Assert.Equal(Yellow, MeshOf("Duck").MaterialPath);                       // not moved
            Assert.Equal("Models/Robots/arm.glb#m0p1", MeshOf("Arm").MeshPath);      // what follows '#' is kept
            Assert.Equal("Models/Robots/arm.glb#mat2", MeshOf("Arm").MaterialPath);
            Assert.Equal("Prefabs/Shots/Bullet.prefab", World.GetComponent<ScriptComponent>(Find("Gun")).Properties!["Bullet"]);

            string again = Save(loaded);
            Assert.Equal(new[] { Yellow, "Models/Robots/arm.glb", "Prefabs/Shots/Bullet.prefab", "Props/bird.obj" }, TableKeys(again));
            Assert.DoesNotContain(Duck, again);
            Assert.Equal(again, Save(Load(again)));   // and from then on the file does not change
        }

        [Fact]
        public void Two_files_that_swapped_names_come_out_right()
        {
            Known("Models/a.obj", "Models/b.obj");
            string idA = AssetDatabase.IdOfKey("Models/a.obj")!, idB = AssetDatabase.IdOfKey("Models/b.obj")!;
            var s = NewScene();
            Mesh(s, "A", "Models/a.obj");
            Mesh(s, "B", "Models/b.obj");
            string saved = Save(s);

            AssetDatabase.Clear();
            AssetDatabase.Set(idA, "Assets/Models/b.obj");
            AssetDatabase.Set(idB, "Assets/Models/a.obj");

            Load(saved);
            Assert.Equal("Models/b.obj", MeshOf("A").MeshPath);
            Assert.Equal("Models/a.obj", MeshOf("B").MeshPath);
        }

        [Fact]
        public void An_id_that_is_unknown_leaves_the_key_and_keeps_its_line()
        {
            Known(Duck, Yellow);
            string duckId = AssetDatabase.IdOfKey(Duck)!, yellowId = AssetDatabase.IdOfKey(Yellow)!;
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            string saved = Save(s);

            // The duck's file is gone (deleted, or its .meta lost); the material is still there.
            AssetDatabase.Clear();
            AssetDatabase.Set(yellowId, "Assets/" + Yellow);

            var loaded = Load(saved);
            Assert.Equal(Duck, MeshOf("Duck").MeshPath);
            string again = Save(loaded);
            Assert.Equal(duckId, AssetRefs.ReadTable(again)![Duck]);   // a file that is missing only for now keeps its id
            Assert.Equal(saved, again);

            // The key is no longer named: its line goes.
            var mr = MeshOf("Duck");
            mr.MeshPath = "prim:box";
            World.AddComponent(Find("Duck"), mr);
            Assert.Equal(new[] { Yellow }, TableKeys(Save(loaded)));
        }

        [Fact]
        public void A_file_with_no_table_reads_as_before()
        {
            Known(Duck, Yellow);
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            string plain = SceneSerializer.Serialize(s);   // as a version before ids wrote it
            MoveKey(Duck, "Props/bird.obj");

            Assert.Same(plain, AssetRefs.Resolve(plain));
            Load(plain);
            Assert.Equal(Duck, MeshOf("Duck").MeshPath);   // nothing says which file it was
        }

        [Fact]
        public void The_id_wins_over_a_new_file_at_the_old_path()
        {
            Known(Duck);
            var s = NewScene();
            Mesh(s, "Duck", Duck);
            string saved = Save(s);

            MoveKey(Duck, "Props/bird.obj");
            AssetDatabase.Set(Id(99), "Assets/" + Duck);   // another file takes the old name

            Load(saved);
            Assert.Equal("Props/bird.obj", MeshOf("Duck").MeshPath);
        }

        [Fact]
        public void The_table_is_laid_out_as_the_text_is_and_nothing_else_changes()
        {
            Known(Duck, Yellow);
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            s.Blocks["plugin"] = "{\"gain\":2}";
            string plain = SceneSerializer.Serialize(s);
            string indented = SceneSerializer.Indent(plain);

            // An indented file given its table is the file a save would write.
            string stamped = AssetRefs.Stamp(indented);
            Assert.Equal(SceneSerializer.Indent(AssetRefs.Stamp(plain)), stamped);
            Assert.StartsWith(indented.Substring(0, indented.Length - 2), stamped);   // every byte before the closing brace

            // A table that is not right — a line for a key not named, a line missing — is put right, in place.
            string stale = indented.Substring(0, indented.Length - 2) + ",\n  \"assets\": {\n    \"Models/gone.obj\": \"" + Id(50) + "\"\n  }\n}";
            Assert.Equal(stamped, AssetRefs.Stamp(stale));

            // A table before other members: taken out there, written last.
            string early = "{\"assets\":{\"x\":\"" + Id(51) + "\"}," + plain.Substring(1);
            Assert.Equal(AssetRefs.Stamp(plain), AssetRefs.Stamp(early));

            // A text that names no file any more loses its table and is what it was.
            var bare = NewScene();
            Mesh(bare, "Box", "prim:box");
            string none = SceneSerializer.Indent(SceneSerializer.Serialize(bare));
            string left = none.Substring(0, none.Length - 2) + ",\n  \"assets\": {\n    \"Models/gone.obj\": \"" + Id(52) + "\"\n  }\n}";
            Assert.Equal(none, AssetRefs.Stamp(left));
        }

        // ── Prefabs ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_moved_prefab_keeps_its_instances()
        {
            Known(Duck, Yellow, Enemy);
            EnemyPrefab();
            var s = NewScene();
            Entity root = SceneSerializer.InstantiatePrefab(Enemy, out string? error);
            Assert.Null(error);
            var tr = World.GetComponent<Transform>(root);
            tr.Position = new System.Numerics.Vector3(4, 0, 2);
            World.AddComponent(root, tr);
            string saved = Save(s);
            Assert.Equal(new[] { Enemy }, TableKeys(saved));   // the duck is the prefab's, not the scene's

            const string moved = "Prefabs/Foes/Enemy.prefab";
            MoveKey(Enemy, moved);
            _files[moved] = _files[Enemy];
            _files.Remove(Enemy);
            PrefabLibrary.Invalidate();

            var loaded = Load(saved);
            var link = World.GetComponent<PrefabLink>(Find("Enemy"));
            Assert.False(link.Missing);
            Assert.Equal(moved, link.Asset);
            Assert.Equal(Duck, MeshOf("Body").MeshPath);

            string again = Save(loaded);
            Assert.Contains("\"prefab\":\"" + moved + "\"", again);
            Assert.Equal(new[] { moved }, TableKeys(again));
            Assert.Equal(again, Save(Load(again)));
        }

        [Fact]
        public void A_prefab_inside_a_prefab_is_found_after_it_moved()
        {
            Known(Duck, Yellow, Enemy, Squad);
            EnemyPrefab();
            var p = NewScene();
            Entity squad = p.CreateEntity("Squad");
            Entity inner = SceneSerializer.InstantiatePrefab(Enemy, out _);
            var tr = World.GetComponent<Transform>(inner);
            tr.Parent = squad;
            World.AddComponent(inner, tr);
            _files[Squad] = AsPrefab(p, "Squad");
            Assert.Equal(new[] { Enemy }, TableKeys(_files[Squad]));

            var s = NewScene();
            SceneSerializer.InstantiatePrefab(Squad, out string? error);
            Assert.Null(error);
            string saved = Save(s);

            const string moved = "Prefabs/Foes/Enemy.prefab";
            MoveKey(Enemy, moved);
            _files[moved] = _files[Enemy];
            _files.Remove(Enemy);
            PrefabLibrary.Invalidate();

            Load(saved);
            Assert.False(World.GetComponent<PrefabLink>(Find("Squad")).Missing);
            Assert.Equal(Duck, MeshOf("Body").MeshPath);   // the entity two prefabs down is there
            Assert.Contains(moved, Prefabs.KeysIn(_files[Squad]));   // and a game asks for the file where it is now
        }

        [Fact]
        public void A_file_a_prefab_names_moved_and_the_instance_has_no_override_for_it()
        {
            Known(Duck, Yellow, Enemy);
            EnemyPrefab();
            var s = NewScene();
            SceneSerializer.InstantiatePrefab(Enemy, out _);
            string saved = Save(s);

            MoveKey(Duck, "Props/bird.obj");
            PrefabLibrary.Invalidate();

            var loaded = Load(saved);
            Assert.Equal("Props/bird.obj", MeshOf("Body").MeshPath);
            string again = Save(loaded);
            Assert.DoesNotContain("MeshRenderer", again);   // the instance differs from its prefab in nothing
            Assert.DoesNotContain("bird", again);
        }

        // ── Materials and the project settings ──────────────────────────────────────────

        [Fact]
        public void A_material_finds_a_texture_that_moved()
        {
            Known("Textures/wood.png", "Textures/wood_n.png");
            var mat = new PbrMaterial { Name = "Wood", BaseColorMapPath = "Textures/wood.png", NormalMapPath = "Textures/wood_n.png" };
            string plain = PbrMaterialSerializer.Serialize(mat);
            string saved = AssetRefs.Stamp(plain);
            Assert.Equal(new[] { "Textures/wood.png", "Textures/wood_n.png" }, TableKeys(saved));
            Assert.StartsWith(plain.Substring(0, plain.TrimEnd().Length - 1).TrimEnd(), saved);   // the file's own lines are as they were

            MoveKey("Textures/wood.png", "Textures/Oak/base.png");
            PbrMaterial read = PbrMaterialSerializer.Deserialize(Encoding.UTF8.GetBytes(saved), "x");
            Assert.Equal("Textures/Oak/base.png", read.BaseColorMapPath);
            Assert.Equal("Textures/wood_n.png", read.NormalMapPath);
            Assert.Equal("Wood", read.Name);
        }

        [Fact]
        public void The_project_settings_find_a_start_scene_that_moved()
        {
            Known("Scenes/Main.scene.json");
            var cfg = new ProjectConfig { ProjectName = "Demo", DefaultScene = "Scenes/Main.scene.json" };
            string saved = ConfigManager.Serialize(cfg);
            Assert.Equal(new[] { "Scenes/Main.scene.json" }, TableKeys(saved));
            using (var doc = JsonDocument.Parse(saved))
                Assert.Equal("Demo", doc.RootElement.GetProperty("projectName").GetString());

            MoveKey("Scenes/Main.scene.json", "Levels/Start.scene.json");
            ProjectConfig? read = ConfigManager.LoadFromMemory(Encoding.UTF8.GetBytes(saved));
            Assert.Equal("Levels/Start.scene.json", read!.DefaultScene);
            Assert.Equal(new[] { "Levels/Start.scene.json" }, TableKeys(ConfigManager.Serialize(read)));
        }

        [Fact]
        public void An_objs_mtl_is_keyed_by_where_the_file_is()
        {
            // The .mtl an .obj names lies beside it: two files of one name in two folders have two keys.
            Assert.Equal("Models/Red/shape.mtl", GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.MtlKey("Models/Red/shape.obj", "shape.mtl"));
            Assert.Equal("Models/Blue/shape.mtl", GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.MtlKey("Models/Blue/shape.obj", "shape.mtl"));
            Assert.Equal("shape.mtl", GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.MtlKey("shape.obj", "shape.mtl"));
            Assert.Equal("Models/Kit/Materials/a.mtl", GameEditor.Framework.Renderer.Server.Resources.MeshRegistry.MtlKey("Models/Kit/a.obj", "Materials/a.mtl"));
        }

        // ── The .meta files of a project ────────────────────────────────────────────────

        [Fact]
        public void Every_asset_and_script_gets_a_meta_file_once()
        {
            string folder = Project("Assets/Models/duck.obj", "Assets/Scenes/Main.scene.json", "Assets/readme", "Source/Player.cs",
                                    "Assets/config.json", "Assets/assets.index.json", "Assets/.DS_Store", "Assets/.cache/tmp.bin",
                                    "Source/Generated/RegisteredScripts.g.cs", "Source/notes.txt", "Source/obj/Debug/x.cs", "config.json", "Game.csproj");

            var report = AssetDatabase.Open(folder);
            Assert.Equal(4, report.Created);
            Assert.Equal(4, AssetDatabase.Count);
            Assert.False(report.NotSaved);
            foreach (string file in new[] { "Assets/Models/duck.obj", "Assets/Scenes/Main.scene.json", "Assets/readme", "Source/Player.cs" })
            {
                string id = AssetDatabase.IdOfPath(file)!;
                Assert.True(AssetDatabase.IsId(id));
                Assert.Equal("{ \"meta\": 1, \"id\": \"" + id + "\" }\n", Read(folder, file + ".meta"));
            }
            foreach (string file in new[] { "Assets/config.json", "Assets/assets.index.json", "Assets/.DS_Store", "Assets/.cache/tmp.bin",
                                            "Source/Generated/RegisteredScripts.g.cs", "Source/notes.txt", "Source/obj/Debug/x.cs", "config.json", "Game.csproj" })
                Assert.False(Exists(folder, file + ".meta"), file);
            Assert.Equal("Models/duck.obj", AssetDatabase.KeyOfId(AssetDatabase.IdOfKey("Models/duck.obj")!));
            Assert.Null(AssetDatabase.KeyOfId(AssetDatabase.IdOfPath("Source/Player.cs")!));   // a script has an id and no key

            string duck = AssetDatabase.IdOfKey("Models/duck.obj")!;
            report = AssetDatabase.Open(folder);   // opened again: the same ids, nothing written
            Assert.Equal(0, report.Created);
            Assert.Empty(report.Notes);
            Assert.Equal(duck, AssetDatabase.IdOfKey("Models/duck.obj"));
        }

        [Fact]
        public void A_file_copied_with_its_meta_gets_an_id_of_its_own()
        {
            string folder = Project("Assets/a.png");
            Write(folder, "Assets/a.png.meta", "{ \"meta\": 1, \"id\": \"" + Id(7) + "\", \"import\": { \"size\": 512 } }\n");
            File.Copy(Path.Combine(folder, "Assets/a.png"), Path.Combine(folder, "Assets/b.png"));
            File.Copy(Path.Combine(folder, "Assets/a.png.meta"), Path.Combine(folder, "Assets/b.png.meta"));

            var report = AssetDatabase.Open(folder);
            Assert.Equal(1, report.Duplicates);
            Assert.Equal(Id(7), AssetDatabase.IdOfKey("a.png"));   // the first in path order keeps it
            string other = AssetDatabase.IdOfKey("b.png")!;
            Assert.NotEqual(Id(7), other);
            // Members this version does not know stay in both.
            Assert.Equal("{ \"meta\": 1, \"id\": \"" + other + "\", \"import\": { \"size\": 512 } }\n", Read(folder, "Assets/b.png.meta"));
            Assert.Contains("\"size\": 512", Read(folder, "Assets/a.png.meta"));
            Assert.Equal(0, AssetDatabase.Open(folder).Duplicates);
        }

        [Fact]
        public void A_file_moved_or_renamed_without_its_meta_is_recognised_when_it_is_clear()
        {
            string folder = Project("Assets/Models/duck.obj", "Assets/Textures/wood.png", "Assets/Textures/a.jpg", "Assets/Textures/b.jpg", "Assets/gone.txt");
            AssetDatabase.Open(folder);
            string duck = AssetDatabase.IdOfKey("Models/duck.obj")!, wood = AssetDatabase.IdOfKey("Textures/wood.png")!,
                   a = AssetDatabase.IdOfKey("Textures/a.jpg")!, b = AssetDatabase.IdOfKey("Textures/b.jpg")!;

            // Outside the editor, the files only: one moved to another folder, one renamed where it is, two
            // renamed in one folder (which is which cannot be known), one deleted.
            Directory.CreateDirectory(Path.Combine(folder, "Assets/Props"));
            File.Move(Path.Combine(folder, "Assets/Models/duck.obj"), Path.Combine(folder, "Assets/Props/duck.obj"));
            File.Move(Path.Combine(folder, "Assets/Textures/wood.png"), Path.Combine(folder, "Assets/Textures/oak.png"));
            File.Move(Path.Combine(folder, "Assets/Textures/a.jpg"), Path.Combine(folder, "Assets/Textures/c.jpg"));
            File.Move(Path.Combine(folder, "Assets/Textures/b.jpg"), Path.Combine(folder, "Assets/Textures/d.jpg"));
            File.Delete(Path.Combine(folder, "Assets/gone.txt"));

            var report = AssetDatabase.Open(folder);
            Assert.Equal(2, report.Adopted);
            Assert.Equal(duck, AssetDatabase.IdOfKey("Props/duck.obj"));
            Assert.Equal(wood, AssetDatabase.IdOfKey("Textures/oak.png"));
            Assert.True(Exists(folder, "Assets/Props/duck.obj.meta"));
            Assert.False(Exists(folder, "Assets/Models/duck.obj.meta"));

            Assert.Equal(2, report.Created);
            Assert.Equal(3, report.Orphans);
            Assert.NotEqual(a, AssetDatabase.IdOfKey("Textures/c.jpg"));
            Assert.NotEqual(b, AssetDatabase.IdOfKey("Textures/c.jpg"));
            Assert.False(Exists(folder, "Assets/Textures/a.jpg.meta"));
            Assert.False(Exists(folder, "Assets/gone.txt.meta"));
            Assert.Equal(5, report.Notes.Count);   // each file taken for another, and each .meta removed, is told

            Assert.Empty(AssetDatabase.Open(folder).Notes);
        }

        [Fact]
        public void A_move_writes_nothing_but_the_meta_and_everything_that_names_the_file_finds_it()
        {
            string folder = Project("Assets/Models/duck.obj", "Assets/Models/Kit/a.obj", "Assets/Models/Kit/Sub/b.obj", "Source/Player.cs");
            Write(folder, "Assets/Models/duck.obj.meta", "{ \"meta\": 1, \"id\": \"" + Id(7) + "\", \"import\": { \"scale\": 2 } }\n");
            AssetDatabase.Open(folder);
            string a = AssetDatabase.IdOfKey("Models/Kit/a.obj")!, b = AssetDatabase.IdOfKey("Models/Kit/Sub/b.obj")!;
            var s = NewScene();
            Mesh(s, "Duck", "Models/duck.obj");
            Mesh(s, "B", "Models/Kit/Sub/b.obj");
            string saved = Save(s);

            var told = new List<string>();
            Action<string, string> onMoved = (from, to) => told.Add(from + " > " + to);
            AssetDatabase.Moved += onMoved;
            try
            {
                Assert.Null(AssetDatabase.Move("Assets/Models/duck.obj", "Assets/Props/Birds/duck2.obj"));   // into a folder that is not there yet
                Assert.Null(AssetDatabase.Move("Assets/Models/Kit", "Assets/Kits/City"));                    // a folder, with what is in it
            }
            finally { AssetDatabase.Moved -= onMoved; }

            Assert.True(Exists(folder, "Assets/Props/Birds/duck2.obj"));
            Assert.False(Exists(folder, "Assets/Models/duck.obj"));
            Assert.False(Exists(folder, "Assets/Models/duck.obj.meta"));
            Assert.Equal("{ \"meta\": 1, \"id\": \"" + Id(7) + "\", \"import\": { \"scale\": 2 } }\n", Read(folder, "Assets/Props/Birds/duck2.obj.meta"));
            Assert.Equal(Id(7), AssetDatabase.IdOfKey("Props/Birds/duck2.obj"));
            Assert.Null(AssetDatabase.IdOfKey("Models/duck.obj"));
            Assert.Equal(a, AssetDatabase.IdOfKey("Kits/City/a.obj"));
            Assert.Equal(b, AssetDatabase.IdOfKey("Kits/City/Sub/b.obj"));
            Assert.True(Exists(folder, "Assets/Kits/City/Sub/b.obj.meta"));
            Assert.Equal(new[] { "Assets/Models/duck.obj > Assets/Props/Birds/duck2.obj",
                                 "Assets/Models/Kit/Sub/b.obj > Assets/Kits/City/Sub/b.obj",
                                 "Assets/Models/Kit/a.obj > Assets/Kits/City/a.obj" }, told);

            // A key from before the move, as an undo brings one back, is known for this session.
            Assert.Equal("Props/Birds/duck2.obj", AssetDatabase.Current("Models/duck.obj"));
            Assert.Equal("Kits/City/a.obj#Body", AssetDatabase.Current("Models/Kit/a.obj#Body"));
            Assert.Equal("Models/other.obj", AssetDatabase.Current("Models/other.obj"));

            // The scene, saved before the moves and not touched since, loads complete.
            Load(saved);
            Assert.Equal("Props/Birds/duck2.obj", MeshOf("Duck").MeshPath);
            Assert.Equal("Kits/City/Sub/b.obj", MeshOf("B").MeshPath);

            // And after the project is opened again, from the .meta files alone.
            AssetDatabase.Open(folder);
            Load(saved);
            Assert.Equal("Props/Birds/duck2.obj", MeshOf("Duck").MeshPath);
            Assert.Equal("Kits/City/Sub/b.obj", MeshOf("B").MeshPath);

            // Refused: a target that exists, a file that is not there, a folder into itself.
            Assert.Contains("already exists", AssetDatabase.Move("Assets/Kits/City/a.obj", "Assets/Props/Birds/duck2.obj"));
            Assert.Contains("not found", AssetDatabase.Move("Assets/nothing.obj", "Assets/x.obj"));
            Assert.Contains("into itself", AssetDatabase.Move("Assets/Kits", "Assets/Kits/City/Kits"));
            Assert.True(Exists(folder, "Assets/Kits/City/a.obj"));
        }

        [Fact]
        public void Files_changed_while_the_project_is_open_are_taken_in()
        {
            string folder = Project("Assets/Models/duck.obj", "Assets/Textures/wood.png", "Assets/Textures/gone.png", "Assets/keep.txt");
            AssetDatabase.Open(folder);
            string duck = AssetDatabase.IdOfKey("Models/duck.obj")!, wood = AssetDatabase.IdOfKey("Textures/wood.png")!,
                   keep = AssetDatabase.IdOfKey("keep.txt")!;
            Assert.Empty(AssetDatabase.Sync().Notes);   // nothing changed: nothing done

            // Outside the editor: a new file; a file moved with its .meta; a file moved without it; a file deleted;
            // a .meta taken away from a file that stays.
            Write(folder, "Assets/new.txt", "x");
            Directory.CreateDirectory(Path.Combine(folder, "Assets/Props"));
            File.Move(Path.Combine(folder, "Assets/Models/duck.obj"), Path.Combine(folder, "Assets/Props/bird.obj"));
            File.Move(Path.Combine(folder, "Assets/Models/duck.obj.meta"), Path.Combine(folder, "Assets/Props/bird.obj.meta"));
            File.Move(Path.Combine(folder, "Assets/Textures/wood.png"), Path.Combine(folder, "Assets/Props/wood.png"));
            File.Delete(Path.Combine(folder, "Assets/Textures/gone.png"));
            File.Delete(Path.Combine(folder, "Assets/keep.txt.meta"));

            var told = new List<string>();
            Action<string, string> onMoved = (from, to) => told.Add(from + " > " + to);
            AssetDatabase.Moved += onMoved;
            AssetScanReport report;
            try { report = AssetDatabase.Sync(); }
            finally { AssetDatabase.Moved -= onMoved; }

            Assert.Equal(1, report.Created);
            Assert.True(AssetDatabase.IsId(AssetDatabase.IdOfKey("new.txt")));
            Assert.Equal(duck, AssetDatabase.IdOfKey("Props/bird.obj"));
            Assert.Equal(wood, AssetDatabase.IdOfKey("Props/wood.png"));
            Assert.True(Exists(folder, "Assets/Props/wood.png.meta"));
            Assert.Null(AssetDatabase.IdOfKey("Models/duck.obj"));
            Assert.Null(AssetDatabase.IdOfKey("Textures/gone.png"));
            Assert.False(Exists(folder, "Assets/Textures/gone.png.meta"));
            Assert.Equal(keep, AssetDatabase.IdOfKey("keep.txt"));
            Assert.Contains(keep, Read(folder, "Assets/keep.txt.meta"));
            told.Sort(StringComparer.Ordinal);
            Assert.Equal(new[] { "Assets/Models/duck.obj > Assets/Props/bird.obj", "Assets/Textures/wood.png > Assets/Props/wood.png" }, told);
            Assert.True(AssetDatabase.HasMoves);
            Assert.Equal("Props/bird.obj#m0", AssetDatabase.Current("Models/duck.obj#m0"));
            Assert.Equal(4, AssetDatabase.Count);

            Assert.Empty(AssetDatabase.Sync().Notes);
        }

        [Fact]
        public void A_file_on_disk_is_given_the_lines_it_lacks_and_keeps_the_ones_it_has()
        {
            Known(Duck, Yellow);
            string duckId = AssetDatabase.IdOfKey(Duck)!;
            var s = NewScene();
            Mesh(s, "Duck", Duck, Yellow);
            string plain = SceneSerializer.Indent(SceneSerializer.Serialize(s));   // written before ids
            string full = AssetRefs.Stamp(plain, keep: true);
            Assert.Equal(new[] { Yellow, Duck }, TableKeys(full));
            Assert.Same(full, AssetRefs.Stamp(full, keep: true));

            // A file that has a line for one of the two files it names gets the other.
            string half = AssetRefs.Stamp(plain.Replace(Yellow, ""), keep: true).Replace("\"Mat\": \"\"", "\"Mat\": \"" + Yellow + "\"");
            Assert.Equal(new[] { Duck }, TableKeys(half));
            var table = AssetRefs.ReadTable(AssetRefs.Stamp(half, keep: true))!;
            Assert.Equal(duckId, table[Duck]);
            Assert.Equal(AssetDatabase.IdOfKey(Yellow), table[Yellow]);

            // The duck moves and another file takes its old name. The file on disk still says the old name, and
            // its line still says which file that was: it is not given the newcomer's id.
            MoveKey(Duck, "Props/bird.obj");
            AssetDatabase.Set(Id(99), "Assets/" + Duck);
            Assert.Same(full, AssetRefs.Stamp(full, keep: true));
            Load(full);
            Assert.Equal("Props/bird.obj", MeshOf("Duck").MeshPath);
        }

        [Fact]
        public void A_key_from_before_a_move_held_in_memory_is_brought_up_to_date()
        {
            Known(Duck, Yellow, Robot);
            var s = NewScene();
            Entity e = Mesh(s, "Arm", Robot + "#m0p1", Yellow);
            string text = SceneSerializer.SerializeEntity(e);
            Assert.Same(text, AssetRefs.Current(text));   // nothing moved

            MoveKey(Robot, "Models/Robots/arm.glb");
            string now = AssetRefs.Current(text);
            Assert.Contains("\"Path\":\"Models/Robots/arm.glb#m0p1\"", now);
            Assert.Contains("\"Mat\":\"" + Yellow + "\"", now);
            Assert.Same(now, AssetRefs.Current(now));

            // A material saved after its texture moved names the texture where it is.
            Known("Textures/wood.png");
            var mat = new PbrMaterial { Name = "Wood", BaseColorMapPath = "Textures/wood.png" };
            MoveKey("Textures/wood.png", "Textures/oak.png");
            string saved = AssetRefs.Stamp(PbrMaterialSerializer.Serialize(mat));
            Assert.Contains("\"baseColorMap\": \"Textures/oak.png\"", saved);
            Assert.Equal(new[] { "Textures/oak.png" }, TableKeys(saved));
        }

        [Fact]
        public void A_new_file_is_given_its_meta_and_a_deleted_one_loses_it()
        {
            string folder = Project("Assets/a.png", "Assets/Kit/b.png");
            AssetDatabase.Open(folder);

            Write(folder, "Assets/Materials/New.pbrmat", "{}");
            string id = AssetDatabase.Register("Assets/Materials/New.pbrmat")!;
            Assert.True(AssetDatabase.IsId(id));
            Assert.Equal(id, AssetDatabase.IdOfKey("Materials/New.pbrmat"));
            Assert.True(Exists(folder, "Assets/Materials/New.pbrmat.meta"));
            Assert.Equal(id, AssetDatabase.Register("Assets/Materials/New.pbrmat"));
            Assert.Null(AssetDatabase.Register("Assets/not_there.png"));
            Assert.Null(AssetDatabase.Register("Assets/config.json"));

            Assert.Null(AssetDatabase.Delete("Assets/a.png"));
            Assert.False(Exists(folder, "Assets/a.png"));
            Assert.False(Exists(folder, "Assets/a.png.meta"));
            Assert.Null(AssetDatabase.IdOfKey("a.png"));
            Assert.Null(AssetDatabase.Delete("Assets/Kit"));
            Assert.Null(AssetDatabase.IdOfKey("Kit/b.png"));
            Assert.Equal(1, AssetDatabase.Count);
            Assert.Contains("not found", AssetDatabase.Delete("Assets/a.png"));
        }
    }
}

using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using GameEditor.Framework.UI;
using Sokol.GUI;
using Xunit;
using Rect = Sokol.GUI.Rect;
using Vector2 = System.Numerics.Vector2;

namespace Scene.Tests
{
    /// <summary>
    /// A game's UI: where an element lies by its anchor, offset and size on screens of several shapes; the widgets
    /// made from the scene's UI entities — placed, ordered, changed and taken away with them; a button's click on
    /// its way to the scripts; the scene blocks.
    /// </summary>
    public sealed class GameUITests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        static readonly List<string> Clicks = new();

        public sealed class Presser : GameBehaviour
        {
            public string Tag = "";
            public override void OnClick() => Clicks.Add(Tag);
        }

        public GameUITests()
        {
            World.Clear();
            GameUI.Clear();
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            ScriptSystem.RegisterType<Presser>();
            SceneManager.NewScene("Screen");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            Clicks.Clear();
        }

        public void Dispose()
        {
            SceneManager.Stop();
            SceneManager.SimulatorFactory = null;
            ScriptSystem.ClearFactories();
            GameUI.Clear();
            World.Clear();
        }

        static Entity Element(string name, UIElement element, Entity? parent = null)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            if (parent is { } p)
            {
                var tr = World.GetComponent<Transform>(e);
                tr.Parent = p;
                World.AddComponent(e, tr);
            }
            World.AddComponent(e, element);
            return e;
        }

        static UIElement At(UIAnchor anchor, float ox, float oy, float w, float h, int order = 0)
            => new() { Anchor = anchor, Offset = new Vector2(ox, oy), Size = new Vector2(w, h), Order = order, Visible = true };

        static void Same(Rect want, Rect got)
        {
            Assert.Equal(want.X, got.X, 3); Assert.Equal(want.Y, got.Y, 3);
            Assert.Equal(want.Width, got.Width, 3); Assert.Equal(want.Height, got.Height, 3);
        }

        [Fact]
        public void An_element_lies_from_its_anchor_towards_the_inside()
        {
            var screen = new Rect(0, 0, 1280, 720);
            Same(new Rect(16, 8, 200, 40), GameUI.Place(At(UIAnchor.TopLeft, 16, 8, 200, 40), screen, 1f));
            Same(new Rect(540, 8, 200, 40), GameUI.Place(At(UIAnchor.Top, 0, 8, 200, 40), screen, 1f));
            Same(new Rect(1064, 8, 200, 40), GameUI.Place(At(UIAnchor.TopRight, 16, 8, 200, 40), screen, 1f));
            Same(new Rect(16, 340, 200, 40), GameUI.Place(At(UIAnchor.Left, 16, 0, 200, 40), screen, 1f));
            Same(new Rect(550, 370, 200, 40), GameUI.Place(At(UIAnchor.Center, 10, 30, 200, 40), screen, 1f));
            Same(new Rect(1064, 340, 200, 40), GameUI.Place(At(UIAnchor.Right, 16, 0, 200, 40), screen, 1f));
            Same(new Rect(16, 672, 200, 40), GameUI.Place(At(UIAnchor.BottomLeft, 16, 8, 200, 40), screen, 1f));
            Same(new Rect(540, 672, 200, 40), GameUI.Place(At(UIAnchor.Bottom, 0, 8, 200, 40), screen, 1f));
            Same(new Rect(1064, 672, 200, 40), GameUI.Place(At(UIAnchor.BottomRight, 16, 8, 200, 40), screen, 1f));

            // Half the height: half of everything, in the same corner; in a parent that does not start at 0.
            Same(new Rect(100 + 300 - 100 - 8, 50 + 4, 100, 20), GameUI.Place(At(UIAnchor.TopRight, 16, 8, 200, 40), new Rect(100, 50, 300, 200), 0.5f));
        }

        // ── The canvas's arithmetic (Phase G2.1): an element's rectangle in design units, and back ───────────────

        [Theory]
        [InlineData(UIAnchor.TopLeft)] [InlineData(UIAnchor.Top)] [InlineData(UIAnchor.TopRight)]
        [InlineData(UIAnchor.Left)] [InlineData(UIAnchor.Center)] [InlineData(UIAnchor.Right)]
        [InlineData(UIAnchor.BottomLeft)] [InlineData(UIAnchor.Bottom)] [InlineData(UIAnchor.BottomRight)]
        public void A_rectangle_given_back_is_the_element_it_came_from_whatever_the_anchor(UIAnchor anchor)
        {
            var element = At(anchor, 16, 24, 200, 40);
            var rect = GameUI.DesignRect(element, 1280, 720);
            var back = GameUI.WithRect(element, rect, 1280, 720);
            Assert.Equal(element.Offset.X, back.Offset.X, 3);
            Assert.Equal(element.Offset.Y, back.Offset.Y, 3);
            Assert.Equal(element.Size, back.Size);
            Assert.Equal(anchor, back.Anchor);
        }

        [Theory]
        [InlineData(UIAnchor.TopLeft, 30, 10)] [InlineData(UIAnchor.Top, 30, 10)] [InlineData(UIAnchor.TopRight, -30, 10)]
        [InlineData(UIAnchor.Left, 30, 10)] [InlineData(UIAnchor.Center, 30, 10)] [InlineData(UIAnchor.Right, -30, 10)]
        [InlineData(UIAnchor.BottomLeft, 30, -10)] [InlineData(UIAnchor.Bottom, 30, -10)] [InlineData(UIAnchor.BottomRight, -30, -10)]
        public void A_move_right_and_down_changes_the_offset_as_its_anchor_counts(UIAnchor anchor, float wantDx, float wantDy)
        {
            // Moved 30 right and 10 down on the screen: from a right-hand anchor the offset counts leftwards, from a bottom one upwards.
            var element = At(anchor, 16, 24, 200, 40);
            var rect = GameUI.DesignRect(element, 1280, 720);
            var moved = GameUI.WithRect(element, new Rect(rect.X + 30, rect.Y + 10, rect.Width, rect.Height), 1280, 720);
            Assert.Equal(16 + wantDx, moved.Offset.X, 3);
            Assert.Equal(24 + wantDy, moved.Offset.Y, 3);
            Same(new Rect(rect.X + 30, rect.Y + 10, 200, 40), GameUI.DesignRect(moved, 1280, 720));
        }

        [Fact]
        public void A_handle_moved_keeps_the_opposite_side_and_a_smallest_size()
        {
            var r = new Rect(100, 100, 200, 40);
            Same(new Rect(90, 95, 210, 45), GameUI.Resized(r, 0, -10, -5, 8));      // top-left: right and bottom stay
            Same(new Rect(100, 95, 200, 45), GameUI.Resized(r, 1, -10, -5, 8));     // top: only the top
            Same(new Rect(100, 100, 220, 40), GameUI.Resized(r, 3, 20, 7, 8));      // right: only the right
            Same(new Rect(100, 100, 220, 47), GameUI.Resized(r, 4, 20, 7, 8));      // bottom-right
            Same(new Rect(100, 100, 200, 47), GameUI.Resized(r, 5, 20, 7, 8));      // bottom
            Same(new Rect(300 - 8, 100, 8, 40), GameUI.Resized(r, 7, 500, 0, 8));   // left past the right: the smallest width
            Same(new Rect(100, 100, 200, 8), GameUI.Resized(r, 5, 0, -500, 8));     // bottom past the top: the smallest height
        }

        [Fact]
        public void A_new_anchor_given_the_same_rectangle_keeps_the_element_where_it_is()
        {
            var element = At(UIAnchor.TopLeft, 50, 60, 200, 40);
            var rect = GameUI.DesignRect(element, 1280, 720);
            foreach (UIAnchor anchor in Enum.GetValues<UIAnchor>())
            {
                var anchored = element;
                anchored.Anchor = anchor;
                anchored = GameUI.WithRect(anchored, rect, 1280, 720);
                Same(rect, GameUI.DesignRect(anchored, 1280, 720));
            }
            Assert.Equal(new Vector2(1280 - 200 - 50, 720 - 40 - 60), GameUI.WithRect(At(UIAnchor.BottomRight, 0, 0, 200, 40), rect, 1280, 720).Offset);
        }

        [Fact]
        public void A_point_finds_the_element_in_front_and_a_child_before_its_parent()
        {
            Entity panel = Element("Panel", At(UIAnchor.TopLeft, 100, 100, 400, 300));
            Entity button = Element("Button", At(UIAnchor.TopLeft, 20, 20, 100, 40), panel);
            Entity over = Element("Over", At(UIAnchor.TopLeft, 450, 100, 200, 100, order: 1));
            Entity hidden = Element("Hidden", At(UIAnchor.TopLeft, 700, 500, 100, 100));
            var h = World.GetComponent<UIElement>(hidden); h.Visible = false; World.AddComponent(hidden, h);
            World.AddComponent(panel, new UIPanel { Color = new Vector4(0, 0, 0, 1) });
            World.AddComponent(button, new UIButton { Text = "B", FontSize = 20 });
            GameUI.Sync(World, 1280, 720, Thickness.Zero);

            Assert.Equal(button, GameUI.HitTest(130, 130));          // the child, on its parent
            Assert.Equal(panel, GameUI.HitTest(300, 300));           // the parent elsewhere
            Assert.Equal(over, GameUI.HitTest(470, 150));            // the one with the higher order, over the panel
            Assert.True(GameUI.HitTest(750, 550).IsNull);            // a hidden element is not found
            Assert.True(GameUI.HitTest(50, 50).IsNull);              // nothing
            Assert.True(GameUI.TryGetParentRect(button, out var inside));
            Same(new Rect(100, 100, 400, 300), inside);              // a child is placed in its parent's rectangle
            Assert.True(GameUI.TryGetParentRect(panel, out var screen));
            Same(new Rect(0, 0, 1280, 720), screen);
        }

        [Fact]
        public void The_UI_is_as_large_in_parts_of_the_screen_on_every_screen_and_keeps_to_its_corners()
        {
            Entity score = Element("Score", At(UIAnchor.TopRight, 16, 16, 220, 48));
            World.AddComponent(score, new UIText { Text = "Coins: 3", FontSize = 28f, Color = new Vector4(1f, 0.9f, 0.2f, 1f), Align = UIAlign.Right });
            Entity pause = Element("Pause", At(UIAnchor.BottomLeft, 16, 16, 96, 96));
            World.AddComponent(pause, UIButton.Default);

            GameUI.Sync(World, 1280, 720, Thickness.Zero);                       // 16:9, the design's own height
            Assert.Equal(1f, GameUI.Scale);
            Assert.True(GameUI.TryGetRect(score, out Rect r));
            Same(new Rect(1280 - 16 - 220, 16, 220, 48), r);
            var label = Assert.IsType<Label>(GameUI.WidgetOf(score));
            Assert.Equal(("Coins: 3", 28f, TextAlign.Right, GameUI.DefaultFont), (label.Text, label.FontSize, label.Align, label.FontName));
            Assert.Equal(new UIColor(1f, 0.9f, 0.2f, 1f), label.ForeColor);
            Assert.Equal(new Sokol.GUI.Vector2(1044, 16), label.Position);
            Assert.Equal(new Sokol.GUI.Vector2(220, 48), label.FixedSize);

            GameUI.Sync(World, 800, 360, Thickness.Zero);                        // a phone lying down, half as high
            Assert.Equal(0.5f, GameUI.Scale);
            GameUI.TryGetRect(score, out r);
            Same(new Rect(800 - 8 - 110, 8, 110, 24), r);
            Assert.Equal(14f, Assert.IsType<Label>(GameUI.WidgetOf(score)).FontSize);
            GameUI.TryGetRect(pause, out r);
            Same(new Rect(8, 360 - 8 - 48, 48, 48), r);

            // A notch on the left and a home bar below: the corners move inwards.
            GameUI.Sync(World, 800, 360, new Thickness(44, 0, 0, 21));
            GameUI.TryGetRect(pause, out r);
            Same(new Rect(44 + 8, 360 - 21 - 8 - 48, 48, 48), r);
            GameUI.TryGetRect(score, out r);
            Same(new Rect(800 - 8 - 110, 8, 110, 24), r);
        }

        [Fact]
        public void A_child_lies_in_its_parents_rectangle_over_it_and_is_hidden_with_it()
        {
            Entity panel = Element("Panel", At(UIAnchor.Center, 0, 0, 400, 300, order: 5));
            World.AddComponent(panel, UIPanel.Default);
            Entity ok = Element("OK", At(UIAnchor.Bottom, 0, 20, 160, 60), panel);
            World.AddComponent(ok, UIButton.Default);
            Entity behind = Element("Behind", At(UIAnchor.TopLeft, 0, 0, 50, 50, order: -1));
            World.AddComponent(behind, UIPanel.Default);
            Entity front = Element("Front", At(UIAnchor.TopLeft, 0, 0, 50, 50, order: 9));
            World.AddComponent(front, UIPanel.Default);

            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            GameUI.TryGetRect(panel, out Rect p);
            Same(new Rect(440, 210, 400, 300), p);
            GameUI.TryGetRect(ok, out Rect b);
            Same(new Rect(440 + 120, 210 + 300 - 20 - 60, 160, 60), b);
            // Drawn in this order: by order, a child with its parent — over it, and under what is over it.
            Assert.Equal(new[] { GameUI.WidgetOf(behind), GameUI.WidgetOf(panel), GameUI.WidgetOf(ok), GameUI.WidgetOf(front) }, GameUI.Root.Children.ToArray());

            // A child of the child, made before a second child that has a lower order: the lower order first,
            // each with what lies in it.
            Entity mark = Element("Mark", At(UIAnchor.Center, 0, 0, 20, 20), ok);
            World.AddComponent(mark, UIText.Default);
            Entity title = Element("Title", At(UIAnchor.Top, 0, 10, 200, 40, order: -3), panel);
            World.AddComponent(title, UIText.Default);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Assert.Equal(new[] { GameUI.WidgetOf(behind), GameUI.WidgetOf(panel), GameUI.WidgetOf(title), GameUI.WidgetOf(ok), GameUI.WidgetOf(mark), GameUI.WidgetOf(front) },
                         GameUI.Root.Children.ToArray());
            GameUI.TryGetRect(mark, out Rect m);
            Same(new Rect(b.X + 70, b.Y + 20, 20, 20), m);

            // Taken out of the panel, the button lies in the screen and is drawn by its own order.
            var free = World.GetComponent<Transform>(ok);
            free.Parent = null;
            World.AddComponent(ok, free);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Assert.Equal(new[] { GameUI.WidgetOf(behind), GameUI.WidgetOf(ok), GameUI.WidgetOf(mark), GameUI.WidgetOf(panel), GameUI.WidgetOf(title), GameUI.WidgetOf(front) },
                         GameUI.Root.Children.ToArray());
            free.Parent = panel;
            World.AddComponent(ok, free);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);

            var hidden = World.GetComponent<UIElement>(panel);
            hidden.Visible = false;
            World.AddComponent(panel, hidden);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Assert.False(GameUI.WidgetOf(panel)!.Visible);
            Assert.False(GameUI.WidgetOf(ok)!.Visible);
            Assert.True(GameUI.WidgetOf(front)!.Visible);
        }

        [Fact]
        public void A_widget_follows_its_components_and_goes_with_its_entity()
        {
            Entity e = Element("Text", At(UIAnchor.Center, 0, 0, 200, 40));
            World.AddComponent(e, UIText.Default);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Widget first = GameUI.WidgetOf(e)!;
            Assert.Equal(1, GameUI.Count);

            var text = World.GetComponent<UIText>(e);
            text.Text = "Coins: 4";
            World.AddComponent(e, text);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Assert.Same(first, GameUI.WidgetOf(e));                              // the same widget, with the new words
            Assert.Equal("Coins: 4", ((Label)first).Text);

            World.RemoveComponent<UIText>(e);                                    // another kind: another widget
            World.AddComponent(e, UIPanel.Default);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Assert.NotSame(first, GameUI.WidgetOf(e));
            Assert.Single(GameUI.Root.Children);

            SceneManager.ActiveScene!.DestroyEntity(e);
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            Assert.Equal(0, GameUI.Count);
            Assert.Empty(GameUI.Root.Children);
            Assert.False(GameUI.TryGetRect(e, out _));
        }

        [Fact]
        public void A_button_let_go_on_tells_its_entitys_scripts_once_in_the_next_frame()
        {
            Entity play = Element("Play", At(UIAnchor.Center, 0, 0, 200, 80));
            World.AddComponent(play, UIButton.Default);
            World.AddComponent(play, new ScriptComponent { TypeName = nameof(Presser), Properties = new() { ["Tag"] = "play" } });
            Entity quit = Element("Quit", At(UIAnchor.Bottom, 0, 20, 200, 80));
            World.AddComponent(quit, UIButton.Default);
            World.AddComponent(quit, new ScriptComponent { TypeName = nameof(Presser), Properties = new() { ["Tag"] = "quit" } });
            GameUI.Sync(World, 1280, 720, Thickness.Zero);
            SceneManager.Play();
            Widget button = GameUI.WidgetOf(play)!;

            // Pressed on it and let go on it: a click. Pressed on it and let go beside it: none.
            var on = new MouseEvent { Button = MouseButton.Left, LocalPosition = new Sokol.GUI.Vector2(100, 40) };
            var off = new MouseEvent { Button = MouseButton.Left, LocalPosition = new Sokol.GUI.Vector2(300, 40) };
            Assert.True(button.OnMouseDown(on));
            Assert.True(button.OnMouseUp(off));
            ScriptSystem.UpdateAll(1f / 60f);
            Assert.Empty(Clicks);
            Assert.False(GameUI.WasClicked(play));

            button.OnMouseDown(on);
            button.OnMouseUp(on);
            Assert.Empty(Clicks);                                               // told in the game's own frame
            ScriptSystem.UpdateAll(1f / 60f);
            Assert.Equal(new[] { "play" }, Clicks.ToArray());
            Assert.True(GameUI.WasClicked(play));
            Assert.False(GameUI.WasClicked(quit));
            ScriptSystem.UpdateAll(1f / 60f);
            Assert.Equal(new[] { "play" }, Clicks.ToArray());
            Assert.False(GameUI.WasClicked(play));

            GameUI.Click(quit);                                                  // as a finger would have
            Entity none = SceneManager.ActiveScene!.CreateEntity("NoButton");
            GameUI.Click(none);                                                  // no button: no click
            ScriptSystem.UpdateAll(1f / 60f);
            Assert.Equal(new[] { "play", "quit" }, Clicks.ToArray());
            Assert.True(GameUI.WasClicked(quit));
            Assert.False(GameUI.WasClicked(none));
        }

        [Fact]
        public void A_frame_of_the_UI_allocates_nothing_with_many_entities_or_a_UI()
        {
            for (int i = 0; i < 1000; i++)
            {
                Entity dot = SceneManager.ActiveScene!.CreateEntity("Dot");
                World.AddComponent(dot, SpriteRenderer.Default);
            }
            long Frame()
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                GameUI.Sync(World, 1280, 720, Thickness.Zero);
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }
            for (int i = 0; i < 3; i++) Frame();
            Assert.Equal(("no UI", 0L), ("no UI", Frame()));

            Entity panel = Element("Panel", At(UIAnchor.Top, 0, 8, 400, 60));
            World.AddComponent(panel, UIPanel.Default);
            Entity text = Element("Text", At(UIAnchor.Left, 8, 0, 200, 40), panel);
            World.AddComponent(text, new UIText { Text = "Coins: 3", FontSize = 28f, Color = Vector4.One });
            Entity go = Element("Go", At(UIAnchor.Center, 0, 0, 200, 80));
            World.AddComponent(go, UIButton.Default);
            for (int i = 0; i < 3; i++) Frame();
            Assert.Equal(("a UI", 0L), ("a UI", Frame()));
        }

        [Fact]
        public void The_root_is_on_the_screen_only_while_there_is_a_UI()
        {
            var screen = new Panel();                                            // in a game: its screen
            try
            {
                GameUI.ShowOn(screen);
                GameUI.Sync(World, 1280, 720, Thickness.Zero);
                Assert.Null(GameUI.Root.Parent);                                 // no UI: the screen has nothing of it to walk

                Entity go = Element("Go", At(UIAnchor.Center, 0, 0, 200, 80));
                World.AddComponent(go, UIButton.Default);
                GameUI.Sync(World, 1280, 720, Thickness.Zero);
                Assert.Same(screen, GameUI.Root.Parent);

                SceneManager.ActiveScene!.DestroyEntity(go);
                GameUI.Sync(World, 1280, 720, Thickness.Zero);
                Assert.Null(GameUI.Root.Parent);
            }
            finally
            {
                GameUI.ShowOn(null);
            }
        }

        [Fact]
        public void The_UI_blocks_round_trip_and_blocks_with_nothing_in_them_have_the_defaults()
        {
            Entity e = Element("All", new UIElement { Anchor = UIAnchor.BottomRight, Offset = new Vector2(8, 12), Size = new Vector2(300, 90), Order = 3, Visible = false });
            World.AddComponent(e, new UIText { Text = "Say \"hi\"", Font = "Fonts/pixel.ttf", FontSize = 40f, Color = new Vector4(0.1f, 0.2f, 0.3f, 0.4f), Align = UIAlign.Center, Wrap = true });
            Entity b = Element("B", UIElement.Default);
            World.AddComponent(b, new UIButton { Text = "Go", Font = "", FontSize = 30f, Color = new Vector4(1, 0, 0, 1), TextColor = new Vector4(0, 0, 0, 1), Radius = 4f });
            Entity i = Element("I", UIElement.Default);
            World.AddComponent(i, new UIPicture { Texture = "Sprites/hud.sprites#heart", Color = new Vector4(1, 1, 1, 0.5f) });
            Entity p = Element("P", UIElement.Default);
            World.AddComponent(p, new UIPanel { Color = new Vector4(0, 0, 0, 0.25f), Radius = 20f });

            string text = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"UI\":{\"Anchor\":\"BottomRight\",\"Offset\":[8,12],\"Size\":[300,90],\"Order\":3,\"Vis\":false}", text);
            Assert.Contains("\"UIImage\":{\"Tex\":\"Sprites/hud.sprites#heart\",\"Color\":[1,1,1,0.5]}", text);
            SceneSerializer.Deserialize(text, SceneManager.ActiveScene!);
            Entity Named(string name) => World.Entities.First(x => World.GetComponent<NameTag>(x).Name == name);
            var el = World.GetComponent<UIElement>(Named("All"));
            Assert.Equal((UIAnchor.BottomRight, new Vector2(8, 12), new Vector2(300, 90), 3, false), (el.Anchor, el.Offset, el.Size, el.Order, el.Visible));
            var t = World.GetComponent<UIText>(Named("All"));
            Assert.Equal(("Say \"hi\"", "Fonts/pixel.ttf", 40f, new Vector4(0.1f, 0.2f, 0.3f, 0.4f), UIAlign.Center, true), (t.Text, t.Font, t.FontSize, t.Color, t.Align, t.Wrap));
            var bt = World.GetComponent<UIButton>(Named("B"));
            Assert.Equal(("Go", 30f, new Vector4(1, 0, 0, 1), new Vector4(0, 0, 0, 1), 4f), (bt.Text, bt.FontSize, bt.Color, bt.TextColor, bt.Radius));
            Assert.Equal(("Sprites/hud.sprites#heart", new Vector4(1, 1, 1, 0.5f)), (World.GetComponent<UIPicture>(Named("I")).Texture, World.GetComponent<UIPicture>(Named("I")).Color));
            Assert.Equal((new Vector4(0, 0, 0, 0.25f), 20f), (World.GetComponent<UIPanel>(Named("P")).Color, World.GetComponent<UIPanel>(Named("P")).Radius));

            // Blocks with nothing in them.
            string bare = text.Substring(0, text.IndexOf("\"entities\"", StringComparison.Ordinal))
                        + "\"entities\":[{\"id\":1,\"c\":{\"NameTag\":{\"Name\":\"Bare\"},\"Transform\":{},\"UI\":{},\"UIText\":{},\"UIButton\":{},\"UIPanel\":{},\"UIImage\":{}}}],\"nextId\":2}";
            SceneSerializer.Deserialize(bare, SceneManager.ActiveScene!);
            Entity bareE = Named("Bare");
            var d = World.GetComponent<UIElement>(bareE);
            Assert.Equal((UIAnchor.Center, Vector2.Zero, new Vector2(200, 48), 0, true), (d.Anchor, d.Offset, d.Size, d.Order, d.Visible));
            Assert.Equal(("Text", 28f, Vector4.One, UIAlign.Left, false), (World.GetComponent<UIText>(bareE).Text, World.GetComponent<UIText>(bareE).FontSize, World.GetComponent<UIText>(bareE).Color, World.GetComponent<UIText>(bareE).Align, World.GetComponent<UIText>(bareE).Wrap));
            Assert.Equal(("Button", 10f), (World.GetComponent<UIButton>(bareE).Text, World.GetComponent<UIButton>(bareE).Radius));
            Assert.Equal(new Vector4(0, 0, 0, 0.6f), World.GetComponent<UIPanel>(bareE).Color);
            Assert.Equal("", World.GetComponent<UIPicture>(bareE).Texture);
        }
    }
}

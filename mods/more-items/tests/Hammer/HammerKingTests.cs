using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static int checks;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private sealed class World : IHammerWorld
        {
            internal readonly List<Rectangle> Blocks = new List<Rectangle>();
            internal float Grip = 1.25f;
            public bool Solid(Vector2 center, Vector2 halfSize)
            {
                foreach (Rectangle block in Blocks)
                    if (center.X + halfSize.X > block.Left && center.X - halfSize.X < block.Right
                        && center.Y + halfSize.Y > block.Top && center.Y - halfSize.Y < block.Bottom) return true;
                return false;
            }
            public float Friction(Vector2 center) { return Grip; }
            public bool Snow(Vector2 center, Vector2 halfSize) { return false; }
        }
        private static HammerPhysics Pose(Vector2 center, Vector2 offset)
        { return new HammerPhysics { Ready = true, LastBody = center, Head = center + offset, Target = offset }; }
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); checks++; }
        private static void Near(Vector2 actual, Vector2 expected, string name)
        { Check(Vector2.DistanceSquared(actual, expected) < 0.001f, name + ": " + actual + " != " + expected); }

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "--perf") { Performance(); return 0; }
                if (args.Length == 1 && args[0] == "--rock") { Rocking(); return 0; }
                if (args.Length == 1 && args[0] == "--balance") { Balance(); return 0; }
                if (args.Length == 1 && args[0] == "--equipment") { equipmentIntegration = true; Native(); NativeMotion(); LandingPipeline(); return 0; }
                if (args.Length == 3 && args[0] == "--package") { Package(args[1], args[2]); return 0; }
                if (args.Length == 2 && args[0] == "--audio") { Audio(true, args[1]); return 0; }
                if ((args.Length == 3 || args.Length == 4) && args[0] == "--charge") { ConfigureChargeTest(args[1], args[2]); if (args.Length == 4) casualAssembly = LoadImplementation(args[3]); Native(); NativeMotion(); LandingPipeline(); return 0; }
                HammerController.ValidateContract();
                Air(); Contacts(); Sweeps(); Input(); Native(); NativeMotion(); GroundAndWalls(); RestingGrip(); Snapshots();
                LandingPipeline(); ImpactSounds(); Audio(false);
                WeightAndMaterials(); SlidingHandle(); GeometryCache(); Performance(); StrengthSettings(); Rocking(); Balance();
                Console.WriteLine("[OK] Hammer King: " + checks + " physics/input/native/state checks");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Air()
        {
            var world = new World();
            var physics = Pose(Vector2.Zero, new Vector2(24, -12));
            Vector2 velocity = new Vector2(3, -5);
            for (int i = 0; i < 360; i++)
            {
                Vector2 mouse = new Vector2((float)Math.Cos(i * 0.12) * 8f, (float)Math.Sin(i * 0.12) * 8f);
                Near(physics.Step(Vector2.Zero, velocity, mouse, world), velocity, "Air swing must not propel the king");
                Check(!physics.Contact && physics.Target.Length() <= HammerPhysics.Reach + 0.01f, "Free swing reach and contact");
            }
            physics.Reset(new Vector2(200, -400), world);
            Check(Vector2.Distance(physics.Head, new Vector2(200, -400)) <= HammerPhysics.Reach, "Reset follows world position");
            physics.AfterBody(new Vector2(400, -900), world);
            Check(Vector2.Distance(physics.Head, new Vector2(400, -900)) <= HammerPhysics.Reach, "Teleport drops old anchor");
            physics.Drive = new Vector2(8, 4);
            physics.AfterBody(new Vector2(400, -1200), world);
            Near(physics.Drive, Vector2.Zero, "Teleport cannot carry a pending drive into the next contact");
            Check(HammerPhysics.ClampReach(Vector2.Zero).Length() == HammerPhysics.MinimumReach, "Zero aim stays finite");
            physics = Pose(Vector2.Zero, new Vector2(24, -12));
            for (int i = 0; i < 180; i++)
            {
                Vector2 center = new Vector2(i * 12f, -i * 10f);
                physics.Step(center, new Vector2(12, -10), Vector2.Zero, world);
                Check(Vector2.Distance(physics.Head, center + new Vector2(12, -10)) <= HammerPhysics.Reach + 0.01f, "Airborne hammer follows fast body motion");
            }
        }

        private static void Contacts()
        {
            var world = new World(); world.Blocks.Add(new Rectangle(-200, 24, 400, 20));
            var physics = Pose(Vector2.Zero, new Vector2(0, 22));
            Vector2 push = physics.Step(Vector2.Zero, Vector2.Zero, new Vector2(0, 8), world);
            Check(push.Y < -1 && physics.Contact, "Downward hammer push launches upward");
            Check(!world.Solid(physics.Head, HammerPhysics.HeadSize), "Contact does not penetrate floor");
            Vector2 release = physics.Step(Vector2.Zero, Vector2.Zero, new Vector2(0, -30), world);
            Near(release, Vector2.Zero, "Pulling away must release without magnetic force");
            Check(!physics.Contact && physics.Head.Y < 22, "Head leaves support");
            physics = Pose(Vector2.Zero, new Vector2(0, 22));
            Vector2 normal = physics.Step(Vector2.Zero, Vector2.Zero, new Vector2(8, 8), world);
            world.Grip = 0.08f;
            physics = Pose(Vector2.Zero, new Vector2(0, 22));
            Vector2 ice = physics.Step(Vector2.Zero, Vector2.Zero, new Vector2(8, 8), world);
            Check(Math.Abs(normal.X) > Math.Abs(ice.X) + 0.25f, "Ice slips more than ordinary stone: dry=" + normal + ", ice=" + ice);
            world = new World(); world.Blocks.Add(new Rectangle(24, -100, 20, 200));
            physics = Pose(Vector2.Zero, new Vector2(21, 0));
            push = physics.Step(Vector2.Zero, Vector2.Zero, new Vector2(10, 0), world);
            Check(push.X < -1, "Wall push launches away from wall");
            physics = Pose(Vector2.Zero, new Vector2(21, 0));
            Near(physics.Step(Vector2.Zero, Vector2.Zero, new Vector2(0, 6), world), Vector2.Zero,
                "Wall has no tangential adhesion without pressure");
            // Supporting a hanging king: the head rests on top of an offset ledge.
            world = new World(); world.Blocks.Add(new Rectangle(18, -20, 50, 10));
            // Hook the near edge; placing the head deep over this ledge while
            // the body hangs below would put the diagonal shaft through it.
            physics = Pose(Vector2.Zero, new Vector2(17, -22));
            push = physics.Step(new Vector2(0, 2), new Vector2(0, 2), new Vector2(0, 6), world);
            Check(push.Y < 0, "Retracting from an overhead ledge lifts the king");
        }

        private static void Sweeps()
        {
            var world = new World(); world.Blocks.Add(new Rectangle(25, -100, 1, 200));
            Vector2 start = new Vector2(10, 0);
            Vector2 moved = HammerPhysics.Sweep(Vector2.Zero, start, new Vector2(200, 0), world);
            Check(start.X + moved.X <= 22.01f, "Fast head cannot cross one-pixel wall");
            world = new World(); world.Blocks.Add(new Rectangle(-100, 25, 200, 1));
            start = new Vector2(0, 10);
            moved = HammerPhysics.Sweep(Vector2.Zero, start, new Vector2(0, 200), world);
            Check(start.Y + moved.Y <= 23.01f, "Fast head cannot cross one-pixel platform");
            world = new World(); world.Blocks.Add(new Rectangle(22, 9, 3, 3));
            start = new Vector2(28, 0);
            moved = HammerPhysics.Sweep(Vector2.Zero, start, new Vector2(0, 35), world);
            Check(moved.Y < 30, "Shaft catches geometry even when the head is clear");
            world = new World(); world.Blocks.Add(new Rectangle(25, 25, 40, 40));
            var physics = Pose(Vector2.Zero, new Vector2(20, 20));
            for (int i = 0; i < 100; i++)
            {
                Vector2 velocity = physics.Step(Vector2.Zero, new Vector2(5, 5), new Vector2(25, 25), world);
                Check(!world.Solid(physics.Head, HammerPhysics.HeadSize), "Repeated corner pressure is nonpenetrating");
                Check(velocity.Length() <= HammerPhysics.MaximumSpeed + 0.01f, "Reaction remains bounded");
            }
        }

        private static void Input()
        {
            var gate = new MouseDeltaGate();
            Near(gate.Read(new Point(100, 100), true), Vector2.Zero, "First active sample is discarded");
            gate.Recenter(new Point(200, 200));
            Near(gate.Read(new Point(205, 197), true), new Vector2(5, -3), "Warp is excluded from mouse delta");
            Near(gate.Read(new Point(800, 900), false), Vector2.Zero, "Inactive input is ignored");
            Near(gate.Read(new Point(900, 1000), true), Vector2.Zero, "Resume has no accumulated input");
            gate.Release();
            Near(gate.Read(new Point(-500, 0), true), Vector2.Zero, "Snapshot restore resets physical input");
            Rectangle box = NativeWorld.Box(new Vector2(-0.5f, -1.25f), new Vector2(5, 3));
            Check(box.Left <= -5.5f && box.Right >= 4.5f && box.Top <= -4.25f, "Negative-world collision rounding is conservative");
        }

        private static void Native()
        {
            // Exercise the installed game's collision implementation in memory.
            var screen = (LevelScreen)FormatterServices.GetUninitializedObject(typeof(LevelScreen));
            typeof(LevelScreen).GetField("m_hitboxes", Flags).SetValue(screen, new IBlock[] {
                new BoxBlock(new Rectangle(0, 100, 480, 20)),
                new SlopeBlock(new Rectangle(100, 50, 32, 32), SlopeType.TopLeft)
            });
            typeof(LevelManager).GetField("m_screens", Flags).SetValue(null, new[] { screen });
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
            var world = new NativeWorld();
            world.Prepare(new Vector2(70, 70));
            Check(world.Solid(new Vector2(40, 100), Vector2.One), "Installed native solid block query");
            Check(!world.Solid(new Vector2(40, 90), Vector2.One), "Installed native empty query");
            Check(world.Solid(new Vector2(128, 77), Vector2.One), "Native slope solid half");
            Check(!world.Solid(new Vector2(103, 53), Vector2.One), "Native slope empty half");
            var body = new BodyComp(new Vector2(30, 50), 18, 26);
            string[] names = body.GetBehaviourList().Select(b => b.GetType().Name).ToArray();
            Check(Array.IndexOf(names, "WindVelocityUpdateBehaviour") < Array.IndexOf(names, "UpdateXPositionFromVelocityBehaviour"), "Native motor phase precedes movement");
            Check(Array.IndexOf(names, "ApplyGravityBehaviour") > Array.IndexOf(names, "ResolveYCollisionBehaviour"), "Native post phase follows collisions");
            Check(body.GetHitbox().Size == new Point(18, 26), "Native king body is preserved");
        }

        private static void Snapshots()
        {
            var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            player.m_body = new BodyComp(Vector2.Zero, 18, 26);
            var controller = (HammerController)Activator.CreateInstance(typeof(HammerController), Flags, null, new object[] { player }, null);
            var physics = (HammerPhysics)typeof(HammerController).GetField("physics", Flags).GetValue(controller);
            physics.Reset(Vector2.Zero, new World());
            physics.Drive = new Vector2(.5f, -.25f);
            object snapshot = controller.Capture();
            Vector2 head = physics.Head;
            physics.Head += new Vector2(200, 200);
            physics.Drive = Vector2.Zero;
            controller.Restore(snapshot);
            Near(physics.Head, head, "Snapshot restores head position");
            Near(physics.Drive, new Vector2(.5f, -.25f), "Snapshot restores the filtered drive for deterministic continuation");
            Check(controller.Version == 3, "Filtered drive changes the snapshot contract version");
            snapshot.GetType().GetField("Drive", Flags).SetValue(snapshot, new Vector2(float.PositiveInfinity, 0));
            bool badDrive = false;
            try { controller.Restore(snapshot); } catch (ArgumentException) { badDrive = true; }
            Check(badDrive && physics.Head == head, "Invalid drive is rejected before snapshot mutation");
            snapshot.GetType().GetField("Drive", Flags).SetValue(snapshot, new Vector2(.5f, -.25f));
            bool rejected = false;
            try { controller.Validate(new object()); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Foreign snapshots rejected before mutation");
            snapshot.GetType().GetField("Head", Flags).SetValue(snapshot, new Vector2(float.NaN, 0));
            rejected = false;
            try { controller.Restore(snapshot); } catch (ArgumentException) { rejected = true; }
            Check(rejected && physics.Head == head, "Nonfinite snapshot rejected without partial restore");
            typeof(HammerController).GetMethod("ReleaseMouse", Flags).Invoke(controller, null);
            Near(physics.Drive, Vector2.Zero, "Pause and focus release discard pending filtered input");
            controller.Dispose(); controller.Dispose();
        }

        private static void NativeMotion()
        {
            var screens = new[] { new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(0, 300, 480, 60)) },
                new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) };
            typeof(LevelManager).GetField("m_screens", Flags).SetValue(null, screens);
            // Seed native inventory reads in memory; do not open the player's save.
            Type save = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            var cache = (Dictionary<string, object>)save.GetField("loaded_objects", Flags).GetValue(null);
            cache["SavesPermainventory.inv"] = new JumpKing.MiscEntities.WorldItems.Inventory.Inventory().GetDefault();
            cache["SavesPermageneral_settings.set"] = new JumpKing.SaveThread.GeneralSettings().GetDefault();
            var body = new BodyComp(new Vector2(190, 274), 18, 26);
            var world = new NativeWorld();
            var physics = Pose(body.Position + new Vector2(9, 13), new Vector2(16, 11));
            MethodInfo tick = typeof(BodyComp).GetMethod("UpdateInternal", Flags);
            float highest = body.Position.Y;
            bool landed = false;
            for (int i = 0; i < 180; i++)
            {
                Vector2 center = body.Position + new Vector2(9, 13);
                world.Prepare(center);
                Vector2 mouse = i < 4 ? new Vector2(0, 10) : i == 4 ? new Vector2(0, -45) : Vector2.Zero;
                body.Velocity = physics.Step(center, body.Velocity, mouse, world);
                tick.Invoke(body, new object[] { 1f / 60f });
                physics.AfterBody(body.Position + new Vector2(9, 13), world);
                highest = Math.Min(highest, body.Position.Y);
                if (i > 20 && body.IsOnGround) landed = true;
                Check(body.GetHitbox().Bottom <= 300, "Actual native body never sinks through support");
            }
            Check(highest < 254, "Hammer impulse produces actual native ascent of over 20 pixels: " + highest);
            Check(landed, "Native gravity returns the released king to the floor");
            Console.WriteLine("[OK] Native hammer launch height: " + (274 - highest).ToString("F1") + " px; release and landing");
        }

        private static void Package(string path, string implementation)
        {
            Assembly shell = Assembly.LoadFrom(path);
            Type entry = shell.GetTypes().Single(t => t.Name == "PackageEntry");
            Check(!shell.GetReferencedAssemblies().Any(a => a.Name == "JKRuntime"), "Discovery shell must load without Runtime");
            Check(entry.GetMethods().Any(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "MainMenuItemSettingAttribute")), "Packaged main menu toggle");
            Check(entry.GetMethods().Any(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "PauseMenuItemSettingAttribute")), "Packaged pause toggle can release the controller");
            using (Stream payload = shell.GetManifestResourceStream("JKRuntime.Module"))
            using (var bytes = new MemoryStream())
            {
                payload.CopyTo(bytes);
                Check(bytes.ToArray().SequenceEqual(File.ReadAllBytes(implementation)), "Package contains this exact tested implementation");
            }
            var manifest = new System.Xml.XmlDocument();
            using (Stream stream = shell.GetManifestResourceStream("JKRuntime.Manifest")) manifest.Load(stream);
            Check(manifest.DocumentElement.GetAttribute("id") == "more-items", "Hammer ships inside More Items");
            Check(int.Parse(manifest.DocumentElement.GetAttribute("apiMinor")) >= 14, "Package declares adjustable Debug Actions SDK");
            Check(!typeof(MoreItems.ModEntry).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("HammerKing")), "Hammer has no standalone mod dependency");
            Console.WriteLine("[OK] Packaged discovery, both menus, exact embedded payload and Runtime dependency");
        }

        private static void Scene(params IBlock[] blocks)
        {
            typeof(LevelManager).GetField("m_screens", Flags).SetValue(null, new[] {
                new LevelScreen(0, blocks, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null)
            });
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
            typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
        }

        private static void Tick(BodyComp body)
        { typeof(BodyComp).GetMethod("UpdateInternal", Flags).Invoke(body, new object[] { 1f / 60f }); }

        private static IDisposable Responses(BodyComp body)
        {
            // Only mute audio in this in-memory fixture; retain the real native
            // movement, material, collision and gravity behaviours in order.
            var behaviours = (LinkedList<IBodyCompBehaviour>)typeof(BodyComp).GetField("m_behaviours", Flags).GetValue(body);
            var sound = behaviours.First(b => b is JumpKing.BodyCompBehaviours.PlayBumpSFXBehaviour);
            behaviours.Remove(sound);
            var pipeline = new JKRuntime.Gameplay.BodyPipeline(body, false, -10000, "hammer-king");
            HammerBodyResponse.Register(pipeline);
            return pipeline;
        }

        private static void GroundAndWalls()
        {
            Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
            var body = new BodyComp(new Vector2(150, 274), 18, 26) { Velocity = new Vector2(8, 0) };
            using (Responses(body))
            {
                for (int i = 0; i < 120; i++) Tick(body);
                Check(body.Velocity.X == 0f, "Ground friction stops the native king completely");
                Check(body.Position.X < 210, "Stopping distance is bounded instead of endless ground sliding");
                float stopped = body.Position.X;
                for (int i = 0; i < 300; i++) Tick(body);
                Check(body.Position.X == stopped, "Stationary grounded king does not creep");
            }
            Scene(new IceBlock(new Rectangle(0, 300, 480, 60)));
            body = new BodyComp(new Vector2(150, 274), 18, 26) { Velocity = new Vector2(4, 0) };
            using (Responses(body))
            {
                for (int i = 0; i < 20; i++) Tick(body);
                Check(body.Velocity.X > 0.1f, "Ice retains reduced body friction: " + body.Velocity.X);
            }
            foreach (int direction in new[] { -1, 1 })
            {
                Scene(new BoxBlock(new Rectangle(270, 50, 20, 250)), new BoxBlock(new Rectangle(0, 300, 480, 60)));
                body = new BodyComp(new Vector2(direction > 0 ? 250 : 291, 150), 18, 26) { Velocity = new Vector2(direction * 8, 1) };
                using (Responses(body))
                {
                    Tick(body);
                    Check(body.Velocity.X == 0f, "Wall impact stops instead of reflecting velocity: " + direction);
                    for (int i = 0; i < 10; i++) Tick(body);
                    Check(body.Velocity.X == 0f, "No later ping-pong rebound after wall impact: " + direction);
                }
            }
            // Ground drag must not destroy a genuine hammer launch in the air.
            Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
            body = new BodyComp(new Vector2(180, 100), 18, 26) { Velocity = new Vector2(6, -4) };
            using (Responses(body)) { Tick(body); Check(body.Velocity.X == 6f, "Airborne momentum is preserved"); }
            Console.WriteLine("[OK] Native ground stop/rest, ice drag, left/right nonelastic wall impacts and air momentum");
        }

        private static void RestingGrip()
        {
            Scene(new BoxBlock(new Rectangle(0, 300, 480, 60)));
            var body = new BodyComp(new Vector2(190, 265), 18, 26) { Velocity = new Vector2(1.5f, 0.35f) };
            Vector2 center = body.Position + new Vector2(9, 13);
            var physics = Pose(center, new Vector2(8, 20));
            var world = new NativeWorld();
            Vector2 head = physics.Head;
            float lowest = body.Position.Y, highest = body.Position.Y;
            using (Responses(body))
            {
                for (int i = 0; i < 600; i++)
                {
                    center = body.Position + new Vector2(9, 13); world.Prepare(center);
                    body.Velocity = physics.Step(center, body.Velocity, Vector2.Zero, world);
                    Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), world);
                    lowest = Math.Max(lowest, body.Position.Y); highest = Math.Min(highest, body.Position.Y);
                }
                Check(lowest - highest < 1f, "Resting on the hammer has no passive pogo or spring oscillation: " + (lowest - highest));
                Near(physics.Head, head, "Resting hammer friction holds against tangential drift for ten seconds");
                Check(Math.Abs(body.Velocity.X) < 0.01f, "Supported body loses lateral drift");
                float before = body.Position.X;
                for (int i = 0; i < 3; i++)
                {
                    center = body.Position + new Vector2(9, 13); world.Prepare(center);
                    body.Velocity = physics.Step(center, body.Velocity, new Vector2(1, 0), world);
                    Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), world);
                }
                Near(physics.Head, head, "Slow horizontal mouse movement keeps the grounded hammer planted");
                Check(body.Position.X < before - .1f && body.Position.X > before - 1f,
                    "Three one-pixel corrections move the supported king gradually by less than one pixel");
                for (int i = 0; i < 120; i++)
                {
                    center = body.Position + new Vector2(9, 13); world.Prepare(center);
                    body.Velocity = physics.Step(center, body.Velocity, Vector2.Zero, world);
                    Tick(body); physics.AfterBody(body.Position + new Vector2(9, 13), world);
                }
                Check(Math.Abs(body.Velocity.X) < 0.01f && Math.Abs(body.Position.Y - 265f) < 1f,
                    "Stopping the mouse does not build stored impulses or bounce the king");
                center = body.Position + new Vector2(9, 13); world.Prepare(center);
                physics.Step(center, Vector2.Zero, new Vector2(0, -12), world);
                Check(!physics.Contact && physics.Head.Y < head.Y - 1, "Lifting the hammer releases static grip immediately");
            }
            Console.WriteLine("[OK] Ten-second stationary hammer support, static drag grip, stop stability and explicit release");
        }
    }
}

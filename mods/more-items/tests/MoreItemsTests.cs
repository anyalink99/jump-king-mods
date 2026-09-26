using System;
using System.Reflection;
using System.Reflection.Emit;
using JumpKingJetpack;
using MoreItems;

internal static class MoreItemsTests
{
    private const float Gravity = 0.2571428571428571f;
    private const float FullUpwardSpeed = 8.742857142857142f;
    private static int failures;

    private static int Main()
    {
        TestThrust();
        TestActivation();
        TestAirAnimation();
        TestFlameAnimation();
        TestSoundEnvelope();
        TestAtlas();
        TestPermission();
        TestLateOptionalApiResolution();
        TestMorphSpriteOwnership();
        TestPersistentInventoryModel();
        TestInventoryPreparation();
        TestWorldItemPreparation();
        TestNewRunPickupReset();
        TestItemModuleRegistry();
        TestEquipmentContract();
        if (failures != 0)
        {
            Console.Error.WriteLine("More Items tests failed: " + failures);
            return 1;
        }
        Console.WriteLine("[OK] More Items tests");
        return 0;
    }

    private static void TestWorldItemPreparation()
    {
        string root = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "world-items-" + Guid.NewGuid().ToString("N"));
        string directory = System.IO.Path.Combine(root, "props", "more-items"); System.IO.Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, "items.xml");
        System.IO.File.WriteAllText(path, "<MoreItems version='1'><Pickup item='fixture' count='1' screen='1' x='2' y='3'/></MoreItems>");
        var data = ItemWorldLoader.Read(root);
        AssertTrue(data.Pickups.Length == 1 && data.Pickups[0].Id == "pickup-0", "Prepared world data preserves stable generated pickup IDs");
        using (var scope = new JKRuntime.RuntimeScope())
        {
            ItemWorldLoader.Prepare(scope, root);
            var field = typeof(ItemWorldLoader).GetField("prepared", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var ready = (ConsumablePickupFile)field.GetValue(null);
            System.IO.File.WriteAllText(path, "broken after preparation");
            AssertTrue(ready.Pickups.Length == 1, "Attempt preparation retains validated data independently of subsequent disk writes");
        }
        var prepared = typeof(ItemWorldLoader).GetField("prepared", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        AssertTrue(prepared.GetValue(null) == null, "Cancelled attempts release prepared world data");
        foreach (string invalid in new[] {
            "<MoreItems version='2'/>",
            "<MoreItems><Pickup id='same' item='fixture' count='1' screen='1'/><Pickup id='same' item='fixture' count='1' screen='1'/></MoreItems>",
            "<MoreItems><Dispenser id='bad' item='fixture' x='NaN'/></MoreItems>",
            "<!DOCTYPE MoreItems [<!ENTITY x 'bad'>]><MoreItems/>" })
        {
            System.IO.File.WriteAllText(path, invalid); bool rejected = false;
            try { ItemWorldLoader.Read(root); } catch { rejected = true; }
            AssertTrue(rejected, "Invalid world documents reject before any entity is created");
        }
    }

    private static void TestInventoryPreparation()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        string previousDirectory = Environment.CurrentDirectory;
        string directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory-fixture-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(directory, "Content/Saves"));
        Environment.CurrentDirectory = directory;
        string path = JKRuntime.Settings.NativeSaveFiles.GetPath("Saves", "more_items_inventory.sav");
        var instance = typeof(JumpKing.Game1).GetField("_instance", flags); object oldGame = instance.GetValue(null);
        var game = (JumpKing.Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Game1));
        GC.SuppressFinalize(game);
        var content = typeof(JumpKing.Game1).GetField("contentManager", flags);
        content.SetValue(game, System.Runtime.Serialization.FormatterServices.GetUninitializedObject(content.FieldType));
        var oldSave = JumpKing.SaveThread.SaveManager.instance;
        Func<int, ConsumableSaveDatabase> make = count => new ConsumableSaveDatabase { Version = 1, Saves = new[] {
            new ConsumableSave { Key = "inventory", Items = new[] { new ConsumableStack { Id = "fixture", Count = count } } },
            new ConsumableSave { Key = "vanilla", Collected = new[] { "retained-pickup" } }
        } };
        try
        {
            instance.SetValue(null, game); JumpKing.SaveThread.SaveManager.instance = null;
            var first = make(3); JKRuntime.Settings.AtomicXmlFile.Save(path, first);
            using (var scope = new JKRuntime.RuntimeScope())
            {
                ItemInventory.Prepare(scope);
                AssertTrue(first.Saves[1].Collected.Length == 1, "preparation never resets saved pickups");
                JKRuntime.Settings.AtomicXmlFile.Save(path, make(7)); ItemInventory.Reload();
                AssertEqual(3, ItemInventory.GetCount("fixture"), "activation consumes the prepared inventory read");
                AssertTrue(ItemInventory.IsCollected("retained-pickup"), "resumed attempt preserves pickup state");
            }
            using (var scope = new JKRuntime.RuntimeScope()) { ItemInventory.Prepare(scope); }
            JKRuntime.Settings.AtomicXmlFile.Save(path, make(9)); ItemInventory.Reload();
            AssertEqual(9, ItemInventory.GetCount("fixture"), "cancelled preparation cannot leak into another attempt");
            AssertTrue(ItemInventory.TryCollect("fixture", 2, "transaction"), "Pickup count and marker commit together");
            var committed = JKRuntime.Settings.AtomicXmlFile.Load<ConsumableSaveDatabase>(path);
            AssertTrue(Array.Exists(committed.Saves, s => s.Key == "inventory" && Array.Exists(s.Items, i => i.Id == "fixture" && i.Count == 11))
                && Array.Exists(committed.Saves, s => s.Key == "vanilla" && Array.IndexOf(s.Collected, "transaction") >= 0), "One durable document contains both pickup changes");
            AssertTrue(!ItemInventory.TryCollect("fixture", 2, "transaction") && ItemInventory.GetCount("fixture") == 11, "A repeated pickup cannot grant twice");
            using (var locked = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
                AssertTrue(!ItemInventory.TryCollect("fixture", 2, "retry"), "Storage failure rejects the whole pickup");
            AssertTrue(ItemInventory.GetCount("fixture") == 11 && !ItemInventory.IsCollected("retry"), "Failed pickup restores count and marker in memory");
            AssertTrue(ItemInventory.TryCollect("fixture", 2, "retry"), "A failed pickup can retry after storage recovers");

            int notifications = 0;
            using (MoreItemsApi.SubscribeInventoryChanged(delegate { throw new Exception("Broken third-party listener"); }))
            using (MoreItemsApi.SubscribeInventoryChanged(delegate(string id, int count) { if (id == "fixture" && count == 13) notifications++; }))
                typeof(MoreItemsApi).GetMethod("NotifyInventoryChanged", flags).Invoke(null, new object[] { "fixture" });
            AssertEqual(1, notifications, "An observer failure cannot hide committed data from later observers");

            var pending = ItemInventory.BeginCollect("fixture", 3, "async-pickup");
            AssertTrue(pending != null && ItemInventory.GetCount("fixture") == 13
                && !ItemInventory.IsCollected("async-pickup"), "Queued pickup does not publish uncommitted state");
            WaitForCollection(pending);
            AssertTrue(pending.Succeeded && ItemInventory.GetCount("fixture") == 16
                && ItemInventory.IsCollected("async-pickup"), "Completed pickup publishes count and marker together");
            AssertTrue(ItemInventory.BeginCollect("fixture", 3, "async-pickup") == null, "Completed asynchronous pickup cannot grant twice");
            using (var locked = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                pending = ItemInventory.BeginCollect("fixture", 3, "async-retry");
                WaitForCollection(pending);
                AssertTrue(!pending.Succeeded && ItemInventory.GetCount("fixture") == 16
                    && !ItemInventory.IsCollected("async-retry"), "Background storage failure publishes neither half of a pickup");
            }
            pending = ItemInventory.BeginCollect("fixture", 3, "async-retry");
            ItemInventory.Reload();
            AssertTrue(pending.Finished && pending.Succeeded && ItemInventory.GetCount("fixture") == 19
                && ItemInventory.IsCollected("async-retry"), "Attempt transition drains accepted writes before reading inventory");

            const string newer = "<MoreItemsInventory version=\"999\"><Future>preserve</Future></MoreItemsInventory>";
            System.IO.File.WriteAllText(path, newer); ItemInventory.Reload();
            bool rejected = false;
            try { ItemInventory.GetCount("fixture"); } catch (InvalidOperationException) { rejected = true; }
            AssertTrue(rejected && System.IO.File.ReadAllText(path) == newer, "Unsupported inventory remains unavailable and is never downgraded");
            const string corrupt = "<MoreItemsInventory broken";
            System.IO.File.WriteAllText(path, corrupt); ItemInventory.Reload();
            rejected = false;
            try { ItemInventory.Add("fixture", 1); } catch (InvalidOperationException) { rejected = true; }
            AssertTrue(rejected && System.IO.File.ReadAllText(path) == corrupt, "Corrupt inventory does not pass through native lossy fallback");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            JumpKing.SaveThread.SaveManager.instance = oldSave; instance.SetValue(null, oldGame);
            typeof(ItemInventory).GetField("loaded", flags).SetValue(null, false);
        }
    }

    private static void WaitForCollection(ItemInventory.PendingCollection pending)
    {
        if (pending == null) throw new Exception("Pickup was not accepted");
        if (!System.Threading.SpinWait.SpinUntil(delegate { return pending.Work.IsCompleted; }, 5000))
            throw new Exception("Pickup writer did not complete");
        ItemInventory.PollCollection(pending);
        AssertTrue(pending.Finished && pending.Candidate == null, "Completed pickup releases its detached snapshot");
    }

    private static void TestThrust()
    {
        float velocity = 4f;
        float first = JetpackPhysics.ApplyThrust(
            velocity,
            Gravity,
            1,
            FullUpwardSpeed);
        AssertTrue(first < velocity, "thrust must start immediately");
        AssertTrue(
            first > velocity - Gravity * 0.1f,
            "thrust must preserve falling momentum");

        float maximum = FullUpwardSpeed
            * JetpackPhysics.MaximumUpwardSpeedScale;
        for (int frame = 1; frame <= 120; frame++)
        {
            velocity = JetpackPhysics.ApplyThrust(
                velocity,
                Gravity,
                frame,
                FullUpwardSpeed);
            AssertTrue(
                velocity >= -maximum - 0.0001f,
                "thrust exceeds its speed cap");
            velocity += Gravity;
        }
    }

    private static void TestActivation()
    {
        JetpackActivationState state = new JetpackActivationState();
        AssertTrue(
            !state.Update(true, true, false, true, true, true),
            "takeoff must not activate thrust");
        state.Update(true, true, false, false, false, false);
        AssertTrue(
            state.Update(true, true, false, true, true, false),
            "a new airborne press must activate thrust");
        AssertEqual(1, state.HeldFrames, "first thrust frame");
        AssertTrue(
            state.Update(true, true, false, true, false, false),
            "holding must sustain thrust");
        state.Stop(false);
        AssertTrue(!state.Active, "a ceiling hit must stop thrust");
        state.Stop(true);
        AssertTrue(
            !state.Update(true, true, false, true, true, false),
            "a Vanilla ceiling hit must block thrust");
        state.Update(true, false, true, false, false, false);
        AssertTrue(
            !state.BlockedUntilLanding,
            "landing must clear the ceiling lock");
    }

    private static void TestAirAnimation()
    {
        JetpackAirAnimation animation = new JetpackAirAnimation();
        AssertTrue(
            animation.Select(-1f) == JetpackAirSprite.Up,
            "ascent sprite");
        AssertTrue(
            animation.Select(1f) == JetpackAirSprite.Fall,
            "descent sprite");
    }

    private static void TestFlameAnimation()
    {
        JetpackFlameAnimation animation = new JetpackFlameAnimation();
        animation.Update(true);
        AssertTrue(animation.Visible, "flame ignition");
        for (int update = 0; update < 20; update++)
        {
            animation.Update(true);
        }
        AssertTrue(
            animation.Phase == JetpackFlamePhase.Sustained,
            "sustained flame");
        for (int update = 0; update < 20; update++)
        {
            animation.Update(false);
        }
        AssertTrue(!animation.Visible, "flame shutdown");
    }

    private static void TestSoundEnvelope()
    {
        JetpackSoundEnvelope envelope = new JetpackSoundEnvelope();
        envelope.Update(true);
        AssertTrue(envelope.Playing, "sound start");
        for (int update = 0;
            update < JetpackSoundEnvelope.FadeUpdates;
            update++)
        {
            envelope.Update(false);
        }
        AssertTrue(!envelope.Playing, "sound stop");
    }

    private static void TestAtlas()
    {
        char[] atlas = JetpackAtlasData.BuildBodyAtlas();
        AssertEqual(
            JetpackAtlasData.Width * JetpackAtlasData.Height,
            atlas.Length,
            "atlas size");
        for (int pose = 0; pose < JetpackAtlasData.RegularPoseCount; pose++)
        {
            AssertTrue(
                JetpackAtlasData.CountPixelsInPose(pose, atlas) > 40,
                "pose coverage");
        }
    }

    private static void TestPermission()
    {
        AssertTrue(HammerLevelPermission.AllowsHammer(new[] { "Unrelated", null, "AllowHammer" }),
            "AllowHammer permits native run eligibility on an author-approved map");
        foreach (string[] tags in new[] { null, new string[0], new[] { "AllowJetpack", "AllowRewinder" },
            new[] { "allowhammer", "AllowHammer ", " AllowHammer", "AllowHammerExtra" } })
            AssertTrue(!HammerLevelPermission.AllowsHammer(tags), "Hammer requires its own exact case-sensitive level tag");
        AssertTrue(
            !JumpKingJetpack.LevelPermission.AllowsJetpack(null),
            "missing tag must reject Jetpack");
        AssertTrue(
            JumpKingJetpack.LevelPermission.AllowsJetpack(new[] { "AllowJetpack" }),
            "AllowJetpack must permit Jetpack");
        AssertTrue(
            !JumpKingJetpack.LevelPermission.AllowsJetpack(
                new[] { "AllowCasualJumping" }),
            "Casual tag must not permit Jetpack");
    }

    private sealed class Form : JKRuntime.Gameplay.IPlayerForm
    {
        public bool Morphed { get { return true; } }
        public bool Attached { get { return true; } }
        public int JumpSequence { get { return 7; } }
        public object Sprite;
        public bool OwnsSprite(object sprite) { return object.ReferenceEquals(Sprite, sprite); }
    }
    private static void TestLateOptionalApiResolution()
    {
        AssertTrue(!JKRuntime.Gameplay.GameFeatures.IsMorphed, "absent form provider");
        using (JKRuntime.Gameplay.GameFeatures.RegisterForm(new Form()))
            AssertTrue(JKRuntime.Gameplay.GameFeatures.IsMorphed && JKRuntime.Gameplay.GameFeatures.FormJumpSequence == 7, "late typed form provider");
        AssertTrue(!JKRuntime.Gameplay.GameFeatures.IsMorphed, "form provider cleanup");
    }

    private static void TestMorphSpriteOwnership()
    {
        object sprite = new object();
        using (JKRuntime.Gameplay.GameFeatures.RegisterForm(new Form { Sprite = sprite }))
        {
            AssertTrue(JKRuntime.Gameplay.GameFeatures.FormOwnsSprite(sprite), "form keeps visual ownership");
            AssertTrue(!JKRuntime.Gameplay.GameFeatures.FormOwnsSprite(new object()), "ordinary sprites remain available to Jetpack");
        }
    }

    private static void TestPersistentInventoryModel()
    {
        ConsumableSaveDatabase database =
            new ConsumableSaveDatabase().GetDefault();
        AssertEqual(1, database.Version,
            "More Items inventory starts at its first release format");
        AssertEqual(0, database.Saves.Length,
            "new inventory database is empty");
    }

    private static void TestNewRunPickupReset()
    {
        ConsumableSave world = new ConsumableSave
        {
            Key = "level:example",
            Collected = new[] { "pickup-a" }
        };
        AssertEqual(1, ItemInventory.GetCollectedForRun(world, false).Length,
            "continued run keeps pickup state");
        AssertEqual(0, ItemInventory.GetCollectedForRun(world, true).Length,
            "new run respawns world pickups");
    }

    private static void TestItemModuleRegistry()
    {
        bool enabled = true;
        int installs = 0;
        int uninstalls = 0;
        const string lifecycleId = "test-lifecycle";
        MoreItemsApi.RegisterModule(new ItemModuleDefinition(
            lifecycleId,
            "Lifecycle Test",
            delegate { return enabled; },
            delegate(bool value) { enabled = value; },
            null,
            delegate { installs++; },
            delegate { uninstalls++; }));
        ItemModuleRegistry.StartRuntime();
        AssertEqual(1, installs,
            "enabled item module installs with More Items runtime");
        enabled = false;
        ItemModuleRegistry.RefreshRuntime(lifecycleId);
        AssertEqual(1, uninstalls,
            "disabled item module uninstalls immediately");
        enabled = true;
        ItemModuleRegistry.RefreshRuntime(lifecycleId);
        AssertEqual(2, installs,
            "re-enabled item module reinstalls immediately");
        ItemModuleRegistry.StopRuntime();
        AssertEqual(2, uninstalls,
            "More Items shutdown uninstalls active item modules");
        MoreItemsApi.UnregisterModule(lifecycleId);

        RewinderDefinition.RegisterModule();
        JetpackDefinition.RegisterModule();
        HammerDefinition.RegisterModule();
        ItemModuleDefinition hammer;
        AssertTrue(MoreItemsApi.TryGetModule(HammerDefinition.Id, out hammer) && hammer.AddSettingsItems == null,
            "Hammer registers as equipment with force controls in Debug Actions, not an Items Settings card");
        ItemModuleDefinition jetpack;
        AssertTrue(MoreItemsApi.TryGetModule(JetpackDefinition.Id, out jetpack),
            "jetpack module is registered");
        AssertTrue(jetpack.AddSettingsItems != null,
            "jetpack module owns its settings card contents");
        ItemModuleDefinition rewinder;
        AssertTrue(MoreItemsApi.TryGetModule(RewinderDefinition.Id, out rewinder),
            "rewinder module is registered");
        AssertTrue(rewinder.AddSettingsItems != null,
            "rewinder module owns its render setting");
        AssertTrue(new MoreItemsSettings().RenderRewind,
            "rewind rendering defaults to enabled");
    }

    private static void TestEquipmentContract()
    {
        ConsumableDefinition equipment = new ConsumableDefinition(
            "test-equipment",
            "Test Equipment",
            string.Empty,
            delegate { return true; },
            null,
            Microsoft.Xna.Framework.Color.White,
            null,
            null,
            null,
            false,
            null,
            delegate { return true; },
            delegate { return false; },
            delegate(bool value) { return value; });
        AssertTrue(equipment.IsEquipment,
            "More Items API exposes persistent equipment explicitly");
        AssertTrue(!equipment.IsUsable,
            "equipment is not represented as an item-use callback");
    }

    private static Type DefineType(string typeName)
    {
        AssemblyName name = new AssemblyName(
            "OptionalApiTest" + Guid.NewGuid().ToString("N"));
        AssemblyBuilder assembly = AppDomain.CurrentDomain
            .DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule(name.Name);
        return module.DefineType(
            typeName,
            TypeAttributes.Public).CreateType();
    }

    private static void AssertEqual(int expected, int actual, string name)
    {
        if (expected != actual)
        {
            failures++;
            Console.Error.WriteLine(
                name + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
        {
            failures++;
            Console.Error.WriteLine(name);
        }
    }
}

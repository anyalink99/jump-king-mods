using BehaviorTree;
using EntityComponent.BT;
using EntityComponent;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.Player;
using JumpKing.SaveThread.SaveComponents;
using JumpKing.SaveThread;
using JumpKing;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System;
namespace JKRuntime.Inspection
{
    internal static partial class Tests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static GimmickAttempt preparedInspector;
        private static void PrepareInspector(RuntimeScope scope) { preparedInspector = scope.Own(GimmickAttempt.Prepare(false)); scope.Defer(() => preparedInspector = null); }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static LevelScreen[] Scene(IBlock[] blocks)
        { return new[] { new LevelScreen(0, blocks, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) }; }
        private static int Main(string[] args)
        {
            try {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
                    string path = Path.Combine(args[0], new AssemblyName(request.Name).Name + ".dll");
                    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
                };
                Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"));
                Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnknownProvider.dll"));
                InspectorSettings.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inspector-test-" + Guid.NewGuid().ToString("N") + ".xml"));
                if (args.Length > 1 && args[1] == "slopes") { SlopeLoadingRegression(args[0], args[2], args[3]); return 0; }
                if (args.Length > 1 && args[1] == "construction-audit") return InstalledConstruction(args[0]);
                InspectorSettingsRegression();
                ObservationRegression();
                GimmickRegression(args[0]);
                if (args.Length > 1 && args[1] == "graphics") GimmickGraphics(args[0]);
                Console.WriteLine("[OK] Runtime Mod Inspector regression checks"); return 0;
            } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private sealed class SpriteGameFixture : IDisposable
        {
            private readonly FieldInfo instance=typeof(Game1).GetField("_instance",Flags);
            private readonly object previous;
            internal SpriteGameFixture()
            {
                previous=instance.GetValue(null);
                var game=(Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Game1));
                game.contentManager=(JKContentManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JKContentManager));
                game.contentManager.playerSprites=new JKContentManager.PlayerSprites { _CurrentSprites=new JumpKing.JKMemory.LayeredKingSprites(null) };
                game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
                instance.SetValue(null,game);
            }
            public void Dispose() { instance.SetValue(null,previous); }
        }
        private sealed class WalkInputFixture : IDisposable
        {
            private readonly ControllerManager previous = ControllerManager.instance;
            internal readonly PadInstance Pad;
            private readonly Dictionary<string, object> cache;
            private readonly Dictionary<string, object> oldCache;
            private readonly string folder;
            private readonly System.Reflection.FieldInfo savedLoopField, combinedField, modifierCountField;
            private readonly object savedLoop, savedCombined, savedModifierCount;
            private readonly bool achievementsDisabled;
            private readonly System.Reflection.PropertyInfo achievementsProperty;
            internal WalkInputFixture()
            {
                var manager = (ControllerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ControllerManager));
                Pad = (PadInstance)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PadInstance));
                typeof(ControllerManager).GetField("m_pads", Flags).SetValue(manager, new List<PadInstance> { Pad });
                ControllerManager.instance = manager;
                var save = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
                cache = (Dictionary<string, object>)save.GetField("loaded_objects", Flags).GetValue(null);
                oldCache = new Dictionary<string, object>(cache);
                folder = (string)save.GetField("PERMANENT_FOLDER", Flags).GetValue(null);
                savedLoopField = typeof(JumpKing.GameManager.GameLoop).GetField("_instance", Flags);
                savedLoop = savedLoopField.GetValue(null);
                var loop = new JumpKing.GameManager.GameLoop();
                var completion = loop.GetType().GetField("m_ending_body_modifiers", Flags);
                completion.SetValue(loop, Activator.CreateInstance(completion.FieldType, true));
                combinedField = save.GetField("_COMBINED_SAVE", Flags);
                savedCombined = combinedField.GetValue(null);
                combinedField.SetValue(null, new CombinedSaveFile { full_run = new SaveCompCushion<FullRunSave> { initialized = true } });
                modifierCountField = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.FullRunManager").GetField("modifiers_count", Flags);
                savedModifierCount = modifierCountField.GetValue(null);
                achievementsProperty = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementRegister").GetProperty("AchievementsDisabled", Flags);
                achievementsDisabled = (bool)achievementsProperty.GetValue(null, null);
                // in-memory inventory only: never call native save setters
                cache[folder + "general_settings.set"] = new GeneralSettings().GetDefault();
                Snake(false);
            }
            internal void Snake(bool enabled)
            {
                var inventory = new Inventory().GetDefault();
                if (enabled) inventory.items.Add(new InventoryItem { item = Items.SnakeRing, count = 1 });
                cache[folder + "inventory.inv"] = inventory;
            }
            internal void Direction(int direction)
            { Buttons(direction < 0, direction > 0); }
            internal void Buttons(bool left, bool right)
            {
                var current = (PadState)typeof(PadInstance).GetField("current_state", Flags).GetValue(Pad);
                typeof(PadInstance).GetField("last_state", Flags).SetValue(Pad, current);
                typeof(PadInstance).GetField("current_state", Flags).SetValue(Pad, new PadState { left = left, right = right });
            }
            public void Dispose()
            {
                ControllerManager.instance = previous;
                cache.Clear(); foreach (var pair in oldCache) cache.Add(pair.Key, pair.Value);
                savedLoopField.SetValue(null, savedLoop); combinedField.SetValue(null, savedCombined);
                modifierCountField.SetValue(null, savedModifierCount);
                achievementsProperty.SetValue(null, achievementsDisabled, null);
            }
        }
        private sealed class ResumeTestPlayer : PlayerEntity
        {
            // only omit disk-save bookkeeping. Entity.UpdateComponents itself
            // and all component dispatch remain the installed game's methods
            protected override void Update(float delta) { }
            protected override void OnDestroy() { }
        }
        private static PlayerEntity ResumePlayer()
        {
            var player=(PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ResumeTestPlayer));
            typeof(Entity).GetField("m_components",Flags).SetValue(player,new List<Component>());
            player.m_body=new BodyComp(new Vector2(180,294),18,26);
            var ground=new IsOnGround(player);
            typeof(PlayerEntity).GetField("m_is_on_ground_state",Flags).SetValue(player,ground);
            var root=new BTsequencor(); root.AddChild(ground); root.AddChild(new Walk(player));
            var brain=new BehaviorTreeComp(root);
            typeof(PlayerEntity).GetField("m_bt",Flags).SetValue(player,brain);
            // the installed PlayerEntity.SetComponents order, not manual Advance calls
            player.AddComponents(player.m_body,new InputComponent(),brain);
            player.SetSprite(Game1.instance.contentManager.playerSprites.jump_charge);
            return player;
        }
    }
}

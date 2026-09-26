using System;
using System.Collections.Generic;
using System.Linq;
using EntityComponent;
using JKRuntime.Gameplay;
using JKRuntime.State;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed class GimmickSession : Component, IDisposable, IStateParticipant
    {
        private readonly PlayerEntity player;
        private readonly ComponentAttachment attachment;
        private readonly LevelScreen[] screens;
        private IBlock[][] original;
        private IBlock[][] installed;
        private GimmickRule[] active = new GimmickRule[0];
        private readonly Dictionary<string, StateOverride> states = new Dictionary<string, StateOverride>();
        private readonly Dictionary<Type, GimmickHandlers.Lease> handlers = new Dictionary<Type, GimmickHandlers.Lease>();
        private readonly List<GimmickWind> winds = new List<GimmickWind>();
        private bool started, disposed, marked, catalogueReady;
        private long generation;
        private readonly Marker marker = new Marker();
        private readonly GimmickAttempt preparation;
        internal bool IsEnabled(string id) { return active.Any(r => r.Id == id && r.Enabled); }
        internal string WindStatus(string id)
        { var rule = active.FirstOrDefault(r => r.Id == id && r.Enabled); return rule == null ? "Override is disabled" : GimmickWind.Status(screens, player, rule); }
        private sealed class Marker : IBodyCompBehaviour { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        internal GimmickSession(PlayerEntity value, GimmickAttempt prepared = null)
        {
            player = value; preparation = prepared;
            screens = (LevelScreen[])GimmickBlocks.Screens.GetValue(null);
            attachment = new ComponentAttachment(player, this);
            try
            {
                var components = (List<Component>)typeof(Entity).GetField("m_components", Gimmicks.Members).GetValue(player);
                components.Remove(this); components.Insert(components.IndexOf(player.m_body), this);
            }
            catch { attachment.Dispose(); throw; }
        }
        internal void RefreshStates()
        {
            if (states.Count != 0) throw new InvalidOperationException("Release state overrides before refreshing live state paths.");
            using (JKRuntime.RuntimeApi.MeasureStartup("mega-gameplay.browse-live-states")) GimmickStates.Discover(player);
            catalogueReady = true;
            GimmickMenu.RefreshAll();
        }
        internal void EnsureStates()
        {
            if (catalogueReady) return;
            // The first explicit browser visit can fill the remaining catalogue
            // while retaining the exact slots already leased by saved overrides.
            var retained = active.Select(r => r.Id).Where(id => Gimmicks.Entries.ContainsKey(id))
                .Select(id => Gimmicks.Entries[id]).Where(e => e.Slot != null).ToArray();
            using (JKRuntime.RuntimeApi.MeasureStartup("mega-gameplay.browse-live-states")) GimmickStates.Discover(player);
            foreach (var entry in retained) Gimmicks.Add(entry);
            catalogueReady = true; GimmickMenu.RefreshAll();
        }
        private void ActivatePrepared()
        {
            using (JKRuntime.RuntimeApi.MeasureStartup("mega-gameplay.bind-saved-overrides"))
            try
            {
                if (preparation.Disposed || !preparation.MatchesSettings()) throw new InvalidOperationException("Prepared settings changed; reopen the library to apply the current configuration.");
                if (preparation.Error != null) throw new InvalidOperationException(preparation.Error);
                var ids = new HashSet<string>(preparation.Rules.Where(r => r.Id.StartsWith("state:", StringComparison.Ordinal)).Select(r => r.Id));
                if (ids.Count != 0) GimmickStates.Discover(player, ids, preparation.StateTypes);
                Apply(preparation.Rules);
            }
            catch (Exception error) { Gimmicks.Status = "Saved overrides not applied: " + error.GetBaseException().Message; }
        }
        internal void Activate()
        {
            if (started || disposed) return;
            started = true;
            if (preparation != null && preparation.Rules.Length != 0) ActivatePrepared();
        }
        protected override void Update(float delta)
        {
            if (disposed) return;
            Activate();
            foreach (var rule in active)
            {
                GimmickEntry entry;
                if (!rule.Enabled || !Gimmicks.Entries.TryGetValue(rule.Id, out entry) || entry.Slot == null) continue;
                bool inScope = entry.Slot.ScreenSet || entry.Slot.ScreenDictionary || entry.Slot.ScreenTargets != null || InRange(rule, Camera.CurrentScreen);
                StateOverride state;
                try
                {
                    if (!inScope) { if (states.TryGetValue(rule.Id, out state)) { state.Dispose(); states.Remove(rule.Id); } continue; }
                    if (!states.TryGetValue(rule.Id, out state)) states.Add(rule.Id, state = new StateOverride(entry.Slot, rule, screens.Length));
                    state.Tick(); Mark();
                }
                catch (Exception error)
                {
                    rule.Enabled = false;
                    if (states.TryGetValue(rule.Id, out state))
                    { try { state.Dispose(); states.Remove(rule.Id); } catch { } }
                    Gimmicks.Status = entry.Label + ": " + error.GetBaseException().Message;
                }
            }
            foreach (var wind in winds) wind.Tick();
        }
        internal static bool InRange(GimmickRule rule, int screen)
        { return (rule.Screens == null || rule.Screens.Contains(screen + 1)) && (rule.FirstScreen == 0 || screen + 1 >= rule.FirstScreen) && (rule.LastScreen == 0 || screen + 1 <= rule.LastScreen); }
        internal static bool Select(IBlock block, GimmickApplication application)
        {
            if (application == GimmickApplication.ExistingBlocks) return true;
            if (application == GimmickApplication.OrdinarySolid || application == GimmickApplication.Surface) return block.GetType() == typeof(BoxBlock);
            if (application == GimmickApplication.ReplaceMedium) return GimmickClassification.Blocking(block) == false;
            Rectangle overlap;
            return block.Intersects(block.GetRect(), out overlap) == BlockCollisionType.Collision_Blocking;
        }
        internal static IBlock[] Build(IBlock[] source, int screen, GimmickEntry entry, GimmickRule rule)
        {
            if (!InRange(rule, screen)) return source;
            if (entry.Template == null) throw new InvalidOperationException(entry.Error ?? "No portable block instance");
            if (rule.Application == GimmickApplication.Auto) { rule = Gimmicks.Copy(rule); rule.Application = GimmickClassification.Resolve(entry, rule); }
            if (rule.Application == GimmickApplication.Surface && (entry.Template is SlopeBlock || GimmickClassification.Blocking(entry.Template) != true))
                throw new InvalidOperationException("Surface replacement requires confirmed blocking geometry. Select an explicit volume placement for contact zones.");
            if (rule.Application == GimmickApplication.FillEmpty) return GimmickSpace.Fill(source, screen, entry.Template);
            if (rule.Application == GimmickApplication.FillSpace)
                return new[] { GimmickBlocks.Copy(entry.Template, new Rectangle(0, -360 * screen, 480, 360)) };
            var result = new List<IBlock>();
            if (rule.Application == GimmickApplication.Overlay)
            {
                IBlock overlay = GimmickBlocks.Copy(entry.Template, new Rectangle(0, -360 * screen, 480, 360)); Rectangle overlap;
                if (overlay.Intersects(overlay.GetRect(), out overlap) == BlockCollisionType.Collision_Blocking)
                    throw new InvalidOperationException("Overlay requires a nonblocking material; use a replacement mode for terrain.");
                result.AddRange(source); result.Add(overlay); return result.ToArray();
            }
            foreach (IBlock block in source)
            {
                if (!Select(block, rule.Application)) { result.Add(block); continue; }
                if (block is SlopeBlock && !(entry.Template is SlopeBlock) && !rule.ConvertSlopes)
                    throw new InvalidOperationException("This replacement converts slopes to rectangles. Enable Convert slopes explicitly or use Ordinary solid.");
                result.Add(GimmickBlocks.Copy(entry.Template, block.GetRect()));
            }
            return result.ToArray();
        }
        internal void Apply(GimmickRule[] rules)
        {
            if (disposed) throw new ObjectDisposedException("Gimmick session");
            // A no-op must not enumerate geometry, query every mechanic or call
            // foreign getters. In particular, empty startup owns no world arrays.
            if (active.Length == 0 && !rules.Any(r => r != null && r.Enabled)) return;
            if (!ReferenceEquals(GimmickBlocks.Screens.GetValue(null), screens)) throw new InvalidOperationException("The loaded world changed");
            if (original == null) { original = screens.Select(s => (IBlock[])GimmickBlocks.Hitboxes.GetValue(s)).ToArray(); installed = original; }
            if (JKRuntime.RuntimeApi.Mechanics.Inspect().Any(m => m.Id == "mega.warp" && m.State != null && m.State.Active))
                throw new InvalidOperationException("Finish the current Warp transition before changing overrides.");
            var next = rules.Where(r => r != null && r.Enabled).Select(Gimmicks.Copy).ToArray();
            bool usePreparedGeometry = active.Length == 0 && preparation != null && preparation.Geometry != null && preparation.Matches(next);
            if (next.Select(r => r.Id).Distinct().Count() != next.Length) throw new InvalidOperationException("Duplicate override ID");
            var replacements = new GimmickEntry[screens.Length];
            var replacementRules = new GimmickRule[screens.Length];
            var overlays = new List<Tuple<GimmickEntry, GimmickRule>>();
            var windRules = new List<GimmickRule>();
            foreach (var rule in next)
            {
                if (!Enum.IsDefined(typeof(GimmickApplication), rule.Application) || rule.FirstScreen < 0 || rule.LastScreen < 0
                    || (rule.LastScreen != 0 && rule.LastScreen < rule.FirstScreen)) throw new InvalidOperationException("Invalid saved application mode/range");
                GimmickEntry entry;
                if (!Gimmicks.Entries.TryGetValue(rule.Id, out entry)) throw new InvalidOperationException("Provider is unavailable: " + rule.Id);
                if (!Enumerable.Range(0, screens.Length).Any(i => InRange(rule, i))) throw new InvalidOperationException("The enabled configuration has no target screens in this map.");
                if (entry.Kind == "Wind") {
                    GimmickWind.Validate(rule);
                    if (windRules.Any(other => Enumerable.Range(0, screens.Length).Any(s => InRange(rule, s) && InRange(other, s)))) throw new InvalidOperationException("Wind configurations overlap. Use separate target screens.");
                    windRules.Add(rule); continue;
                }
                if (entry.Slot != null)
                {
                    entry.Slot.RequireWritable();
                    if (rule.Contract != entry.Slot.Contract) throw new InvalidOperationException("State provider changed. Re-enable this entry to accept its current definition: " + entry.Label);
                    entry.Slot.Parse(rule.Value); continue;
                }
                if (entry.Kind != "Block") continue;
                GimmickRecipes.Prepare(entry);
                if (entry.Template == null) throw new InvalidOperationException(entry.Error ?? "No block template");
                rule.Application = GimmickClassification.Resolve(entry, rule);
                if (rule.Application == GimmickApplication.Overlay || rule.Application == GimmickApplication.FillEmpty) { overlays.Add(Tuple.Create(entry, rule)); continue; }
                for (int screen = 0; screen < screens.Length; screen++) if (InRange(rule, screen))
                {
                    if (replacements[screen] != null) throw new InvalidOperationException("Two material replacements overlap. Disable one or use separate screen ranges.");
                    replacements[screen] = entry; replacementRules[screen] = rule;
                }
            }
            var prepared = new IBlock[screens.Length][];
            foreach (var rule in next) {
                var slot = Gimmicks.Entries[rule.Id].Slot;
                if (slot != null && GimmickWind.Owns(slot.Field) && windRules.Any(w => Enumerable.Range(0, screens.Length).Any(i => InRange(w, i) && InRange(rule, i))))
                    throw new InvalidOperationException("Wind and an advanced native wind state overlap. Keep one owner per target screen.");
            }
            for (int i = 0; i < screens.Length; i++)
            {
                if (!ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[i]), installed[i]))
                    throw new InvalidOperationException("Another mod replaced collision geometry; release overrides and reload the map.");
                if (usePreparedGeometry) {
                    if (!ReferenceEquals(original[i], preparation.Original[i])) throw new InvalidOperationException("Geometry changed after preparation; reopen the library to rebuild explicitly.");
                    prepared[i] = preparation.Geometry[i];
                } else {
                    prepared[i] = replacements[i] == null ? original[i] : Build(original[i], i, replacements[i], replacementRules[i]);
                    foreach (var overlay in overlays) prepared[i] = Build(prepared[i], i, overlay.Item1, overlay.Item2);
                }
                if (prepared[i].Length > 32768) throw new InvalidOperationException("Too many resulting collision blocks");
                if (!ReferenceEquals(prepared[i], original[i]) && Math.Abs(i - Camera.CurrentScreen) <= 1)
                {
                    Rectangle box = player.m_body.GetHitbox(), overlap;
                    var authored = new HashSet<IBlock>(original[i]);
                    foreach (var block in prepared[i])
                        if (!authored.Contains(block) && block.Intersects(box, out overlap) == BlockCollisionType.Collision_Blocking)
                            throw new InvalidOperationException("The new terrain intersects the player. Move clear of the selected area first.");
                }
            }
            // Preparation above cannot mutate the live world. Release old state
            // leases before committing new geometry; retained arrays preserve identity.
            if(prepared.Any(s=>s.Any(b=>b is NoWalkOffSurfaceBlock || b is NoWalkOffZoneBlock))) ModEntry.EnsureMotion();
            var needed = new HashSet<Type>(next.Where(r => Gimmicks.Entries[r.Id].Kind == "Block").Select(r => Gimmicks.Entries[r.Id].Template.GetType()));
            var acquired = new Dictionary<Type, GimmickHandlers.Lease>();
            try
            {
                foreach (var type in needed) if (!handlers.ContainsKey(type))
                {
                    try {
                        var lease = GimmickHandlers.Acquire(player, type); if (lease != null) acquired.Add(type, lease);
                        foreach (var entry in Gimmicks.Entries.Values.Where(e => e.Template != null && e.Template.GetType() == type))
                            if (entry.Error != null && entry.Error.StartsWith("Cannot activate: ", StringComparison.Ordinal)) entry.Error = null;
                    }
                    catch (Exception error) {
                        foreach (var entry in Gimmicks.Entries.Values.Where(e => e.Template != null && e.Template.GetType() == type))
                            entry.Error = "Cannot activate: " + error.GetBaseException().Message;
                        throw;
                    }
                }
                ReleaseStates();
            }
            catch { foreach (var lease in acquired.Values) lease.Dispose(); throw; }
            foreach (var wind in winds) wind.Dispose(); winds.Clear();
            try { foreach (var rule in windRules) winds.Add(new GimmickWind(screens, player, rule)); }
            catch { foreach (var lease in acquired.Values) lease.Dispose(); foreach (var wind in winds) wind.Dispose(); winds.Clear(); foreach (var old in active.Where(r => Gimmicks.Entries[r.Id].Kind == "Wind")) winds.Add(new GimmickWind(screens, player, old)); throw; }
            foreach (var pair in acquired) handlers.Add(pair.Key, pair.Value);
            if (next.Any(r => Gimmicks.Entries[r.Id].Kind == "Block")) Mark();
            for (int i = 0; i < screens.Length; i++) GimmickBlocks.Hitboxes.SetValue(screens[i], prepared[i]);
            installed = prepared; active = next; generation++;
            if (windRules.Count != 0) Mark();
            foreach (var type in handlers.Keys.Where(t => !needed.Contains(t)).ToArray()) { handlers[type].Dispose(); handlers.Remove(type); }
            if (next.Length == 0 && marked) { RunModifiers.Remove(player.m_body, marker); marked = false; }
            Gimmicks.Status = next.Length + " override(s) active. Disable to restore authored geometry/state.";
        }
        private void Mark() { if (!marked) { RunModifiers.Register(player.m_body, marker); marked = true; } }
        internal static IBlock[][] CompileGeometry(IBlock[][] original, GimmickRule[] rules)
        {
            var result = (IBlock[][])original.Clone(); var replaced = new bool[result.Length];
            var selected = new List<Tuple<GimmickEntry, GimmickRule>>();
            foreach (var value in rules.Where(r => r.Enabled)) {
                GimmickEntry entry; if (!Gimmicks.Entries.TryGetValue(value.Id, out entry) || entry.Kind != "Block") continue;
                GimmickRecipes.Prepare(entry); var rule = Gimmicks.Copy(value); rule.Application = GimmickClassification.Resolve(entry, rule);
                selected.Add(Tuple.Create(entry, rule));
            }
            foreach (var item in selected.OrderBy(p => p.Item2.Application == GimmickApplication.Overlay || p.Item2.Application == GimmickApplication.FillEmpty ? 1 : 0))
                for (int i = 0; i < result.Length; i++) if (InRange(item.Item2, i)) {
                    bool layer = item.Item2.Application == GimmickApplication.Overlay || item.Item2.Application == GimmickApplication.FillEmpty;
                    if (!layer && replaced[i]) throw new InvalidOperationException("Two material replacements overlap.");
                    if (!layer) replaced[i] = true;
                    result[i] = Build(result[i], i, item.Item1, item.Item2);
                    if (result[i].Length > 32768) throw new InvalidOperationException("Too many resulting collision blocks.");
                }
            return result;
        }
        internal IBlock[][] Preview(GimmickRule rule)
        {
            var baseline = original ?? screens.Select(s => (IBlock[])GimmickBlocks.Hitboxes.GetValue(s)).ToArray();
            var rules = (Settings.Current.GimmickRules ?? new GimmickRule[0]).Where(r => r.Id != rule.Id).Concat(new[] { rule }).ToArray();
            return CompileGeometry(baseline, rules);
        }
        private void ReleaseStates()
        {
            var errors = new List<Exception>();
            foreach (string id in states.Keys.ToArray())
                try { states[id].Dispose(); states.Remove(id); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("State overrides remain pending", errors);
        }
        public string Id { get { return "mega.gimmick-configuration"; } }
        public int Version { get { return 1; } }
        public object Capture() { return generation; }
        public void Validate(object snapshot)
        { if (!(snapshot is long) || (long)snapshot != generation) throw new InvalidOperationException("Gimmick configuration changed since this snapshot"); }
        public void Restore(object snapshot) { Validate(snapshot); }
        public void Dispose()
        {
            if (disposed) return;
            Enabled = false;
            var release = new JKRuntime.RuntimeScope();
            release.Defer(ReleaseStates);
            foreach (var value in winds.ToArray()) { var wind = value; release.Defer(delegate { wind.Dispose(); winds.Remove(wind); }); }
            foreach (var key in handlers.Keys.ToArray()) { var type = key; release.Defer(delegate { handlers[type].Dispose(); handlers.Remove(type); }); }
            if (installed != null && ReferenceEquals(GimmickBlocks.Screens.GetValue(null), screens))
                for (int i = 0; i < screens.Length; i++) {
                    int index = i;
                    release.Defer(delegate { if (ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[index]), installed[index])) GimmickBlocks.Hitboxes.SetValue(screens[index], original[index]); });
                }
            release.Defer(delegate { if (marked) { RunModifiers.Remove(player.m_body, marker); marked = false; } });
            release.Own(attachment);
            release.Dispose(); disposed = true;
            if (ReferenceEquals(Gimmicks.Session, this)) Gimmicks.Session = null;
            Gimmicks.Status = "Load a level to inspect live states and apply blocks.";
            foreach (string id in Gimmicks.Entries.Where(p => p.Value.Slot != null).Select(p => p.Key).ToArray()) Gimmicks.Entries.Remove(id);
            Gimmicks.Generation++;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.MiscEntities.OldMan;
using JumpKing.Util.DrawBT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal static class NativeNarrative
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static OwnedPatches patches;
        private static SceneHost owner;
        private sealed class Actor { internal NativeActorData Data; internal ISpriteEntity Entity; internal OldManSettings Settings; internal Sprite DrawSprite; internal int Drawing; internal string[] QuoteLines; }
        private static readonly Dictionary<Entity, Actor> actors = new Dictionary<Entity, Actor>();
        private static readonly Dictionary<string, Actor> actorIds = new Dictionary<string, Actor>();
        private sealed class Page { internal string[] Lines; internal Color Color, Background; }
        private static Page[] resultPages = new Page[0];
        private static int resultPage;
        private static float pageTime;
        internal static void ValidateResources(SceneHost host)
        {
            if (host.SceneData.NativeActors.Length + host.SceneData.IntroPages.Length + host.SceneData.ResultPages.Length == 0) return;
            var content = Game1.instance.contentManager;
            foreach (NativeActorData actor in host.SceneData.NativeActors)
                if (!(actor.Kind == "oldman" ? content.oldMan.spawn_names : content.oldMan.merchant_names).Contains(actor.Name))
                    throw new System.IO.InvalidDataException(actor.Id + ": native " + actor.Kind + " named '" + actor.Name + "' was not loaded");
            if (host.SceneData.IntroPages.Length + host.SceneData.ResultPages.Length == 0) return;
            var font = SceneTextService.Font("menu");
            foreach (IntroPage page in host.SceneData.IntroPages)
                CheckPage(SceneTextService.Layout(font, host.Narrative.Initial(page.String), 440), font);
            foreach (ResultPage page in host.SceneData.ResultPages)
            {
                var lines = new List<string>(SceneTextService.Layout(font, host.Narrative.Initial(page.Title), 440));
                foreach (ResultRow row in page.Rows) lines.AddRange(SceneTextService.Layout(font, host.Narrative.Initial(row.String) + (row.Counter == null ? "" : ": -2147483648"), 440));
                CheckPage(lines.ToArray(), font);
            }
        }
        private static void CheckPage(string[] lines, SpriteFont font)
        { if (lines.Length * font.LineSpacing > 300) throw new System.IO.InvalidDataException("Narrative page exceeds 300 px; split it into pages"); }
        internal static void Prepare(SceneHost host)
        {
            Release(); owner = host;
            if (host == null) return;
            var scene = host.SceneData;
            if (scene.NativeActors.Length + scene.IntroPages.Length + scene.ResultPages.Length == 0) return;
            patches = new OwnedPatches("mega-mapping-expansion.narrative");
            try
            {
                if (scene.IntroPages.Length > 0) Add(typeof(IntroState).GetMethod("MakeLegendHasItText", All), "Intro", null);
                if (scene.ResultPages.Length > 0)
                {
                    Add(typeof(StatsScreen).GetMethod("OnNewRun", All), null, "BeginResults");
                    Add(typeof(StatsScreen).GetMethod("MyRun", All), null, "AdvanceResults");
                    // Draw is a tiny wrapper that JumpGame.Draw can inline before
                    // map preparation installs adapters. DrawStats contains the
                    // native text loop and remains a callable boundary.
                    Add(typeof(StatsScreen).GetMethod("DrawStats", All), "DrawResults", null);
                }
                if (scene.NativeActors.Length > 0)
                {
                    foreach (string kind in scene.NativeActors.Select(a => a.Kind).Distinct())
                    {
                        Type type = ActorType(kind);
                        Add(type.GetMethod("Draw", All), "BeforeActorDraw", null, "AfterActorDraw");
                        Add(type.GetMethod("DrawText", All, null, Type.EmptyTypes, null), "BeforeActorText", null, "AfterActorText");
                    }
                    Add(typeof(FetchQuote).GetMethod("MyRun", All), "FetchQuote", "QuoteStarted");
                    Add(typeof(TargetLine).GetMethod("MyRun", All), "CustomLine", "QuoteEnded");
                    Add(typeof(SayLine).GetMethod("Reset", All), null, "LineStarted");
                    Add(typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.OldMan.OldManComp", true).GetMethod("HasQuotes", All), null, "HasQuotes");
                }
            }
            catch { Release(); throw; }
        }
        private static Type ActorType(string kind)
        { return typeof(Game1).Assembly.GetType(kind == "oldman" ? "JumpKing.MiscEntities.OldManEntity" : "JumpKing.MiscEntities.Merchant.MerchantEntity", true); }
        private static void Add(MethodBase target, string prefix, string postfix, string finalizer = null)
        { if (target == null) throw new MissingMethodException("Unsupported native narrative contract"); patches.Add(target, prefix == null ? null : typeof(NativeNarrative).GetMethod(prefix, All), postfix == null ? null : typeof(NativeNarrative).GetMethod(postfix, All), finalizer: finalizer == null ? null : typeof(NativeNarrative).GetMethod(finalizer, All)); }
        internal static void Activate(SceneHost host)
        {
            if (owner != host) return;
            actors.Clear(); actorIds.Clear();
            if (host.SceneData.NativeActors.Length == 0) return;
            foreach (ISpriteEntity entity in EntityManager.instance.Entities.OfType<ISpriteEntity>())
                foreach (NativeActorData data in host.SceneData.NativeActors)
                {
                    if (entity.GetType() != ActorType(data.Kind)) continue;
                    object settings = entity.GetType().GetField("m_settings", All).GetValue(entity);
                    OldManSettings native = data.Kind == "oldman" ? (OldManSettings)settings : (OldManSettings)settings.GetType().GetField("settings").GetValue(settings);
                    if (native.name != data.Name) continue;
                    var actor = new Actor { Entity = entity, Data = data, Settings = native }; actors.Add(entity, actor); actorIds.Add(data.Id, actor);
                }
            foreach (NativeActorData data in host.SceneData.NativeActors)
            {
                if (!actorIds.ContainsKey(data.Id)) throw new InvalidOperationException("Native actor was not constructed: " + data.Name);
                if (data.Quotes.Length == 0) continue;
                var font = (SpriteFont)ActorType("oldman").GetMethod("GetOldManFont", All).Invoke(null, new object[] { actorIds[data.Id].Settings.font });
                foreach (ActorQuote quote in data.Quotes) SceneTextService.Layout(font, host.Narrative.Initial(quote.String), 440);
            }
        }
        private static bool Active { get { return owner != null && owner == SceneHost.Current && MappingSettings.Enabled; } }
        private static bool Bound(Entity entity, out Actor actor) { actor = null; return Active && actors.TryGetValue(entity, out actor) && entity.IsAlive; }
        private struct DrawState { internal ISpriteEntity Entity; internal Sprite Sprite; internal Vector2 Position; internal Actor Actor; }
        private static void BeforeActorDraw(ISpriteEntity __instance, out DrawState __state)
        {
            __state = new DrawState(); Actor actor; if (!Bound(__instance, out actor)) return;
            __state = new DrawState { Entity = __instance, Sprite = __instance.sprite, Position = __instance.Position, Actor = actor };
            if (actor.DrawSprite == null) actor.DrawSprite = Sprite.CreateSpriteWithCenter(__instance.sprite.texture, __instance.sprite.source, __instance.sprite.center);
            Sprite sprite = actor.DrawSprite;
            sprite.texture = __instance.sprite.texture; sprite.source = __instance.sprite.source; sprite.center = __instance.sprite.center;
            sprite.SetColor(__instance.sprite.GetColor() * (actor.Data.Visible ? actor.Data.Opacity : 0));
            Color tint = owner.NarrativeColor(actor.Data.Tint);
            sprite.SetColor(new Color(sprite.GetColor().ToVector4() * tint.ToVector4()));
            __instance.SetSprite(sprite); __instance.Position += new Vector2(actor.Data.OffsetX, actor.Data.OffsetY); actor.Drawing++;
        }
        private static void AfterActorDraw(DrawState __state)
        { if (__state.Entity != null) { __state.Entity.SetSprite(__state.Sprite); __state.Entity.Position = __state.Position; if (__state.Actor.Drawing > 0) __state.Actor.Drawing--; } }
        private static bool BeforeActorText(ISpriteEntity __instance, out DrawState __state)
        {
            __state = new DrawState(); Actor actor; if (!Bound(__instance, out actor)) return true;
            if (!actor.Data.TextVisible) return false;
            if (actor.Drawing == 0) { __state.Entity = __instance; __state.Position = __instance.Position; __instance.Position += new Vector2(actor.Data.OffsetX, actor.Data.OffsetY); }
            return true;
        }
        private static void AfterActorText(DrawState __state) { if (__state.Entity != null) __state.Entity.Position = __state.Position; }
        private static ActorQuote Eligible(Actor actor)
        { return actor.Data.Quotes.FirstOrDefault(q => owner.behaviors.GetFlag(q.RequiresFlag) == q.EqualsValue); }
        private static bool FetchQuote(FetchQuote __instance, ref BTresult __result)
        {
            Actor actor; if (!Bound(__instance.game_object, out actor)) return true;
            ActorQuote quote = Eligible(actor); actor.QuoteLines = null; if (quote == null) return true;
            actor.QuoteLines = owner.Narrative.Resolve(quote.String, owner.behaviors.GetFlag).Split('\n');
            new SetBBKeyNode<OldManQuote>(__instance.game_object, "BB_QUOTE_KEY", new OldManQuote { lines = actor.QuoteLines }).Run(new TickData(0, 0));
            __result = BTresult.Success; return false;
        }
        private static void HasQuotes(OldManSettings ___m_settings, ref bool __result)
        { if (Active && actors.Values.Any(a => a.Settings.name == ___m_settings.name && Eligible(a) != null)) __result = true; }
        private static bool CustomLine(TargetLine __instance, int ___m_index, ref BTresult __result)
        {
            Actor actor; if (!Bound(__instance.game_object, out actor) || actor.QuoteLines == null) return true;
            if (___m_index >= actor.QuoteLines.Length) { actor.QuoteLines = null; __result = BTresult.Failure; return false; }
            new SetBBKeyNode<string>(__instance.game_object, "BB_LINE_KEY", SpeechBubbleFormat.FormatString(actor.QuoteLines[___m_index])).Run(new TickData(0, 0));
            __result = BTresult.Success; return false;
        }
        private static void Emit(Entity entity, string name)
        { Actor actor; if (Bound(entity, out actor)) owner.behaviors.Emit(name + ":" + actor.Data.Id, actor.Settings.home_screen); }
        private static void QuoteStarted(FetchQuote __instance, BTresult __result) { if (__result == BTresult.Success) Emit(__instance.game_object, "dialoguebegin"); }
        private static void QuoteEnded(TargetLine __instance, BTresult __result) { if (__result == BTresult.Failure) Emit(__instance.game_object, "dialogueend"); }
        private static void LineStarted(SayLine __instance) { Emit(__instance.game_object, "linebegin"); }
        internal static bool Pose(string id, out Vector2 position, out int screen, out bool visible)
        {
            Actor actor; position = Vector2.Zero; screen = 0; visible = false;
            if (!Active || !actorIds.TryGetValue(id, out actor)) return false;
            screen = actor.Settings.home_screen; visible = actor.Entity.IsAlive && actor.Data.Visible;
            position = Camera.TransformVector2(actor.Entity.Position) + new Vector2(actor.Data.OffsetX, actor.Data.OffsetY); return true;
        }
        internal static void DrawReflection(string id)
        {
            Actor actor; if (!Active || !actorIds.TryGetValue(id, out actor) || !actor.Entity.IsAlive || !actor.Data.Visible || actor.Settings.home_screen != Camera.CurrentScreenIndex1) return;
            Sprite sprite = actor.Entity.sprite;
            Game1.spriteBatch.Draw(sprite.texture, Camera.TransformVector2(actor.Entity.Position) + new Vector2(actor.Data.OffsetX, actor.Data.OffsetY), sprite.source,
                new Color(sprite.GetColor().ToVector4() * owner.NarrativeColor(actor.Data.Tint).ToVector4()) * actor.Data.Opacity, 0, sprite.source.Size.ToVector2() * sprite.center, 1,
                actor.Settings.bubble_format.direction == SpeechBubbleFormat.DirectionX.Left ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        }
        private static bool Intro(bool isFast, ref IBTnode __result)
        {
            if (owner == null || !MappingSettings.Enabled || owner.SceneData.IntroPages.Length == 0) return true;
            var sequence = new BTsequencor(); var font = SceneTextService.Font("menu");
            foreach (IntroPage page in owner.SceneData.IntroPages)
            {
                string[] lines = SceneTextService.Layout(font, owner.Narrative.Initial(page.String), 440);
                var together = new BTsimultaneous(); float y = (360 - lines.Length * font.LineSpacing) / 2f;
                foreach (string line in lines)
                {
                    together.AddChild(new SpawnTextNode(line, new Vector2(240, y), new Vector2(.5f, 0), new FadeTextEntity.TimeSections {
                        fade_in_time = isFast ? Math.Min(.5f, page.FadeIn) : page.FadeIn, stay_time = isFast ? Math.Min(1, page.Stay) : page.Stay,
                        fade_out_time = isFast ? Math.Min(.5f, page.FadeOut) : page.FadeOut }, SceneValidation.ParseColor(page.Color, "Intro"), font)); y += font.LineSpacing;
                }
                sequence.AddChild(together);
            }
            __result = sequence; return false;
        }
        internal static void CaptureResults(SceneHost host)
        {
            if (owner != host || host.SceneData.ResultPages.Length == 0) return;
            var font = SceneTextService.Font("menu");
            resultPages = host.SceneData.ResultPages.Select(page => {
                var lines = new List<string>(SceneTextService.Layout(font, host.Narrative.Resolve(page.Title, host.behaviors.GetFlag), 440));
                foreach (ResultRow row in page.Rows)
                    if (string.IsNullOrEmpty(row.RequiresFlag) || host.behaviors.GetFlag(row.RequiresFlag) == row.EqualsValue)
                        lines.AddRange(SceneTextService.Layout(font, host.Narrative.Resolve(row.String, host.behaviors.GetFlag) + (string.IsNullOrEmpty(row.Counter) ? "" : ": " + host.behaviors.GetFlag(row.Counter)), 440));
                CheckPage(lines.ToArray(), font); return new Page { Lines = lines.ToArray(), Color = SceneValidation.ParseColor(page.Color, "Results"), Background = SceneValidation.ParseColor(page.Background, "Results") };
            }).ToArray();
        }
        private static void BeginResults() { if (Active) CaptureResults(owner); resultPage = -1; pageTime = 0; }
        private static void AdvanceResults(TickData p_data, BTmanager ___m_BT, ref BTresult __result)
        {
            if (resultPages.Length == 0 || !MappingSettings.Enabled) return;
            pageTime += p_data.delta_time;
            if (__result != BTresult.Success) return;
            if (pageTime < .3f) { __result = BTresult.Running; return; }
            if (resultPage + 1 < resultPages.Length) { resultPage++; pageTime = 0; ___m_BT.Reset(); __result = BTresult.Running; }
        }
        private static bool DrawResults()
        {
            if (!MappingSettings.Enabled || resultPage < 0 || resultPage >= resultPages.Length) return true;
            Page page = resultPages[resultPage]; var font = SceneTextService.Font("menu");
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(0, 0, 480, 360), page.Background);
            SceneTextService.Draw(font, page.Lines, new Vector2(20, (360 - page.Lines.Length * font.LineSpacing) / 2f), 440, "center", page.Color); return false;
        }
        internal static void Release()
        { if (patches != null) { patches.Dispose(); patches = null; } actors.Clear(); actorIds.Clear(); owner = null; resultPages = new Page[0]; resultPage = -1; }
    }
}

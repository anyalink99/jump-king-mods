using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.MiscEntities.WorldItems;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal sealed class Pose
    {
        internal int Group, Frame;
        internal string Name;
    }
    internal sealed class Preview : IDisposable
    {
        internal PreparedAppearance Prepared;
        internal string Error = "";
        internal bool Flipped;
        internal int PoseIndex;
        internal readonly List<Pose> Poses = new List<Pose>();
        private float clock;
        private bool borrowed;
        internal Advanced.PresentationActor Actor;
        internal string TestSurface = "normal", TestState = "idle";
        internal bool AdvancedPreview, SoundPreview;
        internal void FollowActive()
        {
            if (!ReferenceEquals(Prepared, Controller.Active)) ReleaseActor();
            if (!borrowed && Prepared != null) Prepared.Dispose();
            Prepared = Controller.Active; borrowed = true; Error = "";
            EnsureActor();
        }
        internal Preview()
        {
            var template = new KingSprites(null);
            for (int group = 0; group < template.m_groups.Count; group++)
            {
                var type = template.m_groups[group].GetType(); var names = type.GetNestedType("SpriteKey");
                foreach (int frame in NativeAppearance.Frames(template.m_groups[group]).Keys.OrderBy(x => x))
                    Poses.Add(new Pose { Group = group, Frame = frame, Name = type.Name + ": " + (names == null ? frame.ToString() : Enum.GetName(names, frame) ?? frame.ToString()) });
            }
        }
        internal void Refresh(Outfit outfit, int? tryOn = null)
        {
            try
            {
                var next = PreparedAppearance.Build(outfit, Controller.Catalog, false, false, tryOn);
                ReleaseActor(); if (!borrowed && Prepared != null) Prepared.Dispose(); Prepared = next; borrowed = false; Error = ""; EnsureActor();
            }
            catch (Exception error) { Error = error.GetBaseException().Message; }
        }
        private void EnsureActor()
        { if (Actor == null && Prepared != null && Prepared.Advanced != null && !Prepared.Advanced.Failed)
            try{Actor = new Advanced.PresentationActor(Prepared, NativeAppearance.Worn(), true);}catch(Exception error){Error=error.GetBaseException().Message;} }
        private void ReleaseActor() { if (Actor != null) Actor.Dispose(); Actor = null; }
        internal void Trigger(string trigger)
        {
            EnsureActor(); if (Actor == null) return; Actor.Surface = TestSurface; Actor.Flipped = Flipped; Actor.Silent = !SoundPreview;
            Actor.Trigger(trigger); if (trigger == "jump") TestState = "rise"; else if (trigger == "land" || trigger == "splat") TestState = trigger;
        }
        internal void Tick(float delta)
        {
            clock += delta;
            if (Actor != null) { Actor.Flipped = Flipped; Actor.Surface = TestSurface; Actor.Silent = !SoundPreview;
                string state = TestState;
                if (!AdvancedPreview) { int frame = CurrentPose(false).Frame; state = frame == 4 ? "charge" : frame == 5 ? "rise" : frame == 6 ? "fall" : frame == 8 ? "splat" : frame >= 1 && frame <= 3 ? "walk" : "idle"; }
                Actor.SetState(state); if (Controller.Data == null || Controller.Data.Animate) Actor.Update(delta); }
        }
        internal Pose CurrentPose(bool animate)
        {
            var pose = Poses[Math.Max(0, Math.Min(Poses.Count - 1, PoseIndex))];
            if (animate && pose.Group == 0 && pose.Frame >= 1 && pose.Frame <= 3)
                return new Pose { Group = 0, Frame = 1 + (int)(clock * 8) % 3, Name = "Walk" };
            return pose;
        }
        internal void Draw(Outfit outfit, bool animate, int tryOnItem, bool checkerboard, bool fitting)
        {
            Rectangle area = new Rectangle(278, 63, 180, 174);
            UiTheme.Panel(area, new Color(18, 23, 29), UiTheme.Border);
            if (checkerboard)
                for (int y = area.Top + 1; y < area.Bottom - 1; y += 12)
                    for (int x = area.Left + 1; x < area.Right - 1; x += 12)
                        if (((x - area.Left) / 12 + (y - area.Top) / 12) % 2 == 0)
                            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(x, y, Math.Min(12, area.Right - x - 1), Math.Min(12, area.Bottom - y - 1)), new Color(28, 33, 39));
            if (Prepared == null) return;
            if (borrowed) outfit = Prepared.SourceOutfit;
            var pose = CurrentPose(animate && !fitting);
            var worn = NativeAppearance.Worn();
            if (tryOnItem != NativeAppearance.BaseItem)
            {
                var target = Prepared.Settings.skins.FirstOrDefault(x => (int)x.item == tryOnItem);
                if (target.layers != null)
                {
                    worn.RemoveAll(item => Prepared.Settings.skins.Any(x => (int)x.item == item && x.layers.Intersect(target.layers).Any()));
                    worn.Add(tryOnItem);
                }
            }
            worn = worn.Where(x => Prepared.Items.ContainsKey((Items)x)).OrderBy(x => NativeAppearance.LayerOrder(Prepared.Settings.skins.First(s => (int)s.item == x).layers[0])).ToList();
            bool advanced = !fitting && Prepared.Advanced != null && Prepared.Advanced.Animations.Count > 0;
            var parts = new List<Tuple<int, KingSprites>> { Tuple.Create(NativeAppearance.BaseItem, advanced ? Prepared.Base : Prepared.PreviewBase ?? Prepared.Base) };
            parts.AddRange(worn.Select(x => Tuple.Create(x, (advanced ? Prepared.Items : Prepared.PreviewItems ?? Prepared.Items)[(Items)x])));
            // Fit the entire selected pose into the preview, including large ending frames
            int width = 48, height = 48;
            foreach (var part in parts)
            {
                var sprite = NativeAppearance.Frames(part.Item2.m_groups[pose.Group])[pose.Frame];
                var fit = outfit.Fit(Prepared.Resolved[NativeAppearance.BaseItem].Id, Prepared.Resolved[part.Item1].Id, part.Item1, pose.Group, pose.Frame);
                width = Math.Max(width, sprite.source.Width + Math.Abs(fit.X) * 2);
                height = Math.Max(height, sprite.source.Height + Math.Abs(fit.Y) * 2);
            }
            float scale = Math.Min(3f, Math.Min(170f / width, 128f / height));
            Vector2 anchor = new Vector2(368, 184);
            var previousActor = Advanced.PresentationDraw.Current;
            Advanced.PresentationDraw.Current = fitting ? null : Actor;
            try
            {
            if (Actor != null && !fitting) Advanced.PresentationDraw.Particles(Actor, "back", anchor, scale, false, Color.White);
            foreach (var part in parts)
            {
                var sprite = NativeAppearance.Frames(part.Item2.m_groups[pose.Group])[pose.Frame];
                var fit = outfit.Fit(Prepared.Resolved[NativeAppearance.BaseItem].Id, Prepared.Resolved[part.Item1].Id, part.Item1, pose.Group, pose.Frame);
                Vector2 offset = new Vector2(Flipped ? -fit.X : fit.X, fit.Y);
                var animated = sprite as Advanced.AdvancedSprite;
                if (animated != null && animated.DrawAdvanced(anchor, new Vector2(scale), Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None)) continue;
                if(advanced)offset=Vector2.Zero; // published native parts already contain their fitted offsets
                var anchored=sprite as Advanced.AnchoredSprite;if(anchored!=null)offset+=anchored.Offset(Flipped?SpriteEffects.FlipHorizontally:SpriteEffects.None);
                Vector2 position = anchor + (offset - sprite.source.Size.ToVector2() * sprite.center) * scale;
                var crystal = sprite as CrystalSprite;
                var cosmic = sprite as CosmicSprite;
                if (cosmic != null) { cosmic.DrawScaled(anchor + offset * scale, scale, Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None); continue; }
                if (crystal != null) { crystal.DrawScaled(anchor + offset * scale, scale, Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None); continue; }
                Game1.spriteBatch.Draw(sprite.texture, position, sprite.source, Color.White, 0, Vector2.Zero, scale,
                    Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }
            if (Actor != null && !fitting) Advanced.PresentationDraw.Particles(Actor, "front", anchor, scale, false, Color.White);
            }
            finally { Advanced.PresentationDraw.Current = previousActor; }
            UiTheme.TextLine(UiTheme.FitText(pose.Name, 164, true), new Vector2(287, 221), UiTheme.Muted, true);
        }
        public void Dispose() { ReleaseActor(); if (!borrowed && Prepared != null) Prepared.Dispose(); Prepared = null; borrowed = false; }
    }
}

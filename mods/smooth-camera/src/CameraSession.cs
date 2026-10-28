using System;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal struct SubjectSample
    {
        internal Vector2 Center, Velocity;
        internal Vector2 NativeCenter;
        internal bool Presented;
        internal int Screen;
        internal bool Paused, Cut;
    }

    internal sealed class CameraRig
    {
        internal readonly CameraMotion Vertical = new CameraMotion();
        internal readonly HorizontalMotion Horizontal = new HorizontalMotion();
        internal readonly CameraView View = new CameraView();
        internal readonly ScriptedView Shots = new ScriptedView();
        internal void Reset() { Vertical.Reset(); Horizontal.Reset(); View.Reset(); Shots.Reset(); }
        internal void Rebase(Vector2 translation)
        { Vertical.Rebase(translation.Y); View.Rebase(translation.X, translation.Y); Shots.Rebase(translation); }
    }

    // one attempt owns its chart, rig and policy. drawing only borrows the result
    internal sealed class CameraSession
    {
        internal readonly CameraRig Rig = new CameraRig();
        internal readonly CameraScene Scene = new CameraScene();
        internal CameraDecision Decision;
        internal SubjectSample Subject;
        internal ScreenImage[] Images = new ScreenImage[0];
        private bool initialized;
        private bool policyValid;
        private CameraSettings policySettings;
        private MapRules policyRules;
        private LevelScreen[] policyScreens;
        private bool policyEnabled, policyRecommendations;
        private Vector2 policyPosition;
        private int policyScreen;
        private long policyGeneration;
        internal Vector2 Pose { get { return new Vector2(Rig.View.X, Rig.View.Y); } }
        internal void InvalidatePolicy() { policyValid = false; }
        internal void Reset() { initialized = policyValid = false; Rig.Reset(); Scene.Reset(); Images = new ScreenImage[0]; }
        internal void Observe(SubjectSample sample, LevelScreen[] screens, CameraSettings options, MapRules policy, bool refreshPolicy = true)
        {
            Scene.Screens = screens; Scene.Settings = options; Scene.Rules = policy;
            int count = screens == null ? 0 : screens.Length;
            if (count == 0 || sample.Screen < 0 || sample.Screen >= count) return;
            Vector2 native = sample.Presented ? sample.NativeCenter : sample.Center;
            long generation = JKRuntime.Gameplay.MapMechanics.Generation;
            var next = Decision;
            if (refreshPolicy || !policyValid || policySettings != options || policyRules != policy || policyScreens != screens
                || policyEnabled != options.Smooth || policyRecommendations != options.UseMapRecommendations || policyScreen != sample.Screen
                || policyPosition != native || policyGeneration != generation)
            {
                next = Scene.Practice ? new CameraDecision { Active = true, AllowFocus = true, AllowLook = true, First = 0, Last = count - 1, Profile = options, Reason = "Practice scene" }
                    : policy.Resolve(options, sample.Screen, native.X, native.Y + sample.Screen * 360, count);
                policySettings = options; policyRules = policy; policyPosition = native; policyScreen = sample.Screen;
                policyScreens = screens; policyEnabled = options.Smooth; policyRecommendations = options.UseMapRecommendations;
                policyGeneration = generation; policyValid = true;
            }
            Vector2 shift = Vector2.Zero;
            bool crossing = initialized && !sample.Cut && next.Active && Decision.Active && next.Profile.Horizontal
                && JKRuntime.Geometry.MapTopology.TryCrossing(screens, Subject.Center - new Vector2(0, 13), sample.Center - new Vector2(0, 13), out shift);
            bool cut = !initialized || sample.Cut || next.Active != Decision.Active;
            float oldY = initialized ? Rig.View.Y : sample.Screen * 360;
            if (cut)
            {
                Rig.Reset(); Scene.Reset();
                if (initialized && !sample.Cut && next.Active && next.Transition == CameraTransition.Smooth)
                    Rig.View.TransitionFrom(0, oldY);
            }
            if (crossing) Scene.Cross(Subject.Screen, shift);
            Subject = sample; Decision = next; initialized = true;
            if (!next.Active) return;
            Rig.Vertical.Options = next.Profile.Vertical; Rig.Horizontal.Options = next.Profile.HorizontalAxis;
            Rig.Vertical.PreservePose = true;
            int first = next.First, last = next.Last;
            if (screens[sample.Screen] != null)
            {
                int low, high; JKRuntime.Geometry.MapTopology.VerticalRange(screens, sample.Screen, out low, out high);
                first = Math.Max(first, low); last = Math.Min(last, high);
            }
            Rig.Vertical.FirstScreen = Rig.View.FirstScreen = first;
            Rig.Vertical.LastScreen = Rig.View.LastScreen = last;
            Rig.View.LookMargin = next.Profile.LookMargin;
            int anchor; float influence;
            Scene.Neighborhood(sample.Screen, Rig.Vertical.Translation, sample.Center.Y, out anchor, out influence);
            if (Rig.Horizontal.ResolveDestination == null)
                Rig.Horizontal.ResolveDestination = (row, left, y) => Scene.ExposedDestination(row, left, Rig.Vertical.Translation, y);
            float oldX = Rig.Horizontal.Translation;
            float rebase = Rig.Horizontal.Observe(sample.Center.X, sample.Screen, screens,
                next.Profile.Horizontal && next.Profile.HorizontalAxis.Mode != FollowMode.Screen, sample.Paused,
                anchor, influence, sample.Velocity.X, sample.Center.Y);
            Rig.Rebase(new Vector2(Rig.Horizontal.Translation - oldX, rebase));
            Rig.Vertical.Observe(sample.Center.Y, count, sample.Paused, sample.Velocity.Y, sample.Screen);
        }
        internal void Advance(float delta, CameraMode mode)
        {
            if (!initialized || !Decision.Active) return;
            Rig.Vertical.Advance(delta); Rig.Horizontal.Advance(delta);
            Rig.View.Advance(mode, Rig.Horizontal.Translation, Rig.Vertical.Translation, Subject.Center.Y,
                Subject.Velocity.Y, Subject.Screen, Scene.Screens.Length, delta, Subject.Paused);
            BuildPlan();
        }
        internal void BuildPlan()
        { Images = Scene.Plan(Rig.Shots.Active ? Rig.Shots.Screen : Subject.Screen, Pose, Rig.Horizontal.Anchor, Rig.Horizontal.Left, Rig.Horizontal.Right); }
    }
}

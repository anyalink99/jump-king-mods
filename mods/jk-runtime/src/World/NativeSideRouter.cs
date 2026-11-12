using System;
using System.Reflection;
using JumpKing;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.World
{
    public interface ISideLinkDecision
    {
        int Target { get; }
        void Commit();
        void Cancel();
    }
    public interface ISideLinkRouter
    {
        bool Owns(int screen, int side);
        ISideLinkDecision Prepare(int screen, int side);
    }
    public static partial class NativeSideLinks
    {
        private static ISideLinkRouter router;
        private static OwnedPatches routerPatches;
        private static readonly FieldInfo ScreenQuery = typeof(HandlePlayerTeleportBehaviour).GetField("m_screenQuery", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CurrentScreen = ScreenQuery == null ? null : ScreenQuery.FieldType.GetMethod("GetCurrentScreen");
        private sealed class Crossing
        {
            internal ISideLinkDecision Decision;
            internal TeleportLink[] Links;
            internal TeleportLink Previous, Written;
            internal int Slot;
            internal Vector2 Before, Expected;
            internal bool Done;
        }
        /// <summary>Owns dynamic decisions at the real native crossing. screen and side are zero-based; decision targets are one-based.</summary>
        public static IDisposable RegisterRouter(string id, ISideLinkRouter value)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(id);
            if (value == null || router != null) throw new InvalidOperationException("A side-link router is already installed or missing");
            if (ScreenQuery == null || CurrentScreen == null || CurrentScreen.ReturnType != typeof(LevelScreen)) throw new NotSupportedException("Native teleport query changed");
            var patches = new OwnedPatches("jk-runtime.native-side-router");
            try
            {
                patches.Add(typeof(HandlePlayerTeleportBehaviour).GetMethod("ExecuteBehaviour"),
                    prefix: typeof(NativeSideLinks).GetMethod("BeforeCrossing", OwnedPatches.Members),
                    postfix: typeof(NativeSideLinks).GetMethod("AfterCrossing", OwnedPatches.Members),
                    finalizer: typeof(NativeSideLinks).GetMethod("FinishCrossing", OwnedPatches.Members));
                router = value; routerPatches = patches;
                return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); router = null; routerPatches.Dispose(); routerPatches = null; });
            }
            catch { patches.Dispose(); throw; }
        }
        private static int Slot(LevelScreen screen, int side)
        {
            if (screen.IsTwoTeleports) return side;
            for (int i = 0; i < screen.teleport.Length; i++) if (screen.teleport[i] != null && screen.teleport[i].IsEnabled) return i;
            return side;
        }
        internal static void CheckForecast(int screen, int side)
        {
            if (router == null) return;
            var screens = Geometry.NativeWorldGeometry.LoadedScreens;
            if (screens == null || screen < 0 || screen >= screens.Length) return;
            if (router.Owns(screen, Slot(screens[screen], side)))
                throw new NotSupportedException("Warp forecast crosses a dynamic MME transition; ordinary movement keeps the real randomizer intact");
        }
        private static void BeforeCrossing(HandlePlayerTeleportBehaviour __instance, BehaviourContext behaviourContext, out Crossing __state)
        {
            __state = null;
            if (router == null || WorldControl.Following) return;
            LevelScreen screen = (LevelScreen)CurrentScreen.Invoke(ScreenQuery.GetValue(__instance), null);
            if (screen == null || !screen.CanTeleport) return;
            var center = behaviourContext.BodyComp.GetHitbox().Center;
            if (center.X >= 0 && center.X <= 480) return;
            int index = screen.GetIndex0(), side = center.X < 0 ? 0 : 1;
            var screens = Geometry.NativeWorldGeometry.LoadedScreens;
            // shadow bodies with their own query must never spend the live router's state
            if (screens == null || index < 0 || index >= screens.Length || !ReferenceEquals(screens[index], screen)) return;
            int slot = Slot(screen, side);
            if (!router.Owns(index, slot)) return;
            var decision = router.Prepare(index, slot);
            if (decision == null || decision.Target < 1 || decision.Target > screens.Length) { if(decision!=null)decision.Cancel(); throw new InvalidOperationException("Invalid dynamic side-link decision"); }
            try
            {
                var before = behaviourContext.BodyComp.Position;
                int source = (int)(-(before.Y - 360f) / 360f);
                var crossing = new Crossing { Decision = decision, Links = screen.teleport, Slot = slot, Previous = screen.teleport[slot], Written = new TeleportLink(decision.Target), Before = before,
                    Expected = new Vector2(before.X + (side == 0 ? 480 : -480), (int)before.Y - checked(360 * (decision.Target - 1 - source))) };
                __state = crossing; screen.teleport[slot] = crossing.Written;
            }
            catch { decision.Cancel(); throw; }
        }
        private static void AfterCrossing(BehaviourContext behaviourContext, Crossing __state)
        {
            if (__state == null) return;
            if (behaviourContext.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag) && behaviourContext.BodyComp.Position == __state.Expected)
            { __state.Decision.Commit(); __state.Done = true; }
        }
        private static Exception FinishCrossing(Exception __exception, Crossing __state)
        {
            if (__state == null) return __exception;
            try { if (!__state.Done) __state.Decision.Cancel(); }
            finally { if (ReferenceEquals(__state.Links[__state.Slot], __state.Written)) __state.Links[__state.Slot] = __state.Previous; }
            return __exception;
        }
    }
}

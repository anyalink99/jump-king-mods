using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using JKRuntime;
using JumpKing;
using JumpKing.GameManager;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace God
{
    internal static class KingDrag
    {
        internal static readonly Type Native = typeof(Game1).Assembly.GetType("JumpKing.Player.DebugTeleport", true);
        internal static readonly MethodInfo Click = Native.GetMethod("Click", OwnedPatches.Members);
        private static readonly MethodInfo Scroll = Native.GetMethod("Scroll", OwnedPatches.Members);
        private static readonly FieldInfo LastScroll = Native.GetField("last_scroll", OwnedPatches.Members);
        private static readonly MethodInfo Initialize = typeof(Component).GetMethod("InitializeComponent", OwnedPatches.Members);
        private static MethodInfo cameraGrab;
        private static Component teleport;
        private static Entity owner;
        private static IDisposable camera;
        private static Vector2 grab;
        private static bool dispatch;
        internal static bool Active { get; private set; }
        internal static void Prepare(OwnedPatches patches)
        {
            cameraGrab = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SmoothCamera.ModEntry"))
                .Where(t => t != null).Select(t => t.GetMethod("BeginPlayerDrag", new[] { typeof(Point), typeof(Vector2) })).FirstOrDefault(m => m != null);
            patches.Add(Native.GetMethod("Update", OwnedPatches.Members), prefix: Method("NativeUpdate"), priority: 1000);
            patches.Add(Click, prefix: Method("NativeClick"), priority: 1000);
            patches.Add(typeof(Entity).GetMethod("UpdateComponents"), postfix: Method("AfterPlayer"));
        }
        private static MethodInfo Method(string name) { return typeof(KingDrag).GetMethod(name, OwnedPatches.Members); }
        private static bool NativeUpdate() { return !Entry.Open && !Entry.Draining; }
        private static bool NativeClick() { return dispatch || (!Entry.Open && !Entry.Draining); }
        internal static Rectangle Bounds(int screen)
        {
            var player = GameLoop.m_player;
            if (player == null) return Rectangle.Empty;
            var r = player.m_body.GetHitbox(); r.Y += screen * Terrain.Height; return r;
        }
        internal static void Begin(Point pointer, Point local, int screen)
        {
            End(); var player = GameLoop.m_player;
            if (player == null) return;
            Attach();
            var bounds = player.m_body.GetHitbox();
            var center = player.m_body.Position + new Vector2(bounds.Width * .5f, bounds.Height * .5f);
            grab = new Vector2(local.X, local.Y - screen * Terrain.Height) - center;
            if (cameraGrab != null) camera = (IDisposable)cameraGrab.Invoke(null, new object[] { pointer, center });
            Active = true; Entry.Mark();
        }
        private static void Attach()
        {
            var player = GameLoop.m_player;
            if (!ReferenceEquals(owner, player))
            {
                teleport = (Component)FormatterServices.GetUninitializedObject(Native);
                Initialize.Invoke(teleport, new object[] { player, false }); owner = player;
            }
        }
        internal static void SeedWheel(MouseState mouse) { LastScroll.SetValue(null, mouse.ScrollWheelValue / 120); }
        internal static void Wheel(MouseState mouse, bool allowed)
        {
            if (!allowed) { SeedWheel(mouse); return; }
            Attach(); var before = GameLoop.m_player.m_body.Position;
            Scroll.Invoke(teleport, new object[] { mouse });
            if (GameLoop.m_player.m_body.Position != before) Entry.Mark();
        }
        internal static void End() { Active = false; if (camera != null) { camera.Dispose(); camera = null; } }
        internal static void Release() { End(); owner = null; teleport = null; }
        private static void AfterPlayer(Entity __instance)
        {
            if (!Entry.Open || !ReferenceEquals(__instance, GameLoop.m_player)) return;
            try
            {
                var mouse = Mouse.GetState();
                if (!Game1.instance.IsActive || JKRuntime.Gameplay.NativePause.IsPaused || JKRuntime.UI.UIApi.IsOpen)
                { End(); SeedWheel(mouse); return; }
                if (Active) Move(mouse);
                Wheel(mouse, Entry.Editor.WheelAvailable);
            }
            catch (Exception error) { End(); Entry.Status = error.GetBaseException().Message; }
        }
        internal static void Move(MouseState mouse)
        {
            if (!Active || mouse.LeftButton != ButtonState.Pressed) { End(); return; }
            if (camera == null)
            {
                var rect = Game1.instance.GetGameRect();
                mouse = new MouseState(NativePixel((mouse.X - rect.X) * 480.0 / rect.Width - grab.X, rect.X, rect.Width, 480),
                    NativePixel((mouse.Y - rect.Y) * 360.0 / rect.Height - grab.Y, rect.Y, rect.Height, 360),
                    mouse.ScrollWheelValue, mouse.LeftButton, mouse.MiddleButton, mouse.RightButton, mouse.XButton1, mouse.XButton2);
            }
            // invoke the patched native method after physics, exactly where debug dragging runs
            dispatch = true;
            try { Click.Invoke(teleport, new object[] { mouse }); }
            finally { dispatch = false; }
            Camera.UpdateCamera(GameLoop.m_player.m_body.GetHitbox().Center);
        }
        private static int NativePixel(double logical, int origin, int size, int extent)
        {
            int target = (int)Math.Floor(logical), pixel = origin + (int)Math.Round(logical * size / extent);
            // native Click truncates a float division; choose a pixel inside that cell
            for (int delta = 0; delta <= Math.Max(1, size / extent); delta++)
                foreach (int sign in new[] { 1, -1 })
                {
                    int candidate = pixel + delta * sign;
                    if ((int)((float)(candidate - origin) / size * extent) == target) return candidate;
                }
            return pixel;
        }
    }
}

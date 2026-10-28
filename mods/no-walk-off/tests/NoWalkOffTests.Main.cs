using System;
using System.IO;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
namespace NoWalkOff {
internal static partial class Tests {
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static void Require(bool value,string text) { if(!value) throw new Exception(text); }
    private static LevelScreen[] Scene(IBlock[] blocks) { return new[] { new LevelScreen(0,blocks,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null) }; }
    private static int Main(string[] args) {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs request) {
            string path=Path.Combine(args[0],new AssemblyName(request.Name).Name+".dll");
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        try {
            Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"0Harmony.dll"));
            Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"UnknownProvider.dll"));
            using(var motion=JKRuntime.Gameplay.MotionObservation.PrepareLoaded()) {
                ModEntry.MotionScope=motion;
                MapVariants();
                using(new SpriteGameFixture()) {
                    NoWalkOffRegression();
                    if(args.Length>1 && args[1]!="Fast") ForeignNoWalkOff(args[0]);
                }
            }
            Console.WriteLine("[OK] No Walk Off native movement and lifecycle"); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { ModEntry.MotionScope=null; }
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
        } }
internal static class NativeFlight {
    internal static T Get<T>(object value,string name) { return NativeBody.Get<T>(value,name); }
    internal static void Set(object value,string name,object field) { NativeBody.Set(value,name,field); }
    internal static void CopyState(JumpKing.Player.BodyComp source,JumpKing.Player.BodyComp target) { JKRuntime.Simulation.NativeFlightSimulation.CopyState(source,target); }
}
}

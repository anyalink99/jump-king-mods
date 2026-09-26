using System;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime;
using Microsoft.Xna.Framework.Audio;
using JumpKing.XnaWrappers;

internal static class PreparedAudioTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static int Main(string[] args)
    {
        try
        {
            typeof(SoundEffect).GetMethod("InitializeSoundEffect", Flags).Invoke(null, null);
            var native = typeof(JumpKing.Game1).Assembly;
            var prefsType = native.GetType("JumpKing.PlayerPreferences.SoundPrefs");
            var managerType = native.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime");
            var instance = managerType.BaseType.GetField("instance", Flags); object previous = instance.GetValue(null);
            var prefs = Activator.CreateInstance(prefsType);
            prefsType.GetField("master", Flags).SetValue(prefs, 0f);
            var manager = FormatterServices.GetUninitializedObject(managerType);
            managerType.BaseType.GetField("m_settings", Flags).SetValue(manager, prefs);
            instance.SetValue(null, manager);
            try
            {
                var entry = Assembly.LoadFrom(args[0]).GetType("MorphBallMod.ModEntry");
                var field = entry.GetField("PreparedSound", Flags);
                using (var world = new RuntimeScope())
                {
                    entry.GetMethod("PrepareWorld").Invoke(null, new object[] { world });
                    object sound = field.GetValue(null); Check(sound != null, "Missing world audio");
                    for (int i = 0; i < 3; i++)
                    {
                        using (var attempt = new RuntimeScope())
                        {
                            entry.GetMethod("PrepareAttempt").Invoke(null, new object[] { attempt });
                            sound.GetType().GetMethod("Play", Flags).Invoke(sound, new object[] { i % 2 == 0 });
                            Check(ReferenceEquals(sound, field.GetValue(null)), "Restart replaced world audio");
                        }
                        foreach (string name in new[] { "transform", "untransform" })
                        {
                            var voice = (JKSound)sound.GetType().GetField(name, Flags).GetValue(sound);
                            Check(System.Threading.SpinWait.SpinUntil(() => voice.State == JKSoundState.Stopped, 250), "Attempt did not stop audio");
                        }
                    }
                    world.Dispose(); Check(field.GetValue(null) == null, "World reference leaked");
                    Check((bool)sound.GetType().GetField("disposed", Flags).GetValue(sound), "World audio leaked");
                }
            }
            finally { instance.SetValue(null, previous); }
            Console.WriteLine("[OK] Ball audio: native decoding, restart reuse, cancelled-attempt stop and world disposal");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

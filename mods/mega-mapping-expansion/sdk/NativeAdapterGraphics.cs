using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using JKRuntime;
using JKRuntime.Audio;
using JumpKing;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static class NativeAdapterGraphics
    {
        private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("[OK] "+message);}
        internal static void Run(Game1 game,string root,string gameDir,string exampleDir)
        {
            typeof(SoundEffect).GetMethod("InitializeSoundEffect",All).Invoke(null,null);
            var prefsType=typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefs");
            var managerType=typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime");
            var instance=managerType.BaseType.GetField("instance",All);object previous=instance.GetValue(null);
            var manager=FormatterServices.GetUninitializedObject(managerType);var prefs=Activator.CreateInstance(prefsType);
            prefsType.GetField("master",All).SetValue(prefs,0f);
            managerType.BaseType.GetField("m_settings",All).SetValue(manager,prefs);instance.SetValue(null,manager);
            string wave=Path.Combine(root,"fixture.wav"),xnb=Path.Combine(root,"fixture.xnb");
            File.Copy(Path.Combine(exampleDir,"mme-assets/chime.wav"),wave,true);
            File.Copy(Path.Combine(gameDir,"Content/audio/talking.xnb"),xnb,true);
            try
            {
                foreach(string path in new[]{wave,xnb})
                using(var input=File.OpenRead(path))
                using(var sound=path==wave?PreparedSound.FromStream(input):PreparedSound.FromContent(input,game.Services))
                {
                    input.Close();sound.IsLooped=true;sound.SetPaused(true);sound.Play();
                    Check(sound.State==JKSoundState.Stopped,"Prepared "+Path.GetExtension(path)+" has no attack while paused");
                    sound.SetPaused(false);Check(sound.State==JKSoundState.Playing,"Prepared sound resumes from owned memory");
                    sound.SetPaused(true);Check(sound.State==JKSoundState.Paused,"Prepared sound holds its active voice on pause");
                    sound.SetPaused(false);sound.Stop();sound.Play();
                    Check(sound.State==JKSoundState.Playing,"An immediate restart replaces a flushing native voice");
                }
                var definition=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(@"<MappingSettings version='1'>
<Components><Component id='voice' kind='audio'><Value name='path' value='fixture.wav'/><Value name='playing' value='true'/><Value name='loop' value='true'/><Value name='fadeIn' value='1'/></Component></Components></MappingSettings>"));
                using(var scene=new SceneProgram(definition))
                using(var pool=new NativeSoundPool())
                using(var controller=new NativeControllers(scene,root,pool))
                using(var attempt=new RuntimeScope())
                {
                    scene.Tick(0,0);controller.Activate(attempt);controller.Tick(.25,true,false,false);
                    Check(scene.Signals.Read("voice.audible").Value.X==1,"Composed audio properties start an owned native loop");
                    controller.Tick(.25,true,true,false);Check(scene.Signals.Read("voice.audible").Value.X==0,"Native audio adapter pauses independently of visual animation");
                    controller.Tick(.25,true,false,false);Check(scene.Signals.Read("voice.audible").Value.X==1,"Native audio adapter resumes after pause");
                    controller.ResetTransient();controller.Tick(.25,true,false,false);
                    Check(scene.Signals.Read("voice.audible").Value.X==1,"A requested loop recovers after scene restore");
                    controller.Tick(.1,false,false,false);Check(scene.Signals.Read("voice.audible").Value.X==0,"Disabling the scene stops its owned voices");
                }
                using(var pool=new NativeSoundPool())
                {
                    var first=pool.Acquire(root,"fixture.xnb",SoundType.SFX);var original=first.Sound;first.Dispose();
                    using(var next=pool.Acquire(root,"fixture.xnb",SoundType.SFX))
                    using(var candidate=pool.Acquire(root,"fixture.xnb",SoundType.SFX))
                    {Check(ReferenceEquals(next.Sound,original),"An unchanged restart reuses its prepared sound");Check(!ReferenceEquals(next.Sound,candidate.Sound),"Reload candidates own independent sound voices");}
                }
            }
            finally{instance.SetValue(null,previous);}
        }
    }
}

using System;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed class NativeCapture
    {
        private static readonly BindingFlags Members=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly FieldInfo Screens=typeof(LevelManager).GetField("m_screens",Members);
        private static readonly FieldInfo Graphics=typeof(LevelScreen).GetField("m_graphics",Members);
        private static readonly Action<LevelScreen,Texture2D> Background=Open("DrawBG");
        private static readonly Action<LevelScreen,Texture2D> Midground=Open("DrawMG");
        private readonly LevelScreen[] screens;
        private readonly LevelScreen.Graphics[] graphics;
        private PlayerEntity player;
        internal static int Drawing{get;private set;}
        private static Action<LevelScreen,Texture2D> Open(string name)
        {
            var method=typeof(LevelScreen).GetMethod(name,Members,null,new[]{typeof(Texture2D)},null);
            if(method==null)throw new NotSupportedException("Native capture method is unavailable: "+name);
            return (Action<LevelScreen,Texture2D>)Delegate.CreateDelegate(typeof(Action<LevelScreen,Texture2D>),method);
        }
        internal NativeCapture()
        {
            if(Screens==null||Screens.FieldType!=typeof(LevelScreen[])||Graphics==null||Graphics.FieldType!=typeof(LevelScreen.Graphics))throw new NotSupportedException("Installed native capture fields are incompatible");
            screens=(LevelScreen[])Screens.GetValue(null);if(screens==null)throw new InvalidOperationException("Native screens must be loaded before capture preparation");
            graphics=new LevelScreen.Graphics[screens.Length];for(int i=0;i<screens.Length;i++)graphics[i]=(LevelScreen.Graphics)Graphics.GetValue(screens[i]);
        }
        internal NativeActorObservations Actors;
        internal void Attach(PlayerEntity actor){player=actor;}
        internal void Draw(string selection,int screen)
        {
            if(screen<1||screen>screens.Length)return;var level=screens[screen-1];var textures=graphics[screen-1];Drawing++;
            bool open=false;
            try
            {
                Game1.instance.StartBatch();open=true;
                switch(selection)
                {
                    case "native.background":Background(level,textures.backbackground);break;
                    case "native.midground":Midground(level,textures.background);break;
                    case "native.foreground":level.DrawForeground();break;
                    case "actor:player":
                        if(player!=null&&player.IsAlive)
                        {
                            var frame=JKRuntime.Presentation.PlayerAppearance.Resolve(player);JKRuntime.Presentation.PlayerAppearance.Draw(frame,JKRuntime.Presentation.PlayerAppearance.Project(frame));
                        }
                        break;
                    case "actor:player.idle":
                        if(player!=null&&player.IsAlive)
                        {
                            var foot=JKRuntime.FrameComposition.ProjectWorld(player.m_body.Position+new Microsoft.Xna.Framework.Vector2(9,26));
                            Game1.instance.contentManager.playerSprites.idle.Draw(foot,NativeSceneAdapter.FacingLeft(player)?SpriteEffects.FlipHorizontally:SpriteEffects.None);
                        }
                        break;
                    default:
                        const string prefix="actor:ending.";
                        if(selection.StartsWith(prefix,StringComparison.Ordinal)&&NativeActorObservations.ValidRole(selection.Substring(prefix.Length)))
                        {if(Actors!=null)Actors.Draw(selection.Substring(prefix.Length));break;}
                        throw new ArgumentException("Unknown native capture selector: "+selection);
                }
            }
            finally{try{if(open)Game1.instance.EndBatch();}finally{Drawing--;}}
        }
    }
}

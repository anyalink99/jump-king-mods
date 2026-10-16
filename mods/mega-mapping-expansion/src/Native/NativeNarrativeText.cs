using System;
using System.IO;
using System.Linq;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JumpKing.GameManager;
using JumpKing;
using JumpKing.Util.DrawBT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal static class NativeNarrativeText
    {
        internal static void ObserveLanguage(SceneProgram scene)
        {scene.Signals.Publish("language.culture",MegaMappingExpansion.Api.SceneValue.String((LanguageJK.language.Culture??System.Globalization.CultureInfo.CurrentUICulture).Name),MegaMappingExpansion.Api.ObservationQuality.Exact);}
        internal static bool IsFont(string name)
        {return name=="menu"||name=="small"||name=="style"||name=="location"||name=="gargoyle";}
        internal static SpriteFont Font(string name)
        {
            var fonts=Game1.instance.contentManager.font;
            switch(name){case "menu":return fonts.MenuFont;case "small":return fonts.MenuFontSmall;case "style":return fonts.StyleFont;case "location":return fonts.LocationFont;case "gargoyle":return fonts.GargoyleFont;default:throw new InvalidDataException("Unknown native font: "+name);}
        }
        internal static CompiledComponent[] IntroPages(SceneProgram scene)
        {return scene.Components.Values.Where(c=>c.Descriptor.Kind=="intro").OrderBy(c=>scene.Properties.Read(c.Id,"order").X).ThenBy(c=>c.Id,StringComparer.Ordinal).ToArray();}
        internal static void Validate(SceneProgram scene)
        {
            var pages=IntroPages(scene);if(pages.Length>16)throw new InvalidDataException("Scene supports at most 16 intro pages");
            foreach(var page in pages)if(!IsFont(scene.Properties.Read(page.Id,"font").Text))throw new InvalidDataException(page.Id+": intro needs a native font");
        }
        internal static IBTnode Intro(SceneProgram scene,bool fast)
        {
            var pages=IntroPages(scene).Where(c=>scene.Properties.Read(c.Id,"enabled").X!=0).ToArray();
            if(pages.Length==0)return null;
            var sequence=new BTsequencor();
            foreach(var page in pages)
            {
                string id=page.Id;var font=Font(scene.Properties.Read(id,"font").Text);
                string[] lines=scene.Properties.Read(id,"value").Text.Replace("\r","").Split('\n');
                if(lines.Length*font.LineSpacing>300||lines.Any(line=>font.MeasureString(line).X>440))throw new InvalidDataException(id+": intro text exceeds 440 by 300 pixels; split it into pages");
                var color=scene.Properties.Read(id,"color");var tint=new Color((float)color.X,(float)color.Y,(float)color.Z,(float)color.W);
                float fadeIn=(float)scene.Properties.Read(id,"fadeIn").X,stay=(float)scene.Properties.Read(id,"stay").X,fadeOut=(float)scene.Properties.Read(id,"fadeOut").X;
                var times=new FadeTextEntity.TimeSections{fade_in_time=fast?Math.Min(.5f,fadeIn):fadeIn,stay_time=fast?Math.Min(1,stay):stay,fade_out_time=fast?Math.Min(.5f,fadeOut):fadeOut};
                var together=new BTsimultaneous();float y=(360-lines.Length*font.LineSpacing)/2f;
                foreach(string line in lines){together.AddChild(new SpawnTextNode(line,new Vector2(240,y),new Vector2(.5f,0),times,tint,font));y+=font.LineSpacing;}
                sequence.AddChild(together);
            }
            return sequence;
        }
    }
}

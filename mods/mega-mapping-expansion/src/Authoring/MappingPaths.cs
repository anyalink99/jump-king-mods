using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;
namespace MegaMappingExpansion
{
    internal static class MappingPaths
    {
        internal static string ResolveLevelRoot(string contentRoot){return ResolveLevelRoot(contentRoot,Environment.GetCommandLineArgs());}
        internal static string ResolveLevelRoot(string contentRoot,string[] arguments)
        {
            var candidates=new List<string>();if(!string.IsNullOrWhiteSpace(contentRoot))candidates.Add(contentRoot);
            if(arguments!=null&&Array.IndexOf(arguments,"-debug")>=0)foreach(string argument in arguments){string path=(argument??"").Trim().Trim('"');if(Directory.Exists(path))candidates.Add(path);}
            foreach(string candidate in candidates){string root=Path.GetFullPath(candidate);if(MappingFiles.Exists(root)||Directory.Exists(Path.Combine(root,"ending")))return root;}
            return Path.GetFullPath(contentRoot);
        }
        internal static Color ParseColor(string text,string id)
        {
            try{var color=SceneValue.Parse(SceneValueKind.Color,text);return new Color((float)color.X,(float)color.Y,(float)color.Z,(float)color.W);}
            catch(Exception error){throw new InvalidDataException(id+": expected #RRGGBB, #RRGGBBAA or normalized RGBA channels",error);}
        }
    }
}
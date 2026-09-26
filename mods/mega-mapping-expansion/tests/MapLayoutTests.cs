using System;
using System.IO;
using JumpKing.Level;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void MapLayoutChecks(string root)
        {
            string folder=Path.Combine(root,"map-layout-test"),path=Path.Combine(folder,MapLayout.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,"<MapLayout version='1' screens='512' atlasSide='23'><SideLink screen='1' side='right' target='512'/><SideLink screen='512' side='left' target='1'/></MapLayout>");
            var layout=MapLayout.Read(folder);layout.ValidateAtlas(1380,1035);
            var links=new[]{new TeleportLink(8),new TeleportLink()};NativeMapLayout.Apply(layout,0,links);
            Require(links[0].GetIndex1()==8&&links[1].GetIndex1()==512,"integer side links preserve unowned native links and exceed byte addressing");
            NativeMapLayout.Apply(layout,511,links);Require(links[0].GetIndex1()==1,"last content screen can return to the first");
            bool rejected=false;try{layout.ValidateAtlas(780,585);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"large-map metadata cannot pretend a 169-slot collision atlas is larger");
            File.WriteAllText(path,"<MapLayout version='1' screens='512' atlasSide='23'><SideLink screen='1' side='right' target='513'/></MapLayout>");
            rejected=false;try{MapLayout.Read(folder);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"links cannot enter padding slots beyond authored content");
            var scene=new SceneFile{Textures=new TextureData[0],VectorAssets=new[]{new VectorAssetData{Width=16384,Height=16384}}};
            rejected=false;try{SceneResourceBudget.Validate(scene,root);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"decoded texture budget rejects unsafe allocations before GPU resource creation");
        }
    }
}

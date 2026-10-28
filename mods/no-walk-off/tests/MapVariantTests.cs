using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace NoWalkOff
{
    internal static partial class Tests
    {
        private static void MapVariants()
        {
            MapActivation.Register();
            bool edge = Settings.Current.NoWalkOff;
            Settings.Current.NoWalkOff = true;
            try
            {
                foreach (string id in new[] { "no-walk-off" })
                    JKRuntime.Gameplay.MapMechanics.DeclareScreen(id, Camera.CurrentScreen, JKRuntime.Gameplay.MapMechanicMode.Off, "test");
                Require(!MapActivation.Edge(true).Enabled, "Off screens defeat global toggles and local surface/zone triggers");
            }
            finally
            {
                foreach (string id in new[] { "no-walk-off" }) JKRuntime.Gameplay.MapMechanics.ClearMarkers(id);
                Settings.Current.NoWalkOff = edge;
            }
            var factory=new BlockFactory();
            Require(!factory.CanMakeBlock(new Color(40,38,75),null),"Native JK+ warp colors belong to their provider");
            for(int alias=120;alias<=124;alias++)Require(!factory.CanMakeBlock(new Color(250,alias,190),null),"No Walk Off doesn't claim return terrain or map-local slope aliases");
            var rect=new Rectangle(80,80,8,8); Rectangle overlap;
            var colours=new HashSet<Color>();
            foreach(var variant in BlockFactory.Variants)
            {
                Require(variant.Colours.Count()==3,"Each mechanic requires all three map variants");
                foreach(var colour in variant.Colours)
                {
                    Require(colours.Add(colour) && factory.CanMakeBlock(colour,null),"Unregistered/duplicate variant colour");
                    Require(JKRuntime.Geometry.BlockCatalog.FindOwners(colour).SequenceEqual(new[] { "no-walk-off" }),
                        "Variant colour must belong to No Walk Off in the shared catalogue");
                }
                var solid=factory.GetBlock(variant.Solid,rect,null,null,0,0,0);
                var zone=factory.GetBlock(variant.Zone,rect,null,null,0,0,0);
                Require(factory.IsSolidBlock(variant.Solid) && solid.Intersects(rect,out overlap)==BlockCollisionType.Collision_Blocking,"Solid variant must block");
                Require(!factory.IsSolidBlock(variant.Zone) && zone.Intersects(rect,out overlap)==BlockCollisionType.Collision_NonBlocking,"Zone must be detectable but nonblocking");
                Require(zone.Intersects(new Rectangle(88,80,8,8),out overlap)==BlockCollisionType.NoCollision,"Zone must not extend beyond its boundary");
                Require(!factory.IsSolidBlock(variant.Screen) && factory.GetBlock(variant.Screen,rect,null,null,5,0,0)==null && variant.Screens.Contains(5),"Screen marker must store scope, not geometry");
            }
            bool rejected=false;
            try { new MapVariantSet("incomplete",Color.Red,Color.Green,Color.Blue,r=>new BoxBlock(r),null); }
            catch(ArgumentException) { rejected=true; }
            Require(rejected,"Missing Zone implementation accepted");
            Require(!factory.CanMakeBlock(new Color(173,49,211,128),null),"Transparent variant code accepted");
            BlockFactory.ResetScreens();
            Require(BlockFactory.Variants.All(v=>v.Screens.Count==0),"Screen state leaked across maps");

            // verify the installed loader keeps local zone geometry, while
            // the Screen colour produces no collider at all
            var factories=(ICollection<IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",Flags).GetValue(null);
            factories.Add(factory);
            try
            {
                foreach (var variant in BlockFactory.Variants)
                {
                var data=new Color[60*45];
                data[10+10*60]=variant.Zone; data[20+20*60]=variant.Solid; data[30+30*60]=variant.Screen;
                var texture=(LevelTexture)typeof(LevelTexture).GetConstructor(Flags,null,new[]{typeof(Color[]),typeof(int),typeof(int)},null).Invoke(new object[]{data,60,45});
                var args=new object[]{texture,null,0,false,null,0f,null};
                var blocks=(IBlock[])typeof(LevelManager).GetMethod("LoadBlocksInterval",Flags).Invoke(null,args);
                Type zoneType=variant.Create(variant.Zone,rect,0).GetType(), solidType=variant.Create(variant.Solid,rect,0).GetType();
                Require(blocks.Length==2 && blocks.Count(b=>b.GetType()==zoneType)==1 && blocks.Count(b=>b.GetType()==solidType)==1,"Native loader lost a variant or made Screen geometry");
                Require(blocks.Single(b=>b.GetType()==zoneType).GetRect()==rect && variant.Screens.Contains(0),"Native zone bounds / screen activation");
                }
            }
            finally { factories.Remove(factory); BlockFactory.ResetScreens(); }
            Console.WriteLine("[OK] Mandatory Solid/Zone/Screen registration, collision flags, native pixel loader, map reset");
        }
    }
}

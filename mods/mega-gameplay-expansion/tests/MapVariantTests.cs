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

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void MapVariants()
        {
            var factory=new MegaBlockFactory();
            var rect=new Rectangle(80,80,8,8); Rectangle overlap;
            var colours=new HashSet<Color>();
            foreach(var variant in MegaBlockFactory.Variants)
            {
                Require(variant.Colours.Count()==3,"Each mechanic requires all three map variants");
                foreach(var colour in variant.Colours)
                    Require(colours.Add(colour) && factory.CanMakeBlock(colour,null),"Unregistered/duplicate variant colour");
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
            MegaBlockFactory.ResetScreens();
            Require(MegaBlockFactory.Variants.All(v=>v.Screens.Count==0),"Screen state leaked across maps");

            // Verify the installed loader preserves local zone geometry, while
            // the Screen colour produces no collider at all.
            var factories=(ICollection<IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",Flags).GetValue(null);
            factories.Add(factory);
            try
            {
                foreach (var variant in MegaBlockFactory.Variants)
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
            finally { factories.Remove(factory); MegaBlockFactory.ResetScreens(); }
            Console.WriteLine("[OK] Mandatory Solid/Zone/Screen registration, collision flags, native pixel loader, map reset");
        }
        private static void WarpZoneActivation()
        {
            bool setting=Settings.Current.WarpJump; Settings.Current.WarpJump=false;
            var saved=MegaBlockFactory.WarpScreens.ToArray(); MegaBlockFactory.ResetScreens();
            try
            {
                var floor=new BoxBlock(new Rectangle(0,320,480,40));
                var zone=new WarpZoneBlock(new Rectangle(160,200,80,120));
                var screens=Scene(new IBlock[]{floor,zone});
                typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
                typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
                typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
                foreach(float vy in new[]{-6f,4f})
                {
                    var player=ResumePlayer();
                    player.m_body.Position=new Vector2(100,250); player.m_body.Velocity=new Vector2(0,vy);
                    NativeFlight.Set(player.m_body,"_is_on_ground",false);
                    using(var controller=new WarpController(player))
                    {
                        var context=NativeFlight.Get<BehaviourContext>(player.m_body,"m_behaviourContext");
                        Require(controller.ExecuteBehaviour(context) && player.m_body.Enabled,"Zone affected a player outside its region");
                        player.m_body.Position=new Vector2(180,250);
                        BodyComp expected; int ticks; string reason;
                        Require(NativeFlight.TryPredict(player.m_body,new FlightWorld(screens,0,0),out expected,out ticks,out reason),reason);
                        // Adding a Zone may activate warp but cannot alter its native trajectory.
                        BodyComp bare; int bareTicks;
                        Require(NativeFlight.TryPredict(player.m_body,new FlightWorld(Scene(new IBlock[]{floor}),0,0),out bare,out bareTicks,out reason),reason);
                        Require(bare.Position==expected.Position && bare.Velocity==expected.Velocity && bareTicks==ticks,"Zone changed collision/flight physics");
                        Require(!controller.ExecuteBehaviour(context) && !player.m_body.Enabled,"Entering zone mid-jump/fall failed with global mode off");
                        var plan=typeof(WarpController).GetField("plan",Flags).GetValue(controller);
                        Require(!(bool)plan.GetType().GetField("Global",Flags).GetValue(plan) && !(bool)plan.GetType().GetField("MarkModified",Flags).GetValue(plan),"Authored zone treated as a global cheat");
                        AdvanceFor(controller,MatrixPixels.Duration+.01f);
                        Require(player.m_body.Enabled && player.m_body.Position==expected.Position,"Zone warp did not finish at the native landing");
                    }
                }
                // Walking OUT of a zone while still grounded must not latch it.
                var walker=ResumePlayer(); walker.m_body.Position=new Vector2(180,294);
                using(var controller=new WarpController(walker))
                {
                    var context=NativeFlight.Get<BehaviourContext>(walker.m_body,"m_behaviourContext");
                    Require(controller.ExecuteBehaviour(context) && walker.m_body.Enabled,"Zone teleported an idle grounded player");
                    walker.m_body.Position=new Vector2(300,294); controller.ExecuteBehaviour(context);
                    walker.m_body.Velocity=new Vector2(0,-6);
                    Require(controller.ExecuteBehaviour(context) && walker.m_body.Enabled,"Zone activation stuck after walking out");
                }
                // A departure immediately after the last grounded zone contact
                // remains armed, like the Solid variant's walk-off semantics.
                walker=ResumePlayer(); walker.m_body.Position=new Vector2(230,294);
                using(var controller=new WarpController(walker))
                {
                    var context=NativeFlight.Get<BehaviourContext>(walker.m_body,"m_behaviourContext");
                    controller.ExecuteBehaviour(context);
                    walker.m_body.Position=new Vector2(242,294); walker.m_body.Velocity=new Vector2(0,4);
                    NativeFlight.Set(walker.m_body,"_is_on_ground",false);
                    Require(!controller.ExecuteBehaviour(context),"Zone walk-off lost the last grounded activation");
                }
                Require(MegaBlockFactory.WarpScreens.Count==0,"Zone enabled its entire screen");
            }
            finally { Settings.Current.WarpJump=setting; MegaBlockFactory.ResetScreens(); foreach(int screen in saved) MegaBlockFactory.WarpScreens.Add(screen); }
            Console.WriteLine("[OK] Warp Zone: local bounds, grounded idle/exit/walk-off, airborne entry, jump/fall parity, global-off activation and authored modifier policy");
        }
    }
}

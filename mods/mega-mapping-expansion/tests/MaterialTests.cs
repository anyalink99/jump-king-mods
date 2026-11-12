using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JKRuntime;
using JKRuntime.Geometry;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private sealed class MaterialA:BoxBlock {internal MaterialA(Rectangle r):base(r){} }
        private sealed class MaterialB:BoxBlock {internal MaterialB(Rectangle r):base(r){} }
        private sealed class MaterialFactory:IBlockFactory
        {
            internal int Calls;
            public bool CanMakeBlock(Color c,JumpKing.Workshop.Level l){return c==new Color(31,41,51)||c==new Color(31,41,52);}
            public bool IsSolidBlock(Color c){return true;}
            public IBlock GetBlock(Color c,Rectangle r,JumpKing.Workshop.Level l,LevelTexture t,int s,int x,int y)
            {Calls++;return c.B==51?(IBlock)new MaterialA(r):new MaterialB(r);}
        }
        private sealed class MaterialHandler:IBlockBehaviour
        {
            internal bool Multiply,Fail;
            internal MaterialHandler(bool multiply){Multiply=multiply;}
            public float BlockPriority{get{return Multiply?100:0;}}
            public bool IsPlayerOnBlock{get;set;}
            public float ModifyXVelocity(float value,BehaviourContext c){if(Fail)throw new InvalidOperationException("fixture failure");return Multiply?value*3:value+2;}
            public float ModifyYVelocity(float value,BehaviourContext c){return value;}
            public float ModifyGravity(float value,BehaviourContext c){return value;}
            public bool AdditionalXCollisionCheck(AdvCollisionInfo info,BehaviourContext c){return false;}
            public bool AdditionalYCollisionCheck(AdvCollisionInfo info,BehaviourContext c){return false;}
            public bool ExecuteBlockBehaviour(BehaviourContext c){return true;}
        }
        private sealed class PureMaterialFactory:IBlockFactory
        {
            public bool CanMakeBlock(Color c,JumpKing.Workshop.Level l){return c==new Color(31,41,51)||c==new Color(31,41,52);}
            public bool IsSolidBlock(Color c){return false;}
            public IBlock GetBlock(Color c,Rectangle r,JumpKing.Workshop.Level l,LevelTexture t,int s,int x,int y){return c.B==51?(IBlock)new MaterialA(r):new MaterialB(r);}
        }
        private static string MaterialXml(string extra="")
        {return "<Materials version='1'><Source id='a' color='#1F2933' sizing='rectangle'/><Source id='b' color='#1F2934' sizing='rectangle'/><Material id='mix' collision='trigger'><Use id='add' source='a'/><Use id='multiply' source='b' after='add'/></Material>"+extra+"</Materials>";}
        private static void MaterialChecks(string scratch)
        {
            var plan=MaterialPlan.Parse(MaterialXml("<Paint color='#ABCDEF' material='mix' mode='connected'/><Area id='zone' material='mix' rect='50,50,40,40'><Exclude rect='60,60,20,20'/></Area>"),1);
            Require(plan.Require("mix").Layers.Select(l=>l.Id).SequenceEqual(new[]{"add","multiply"}),"material dependency order is independent of declaration sorting");
            var shape=MaterialGeometry.Compile(plan.Areas[0],scratch);
            Require(shape.Sum(r=>r.Width*r.Height)==1200&&!shape.Any(r=>r.X<=65&&r.X+r.Width>65&&r.Y<=65&&r.Y+r.Height>65),"compound area holes retain exact empty space");
            foreach(string bad in new[]{MaterialXml().Replace("after='add'","before='add' after='add'"),MaterialXml().Replace("after='add'","after='missing'"),MaterialXml("<Area id='bad' material='mix' rect='479,0,2,8'/>"),MaterialXml().Replace("collision='trigger'","walking='deny'")})
            {bool failed=false;try{MaterialPlan.Parse(bad,1);}catch(InvalidDataException){failed=true;}Require(failed,"invalid geometry, cycles, references and ad hoc restrictions are rejected");}
            var inherited=MaterialPlan.Parse(MaterialXml("<Material id='child' extends='mix' collision='solid'><Use id='multiply' source='b' before='add'/></Material>"),1);
            Require(inherited.Require("child").Layers.First().Id=="multiply"&&inherited.Require("mix").Layers.First().Id=="add","inherited materials have independent overrides");
            var factories=(List<IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",OwnedPatches.Members).GetValue(null);var factory=new MaterialFactory();factories.Add(factory);
            var previous=NativeMaterials.Loaded;var priorCurrent=NativeMaterials.Current;
            try
            {
                var world=new NativeMaterials(plan,scratch);NativeMaterials.Loaded=world;MaterialContacts.Clear();
                var blocks=world.Create("mix","zone",0,new Rectangle(100,100,40,40),null,null,0,0,2);
                Require(blocks[0].GetType()==typeof(MaterialA)&&blocks[1].GetType()==typeof(MaterialB),"composite publishes both exact source types");
                Rectangle overlap;Require(blocks.All(b=>b.Intersects(new Rectangle(110,110,1,1),out overlap)==BlockCollisionType.Collision_NonBlocking),"trigger composition does not add solid support");
                var info=new AdvCollisionInfo(blocks.ToList(),false,SlopeType.None,Vector2.Zero);
                Require(info.IsCollidingWith<MaterialA>()&&info.IsCollidingWith<MaterialB>(),"native typed queries retain every material layer");
                int calls=factory.Calls;var instance=world.Instances.Single();instance.PrepareMove();string id=MaterialBlocks.IdentityOf(blocks[0]);instance.Move(10,-10);
                Require(factory.Calls==calls&&blocks[0].GetRect()==new Rectangle(110,90,40,40)&&MaterialBlocks.IdentityOf(blocks[0])==id,"moving material keeps identity and never reruns its factory");instance.Move(0,0);
                var body=new BodyComp(new Vector2(110,110),18,26);var add=new MaterialHandler(false);var multiply=new MaterialHandler(true);
                body.RegisterBlockBehaviour(typeof(MaterialA),add);body.RegisterBlockBehaviour(typeof(MaterialB),multiply);
                var handlers=(LinkedList<IBlockBehaviour>)typeof(BodyComp).GetField("m_blockBehaviours",OwnedPatches.Members).GetValue(body);var original=handlers.ToArray();
                using(world.Activate(body))
                {
                    // remove unrelated native material effects from this arithmetic fixture
                    foreach(var h in handlers.Where(h=>h!=add&&h!=multiply).ToArray())handlers.Remove(h);
                    var baseline=handlers.ToArray();body.Velocity=new Vector2(1,0);body.Position=new Vector2(110,110);
                    new UpdateXPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(new BehaviourContext(body));
                    Require(body.Position.X==119,"local composition runs add then multiply: (1+2)*3");
                    Require(handlers.SequenceEqual(baseline),"native handler order is restored after the phase");
                    multiply.Fail=true;bool threw=false;try{new UpdateXPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(new BehaviourContext(body));}catch(InvalidOperationException){threw=true;}
                    Require(threw&&handlers.SequenceEqual(baseline),"exception unwinding restores handler order");multiply.Fail=false;
                    world.Command("mix","disable");Require(blocks.All(b=>b.Intersects(body.GetHitbox(),out overlap)==BlockCollisionType.NoCollision),"material command disables every layer");
                    world.Command("mix","reset");Require(blocks.Any(b=>b.Intersects(body.GetHitbox(),out overlap)!=BlockCollisionType.NoCollision),"material reset restores authored activation");
                    handlers.Clear();foreach(var h in original)handlers.AddLast(h);
                }
                var packet=world.Capture();world.Command("mix","disable");world.Restore(packet);Require(instance.Enabled,"material state restores independently of native handler state");
                var codec=new JKRuntime.World.WorldStateCodec(typeof(MaterialPacket),true);Require(codec.Capture(packet).SequenceEqual(codec.Capture(world.Capture())),"material snapshot codec preserves identity and flags");
                var malformed=(MaterialPacket)world.Capture();malformed.Enabled=new bool[0];bool rejected=false;try{world.Restore(malformed);}catch(InvalidDataException){rejected=true;}Require(rejected&&instance.Enabled,"bad material packets leave activation unchanged");
                calls=factory.Calls;rejected=false;try{MaterialBlocks.ValidateDetached(factory,new Color(31,41,51),new Rectangle(0,0,8,8),null,null,0,0,0);}catch(InvalidOperationException){rejected=true;}
                Require(rejected&&factory.Calls==calls,"relocation audit rejects external writes without running them");

                foreach(string slope in new[]{"TopLeft","TopRight","BottomLeft","BottomRight"})
                {
                    var shaped=new NativeMaterials(MaterialPlan.Parse(MaterialXml().Replace("collision='trigger'","collision='solid' shape='"+slope+"'"),1),scratch);NativeMaterials.Loaded=shaped;
                    var pieces=shaped.Create("mix","slope",0,new Rectangle(100,100,32,32),null,null,0,0,8);var shapeInstance=shaped.Instances.Single();shapeInstance.PrepareMove();
                    var baseline=Enumerable.Range(0,32*32).Select(n=>pieces.Select(b=>{Rectangle hit;return b.Intersects(new Rectangle(100+n%32,100+n/32,1,1),out hit);}).ToArray()).ToArray();
                    shapeInstance.Move(40,20);
                    Require(Enumerable.Range(0,1024).All(n=>baseline[n].SequenceEqual(pieces.Select(b=>{Rectangle hit;return b.Intersects(new Rectangle(140+n%32,120+n/32,1,1),out hit);}))),"moving "+slope+" retains its source mask and support triangle");
                }
                var palette=MaterialPlan.Parse(MaterialXml("<Paint color='#ABCDEF' material='mix' mode='connected'/>"),2);
                var texture=new PixelLevelTexture(new MapLayout{Screens=2,Columns=1,Rows=2,CellWidth=240,CellHeight=180},Enumerable.Repeat(new Color(171,205,239),240*360).ToArray());
                var paintWorld=new NativeMaterials(palette,scratch);NativeMaterials.Loaded=paintWorld;texture.Materials=paintWorld;
                var output=ReadPixelBlocks(texture,1);
                Require(output.Length==2&&output.All(b=>b.GetRect()==new Rectangle(0,-360,480,360)),"240x180 paint compacts a full screen into exact native layers with correct screen coordinates");
                var replace=new NativeMaterials(MaterialPlan.Parse(MaterialXml("<Area id='cut' material='mix' rect='120,120,8,8' mode='replace'/>"),1),scratch);NativeMaterials.Loaded=replace;
                var retained=new List<IBlock>{new BoxBlock(new Rectangle(100,100,40,40))};replace.AreasScreen(LevelTexture.FromDimensions(60,45),null,0,8,retained);
                Rectangle overlap2;
                Require(!retained.Any(b=>b.Intersects(new Rectangle(122,122,1,1),out overlap2)==BlockCollisionType.Collision_Blocking)&&retained.Any(b=>b.Intersects(new Rectangle(119,122,1,1),out overlap2)==BlockCollisionType.Collision_Blocking),"partial region replacement carves only selected pixels out of a larger collider");
                Require(retained.Sum(b=>{var r=b.GetRect();Rectangle hit;return b.Intersects(r,out hit)==BlockCollisionType.Collision_Blocking?r.Width*r.Height:0;})==1536,"partial replacement preserves the rest of the source area exactly");
                MaterialOverlapChecks(scratch);
            }
            finally{factories.Remove(factory);NativeMaterials.Loaded=previous;NativeMaterials.Current=priorCurrent;MaterialContacts.Clear();}
            MovingMaterialChecks(scratch);
            MaterialMaskChecks(scratch);
            MaterialMarkerChecks();
            Console.WriteLine("[OK] Materials: strict authoring, inheritance, holes, exact types, motion identity, local order and rollback");
        }
        private static void MaterialMarkerChecks()
        {
            var old=MovingPlatformScan.Plan;var cells=MovingPlatformScan.Cells.ToArray();
            try
            {
                MovingPlatformScan.Plan=MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='m' axis='horizontal' distance='20'/><Profile id='p' motion='m'/><Paint color='#000000' profile='p' replace='true'/></MovingPlatforms>",1);MovingPlatformScan.Cells.Clear();
                var data=new Color[240*180];for(int y=100;y<104;y++)for(int x=50;x<74;x++)data[y*240+x]=Color.Black;
                var texture=new PixelLevelTexture(new MapLayout{Screens=1,Columns=1,Rows=1,CellWidth=240,CellHeight=180},data);
                Require(ReadPixelBlocks(texture,0).Length==0&&MovingPlatformScan.Cells.Sum(c=>c.Rect.Width*c.Rect.Height)==384,"explicit moving palette replacement consumes native black without leaving stationary collision");
            }
            finally{MovingPlatformScan.Plan=old;MovingPlatformScan.Cells.Clear();MovingPlatformScan.Cells.AddRange(cells);}
        }
        private static void MaterialOverlapChecks(string scratch)
        {
            var plan=MaterialPlan.Parse(MaterialXml("<Material id='reverse' extends='mix' priority='10'><Use id='multiply' source='b' before='add'/></Material>"),1);
            var world=new NativeMaterials(plan,scratch);NativeMaterials.Loaded=world;
            world.Create("mix","low",0,new Rectangle(100,100,40,40),null,null,0,0,8);world.Create("reverse","high",0,new Rectangle(100,100,40,40),null,null,0,0,8);
            var body=new BodyComp(new Vector2(110,110),18,26);var add=new MaterialHandler(false);var multiply=new MaterialHandler(true);
            body.RegisterBlockBehaviour(typeof(MaterialA),add);body.RegisterBlockBehaviour(typeof(MaterialB),multiply);
            var handlers=(LinkedList<IBlockBehaviour>)typeof(BodyComp).GetField("m_blockBehaviours",OwnedPatches.Members).GetValue(body);foreach(var h in handlers.Where(h=>h!=add&&h!=multiply).ToArray())handlers.Remove(h);
            using(world.Activate(body,false))
            {
                body.Velocity=new Vector2(1,0);new UpdateXPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(new BehaviourContext(body));
                Require(body.Position.X==115&&world.Describe().Any(s=>s.Contains("yields to higher priority")),"overlapping material conflict uses explicit priority and reports the rejected constraint");
                world.Command("reverse","disable");body.Position=new Vector2(110,110);new UpdateXPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(new BehaviourContext(body));Require(body.Position.X==119,"removing the higher-priority region restores the remaining composition order");
            }
            var separate=new NativeMaterials(MaterialPlan.Parse(MaterialXml("<Material id='alpha' collision='trigger'><Use id='a' source='a'/></Material><Material id='beta' collision='trigger'><Use id='b' source='b'/></Material>"),1),scratch);NativeMaterials.Loaded=separate;
            separate.Create("alpha","alpha-zone",0,new Rectangle(100,100,40,40),null,null,0,0,8);separate.Create("beta","beta-zone",0,new Rectangle(100,100,40,40),null,null,0,0,8);
            using(separate.Activate(body,false))
            {body.Position=new Vector2(110,110);body.Velocity=new Vector2(1,0);new UpdateXPositionFromVelocityBehaviour(handlers).ExecuteBehaviour(new BehaviourContext(body));Require(body.Position.X==115,"unconstrained overlapping single-source materials retain native handler priority");}
        }
        private static void MaterialMaskChecks(string scratch)
        {
            string root=Path.Combine(scratch,"material-mask"),mask=Path.Combine(root,"mask.png");Directory.CreateDirectory(Path.Combine(root,"screens/mega-mapping-expansion"));
            File.WriteAllText(Path.Combine(root,MaterialPlan.PathName),MaterialXml("<Area id='mask' material='mix' rect='0,0,20,20' shape='mask' mask='mask.png'/>"));
            using(var image=new System.Drawing.Bitmap(2,2)){image.SetPixel(0,0,System.Drawing.Color.White);image.Save(mask);}
            var first=MaterialPlan.Read(root,1);Require(MaterialGeometry.Compile(first.Areas[0],root).Sum(r=>r.Width*r.Height)==100,"mask alpha and nearest-neighbor scaling preserve exact region area");
            using(var image=new System.Drawing.Bitmap(2,2)){image.SetPixel(0,0,System.Drawing.Color.White);image.SetPixel(1,1,System.Drawing.Color.White);image.Save(mask);}
            Require(MaterialPlan.Read(root,1).Identity!=first.Identity,"changing only mask pixels invalidates material revision");
        }
        private static void MovingMaterialChecks(string scratch)
        {
            var factories=(List<IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",OwnedPatches.Members).GetValue(null);var factory=new PureMaterialFactory();factories.Add(factory);
            var screens=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);var count=typeof(LevelManager).GetField("_total_screens",OwnedPatches.Members);var old=screens.GetValue(null);var oldCount=count.GetValue(null);var prior=NativeMaterials.Loaded;
            try
            {
                var materials=new NativeMaterials(MaterialPlan.Parse(MaterialXml(),1),scratch){Texture=LevelTexture.FromDimensions(60,45)};NativeMaterials.Loaded=materials;
                var screen=new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);screens.SetValue(null,new[]{screen});count.SetValue(null,1);
                var plan=MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='m' axis='horizontal' distance='100' speed='600'/><Profile id='p' motion='m' collision='trigger' material='mix'/><Platform id='hazard' profile='p' rect='100,100,2,30'/></MovingPlatforms>",1);
                var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));player.m_body=new BodyComp(new Vector2(130,100),18,26);
                using(var moving=new NativeMovingPlatforms(plan,scratch,false))using(moving.Activate(player,false))
                {
                    materials.Prepare();using(materials.Activate(player.m_body,false))
                    {
                        moving.Tick(player.m_body,.1);Require(player.m_body.Position==new Vector2(130,100),"moving triggers never push or carry the player");
                        MaterialContacts.Body=player.m_body;
                        var info=screen.GetCollisionInfo(player.m_body.GetHitbox());Require(info.IsCollidingWith<MaterialA>()&&info.IsCollidingWith<MaterialB>(),"fast swept trigger retains both native contacts until body processing");
                        MaterialContacts.ClearSweeps(player.m_body);Require(!screen.GetCollisionInfo(player.m_body.GetHitbox()).IsCollidingWith<MaterialA>(),"swept contact expires without a persistent phantom trigger");MaterialContacts.Body=null;
                    }
                }
                Require(materials.Instances.Count==0&&!screen.GetCollisionInfo(new Rectangle(150,100,30,30)).GetCollidedBlocks().Any(),"attempt teardown removes moving materials and their instances");
                var oneWay=new NativeMaterials(MaterialPlan.Parse(MaterialXml().Replace("collision='trigger'","collision='oneWay'"),1),scratch);NativeMaterials.Loaded=oneWay;
                var deck=oneWay.Create("mix","deck",0,new Rectangle(100,200,48,8),null,null,0,0,8);
                screens.SetValue(null,new[]{new LevelScreen(0,deck,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)});JumpKing.Camera.UpdateCamera(Point.Zero);
                var falling=PlatformBody(new Vector2(110,171));falling.Velocity.Y=4;
                using(oneWay.Activate(falling,false))
                {
                    PlatformBodyStep(falling);Require(falling.Position.Y==174&&falling.IsOnGround,"stationary one-way material supports a downward landing without a moving-platform owner");
                    falling.Position=new Vector2(110,205);falling.Velocity.Y=-8;PlatformBodyStep(falling);Require(falling.Position.Y<200&&!falling.IsOnGround,"stationary one-way material permits upward passage");
                    oneWay.Command("mix","disable");falling.Position=new Vector2(110,202);bool rejected=false;try{oneWay.Command("mix","enable");}catch(InvalidOperationException){rejected=true;}Require(rejected&&!oneWay.Instances.Single().Enabled,"solid enable validation is atomic while the player overlaps support");
                }
            }
            finally{MaterialContacts.Body=null;factories.Remove(factory);screens.SetValue(null,old);count.SetValue(null,oldCount);NativeMaterials.Loaded=prior;MaterialContacts.Clear();}
        }
        private static bool UnpatchedMaterialFlag(BodyComp body,Type type)
        {var lookup=(Dictionary<Type,IBlockBehaviour>)typeof(BodyComp).GetField("m_blockBehaviourLookup",OwnedPatches.Members).GetValue(body);IBlockBehaviour handler;return lookup.TryGetValue(type,out handler)&&handler.IsPlayerOnBlock;}
        private static void MaterialProviderChecks(string assemblyPath)
        {
            var assembly=Assembly.LoadFrom(assemblyPath);
            var factory=(IBlockFactory)Activator.CreateInstance(assembly.GetType("JumpKing_Expansion_Blocks.BlockFactory",true));
            var bounds=new Rectangle(100,100,40,40);
            var level=(JumpKing.Workshop.Level)FormatterServices.GetUninitializedObject(typeof(JumpKing.Workshop.Level));
            var infinity=factory.GetBlock(new Color(64,255,255),bounds,level,null,0,0,0);
            var revoke=factory.GetBlock(new Color(234,234,240),bounds,level,null,0,0,0);
            Require(infinity.GetType().FullName=="JumpKing_Expansion_Blocks.Blocks.InfinityJump"&&revoke.GetType().FullName=="JumpKing_Expansion_Blocks.Blocks.RevokeWalking","installed factory identifies the requested Infinity Jump and Revoke Walking colors");
            var body=PlatformBody(new Vector2(110,110));var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));player.m_body=body;
            var infinityHandler=(IBlockBehaviour)Activator.CreateInstance(assembly.GetType("JumpKing_Expansion_Blocks.Behaviours.InfinityJump",true),new object[]{LevelManager.Instance});
            var revokeHandler=(IBlockBehaviour)Activator.CreateInstance(assembly.GetType("JumpKing_Expansion_Blocks.Behaviours.RevokeWalking",true),true);
            body.RegisterBlockBehaviour(infinity.GetType(),infinityHandler);body.RegisterBlockBehaviour(revoke.GetType(),revokeHandler);
            var entry=assembly.GetType("JumpKing_Expansion_Blocks.ModEntry",true);entry.GetProperty("Player").SetValue(null,player,null);
            entry.GetField("originalIsOnBlock",OwnedPatches.Members).SetValue(null,typeof(Tests).GetMethod("UnpatchedMaterialFlag",OwnedPatches.Members));
            var harmony=new Harmony("mme.material-provider-check");
            harmony.Patch(typeof(BodyComp).GetMethod("IsOnBlock",new[]{typeof(Type)}),postfix:new HarmonyMethod(entry.GetMethod("IsOnBlockPostfix")));
            var prefix=assembly.GetType("JumpKing_Expansion_Blocks.Patches.PatchedWalk",true).GetMethod("PrefixRun",OwnedPatches.Members);
            var screens=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);var count=typeof(LevelManager).GetField("_total_screens",OwnedPatches.Members);var old=screens.GetValue(null);var oldCount=count.GetValue(null);
            var factories=(List<IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",OwnedPatches.Members).GetValue(null);factories.Add(factory);var previous=NativeMaterials.Loaded;
            try
            {
                var plan=MaterialPlan.Parse(MaterialXml().Replace("#1F2933","#40FFFF").Replace("#1F2934","#EAEAF0"),1);
                var materials=new NativeMaterials(plan,"");NativeMaterials.Loaded=materials;
                var composite=materials.Create("mix","request",0,bounds,level,LevelTexture.FromDimensions(60,45),0,0,8);
                Require(composite[0].GetType()==infinity.GetType()&&composite[1].GetType()==revoke.GetType(),"MME composition creates the installed provider's exact block types");
                foreach(var source in plan.Sources.Values)MaterialBlocks.ValidateDetached(factory,NativeMaterials.Colour(source.Color),bounds,level,LevelTexture.FromDimensions(60,45),0,0,0);
                foreach(bool combined in new[]{false,true})
                {
                    JumpKing.Camera.UpdateCamera(Point.Zero);
                    var blocks=combined?composite:new[]{infinity};var screen=new LevelScreen(0,blocks,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
                    screens.SetValue(null,new[]{screen});count.SetValue(null,1);
                    var context=new BehaviourContext(body);typeof(BehaviourContext).GetMethod("ClearCollisionForNewFrame",OwnedPatches.Members).Invoke(context,null);
                    typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo",OwnedPatches.Members).Invoke(context.CollisionInfo,new object[]{new AdvCollisionInfo(blocks.ToList(),false,SlopeType.None,Vector2.Zero)});
                    infinityHandler.ExecuteBlockBehaviour(context);revokeHandler.ExecuteBlockBehaviour(context);
                    Require(infinityHandler.IsPlayerOnBlock,"native query sees Infinity Jump in the composite");
                    typeof(BodyComp).GetField("_is_on_ground",OwnedPatches.Members).SetValue(body,true);
                    body.Velocity=new Vector2(3,0);object[] args={BehaviorTree.BTresult.Success};bool runs=(bool)prefix.Invoke(null,args);
                    Require(runs==!combined,"native Revoke Walking veto is retained only when its source contact is present");
                    Require((bool)typeof(BodyComp).GetMethod("IsOnBlock",new[]{typeof(Type)}).Invoke(body,new object[]{typeof(IceBlock)}),"Infinity Jump also advertises native ice through the installed provider patch");
                    if(combined)Require(body.Velocity.X==3,"provider deliberately preserves ice momentum; a composition must not silently change this");
                }
            }
            finally{factories.Remove(factory);NativeMaterials.Loaded=previous;MaterialContacts.Clear();harmony.UnpatchAll(harmony.Id);entry.GetProperty("Player").SetValue(null,null,null);screens.SetValue(null,old);count.SetValue(null,oldCount);}
            Console.WriteLine("[OK] Installed Expansion Blocks: exact colors, composite contact, walking veto and inherited ice momentum");
        }
    }
}

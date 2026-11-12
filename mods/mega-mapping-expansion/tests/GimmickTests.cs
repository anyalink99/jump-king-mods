using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;
using JKRuntime;
using JKRuntime.Gameplay;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private sealed class GimmickPlayer : PlayerEntity
        {protected override void Update(float delta){}protected override void OnDestroy(){}}
        private sealed class GimmickBrain : BehaviorTree.IBTnode
        {protected override BehaviorTree.BTresult MyRun(BehaviorTree.TickData data){return BehaviorTree.BTresult.Running;}}
        private static void GimmickChecks(string scratch)
        {
            PaintedAreaChecks(scratch);
            const string xml=@"<MappingSettings version='1'>
<Components><Component id='anchor' kind='transform'><Value name='position' value='100,100'/></Component><Component id='hold' kind='playerControl'><Value name='transform' value='anchor'/></Component><Component id='other' kind='playerControl'/></Components>
<Graph><Node id='held' op='input' source='hold.captured'/><Node id='input' op='input' source='input.direction'/><Node id='unit' op='normalize' inputs='input'/><Node id='speed' op='constant' value='420'/><Node id='launch' op='multiply' inputs='unit;speed' units='px/s'/><Node id='entry' op='input' source='hold.entryVelocity'/><Node id='sign' op='sign' inputs='entry'/></Graph>
<Trees><Tree id='test' event='touch' stopEvent='abort'><Node op='sequence'>
<Node op='action'><Action op='capturePlayer' target='hold'/></Node><Node op='check' source='held'/><Node op='wait' seconds='0.1'/>
<Node op='action'><Action op='setPlayerVelocity' target='hold' source='launch'/></Node><Node op='action'><Action op='releasePlayer' target='hold'/></Node>
</Node></Tree></Trees></MappingSettings>";
            var flags=OwnedPatches.Members;var gameField=typeof(Game1).GetField("_instance",flags);var screensField=typeof(LevelManager).GetField("m_screens",flags);var totalField=typeof(LevelManager).GetField("_total_screens",flags);
            var oldGame=gameField.GetValue(null);var oldScreens=screensField.GetValue(null);var oldTotal=totalField.GetValue(null);
            try
            {
                var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));game.contentManager=(JumpKing.JKContentManager)FormatterServices.GetUninitializedObject(typeof(JumpKing.JKContentManager));gameField.SetValue(null,game);
                screensField.SetValue(null,new[]{new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)});totalField.SetValue(null,1);
                var player=(GimmickPlayer)FormatterServices.GetUninitializedObject(typeof(GimmickPlayer));
                typeof(Entity).GetField("m_components",flags).SetValue(player,new List<Component>());
                player.m_body=new BodyComp(new Vector2(91,87),18,26);var brain=new BehaviorTreeComp(new GimmickBrain());
                typeof(PlayerEntity).GetField("m_bt",flags).SetValue(player,brain);player.AddComponents(player.m_body,new InputComponent(),brain);
                using(var world=new RuntimeScope())
                {
                    JKRuntime.Presentation.PlayerVisuals.Prepare(world);
                    using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(xml))))
                    using(var native=new NativePlayerActions(scene,player))
                    {
                        HiddenWallObservations.Validate(scene.Definition);
                        NativePlayerActions.Prepare(scene);scene.InvokeNative=a=>{Require(native.Handle(a),"Generic player actions reach their native service");};
                        Action start=()=>{player.m_body.Velocity=new Vector2(-3,-2);scene.Signals.Emit("touch","fixture","player",1,scene.GameplayTime);scene.Signals.Publish("input.direction",SceneValue.Vector(1,0),ObservationQuality.Exact);scene.Tick(1.0/60,1.0/60);};
                        start();Require(!player.m_body.Enabled&&!brain.Enabled&&scene.Signals.Read("hold.captured").Value.X==1,"Capture suspends native movement and reports ownership");
                        Require(scene.Graph.Read("sign").Equals(SceneValue.Vector(-1,-1)),"Sign keeps both negative entry axes");
                        var held=player.m_body.Position;
                        for(int i=0;i<5;i++)scene.Tick(1.0/60,1.0/60);
                        Require(player.m_body.Position==held&&!brain.Enabled,"Tree holds for the configured duration");
                        scene.Tick(0,0);Require(!brain.Enabled,"Paused scene does not expire a hold");
                        scene.Signals.Publish("input.direction",SceneValue.Vector(-1,-1),ObservationQuality.Exact);scene.Tick(1.0/60,1.0/60);
                        Require(brain.Enabled&&player.m_body.Enabled&&Math.Abs(player.m_body.Velocity.Length()-7)<.00001&&player.m_body.Velocity.Y<0,"Release samples final input and preserves normalized diagonal speed");
                        Require(PlayerControl.Owner(player.m_body)==null,"Tree completion returns player ownership");
                        start();var programState=scene.Capture();var nativeState=native.Capture();scene.Signals.Emit("abort","fixture","player",1,scene.GameplayTime);scene.Tick(1.0/60,1.0/60);
                        Require(brain.Enabled&&player.m_body.Enabled&&player.m_body.Velocity==Vector2.Zero,"Tree cancellation releases the player without an impulse");
                        // native snapshots restore enabled flags before the scene participant
                        brain.Enabled=false;player.m_body.Enabled=false;scene.Restore(programState);native.Restore(nativeState);
                        Require(!brain.Enabled&&PlayerControl.Owner(player.m_body)!=null,"Restored tree reacquires its held player");
                        scene.CancelPlayerTrees();Require(brain.Enabled&&player.m_body.Enabled,"Cancellation after restore restores the original enabled flags");
                        using(PlayerControl.Acquire(player.m_body,"foreign"))
                        {start();Require(player.m_body.Enabled&&scene.Signals.Read("hold.captured").Value.X==0,"Another mechanic's ownership rejects capture without freezing the player");}
                        native.Handle(new NativeSceneAction{Operation="setPlayerVelocity",Target="other",Value=SceneValue.Vector(120,-60)});
                        native.Handle(new NativeSceneAction{Operation="addPlayerVelocity",Target="other",Value=SceneValue.Vector(60,0)});
                        Require(player.m_body.Velocity==new Vector2(3,-1),"The same action service supports unheld velocity changes and additive impulses");
                        start();native.Dispose();Require(brain.Enabled&&player.m_body.Enabled&&PlayerControl.Owner(player.m_body)==null,"Attempt teardown releases a pending hold");
                    }
                }
            }
            finally{gameField.SetValue(null,oldGame);screensField.SetValue(null,oldScreens);totalField.SetValue(null,oldTotal);}
            foreach(string action in new[]{"<Action op='capturePlayer' target='hold' value='1'/>","<Action op='setPlayerVelocity' target='hold' value='7'/>","<Action op='setPlayerVelocity' target='anchor' value='1,2'/>"})
            {
                bool refused=false;try{using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(XDocument.Parse(xml.Replace("<Action op='capturePlayer' target='hold'/>",action))))) {}}
                catch(InvalidDataException){refused=true;}catch(FormatException){refused=true;}Require(refused,"Invalid player actions fail during scene compilation");
            }
        }
        private static void PaintedAreaChecks(string scratch)
        {
            string root=Path.Combine(scratch,"paint");Directory.CreateDirectory(root);
            if(File.Exists(Path.Combine(root,MappingFiles.Transitions)))File.Delete(Path.Combine(root,MappingFiles.Transitions));
            string xml=@"<MappingSettings version='1'><MapLayout version='2' source='atlas' image='level' screens='2' columns='1' rows='2' cellWidth='60' cellHeight='45'/>
<Paints><Paint id='zone' color='#123456'><Components><Component id='${paint.id}' kind='transform'><Value name='position' value='${paint.position}'/><Value name='screen' value='${paint.screen}'/></Component><Component id='${paint.id}.sensor' kind='sensor'><Value name='transform' value='${paint.id}'/><Value name='size' value='${paint.size}'/></Component></Components></Paint></Paints></MappingSettings>";
            File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),xml);
            using(var image=new System.Drawing.Bitmap(60,90))
            {
                var color=System.Drawing.Color.FromArgb(255,18,52,86);
                image.SetPixel(2,3,color);image.SetPixel(3,3,color);image.SetPixel(2,4,color);image.SetPixel(3,4,color);image.SetPixel(7,50,color);
                image.Save(Path.Combine(root,"level.png"));
            }
            var document=new RecipeCompiler(PrimitiveRegistry.Create()).Read(root);
            using(var scene=new SceneProgram(document))
            {
                Require(scene.Components.Count==4,"Paint creates independent components for disconnected rectangles and screens");
                Require(scene.Properties.Read("zone.s1.x16.y24","position").Equals(SceneValue.Vector(24,32)),"Paint center uses collision resolution, not image coordinates");
                Require(scene.Properties.Read("zone.s1.x16.y24.sensor","size").Equals(SceneValue.Vector(16,16)),"Painted area dimensions come from its filled pixels");
                Require(scene.Properties.Read("zone.s2.x56.y40","screen").X==2,"Atlas screen addresses stay one-based");
            }
            File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),xml.Replace("color='#123456'","color='#123456' mode='pixels'"));
            Require(new RecipeCompiler(PrimitiveRegistry.Create()).Read(root).Components.Length==10,"Pixel mode keeps touching markers as separate instances");
            var layout=MapLayout.Read(root);var pixels=PaintedAreas.ReadPixels(root,layout);pixels[4*60+3]=0;
            var shapes=PaintedAreas.Scan(layout,pixels,PaintedAreas.Color("#123456"),"rectangles");
            Require(shapes.Sum(s=>s.Width*s.Height)==4*64,"Rectangle decomposition preserves holes instead of using a bounding box");
            var cache=Path.Combine(root,"cache");var old=ConstructionSceneFile.Read(root,false,cache);
            using(var image=new System.Drawing.Bitmap(60,90)){image.SetPixel(1,1,System.Drawing.Color.FromArgb(255,18,52,86));image.Save(Path.Combine(root,"level.png"));}
            var changed=ConstructionSceneFile.Read(root,false,cache);Require(changed.Components.Length==2&&changed.Components[0].Id!=old.Components[0].Id,"Changing only painted pixels invalidates the compiled scene cache");
            Require(PaintedAreas.Palette(RecipeCompiler.RawSource(root)).Contains(0xff123456),"Paint palette uses arbitrary authored RGB colors");
            string routes=Path.Combine(root,MappingFiles.Transitions);Directory.CreateDirectory(Path.GetDirectoryName(routes));
            File.WriteAllText(routes,"<Transitions version='1'><Pool id='rooms' screens='2' mode='random' refresh='event'/><Exit screen='1' side='right' pool='rooms'/><Paint id='switch' color='#123456'><Reroll id='${paint.id}' on='touch' screen='${paint.screen}' x='${paint.x}' y='${paint.y}' width='${paint.width}' height='${paint.height}' pools='rooms'/></Paint></Transitions>");
            var trigger=MapLayout.Read(root).Transitions.Triggers.Single();Require(trigger.X==8&&trigger.Y==8&&trigger.Width==8&&trigger.Height==8,"Native reroll areas use the same pixel geometry as scene components");
            var code=new Color(18,52,86);var data=new Color[layout.ImageWidth*layout.ImageHeight];data[0]=code;data[1]=Color.Black;
            var texture=new PixelLevelTexture(layout,data);texture.PaintColors.Add(code.PackedValue);
            var blocks=ReadPixelBlocks(texture,0);Require(texture.GetColor(0,0,0)==Color.Transparent&&blocks.Length==1&&blocks[0].GetRect()==new Rectangle(8,0,8,8),"Consumed markers create no native block and preserve adjacent terrain");
        }
    }
}

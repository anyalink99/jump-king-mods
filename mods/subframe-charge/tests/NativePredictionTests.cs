using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private static void NativePredictionTests()
    {
        var screenField=AccessTools.Field(typeof(LevelManager),"m_screens");
        var totalField=AccessTools.Field(typeof(LevelManager),"_total_screens");
        var cameraField=AccessTools.Field(typeof(Camera),"_current_screen");
        object oldScreens=screenField.GetValue(null),oldTotal=totalField.GetValue(null),oldCamera=cameraField.GetValue(null);
        var update=AccessTools.Method(typeof(BodyComp),"UpdateInternal");
        int samples=0; float worst=0; double totalError=0;
        try
        {
            totalField.SetValue(null,1);cameraField.SetValue(null,0);
            var scenes=new List<IBlock[]>();
            foreach(var type in new[]{SlopeType.TopLeft,SlopeType.TopRight,SlopeType.BottomLeft,SlopeType.BottomRight})
                scenes.Add(new IBlock[]{new SlopeBlock(new Rectangle(80,160,96,96),type)});
            scenes.Add(Enumerable.Range(0,20).Select(i=>(IBlock)new SlopeBlock(new Rectangle(160,80+i*8,8,8),SlopeType.TopLeft))
                .Concat(new IBlock[]{new IceBlock(new Rectangle(0,240,240,8))}).ToArray());
            scenes.Add(new IBlock[]{new SandBlock(new Rectangle(0,160,240,96))});
            scenes.Add(new IBlock[]{new SandBlock(new Rectangle(100,80,80,176))});
            scenes.Add(new IBlock[]{new SandBlock(new Rectangle(0,160,240,96)),new WaterBlock(new Rectangle(0,0,240,300))});
            foreach(int shift in new[]{0,-20880})
            foreach(var scene in scenes)
            foreach(float x in new[]{62f,100f,142f,178f})
            foreach(float dx in new[]{-3.5f,0f,3.5f})
            foreach(float dy in new[]{-10f,0f,10f})
            {
                var blocks=scene.Select(block=>{
                    var r=block.GetRect();r.Y+=shift;
                    if(block is SlopeBlock)return (IBlock)new SlopeBlock(r,((SlopeBlock)block).GetSlopeType());
                    if(block is SandBlock)return new SandBlock(r);
                    if(block is WaterBlock)return new WaterBlock(r);
                    return new IceBlock(r);
                }).ToArray();
                screenField.SetValue(null,new[]{new LevelScreen(0,blocks,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)});
                var body=new BodyComp(new Vector2(x,110+shift),18,26) {Velocity=new Vector2(dx,dy)};
                // Compare production prediction with the native collision and
                // material pipeline. Omit only AV/world services absent here.
                var stages=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);
                foreach(var stage in stages.ToArray())
                    if(new[]{"WindVelocityUpdateBehaviour","CacheLastScreenBehaviour","WaterParticleSpawningBehaviour","CapPositionBehaviour","HandlePlayerTeleportBehaviour","PlayBumpSFXBehaviour"}.Contains(stage.GetType().Name))stages.Remove(stage);
                var history=new PositionHistory();var path=new PredictionPath();
                history.Observe(body.Position,body.Velocity,1,false,body.IsOnGround,body.LastVelocity);
                for(int tick=0;tick<55;tick++)
                {
                    update.Invoke(body,new object[]{1f/60});
                    history.Observe(body.Position,body.Velocity,1,false,body.IsOnGround,body.LastVelocity);
                    var before=body.Position;var raw=body.Velocity;bool knocked=body.IsKnocked;
                    path.Prepare(before,history.Velocity,body.GetHitbox(),0,history.CanPredict ? (Vector2?)raw : null,body.IsOnGround);
                    var predicted=path.At(1);
                    Rectangle overlap;
                    for(int fraction=0;fraction<4;fraction++)
                    {
                        var drawn=path.At(fraction/4f);
                        var box=new Rectangle((int)drawn.X,(int)drawn.Y,18,26);
                        foreach(var block in blocks)
                        {
                            // Initial overlap may be authoritative or intentional.
                            if(block.Intersects(body.GetHitbox(),out overlap)==BlockCollisionType.Collision_Blocking)continue;
                            Check(block.Intersects(box,out overlap)!=BlockCollisionType.Collision_Blocking,"Native prediction entered solid geometry");
                        }
                    }
                    Check(body.Position==before && body.Velocity==raw && body.IsKnocked==knocked,"Prediction changed live body state");
                    update.Invoke(body,new object[]{1f/60});
                    float error=Vector2.Distance(predicted,body.Position);
                    // Do not score screen capping (omitted above) or initially
                    // embedded bodies: those have no supported local endpoint.
                    if(before.X>=0 && before.X<=450 && before.Y>=shift && before.Y<=310+shift
                        && blocks.All(b=>b.Intersects(new Rectangle((int)before.X,(int)before.Y,18,26),out overlap)!=BlockCollisionType.Collision_Blocking))
                    {
                        samples++;totalError+=error;worst=Math.Max(worst,error);
                        if(error>2)Console.WriteLine("[PREDICT] error="+error+" scene="+scenes.IndexOf(scene)+" tick="+tick+" before="+before+" raw="+raw+" motion="+history.Velocity+" predicted="+predicted+" actual="+body.Position);
                    }
                    history.Observe(body.Position,body.Velocity,1,false,body.IsOnGround,body.LastVelocity);
                    if(body.Position.Y>330+shift || body.Position.X<0 || body.Position.X>450)break;
                }
            }
            Console.WriteLine("[PREDICT] Native slope/sand samples="+samples+" mean="+(totalError/samples)+" worst="+worst);
            Check(samples>500,"Native prediction fixture did not cover enough transitions");
            Check(worst<=2,"Native slope/sand endpoint diverged by more than two pixels");
        }
        finally { screenField.SetValue(null,oldScreens);totalField.SetValue(null,oldTotal);cameraField.SetValue(null,oldCamera); }
    }
}

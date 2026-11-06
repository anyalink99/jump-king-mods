using System;
using System.Collections.Generic;
using WardrobePlus.Advanced;
using Microsoft.Xna.Framework;
using JumpKing.Level;

internal static partial class WardrobeTests
{
    private sealed class MovingWaterBlock:IBlock
    {
        internal Rectangle Bounds;
        internal bool Blocking=true;
        internal int BoundsReads,Queries;
        public Rectangle GetRect(){BoundsReads++;return Bounds;}
        public BlockCollisionType Intersects(Rectangle box,out Rectangle overlap)
        {Queries++;overlap=Rectangle.Intersect(Bounds,box);return Bounds.Intersects(box)?(Blocking?BlockCollisionType.Collision_Blocking:BlockCollisionType.Collision_NonBlocking):BlockCollisionType.NoCollision;}
    }
    private static void SpilledWaterTests()
    {
        WaterPerformanceTests();
        var definition=new EffectDefinition{collision="water",gravity=310,drag=0};
        Func<Vector2,bool> platform=p=>p.X>=0&&p.X<30&&p.Y>=20&&p.Y<28;
        var drop=new PresentationActor.Particle{Effect=new EffectBinding{Definition=definition},Position=new Vector2(15,0),Velocity=new Vector2(45,400),FlowDirection=1};
        bool touched=false,penetrated=false;
        for(int i=0;i<90;i++){SpilledWater.Step(drop,1f/60,platform);touched|=drop.Grounded;penetrated|=platform(drop.Position);}
        Check(touched&&!penetrated&&drop.Position.X>30&&drop.Position.Y>28,"Water hits thin platforms without tunnelling, flows over their edge and falls");
        Func<Vector2,bool> cup=p=>p.Y>=20||p.X<0||p.X>=30;
        drop.Position=new Vector2(15,2);drop.Velocity=new Vector2(70,100);
        for(int i=0;i<240;i++)SpilledWater.Step(drop,1f/60,cup);
        Check(!cup(drop.Position)&&drop.Position.Y>18&&Math.Abs(drop.Velocity.X)<8,"Trapped water settles inside a basin instead of passing through its walls");
        IBlock slope=new SlopeBlock(new Rectangle(0,20,32,32),SlopeType.TopLeft);
        Func<Vector2,bool> sloped=p=>{Rectangle overlap;return slope.Intersects(new Rectangle((int)Math.Floor(p.X),(int)Math.Floor(p.Y),1,1),out overlap)==BlockCollisionType.Collision_Blocking;};
        drop.Position=new Vector2(15,0);drop.Velocity=new Vector2(-18,100);drop.FlowDirection=-1;
        penetrated=false;for(int i=0;i<90;i++){SpilledWater.Step(drop,1f/60,sloped);penetrated|=sloped(drop.Position);}
        Check(!penetrated&&drop.Position.X<0,"Native slope geometry lets water run downhill without entering solid pixels");
        drop.Position=Vector2.Zero;drop.Velocity=Vector2.Zero;SpilledWater.Step(drop,.1f,null);
        Check(drop.Position.Y>0,"Author previews without a world use gravitational droplets safely");
        var screensField=typeof(LevelManager).GetField("m_screens",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        var blocksField=typeof(LevelScreen).GetField("m_hitboxes",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var old=screensField.GetValue(null);
        try
        {
            var screens=new LevelScreen[6];
            screens[5]=(LevelScreen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LevelScreen));
            blocksField.SetValue(screens[5],new IBlock[]{new BoxBlock(new Rectangle(0,-1700,32,8))});screensField.SetValue(null,screens);
            drop.Position=new Vector2(16,-1705);
            var world=SpilledWater.World(new[]{drop});
            Check(world(new Vector2(16,-1700))&&!world(new Vector2(16,-1701)),"Water queries its own off-camera screen and respects exact block boundaries");
            var frame=new SpilledWater.CollisionFrame();
            var moving=new MovingWaterBlock{Bounds=new Rectangle(0,-1700,32,8)};
            blocksField.SetValue(screens[5],new IBlock[]{moving});
            world=frame.Prepare(new[]{drop});Check(world(new Vector2(16,-1700)),"Particle broad phase accepts foreign blocking geometry");
            moving.Blocking=false;world=frame.Prepare(new[]{drop});
            Check(!world(new Vector2(16,-1700)),"Changed foreign blocking state is observed on the next update");
            moving.Blocking=true;moving.Bounds=new Rectangle(40,-1700,32,8);world=frame.Prepare(new[]{drop});
            Check(!world(new Vector2(16,-1700))&&world(new Vector2(48,-1700)),"Moving geometry invalidates both occupied and empty point results");
            var corpus=new List<IBlock>{moving,new SlopeBlock(new Rectangle(60,-1720,32,32),SlopeType.TopLeft),new WaterBlock(new Rectangle(96,-1710,16,16))};
            for(int y=-1740;y<-1650;y+=9)for(int x=-80;x<140;x+=11)corpus.Add(new BoxBlock(new Rectangle(x,y,3,2)));
            blocksField.SetValue(screens[5],corpus.ToArray());world=frame.Prepare(new[]{drop});bool same=true;
            for(int y=-1742;y<-1648;y++)for(int x=-82;x<144;x++)
            {
                var pixel=new Rectangle(x,y,1,1);bool expected=false;Rectangle overlap;
                foreach(var block in corpus)if(block.GetRect().Intersects(pixel)&&block.Intersects(pixel,out overlap)==BlockCollisionType.Collision_Blocking){expected=true;break;}
                same &= world(new Vector2(x+.2f,y+.8f))==expected;
            }
            Check(same,"Indexed droplets match native point queries across slopes, water, thin blocks and negative coordinates");
            moving.BoundsReads=moving.Queries=0;
            for(int i=0;i<100;i++)world(new Vector2(-80+i%8,-1705));
            Check(moving.BoundsReads==0&&moving.Queries==0,"Repeated droplet queries do not rescan unrelated block bounds");
            frame.Clear();Check(!world(new Vector2(48,-1700)),"Ending a collision update releases the borrowed world and cached points");
            screensField.SetValue(null,new LevelScreen[0]);
            Check(!SpilledWater.World(new[]{drop})(drop.Position),"Unloaded worlds cannot leave cached water collision blocks behind");
            Check(!frame.Prepare(new[]{drop})(drop.Position)&&frame.Prepare(new PresentationActor.Particle[0])==null,"Reusable collision storage handles world unload and zero particles");
        }
        finally{screensField.SetValue(null,old);}
    }
}

using System;
using System.IO;
using System.Linq;
using JumpKing.Level;
using JumpKing.API;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private class ForeignTrigger : IBlock
        {
            internal readonly int Destination=384;
            internal bool Blocking;
            private readonly Rectangle bounds;
            internal ForeignTrigger(Rectangle value,bool blocking) { bounds=value;Blocking=blocking; }
            public Rectangle GetRect(){return bounds;}
            public BlockCollisionType Intersects(Rectangle hitbox,out Rectangle area)
            {
                area=Rectangle.Intersect(bounds,hitbox);
                return bounds.Intersects(hitbox)?Blocking?BlockCollisionType.Collision_Blocking:BlockCollisionType.Collision_NonBlocking:BlockCollisionType.NoCollision;
            }
        }
        private sealed class InheritedTrigger : ForeignTrigger
        { internal InheritedTrigger(Rectangle bounds,bool blocking):base(bounds,blocking){} }
        private sealed class ShapeSourceFactory : IBlockFactory
        {
            internal bool SawSourcePixel;
            public bool CanMakeBlock(Color color,JumpKing.Workshop.Level level){return color==new Color(250,120,190);}
            public bool IsSolidBlock(Color color){return CanMakeBlock(color,null);}
            public IBlock GetBlock(Color color,Rectangle bounds,JumpKing.Workshop.Level level,LevelTexture texture,int screen,int x,int y)
            {
                SawSourcePixel=color==new Color(250,120,190) && texture.GetColor(screen,x,y)==color;
                return new ForeignTrigger(bounds,true);
            }
        }
        private static void CollisionShapeChecks(string scratch)
        {
            foreach(bool inherited in new[]{false,true})foreach(var kind in new[]{SlopeType.TopLeft,SlopeType.TopRight,SlopeType.BottomLeft,SlopeType.BottomRight})
            {
                var bounds=new Rectangle(80,80,8,8);
                var source=inherited?new InheritedTrigger(bounds,false):new ForeignTrigger(bounds,false);
                var pair=SlopeClipping.Apply(source,kind);
                Require(ReferenceEquals(pair[0],source) && ((ForeignTrigger)pair[0]).Destination==384,"Slope keeps foreign type, identity and destination");
                IBlock native=new SlopeBlock(bounds,kind);
                for(int y=78;y<90;y++)for(int x=78;x<90;x++)
                {
                    Rectangle area,expected;
                    var hit=new Rectangle(x,y,2,2);
                    var contact=pair[0].Intersects(hit,out area);
                    var shape=native.Intersects(hit,out expected);
                    Require((contact==BlockCollisionType.NoCollision)==(shape==BlockCollisionType.NoCollision),"Foreign trigger clips to the native triangle: "+kind+" "+hit+" actual="+contact+" expected="+shape);
                    var stampArea=MegaMappingExpansion.Construction.NativeContacts.ContactArea(pair[0],hit);
                    Require((stampArea.Width>0 && stampArea.Height>0)==(contact!=BlockCollisionType.NoCollision),"Slope contact yields usable decal placement without filling its empty half: "+kind);
                    Require(pair[1].Intersects(hit,out area)==BlockCollisionType.NoCollision,"Nonblocking trigger has no physical slope");
                    source.Blocking=true;
                    Require(pair[1].Intersects(hit,out area)==shape,"Blocking state supplies native slope collision");
                    source.Blocking=false;
                }
                source.Blocking=true;
                var world=new LevelScreen(0,pair,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
                var info=world.GetCollisionInfo(new Rectangle(80,80,8,8));
                Require((inherited?info.IsCollidingWith<InheritedTrigger>():info.IsCollidingWith<ForeignTrigger>()) && info.SlopeType==kind,"Native sampler sees the original mechanic and real sliding slope");
            }
            var ordinary=new ForeignTrigger(new Rectangle(80,80,8,8),true);Rectangle overlap;
            Require(ordinary.Intersects(new Rectangle(80,80,1,1),out overlap)==BlockCollisionType.Collision_Blocking,"Unshaped foreign blocks keep their original behavior");
            string root=Path.Combine(scratch,"shape-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root,"screens/mega-mapping-expansion"));
            string file=Path.Combine(root,CollisionShapes.PathName);
            File.WriteAllText(file,"<CollisionShapes version='1'><Slope color='#FA79BE' source='#FA78BE' shape='TopLeft'/></CollisionShapes>");
            var parsed=CollisionShapes.Read(root).Find(new Color(250,121,190));
            Require(parsed.Source==new Color(250,120,190) && parsed.Shape==SlopeType.TopLeft,"Map aliases retain source RGB and explicit orientation");
            var factories=typeof(LevelManager).GetField("BlockFactories",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var registered=(System.Collections.Generic.List<IBlockFactory>)factories.GetValue(null);
            var fixture=new ShapeSourceFactory();registered.Add(fixture);
            try
            {
                var layout=new MapLayout{Version=2,Screens=1,Columns=1,Rows=1,CellWidth=60,CellHeight=45,Source="atlas"};
                layout.ValidatePixel();var pixels=new Color[60*45];pixels[10*60+10]=new Color(250,121,190);
                var texture=new PixelLevelTexture(layout,pixels){Shapes=CollisionShapes.Read(root)};
                var blocks=ReadPixelBlocks(texture,0);
                Require(fixture.SawSourcePixel,"Foreign construction receives source RGB including texture queries");
                Require(blocks.Length==2 && blocks[0] is ForeignTrigger && blocks[1] is SlopeBlock,"Native pixel loader publishes original mechanic plus slope geometry");
                Require(texture.GetColor(0,10,10)==new Color(250,121,190),"Source-color sampling ends when construction returns");
            }
            finally {registered.Remove(fixture);}
            File.WriteAllText(file,"<CollisionShapes version='1'><Slope color='#FA79BE' source='#FA79BE' shape='TopLeft'/></CollisionShapes>");
            bool rejected=false;try{CollisionShapes.Read(root);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"Self-referencing slope alias is rejected");
            var cached=new ForeignTrigger(new Rectangle(80,80,8,8),true);
            var shaped=SlopeClipping.Apply(cached,SlopeType.TopLeft);
            Require(shaped[0].Intersects(new Rectangle(80,80,1,1),out overlap)==BlockCollisionType.NoCollision,"Cached source is shaped in its authored world");
            SlopeClipping.BeginWorld();
            Require(shaped[0].Intersects(new Rectangle(80,80,1,1),out overlap)==BlockCollisionType.Collision_Blocking,"A new world clears shape bindings even for cached foreign instances");
        }
    }
}

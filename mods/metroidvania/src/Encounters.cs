using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using JKRuntime.Geometry;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    public sealed class EnemySpawn
    {
        [XmlAttribute] public string Id,Type;
        [XmlAttribute] public int Screen=1;
        [XmlAttribute] public int X,Y,Patrol=48;
    }
    [XmlRoot("Metroidvania")]
    public sealed class EncounterFile
    {
        [XmlAttribute] public int Version=2;
        [XmlElement("Enemy")] public EnemySpawn[] Enemies=new EnemySpawn[0];
        internal static EncounterFile Read(string root,int screens)
        {
            string path=Path.Combine(root,"props","metroidvania","encounters.xml");if(!File.Exists(path))return new EncounterFile();
            EncounterFile value;
            using(var stream=File.OpenRead(path))using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                value=(EncounterFile)new XmlSerializer(typeof(EncounterFile)).Deserialize(reader,new XmlDeserializationEvents{OnUnknownElement=(sender,e)=>{throw new InvalidDataException("Unknown encounter element: "+e.Element.Name);},OnUnknownAttribute=(sender,e)=>{throw new InvalidDataException("Unknown encounter attribute: "+e.Attr.Name);}});
            // XmlSerializer can replace absent array fields with null
            if(value!=null&&value.Enemies==null)value.Enemies=new EnemySpawn[0];
            Validate(value,screens);return value;
        }
        internal static void Validate(EncounterFile value,int screens)
        {
            if(value==null||value.Version!=2||value.Enemies==null||value.Enemies.Length>Battle.MaximumEnemies)throw new InvalidDataException("Unsupported encounter file; use Version=2 and typed Enemy spawns");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var e in value.Enemies){var definition=e==null?null:ContentCatalog.Enemy(e.Type??ContentCatalog.DefaultEnemy);if(e==null||definition==null||string.IsNullOrWhiteSpace(e.Id)||e.Id.Length>64||!ids.Add(e.Id)||e.Screen<1||e.Screen>screens||e.X<0||e.X>480-definition.Width||e.Y<0||e.Y>360-definition.Height||e.Patrol<16||e.Patrol>180)throw new InvalidDataException("Invalid Quest enemy spawn");}
        }
    }
    internal sealed class CollisionWorld
    {
        private readonly IBlock[][] screens;
        internal CollisionWorld(){var loaded=NativeWorldGeometry.ReadScreens();screens=new IBlock[loaded.Length][];for(int i=0;i<loaded.Length;i++)screens[i]=NativeWorldGeometry.ReadBlocks(loaded[i]);}
        internal CollisionWorld(IBlock[][] blocks){screens=blocks;}
        internal int Screens {get{return screens.Length;}}
        // query live block shapes; no collision-info object or whole-map scan per tick
        internal bool Blocked(Rectangle box)
        {
            if(box.Left<0||box.Right>480)return true;
            int first=Math.Max(0,(int)Math.Floor(-box.Bottom/360.0)+1),last=Math.Min(screens.Length-1,(int)Math.Floor(-box.Top/360.0)+1);
            for(int i=first;i<=last;i++)foreach(var block in screens[i]){Rectangle overlap;if(block.Intersects(box,out overlap)==BlockCollisionType.Collision_Blocking)return true;}
            return false;
        }
        internal bool Placement(Vector2 p,int width=0,int height=0){if(width==0){var recipe=ContentCatalog.Enemy(ContentCatalog.DefaultEnemy);width=recipe.Width;height=recipe.Height;}var r=new Rectangle((int)p.X,(int)p.Y,width,height);return !Blocked(r)&&Blocked(new Rectangle(r.X,r.Bottom,width,2));}
    }
}

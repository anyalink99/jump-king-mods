using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // Every registered mechanic must supply the complete Solid / Zone / Screen
    // triplet. These are activation forms of one effect, not separate controllers.
    internal sealed class MapVariantSet
    {
        internal readonly string Id;
        internal readonly Color Solid, Zone, Screen;
        internal readonly HashSet<int> Screens=new HashSet<int>();
        private readonly Func<Rectangle,IBlock> solidFactory,zoneFactory;
        internal MapVariantSet(string id,Color solid,Color zone,Color screen,
            Func<Rectangle,IBlock> makeSolid,Func<Rectangle,IBlock> makeZone)
        {
            if(string.IsNullOrWhiteSpace(id) || makeSolid==null || makeZone==null)
                throw new ArgumentException("A mechanic requires an id and both Solid and Zone factories");
            if(solid.A!=255 || zone.A!=255 || screen.A!=255 || solid==zone || solid==screen || zone==screen)
                throw new ArgumentException("A mechanic requires three distinct opaque Solid / Zone / Screen colours");
            Id=id; Solid=solid; Zone=zone; Screen=screen;
            solidFactory=makeSolid; zoneFactory=makeZone;
        }
        internal IEnumerable<Color> Colours { get { yield return Solid; yield return Zone; yield return Screen; } }
        internal bool Contains(Color colour) { return colour==Solid || colour==Zone || colour==Screen; }
        internal IBlock Create(Color colour,Rectangle rectangle,int screen)
        {
            if(colour==Screen) { Screens.Add(screen); return null; }
            if(colour==Solid) return solidFactory(rectangle);
            if(colour==Zone) return zoneFactory(rectangle);
            throw new ArgumentException("Unknown variant of "+Id);
        }
    }
}

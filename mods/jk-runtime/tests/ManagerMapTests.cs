using System;
using System.IO;
using System.Xml.Serialization;
using JKRuntime.Compatibility;
using JKRuntime.UI;
using JumpKing.Level;
using JumpKing.MiscSystems.LocationText;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class ManagerMapTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static LevelScreen Screen(int index, params IBlock[] blocks)
        { return new LevelScreen(index, blocks, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null); }
        private static void Main()
        {
            var serializer = new XmlSerializer(typeof(UIApiSettings));
            var old = (UIApiSettings)serializer.Deserialize(new StringReader("<UIApiSettings><DiagnosticMode>false</DiagnosticMode></UIApiSettings>"));
            Check(old.ModCompatibilityFixes, "Existing settings opt into the new fix");
            old.ModCompatibilityFixes = false;
            var xml = new StringWriter(); serializer.Serialize(xml, old);
            Check(!((UIApiSettings)serializer.Deserialize(new StringReader(xml.ToString()))).ModCompatibilityFixes, "Opt-out survives serialization");
            var map = new ManagerMap(new[] {
                new Location { start=1, end=2, name="Custom first" },
                new Location { start=2, end=999, name="Custom second" },
                new Location { start=9, end=10, name="Outside" },
                new Location { start=3, end=2, name="Reversed" } }, 4);
            Check(map.Areas.Length == 2 && map.Areas[0].First == 0 && map.Areas[1].Last == 3, "One-based inclusive ranges are clipped to loaded screens");
            Check(map.Screens.Length == 4 && map.Screens[1].Name.Contains("Custom first / Custom second"), "Overlapping areas retain both names");
            Check(new ManagerMap(null, 205).Screens[204].Name == "Screen 205", "Unnamed screens beyond the vanilla enum remain navigable");
            Check(new ManagerMap(new[] { new Location {start=0,end=2,name=""} }, 2).Areas[0].Name == "Unnamed area", "Missing names have a usable fallback");
            Check(new ManagerMap(null, 0).Screens.Length == 0, "No map never invents vanilla entries");
            Vector2 point;
            var screen = Screen(203, new BoxBlock(new Rectangle(0, -203*360+320, 480, 40)));
            Check(ManagerMap.TryPosition(screen, 18, 42, out point), "Arrival found on actual high-index screen");
            Check(point.Y == -203*360+278, "Arrival uses live collision platform and world offset");
            Rectangle overlap; AdvCollisionInfo info;
            Check(!screen.TryCollision(new Rectangle((int)point.X, (int)point.Y,18,42),out overlap,out info), "Arrival clears the whole player hitbox");
            Check(ManagerMap.TryPosition(Screen(0), 18,42,out point), "Empty screen uses free space");
            Check(!ManagerMap.TryPosition(Screen(0,new BoxBlock(new Rectangle(0,0,480,360))),18,42,out point), "Solid screen refuses navigation");
            Check(!ManagerMap.TryPosition(Screen(0),481,42,out point), "Oversized actors are refused");
            Console.WriteLine("[OK] Manager map: ranges, overlaps, unnamed/high screens, native collision arrivals and persistent opt-out");
        }
    }
}

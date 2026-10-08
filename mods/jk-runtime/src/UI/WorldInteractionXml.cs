using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using JumpKing;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    [Serializable]
    public sealed class WorldInteractionPoint
    {
        [XmlAttribute("id")]
        public string Id { get; set; }
        [XmlAttribute("label")]
        public string Label { get; set; }
        [XmlAttribute("priority")]
        public int Priority { get; set; }
        [XmlAttribute("screen")]
        public int Screen { get; set; }
        [XmlAttribute("x")]
        public int X { get; set; }
        [XmlAttribute("y")]
        public int Y { get; set; }
        [XmlAttribute("width")]
        public int Width { get; set; }
        [XmlAttribute("height")]
        public int Height { get; set; }

        [Obsolete("Use GetLocalArea or WorldScreen.ToWorldPosition for screen-safe code.")]
        public Rectangle GetWorldArea()
        {
            int worldY = Screen <= 1 ? Y : Y - (Screen - 1) * JumpGame.GAME_RECT.Height;
            return new Rectangle(X, worldY, Width, Height);
        }

        public Rectangle GetLocalArea()
        {
            return new Rectangle(X, Y, Width, Height);
        }

        public bool IsPlayerInside()
        {
            return WorldInteractionContext.PlayerIntersectsScreenArea(
                Screen,
                GetLocalArea());
        }

        public WorldInteraction CreateAction(Action activate)
        {
            return WorldInteraction.ScreenAction(Id, Label, Priority, Screen, IsPlayerInside, activate);
        }

        public WorldInteraction CreatePage(Func<IUiPage> createPage)
        {
            return WorldInteraction.ScreenPage(Id, Label, Priority, Screen, IsPlayerInside, createPage);
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) throw new InvalidDataException("Interaction point id is required");
            if (Screen < 1) throw new InvalidDataException("Interaction point screen must be one-based: " + Id);
            if (X < 0 || Y < 0
                || X + Width > JumpGame.GAME_RECT.Width
                || Y + Height > JumpGame.GAME_RECT.Height)
                throw new InvalidDataException("Interaction point must fit inside its screen: " + Id);
            if (Width <= 0 || Height <= 0) throw new InvalidDataException("Interaction point area must be positive: " + Id);
        }
    }

    [Serializable]
    [XmlRoot("Interactions")]
    public sealed class WorldInteractionFile
    {
        [XmlAttribute("version")]
        public int Version { get; set; }
        [XmlElement("Point")]
        public WorldInteractionPoint[] Points { get; set; }
    }

    public static class WorldInteractionMap
    {
        public const string DefaultPath = "props/ui-api/interactions.xml";

        public static IList<WorldInteractionPoint> LoadLevel(string relativePath = DefaultPath)
        {
            if (Game1.instance == null || Game1.instance.contentManager == null)
                throw new InvalidOperationException("Jump King content is unavailable");
            string root = Path.GetFullPath(Game1.instance.contentManager.root);
            string path = Path.GetFullPath(Path.Combine(root, relativePath ?? DefaultPath));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Interaction map must be inside the active level");
            if (!File.Exists(path)) return new List<WorldInteractionPoint>();
            XmlSerializer serializer = new XmlSerializer(typeof(WorldInteractionFile));
            WorldInteractionFile file;
            using (FileStream stream = File.OpenRead(path))
                file = serializer.Deserialize(stream) as WorldInteractionFile;
            if (file != null && file.Version != 0 && file.Version != 1)
                throw new InvalidDataException("Unsupported Interactions XML version: " + file.Version);
            List<WorldInteractionPoint> result = new List<WorldInteractionPoint>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WorldInteractionPoint point in file == null
                ? new WorldInteractionPoint[0]
                : file.Points ?? new WorldInteractionPoint[0])
            {
                point.Validate();
                if (!ids.Add(point.Id)) throw new InvalidDataException("Duplicate interaction point: " + point.Id);
                result.Add(point);
            }
            return result;
        }

        public static WorldInteractionPoint Require(IList<WorldInteractionPoint> points, string id)
        {
            foreach (WorldInteractionPoint point in points ?? new List<WorldInteractionPoint>())
                if (string.Equals(point.Id, id, StringComparison.OrdinalIgnoreCase)) return point;
            throw new InvalidDataException("Required interaction point is missing: " + id);
        }
    }
}

using System;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Presentation
{
    public enum AppearanceCoverage { Unavailable, Approximate, Exact }
    public enum AppearanceStage { Outfit, Body, Complete }
    public enum VisualPhase { Form, Attachment, Replacement, Behind, Front }
    public enum PresentationDelivery { Live, Playback, Restore }
    [Flags]
    public enum AppearanceLayerRole { Body=1, Equipment=2, Attachment=4, WorldEffect=8, ExcludeFromForm=16 }
    /// <summary>Optional camera projection for a form with its own scene camera. Draw must respect the projected anchor argument.</summary>
    public interface IAppearanceProjection { Vector2 Project(Vector2 worldAnchor); }
    public interface IAppearanceLayer { AppearanceLayerRole Role {get;} }

    /// <summary>World placement is independent of collision. Anchors retain fractional body position.</summary>
    public struct VisualGeometry
    {
        public Vector2 AnchorOffset, CenterOffset;
        public Rectangle CollisionBounds;
        public AppearanceCoverage Coverage;
        public string Reason;
        public Vector2 Anchor(Vector2 position) { return position + AnchorOffset; }
    }

    /// <summary>A borrowed game-thread sample. Capture it before the next update/publication if it must survive.</summary>
    public sealed class AppearanceFrame
    {
        public PlayerEntity Player { get; internal set; }
        public Sprite Sprite { get; internal set; }
        public Vector2 WorldAnchor { get; internal set; }
        public SpriteEffects Facing { get; internal set; }
        public VisualGeometry Geometry { get; internal set; }
        public string Pose { get; internal set; }
        public long Revision { get; internal set; }
        public AppearanceCoverage Coverage { get; internal set; }
        public string Reason { get; internal set; }
        public bool IsCapture { get; internal set; }
    }

    /// <summary>Detached premultiplied pixels. Facing and tint are already baked; Offset is relative to floor(anchor).</summary>
    public sealed class AppearancePixels
    {
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public Color[] Pixels { get; internal set; }
        public Point Offset { get; internal set; }
        public AppearanceCoverage Coverage { get; internal set; }
        public string Reason { get; internal set; }
        public Vector2 TopLeft(Vector2 anchor) { return new Vector2((float)Math.Floor(anchor.X)+Offset.X,(float)Math.Floor(anchor.Y)+Offset.Y); }
        public Texture2D CreateTexture(GraphicsDevice device)
        {
            var texture=new Texture2D(device,Width,Height);
            try { texture.SetData(Pixels); return texture; } catch { texture.Dispose(); throw; }
        }
    }

    public sealed class PresentationSignal
    {
        public PlayerEntity Player { get; private set; }
        public string Trigger { get; private set; }
        public PresentationDelivery Delivery { get; private set; }
        public PresentationSignal(PlayerEntity player,string trigger,PresentationDelivery delivery)
        { Player=player;Trigger=trigger;Delivery=delivery; }
    }

    /// <summary>Optional cosmetic playback provider. Payloads retain their provider's versioned serialization.</summary>
    public interface ICosmeticActor : IDisposable
    {
        void Advance(int frame,double stateAge,Vector2 anchor,Vector2 velocity,string state,bool flipped,int[] equipment,string[] events,bool silent);
        void Pause(bool paused);
        IDisposable BeginDraw(Color tint);
    }
}

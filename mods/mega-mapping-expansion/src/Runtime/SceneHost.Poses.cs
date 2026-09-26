using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal struct PropPose
    {
        internal Vector2 Position;
        internal float Rotation, Scale, ScaleX, ScaleY, Opacity, Brightness;
        internal int Screen;
        internal bool Visible;
    }
    internal sealed partial class SceneHost
    {
        private readonly Dictionary<PropData, PropPose> propPoses = new Dictionary<PropData, PropPose>();
        private readonly Dictionary<string, PropData> attachedProps = new Dictionary<string, PropData>(StringComparer.Ordinal);
        private PropPose ResolvePropPose(PropData prop)
        {
            PropPose cached;
            if (propPoses.TryGetValue(prop, out cached)) return cached;
            PreparedProp definition = prepared.Prop(prop);
            float age = Timeline(prop);
            float phase = SceneAnimation.Phase(age, prop.Duration, prop.Loop);
            Vector2 position = new Vector2(prop.X, prop.Y);
            Vector2 gaze;
            if (prop.LookAtKing && gazeOffsets.TryGetValue(prop, out gaze)) position += gaze;
            float rotation = MathHelper.ToRadians(prop.Rotation);
            float scale = prop.Scale, scaleX = prop.ScaleX, scaleY = prop.ScaleY, opacity = prop.Opacity;
            float brightness = 1f;
            MotionKind motion = definition.Motion;
            if (motion == MotionKind.Rotate) rotation += MathHelper.ToRadians(prop.Degrees) * phase;
            else if (motion == MotionKind.Orbit)
            {
                position += SceneAnimation.OrbitPosition(phase, prop.AmplitudeX, prop.AmplitudeY);
                rotation += MathHelper.ToRadians(prop.Degrees) * phase;
            }
            else if (motion == MotionKind.Linear) position += new Vector2(prop.AmplitudeX, prop.AmplitudeY) * phase;
            else if (motion == MotionKind.Bob) position.Y += (float)Math.Sin(MathHelper.TwoPi * phase) * prop.AmplitudeY;
            else if (motion == MotionKind.Sway) rotation += MathHelper.ToRadians(prop.Degrees) * (float)Math.Sin(MathHelper.TwoPi * phase);
            else if (motion == MotionKind.Path)
            {
                Vector2[] points = definition.Path;
                bool spline = string.Equals(prop.PathInterpolation, "spline", StringComparison.OrdinalIgnoreCase);
                position += spline ? SceneAnimation.SplinePathPosition(points, phase, prop.ClosedPath)
                    : SceneAnimation.PathPosition(points, phase, prop.ClosedPath);
                if (prop.OrientToPath)
                {
                    Vector2 tangent = spline ? SceneAnimation.SplinePathTangent(points, phase, prop.ClosedPath)
                        : SceneAnimation.PathTangent(points, phase, prop.ClosedPath);
                    if (tangent.LengthSquared() > 0.0001f)
                        rotation += (float)Math.Atan2(tangent.Y, tangent.X) + MathHelper.ToRadians(prop.OrientationOffset);
                }
            }

            foreach (PreparedTrack track in definition.Tracks)
            {
                float trackPhase = string.Equals(prop.Loop, "once", StringComparison.OrdinalIgnoreCase) && phase >= 1f
                    ? 1f : PositiveModulo(phase * track.Cycles + track.Phase, 1f);
                float value = track.Evaluate(trackPhase);
                switch (track.Property)
                {
                    case TrackProperty.X: position.X += value; break;
                    case TrackProperty.Y: position.Y += value; break;
                    case TrackProperty.Rotation: rotation += MathHelper.ToRadians(value); break;
                    case TrackProperty.Scale: scale *= value; break;
                    case TrackProperty.ScaleX: scaleX *= value; break;
                    case TrackProperty.ScaleY: scaleY *= value; break;
                    case TrackProperty.Opacity: opacity *= value; break;
                    case TrackProperty.Brightness: brightness *= value; break;
                }
            }

            PlayerEntity player = GameLoopPlayer();
            if (player != null)
            {
                Vector2 playerLocal = Camera.TransformVector2(player.m_body.GetHitbox().Center.ToVector2());
                position += new Vector2((playerLocal.X - 240f) * (1f - prop.Depth) * 0.055f,
                    (playerLocal.Y - 180f) * (1f - prop.Depth) * 0.028f);
                ReactionState reaction;
                if (prop.ReactRadius > 0f && reactions.TryGetValue(prop.Id, out reaction))
                { rotation += MathHelper.ToRadians(reaction.Angle); position.X += reaction.Angle * 0.16f; }
            }

            int targetScreen = prop.Screen;
            bool visible = prop.Visible && age >= 0;
            if (!string.IsNullOrEmpty(prop.Attach))
            {
                PropPose parent = AttachmentPose(prop.Attach);
                Vector2 local = position + new Vector2(prop.OffsetX, prop.OffsetY);
                local *= new Vector2(parent.Scale * parent.ScaleX, parent.Scale * parent.ScaleY);
                float cosine = (float)Math.Cos(parent.Rotation), sine = (float)Math.Sin(parent.Rotation);
                position = parent.Position + new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);
                rotation += parent.Rotation; targetScreen = parent.Screen; visible &= parent.Visible;
            }
            cached = new PropPose { Position = position, Rotation = rotation, Scale = scale, ScaleX = scaleX,
                ScaleY = scaleY, Opacity = opacity, Brightness = brightness, Screen = targetScreen, Visible = visible };
            propPoses[prop] = cached;
            return cached;
        }
        private PropPose AttachmentPose(string target)
        {
            SceneActor actor = behaviors.Actor;
            if (target == "player" || target == "player.center" || target == "player.feet")
                return new PropPose { Position = new Vector2(actor.Bounds.Center.X, target == "player.feet" ? actor.Bounds.Bottom : actor.Bounds.Center.Y),
                    Screen = actor.Screen, Visible = actor.Present, Scale = 1, ScaleX = actor.FacingLeft ? -1 : 1, ScaleY = 1 };
            PropData prop;
            if (attachedProps.TryGetValue(target, out prop)) return ResolvePropPose(prop);
            Vector2 position; int actorScreen; bool visible;
            if (NativeNarrative.Pose(target, out position, out actorScreen, out visible)) return new PropPose { Position = position, Screen = actorScreen, Visible = visible, Scale = 1, ScaleX = 1, ScaleY = 1 };
            return new PropPose();
        }
        private bool PlayerAttachment(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            if (target == "player" || target == "player.center" || target == "player.feet") return true;
            PropData prop;
            return attachedProps.TryGetValue(target, out prop) && PlayerAttachment(prop.Attach);
        }
    }
}

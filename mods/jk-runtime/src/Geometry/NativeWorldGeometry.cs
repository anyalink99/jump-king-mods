using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikMaths;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    /// <summary>Read access to the loaded native world. Arrays are copied; blocks remain live.</summary>
    public static class NativeWorldGeometry
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", Flags);
        private static readonly FieldInfo Blocks = typeof(LevelScreen).GetField("m_hitboxes", Flags);
        private static readonly FieldInfo Box = typeof(SlopeBlock).GetField("m_box", Flags);
        private static readonly FieldInfo Type = typeof(SlopeBlock).GetField("m_type", Flags);
        private static readonly FieldInfo Lines = typeof(SlopeBlock).GetField("m_lines", Flags);
        private static readonly FieldInfo Collider = typeof(BoxBlock).GetField("m_collider", Flags);
        private static readonly FieldInfo QuarkCollider = typeof(QuarkBlock).GetField("m_collider", Flags);

        /// <summary>Reads stored bounds of exact native block types without calling block code.
        /// False means unknown geometry, including subclasses; never use it as evidence of empty space.</summary>
        public static bool TryReadNativeBounds(IBlock block, out Rectangle bounds)
        {
            RuntimeApi.Kernel.CheckThread();
            bounds=default(Rectangle);
            if(block==null) return false;
            var type=block.GetType();
            FieldInfo field=type==typeof(SlopeBlock) ? Box : type==typeof(QuarkBlock) ? QuarkCollider
                : type==typeof(BoxBlock) || type==typeof(IceBlock) || type==typeof(SnowBlock)
                    || type==typeof(SandBlock) || type==typeof(WaterBlock) || type==typeof(NoWindBlock) ? Collider : null;
            if(field==null || field.FieldType!=typeof(Rectangle)) return false;
            bounds=(Rectangle)field.GetValue(block); return true;
        }

        public static void ValidateContract()
        {
            if (Screens == null || Screens.FieldType != typeof(LevelScreen[])
                || Blocks == null || Blocks.FieldType != typeof(IBlock[])
                || Box == null || Box.FieldType != typeof(Rectangle)
                || Type == null || Type.FieldType != typeof(SlopeType)
                || Lines == null || Lines.FieldType != typeof(Line[]))
                throw new NotSupportedException("Native world geometry contract unavailable");
        }

        public static LevelScreen[] ReadScreens()
        {
            RuntimeApi.Kernel.CheckThread();
            ValidateContract();
            var value = Screens.GetValue(null) as LevelScreen[];
            if (value == null) throw new InvalidOperationException("No loaded native screens");
            return (LevelScreen[])value.Clone();
        }

        public static IBlock[] ReadBlocks(LevelScreen screen)
        {
            RuntimeApi.Kernel.CheckThread();
            ValidateContract();
            if (screen == null) throw new ArgumentNullException("screen");
            var value = Blocks.GetValue(screen) as IBlock[];
            if (value == null) throw new InvalidOperationException("Native screen has no block array");
            return (IBlock[])value.Clone();
        }

        /// <summary>Copies actual native lines, including loaded corrections, without invoking constructors.</summary>
        /// <remarks>A custom subclass must be separately audited before treating this native-only copy as its simulation.</remarks>
        public static SlopeBlock CopySlopeCollision(SlopeBlock slope)
        {
            RuntimeApi.Kernel.CheckThread();
            ValidateContract();
            if (slope == null) throw new ArgumentNullException("slope");
            var lines = Lines.GetValue(slope) as Line[];
            if (lines == null) throw new InvalidOperationException("Native slope lines unavailable");
            var copy = (SlopeBlock)FormatterServices.GetUninitializedObject(typeof(SlopeBlock));
            Box.SetValue(copy, Box.GetValue(slope));
            Type.SetValue(copy, Type.GetValue(slope));
            Lines.SetValue(copy, lines.Clone());
            return copy;
        }

        /// <summary>Returns native shape vertices, not a replacement for IBlock.Intersects or material rules.</summary>
        public static Vector2[] ReadSlopeVertices(SlopeBlock slope)
        {
            RuntimeApi.Kernel.CheckThread();
            ValidateContract();
            if (slope == null) throw new ArgumentNullException("slope");
            var lines = Lines.GetValue(slope) as Line[];
            if (lines == null) throw new InvalidOperationException("Native slope lines unavailable");
            var vertices = new Vector2[lines.Length];
            for (int i = 0; i < lines.Length; i++) vertices[i] = lines[i].p0.ToVector2();
            return vertices;
        }
    }
}

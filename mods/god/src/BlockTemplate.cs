using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace God
{
    internal sealed class BlockTemplate
    {
        private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        internal readonly IBlock Source;
        internal readonly string Key;
        internal readonly bool Blocking;
        internal readonly string Error;
        internal BlockTemplate(IBlock source)
        {
            Source = source;
            Rectangle overlap;
            Blocking = source.Intersects(source.GetRect(), out overlap) == BlockCollisionType.Collision_Blocking;
            try
            {
                var signature = new List<string>();
                CopyObject(source, source.GetRect(), source.GetRect(), new Dictionary<object, object>(new Identity()), signature, 0);
                Key = string.Join("|", signature); Source = Copy(source.GetRect());
            }
            catch (Exception error) { Error = error.GetBaseException().Message; Key = source.GetType().AssemblyQualifiedName; }
        }
        internal IBlock Copy(Rectangle bounds)
        {
            if (Error != null) throw new NotSupportedException(Error);
            var slope = Source as SlopeBlock;
            if (slope != null && Source.GetType() == typeof(SlopeBlock)) return JKRuntime.Geometry.NativeWorldGeometry.TransformSlopeCollision(slope, bounds);
            var copy = (IBlock)CopyObject(Source, Source.GetRect(), bounds, new Dictionary<object, object>(new Identity()), null, 0);
            if (copy.GetRect() != bounds) throw new NotSupportedException("Block geometry can't be relocated: " + Source.GetType().Name);
            return copy;
        }
        private sealed class Identity : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }
        private static object CopyObject(object value, Rectangle from, Rectangle to, Dictionary<object, object> copies, List<string> key, int depth)
        {
            if (value == null) { if (key != null) key.Add("null"); return null; }
            var type = value.GetType();
            if (key != null) key.Add(type.AssemblyQualifiedName);
            if (type == typeof(Rectangle))
            {
                var r = (Rectangle)value;
                if (key != null) key.Add((r.X - from.X) / (double)from.Width + "," + (r.Y - from.Y) / (double)from.Height + "," + r.Width / (double)from.Width + "," + r.Height / (double)from.Height);
                // bounds inside a block are relative geometry, not another world's objects
                return new Rectangle(to.X + (r.X - from.X) * to.Width / from.Width,
                    to.Y + (r.Y - from.Y) * to.Height / from.Height, r.Width * to.Width / from.Width, r.Height * to.Height / from.Height);
            }
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Color))
            {
                if (type == typeof(IntPtr) || type == typeof(UIntPtr)) throw new NotSupportedException("Block owns a native handle: " + type.Name);
                if (key != null) key.Add(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return value;
            }
            if (value is Delegate || value is IDisposable || value is EntityComponent.Entity || value is EntityComponent.Component || value is Type)
                throw new NotSupportedException("Block needs a copy adapter for external state: " + type.Name);
            object known; if (copies.TryGetValue(value, out known)) { if (key != null) key.Add("reference"); return known; }
            if (depth > 16 || copies.Count > 4096) throw new NotSupportedException("Block state is too large to copy: " + type.Name);
            var array = value as Array;
            if (array != null)
            {
                if (array.Rank != 1 || array.Length > 4096) throw new NotSupportedException("Unsupported block state array.");
                var result = (Array)array.Clone(); copies.Add(value, result);
                for (int i = 0; i < array.Length; i++) result.SetValue(CopyObject(array.GetValue(i), from, to, copies, key, depth + 1), i);
                return result;
            }
            var clone = Clone.Invoke(value, null); copies.Add(value, clone);
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.Name))
                {
                    if (key != null) key.Add(field.Name);
                    field.SetValue(clone, CopyObject(field.GetValue(value), from, to, copies, key, depth + 1));
                }
            return clone;
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class ContactRecovery
    {
        private const float Radius = 96;
        internal static bool Overlaps(Vector2 position, int width, int height, InteractionFrame other, float margin = .01f)
        {
            return position.X + width > other.Position.X + margin && position.X < other.Position.X + other.Width - margin
                && position.Y + height > other.Position.Y + margin && position.Y < other.Position.Y + other.Height - margin;
        }

        internal static bool Needed(ContactBody body, IList<InteractionPeer> peers, bool teleported, ulong support, ulong self, Dictionary<ulong,int> lingering)
        {
            foreach (var peer in peers)
            {
                var other = peer.Sample ?? peer.Frame;
                if (!teleported && (peer.Id == support || other.Support == self)) { lingering.Remove(peer.Id);continue; }
                if (!Overlaps(body.Position, body.Width, body.Height, other)) { lingering.Remove(peer.Id);continue; }
                int ticks;lingering.TryGetValue(peer.Id,out ticks);lingering[peer.Id]=++ticks;
                // a fresh crossing still belongs to the normal swept contact solver
                if (teleported || ticks>=3 || Overlaps(body.Before, body.Width, body.Height, other, 4)) return true;
            }
            return false;
        }

        internal static bool Separate(ContactBody body, IList<InteractionPeer> peers, ulong self, Func<Rectangle, bool> blocked)
        {
            Vector2 start = body.Position;
            var xs = new List<float> { start.X };
            var ys = new List<float> { start.Y };
            ulong first = ulong.MaxValue;
            foreach (var peer in peers)
            {
                var other = peer.Sample ?? peer.Frame;
                if (Overlaps(start, body.Width, body.Height, other)) first = Math.Min(first, peer.Id);
                Add(xs, other.Position.X - body.Width, start.X);
                Add(xs, other.Position.X + other.Width, start.X);
                Add(ys, other.Position.Y - body.Height, start.Y);
                Add(ys, other.Position.Y + other.Height, start.Y);
            }
            var candidates = new List<Vector2>();
            foreach (float x in xs) foreach (float y in ys)
            {
                var point = new Vector2(x, y);
                if (point != start && Vector2.DistanceSquared(point, start) <= Radius * Radius) candidates.Add(point);
            }
            bool preferLeft = self < first;
            candidates.Sort(delegate(Vector2 a, Vector2 b) {
                int distance = Vector2.DistanceSquared(a, start).CompareTo(Vector2.DistanceSquared(b, start));
                if (distance != 0) return distance;
                int side = (preferLeft ? a.X.CompareTo(b.X) : b.X.CompareTo(a.X));
                return side != 0 ? side : a.Y.CompareTo(b.Y);
            });
            var trial = new ContactBody { Width = body.Width, Height = body.Height };
            int tried = 0;
            foreach (var point in candidates)
            {
                if (++tried > 256) break;
                if (Occupied(point, body, peers)) continue;
                trial.Position = start;
                // don't enter terrain or a third player while escaping the original overlap
                Func<Rectangle, bool> obstacle = r => {
                    if (blocked(r)) return true;
                    foreach (var peer in peers)
                    {
                        var other = peer.Sample ?? peer.Frame;
                        if (!Overlaps(start, body.Width, body.Height, other) && Overlaps(new Vector2(r.X,r.Y), r.Width,r.Height,other)) return true;
                    }
                    return false;
                };
                if (!PlayerContacts.Move(trial, point, obstacle, (int)Radius)) continue;
                body.Position = point;
                // recovery has no impulse; only cancel velocity back into the overlap
                Vector2 change = point - start;
                if (change.X * body.Velocity.X < 0) body.Velocity.X = 0;
                if (change.Y * body.Velocity.Y < 0) body.Velocity.Y = 0;
                body.RelocatedVertically = change.Y != 0;
                return true;
            }
            // no reachable space: leave map movement free and retry, without bounce spam
            return false;
        }

        private static bool Occupied(Vector2 position, ContactBody body, IList<InteractionPeer> peers)
        {
            foreach (var peer in peers)
                if (Overlaps(position, body.Width, body.Height, peer.Sample ?? peer.Frame)) return true;
            return false;
        }
        private static void Add(List<float> values, float value, float origin)
        {
            // bound both work and displacement even in a crowded lobby
            if (values.Count < 33 && Math.Abs(value - origin) <= Radius && !values.Contains(value)) values.Add(value);
        }
    }
}

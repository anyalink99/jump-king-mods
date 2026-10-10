using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using JumpKing.Player;
using HarmonyLib;

namespace MultiplayerExpansion
{
    internal sealed class ContactBody
    {
        internal Vector2 Before, Position, Velocity;
        internal int Width, Height;
        internal bool Grounded, NativeGrounded, Knocked, Bump, RelocatedVertically;
    }
    internal sealed class PlayerContacts
    {
        private static readonly System.Reflection.FieldInfo grounded = AccessTools.Field(typeof(BodyComp), "_is_on_ground"), knocked = AccessTools.Field(typeof(BodyComp), "_knocked");
        private readonly ContactBody state = new ContactBody();
        internal ulong Support;
        internal Vector2 SupportOffset, SupportVelocity;
        internal Vector2 SeamShift;
        internal void BeginBodyStep() { SeamShift=Vector2.Zero; }
        internal Action<InteractionPeer,Vector2,Vector2,Vector2> Impact;
        private Vector2 supportPosition;
        private readonly Dictionary<ulong,int> touching = new Dictionary<ulong,int>();
        private readonly Dictionary<ulong,int> lingering = new Dictionary<ulong,int>();
        private readonly ContactConstraints constraints = new ContactConstraints();
        private int tick;
        private bool preparedCarry;
        internal void Reset() { Support = 0;SupportOffset=SupportVelocity=Vector2.Zero;touching.Clear();lingering.Clear();constraints.Reset();preparedCarry=false; }
        internal void Forget(ulong id) {touching.Remove(id);lingering.Remove(id);constraints.Forget(id);if(Support==id) {Support=0;SupportOffset=SupportVelocity=Vector2.Zero;} }
        internal void Confirmed(ulong id) { touching[id]=tick; }
        internal void Takeoff(BodyComp body)
        {
            if(Support!=0) body.Velocity+=SupportVelocity;
            Support=0;SupportOffset=SupportVelocity=Vector2.Zero;
        }
        internal Vector2 BeginStep(BodyComp body,IList<InteractionPeer> peers,InteractionRules rules,Func<Rectangle,bool> blocked)
        {
            preparedCarry=true;
            if(Support!=0 && body.Velocity.Y>=0 && (rules & InteractionRules.Platforms)!=0)
                foreach(var peer in peers) if(peer.Id==Support) {
                    var other=peer.Sample ?? peer.Frame;
                    Vector2 delta=other.Position-supportPosition;
                    Vector2 seam;
                    if(JKRuntime.Geometry.MapTopology.TryCrossing(supportPosition,other.Position,out seam)) {
                        // keep the small carry delta in the old chart, then rebase the attachment
                        delta-=seam;
                    }
                    if(delta.LengthSquared()>128*128) { Forget(peer.Id);break; }
                    var carried=new ContactBody{Position=body.Position,Width=body.GetHitbox().Width,Height=body.GetHitbox().Height};
                    if(Move(carried,body.Position+delta,blocked,128)) body.Position=carried.Position;
                    else {Forget(peer.Id);break;}
                    supportPosition+=delta;
                    Vector2 normalized=JKRuntime.Geometry.MapTopology.Normalize(body.Position,supportPosition+new Vector2(other.Width/2f,other.Height/2f),body.GetHitbox().Width);
                    Vector2 shift=normalized-body.Position;
                    if(shift!=Vector2.Zero) {
                        SeamShift+=shift;
                        body.Position=normalized;supportPosition+=shift;
                        RebasePeers(peers,shift,body.Position);
                        var player=JumpKing.GameManager.GameLoop.m_player;
                        if(player!=null && ReferenceEquals(player.m_body,body)) JumpKing.Camera.UpdateCamera(body.GetHitbox().Center);
                    }
                    break;
                }
            return body.Position;
        }
        internal void Apply(BodyComp body, Vector2 before, IList<InteractionPeer> peers, InteractionRules rules, ulong self, Func<Rectangle, bool> blocked,bool nativePhase=false)
        {
            var bounds = body.GetHitbox();
            state.Before = before; state.Position = body.Position; state.Velocity = body.Velocity;
            state.Width = bounds.Width; state.Height = bounds.Height;
            state.NativeGrounded=body.IsOnGround && (nativePhase || Support==0);
            Resolve(state, peers, rules, self, blocked);
            body.Position = state.Position; body.Velocity = state.Velocity;
            if (state.Grounded) { grounded.SetValue(body, true); knocked.SetValue(body, false); }
            else if(state.Knocked) { grounded.SetValue(body,false);knocked.SetValue(body,true); }
            else if(state.RelocatedVertically || body.Position.Y!=before.Y && !state.NativeGrounded) grounded.SetValue(body,false);
            if(state.Bump) ContactFeedback.Request(body);
            if(nativePhase) ContactFeedback.PrepareNative(body);else ContactFeedback.Flush(body);
        }
        // all corrective movement goes through the map query, including peer pushes
        internal static Rectangle Bounds(Vector2 position,int width,int height)
        {
            int x=(int)Math.Floor(position.X),y=(int)Math.Floor(position.Y);
            return new Rectangle(x,y,(int)Math.Ceiling(position.X+width)-x,(int)Math.Ceiling(position.Y+height)-y);
        }
        internal static bool Move(ContactBody body, Vector2 target, Func<Rectangle, bool> blocked, int budget = 64)
        {
            Vector2 start = body.Position, change = target - start;
            int count = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(change.X), Math.Abs(change.Y))));
            if (count > budget) return false;
            for (int i = 1; i <= count; i++)
            {
                Vector2 next = start + change * ((float)i / count);
                if (blocked(Bounds(next,body.Width,body.Height))) return false;
                body.Position = next;
            }
            return true;
        }
        internal void Resolve(ContactBody body, IList<InteractionPeer> peers, InteractionRules rules, ulong self, Func<Rectangle, bool> blocked)
        {
            body.Grounded = false;body.Knocked=false;body.Bump=false;body.RelocatedVertically=false;tick++;
            if (rules == InteractionRules.Ghosts) { Reset(); return; }
            Vector2 crossing;
            if(JKRuntime.Geometry.MapTopology.TryCrossing(body.Before,body.Position,out crossing)) {
                SeamShift+=crossing;
                body.Before+=crossing;supportPosition+=crossing;
                RebasePeers(peers,crossing,body.Position);
            }
            bool teleported = Vector2.DistanceSquared(body.Before, body.Position) > 4096;
            if(!teleported) peers=constraints.Prepare(body,peers,Support,self,rules);
            else constraints.Reset();
            if ((rules & InteractionRules.Sides)!=0 && ContactRecovery.Needed(body,peers,teleported,Support,self,lingering))
            {
                ContactRecovery.Separate(body,peers,self,blocked);
                Reset();
                return;
            }
            if (teleported) { Reset(); return; }
            if((rules & InteractionRules.Platforms)!=0 && !body.NativeGrounded && body.Position.Y<body.Before.Y)
                foreach(var passenger in peers)
                {
                    var child=passenger.Sample ?? passenger.Frame;
                    if(child.Support!=self) continue;
                    Vector2 offset=child.SupportOffset;
                    Func<Rectangle,bool> blockedWithPassenger=r=>blocked(r) || blocked(Bounds(new Vector2(r.X+offset.X,r.Y+offset.Y),child.Width,child.Height));
                    if(!blockedWithPassenger(Bounds(body.Position,body.Width,body.Height))) continue;
                    Vector2 target=body.Position;body.Position=body.Before;
                    Move(body,target,blockedWithPassenger);
                    if(Math.Abs(target.X-body.Position.X)>.01f) {body.Bump|=!body.NativeGrounded && Math.Abs(body.Velocity.X)>.01f;body.Velocity.X=body.NativeGrounded ? 0 : -body.Velocity.X*JumpKing.PlayerValues.BOUNCE;body.Knocked=!body.NativeGrounded;}
                    if(Math.Abs(target.Y-body.Position.Y)>.01f) {body.Bump|=body.Velocity.Y<0;body.Velocity.Y=0;}
                }
            ulong oldSupport = Support; Support = 0;
            if(!preparedCarry) foreach (var peer in peers)
            {
                var other = peer.Sample ?? peer.Frame;
                if(other.Support==self) continue;
                if (peer.Id != oldSupport || body.Velocity.Y < 0) continue;
                Vector2 delta = other.Position - supportPosition;
                if (delta.LengthSquared() <= 128*128 && (rules & InteractionRules.Platforms) != 0)
                    Move(body, body.Position + delta, blocked,128);
            }
            preparedCarry=false;
            foreach (var peer in peers)
            {
                var other = peer.Sample ?? peer.Frame;
                // the rider owns its attachment; it must never act as the carrier's ceiling
                if(other.Support==self) continue;
                float left = other.Position.X, right = left + other.Width, top = other.Position.Y, bottom = top + other.Height;
                bool horizontal = body.Position.X + body.Width > left + .1f && body.Position.X < right - .1f;
                float previousBottom = body.Before.Y + body.Height, currentBottom = body.Position.Y + body.Height;
                bool supported = oldSupport == peer.Id && Math.Abs(currentBottom - top) <= 16;
                if ((rules & InteractionRules.Platforms) != 0 && body.Velocity.Y >= 0 && horizontal
                    && (supported || previousBottom <= top + 1 && currentBottom >= top))
                {
                    if (Move(body, new Vector2(body.Position.X, top - body.Height), blocked))
                    { body.Velocity.Y = 0; body.Grounded = true; Support = peer.Id; supportPosition = other.Position; SupportOffset=body.Position-other.Position;SupportVelocity=other.Velocity; if(other.Grounded) SupportVelocity.Y=0; }
                    continue;
                }
                if ((rules & InteractionRules.Sides) == 0 || body.Position.Y + body.Height <= top + 1 || body.Position.Y >= bottom - 1) continue;
                // the player's own client applies nearby pushing intent, never a remote teleport
                if ((rules & InteractionRules.Push) != 0 && (!other.Physics || other.Anchored))
                {
                    float push = 0;
                    if (Math.Abs(body.Position.X - right) <= 2 && other.Velocity.X > 0) push = Math.Min(3, other.Velocity.X);
                    if (Math.Abs(body.Position.X + body.Width - left) <= 2 && other.Velocity.X < 0) push = Math.Max(-3, other.Velocity.X);
                    if (push != 0) Move(body, body.Position + new Vector2(push, 0), blocked);
                }
                bool crossesRight = body.Before.X + body.Width <= left && body.Position.X + body.Width > left;
                bool crossesLeft = body.Before.X >= right && body.Position.X < right;
                if (!crossesRight && !crossesLeft && (body.Position.X + body.Width <= left || body.Position.X >= right)) continue;
                // don't turn a jump through a one-way head platform into a side snap
                if (body.Before.Y >= bottom && body.Position.Y < bottom)
                {
                    if (Move(body, new Vector2(body.Position.X, bottom), blocked))
                    {
                        if(other.Physics && !other.Anchored) Bounce(body,peer,new Vector2(0,1));
                        else { body.Bump|=body.Velocity.Y<0;body.Velocity.X*=JumpKing.PlayerValues.BOUNCE; body.Velocity.Y=Math.Max(0,body.Velocity.Y); }
                    }
                    continue;
                }
                bool onLeft = body.Before.X + body.Width <= left + 1 ||
                    !(body.Before.X >= right - 1) && (body.Position.X + body.Width * .5f < left + other.Width * .5f ||
                    body.Position.X + body.Width * .5f == left + other.Width * .5f && self < peer.Id);
                if (Move(body, new Vector2(onLeft ? left - body.Width : right, body.Position.Y), blocked))
                {
                    if(other.Physics && !body.NativeGrounded && !body.Grounded) Bounce(body,peer,new Vector2(onLeft ? -1 : 1,0));
                    else if (onLeft && body.Velocity.X > 0 || !onLeft && body.Velocity.X < 0) body.Velocity.X = 0;
                }
            }
            if(Support==0) SupportOffset=SupportVelocity=Vector2.Zero;
            constraints.Finish(body.Position);
        }
        private static void RebasePeers(IList<InteractionPeer> peers,Vector2 shift,Vector2 observer)
        {
            foreach(var peer in peers) {
                var sample=peer.Sample ?? peer.Frame;
                if(Vector2.DistanceSquared(sample.Position+shift,observer)>=Vector2.DistanceSquared(sample.Position,observer)) continue;
                sample=sample.Copy();sample.Position+=shift;peer.Sample=sample;
            }
        }
        internal InteractionFrame Project(ulong id,InteractionFrame frame,Rectangle local,ulong self)
        {
            if(frame.Support==self || id==Support || !ContactRecovery.Overlaps(local.Location.ToVector2(),local.Width,local.Height,frame)) return frame;
            var projected=frame.Copy();
            if(local.Bottom<=frame.Position.Y+4) projected.Position.Y=local.Bottom;
            else if(local.Top>=frame.Position.Y+frame.Height-4) projected.Position.Y=local.Top-frame.Height;
            else {
                int side=constraints.Side(id,frame);
                // prediction can't swap the established walking side
                projected.Position.X=side<0 || side==0 && local.Center.X<frame.Position.X+frame.Width*.5f ? local.Right : local.Left-frame.Width;
            }
            return projected;
        }
        private void Bounce(ContactBody body,InteractionPeer peer,Vector2 normal)
        {
            var other=peer.Sample ?? peer.Frame;
            float relative=Vector2.Dot(body.Velocity-other.Velocity,normal);
            if(relative>=-.01f) return;
            int previous;if(touching.TryGetValue(peer.Id,out previous) && tick-previous<=2) {touching[peer.Id]=tick;return;}
            touching[peer.Id]=tick;
            bool anchored=other.Anchored;
            float impulse=-(1+JumpKing.PlayerValues.BOUNCE)*relative/(anchored ? 1 : 2);
            body.Velocity+=normal*impulse;body.Knocked=true;body.Bump=true;
            Vector2 remote=anchored ? other.Velocity : other.Velocity-normal*impulse;
            if(Impact!=null) Impact(peer,normal,body.Velocity,remote);
        }
    }
}

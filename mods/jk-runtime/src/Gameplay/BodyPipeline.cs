using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.API;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    public enum BodyPhase { BeforeWind, BeforeXMovement, AfterXCollision, AfterYCollision, AfterGravity }

    // One installed-game adapter. Map-authorized mechanics preserve the native
    // modified-run policy; ownership checks never remove a foreign registration.
    public sealed class BodyPipeline : IDisposable
    {
        private static readonly FieldInfo field = typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly BodyComp body;
        private readonly bool modified;
        private readonly LinkedList<IBodyCompBehaviour> list;
        private readonly List<IBodyCompBehaviour> owned = new List<IBodyCompBehaviour>();
        private sealed class Registration
        {
            internal BodyComp Body;
            internal IBodyCompBehaviour Behaviour, Anchor;
            internal bool Before;
            internal int Priority;
            internal string Key;
        }
        private static readonly List<Registration> registrations = new List<Registration>();
        private readonly int priority;
        private readonly string owner;
        private bool closing, disposed;
        public BodyPipeline(BodyComp target, bool markRunModified, int phasePriority = 0, string ownerId = "anonymous")
        {
            RuntimeApi.Kernel.CheckThread();
            if (target == null) throw new ArgumentNullException("target");
            body = target; modified = markRunModified;
            priority = phasePriority; owner = ownerId;
            list = field == null ? null : field.GetValue(target) as LinkedList<IBodyCompBehaviour>;
            if (list == null) throw new InvalidOperationException("Required native BodyComp.m_behaviours contract is unavailable");
        }
        public IDisposable Register(BodyPhase phase, IBodyCompBehaviour behaviour)
        {
            CheckRegistration();
            string name;
            switch (phase) {
                case BodyPhase.BeforeWind: name = "WindVelocityUpdate"; break;
                case BodyPhase.BeforeXMovement: name = "UpdateXPositionFromVelocity"; break;
                case BodyPhase.AfterXCollision: name = "ResolveXCollision"; break;
                case BodyPhase.AfterYCollision: name = "ResolveYCollision"; break;
                case BodyPhase.AfterGravity: name = "ApplyGravity"; break;
                default: throw new ArgumentOutOfRangeException("phase", phase, "Unknown native body phase");
            }
            var anchors = list.Where(b => b.GetType().FullName == "JumpKing.BodyCompBehaviours." + name + "Behaviour").ToArray();
            if (anchors.Length != 1) throw new InvalidOperationException("Required unique native body phase unavailable: " + phase);
            bool before = phase == BodyPhase.BeforeWind || phase == BodyPhase.BeforeXMovement;
            if (!(before ? RegisterBefore(behaviour, anchors[0]) : RegisterAfter(behaviour, anchors[0])))
                throw new InvalidOperationException("Body phase changed while registering: " + phase);
            return new ActionLease(delegate { if (!Remove(behaviour)) throw new InvalidOperationException("Owned body behaviour was removed externally"); });
        }
        public bool RegisterBefore(IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor) { return Add(behaviour, anchor, true); }
        public bool RegisterAfter(IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor) { return Add(behaviour, anchor, false); }
        private bool Add(IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor, bool before)
        {
            CheckRegistration();
            if (behaviour == null || list.Contains(behaviour)) throw new InvalidOperationException("Invalid or duplicate body registration");
            var node = list.Find(anchor); if (node == null) return false;
            var registration = new Registration { Body = body, Behaviour = behaviour, Anchor = anchor, Before = before, Priority = priority, Key = owner + "/" + behaviour.GetType().FullName };
            var siblings = registrations.Where(r => r.Body == body && r.Anchor == anchor && r.Before == before).OrderBy(r => r.Priority).ThenBy(r => r.Key, StringComparer.Ordinal).ToArray();
            var successor = siblings.FirstOrDefault(r => r.Priority > priority || (r.Priority == priority && string.CompareOrdinal(r.Key, registration.Key) > 0));
            if (successor != null) { anchor = successor.Behaviour; before = true; }
            else if (siblings.Length != 0) { anchor = siblings[siblings.Length - 1].Behaviour; before = false; }
            node = list.Find(anchor);
            if (node == null) throw new InvalidOperationException("An owned body phase disappeared externally");
            if (modified) {
                if (!(before ? RunModifiers.RegisterBefore(body, behaviour, anchor) : RunModifiers.RegisterAfter(body, behaviour, anchor))) return false;
            }
            else if (before) list.AddBefore(node, behaviour); else list.AddAfter(node, behaviour);
            owned.Add(behaviour); registrations.Add(registration); return true;
        }
        public bool Remove(IBodyCompBehaviour behaviour)
        {
            RuntimeApi.Kernel.CheckThread();
            if (!owned.Contains(behaviour)) return false;
            bool removed = modified ? RunModifiers.Remove(body, behaviour) : list.Remove(behaviour);
            if (removed) { owned.Remove(behaviour); registrations.RemoveAll(r => r.Body == body && r.Behaviour == behaviour); }
            return removed;
        }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread();
            if (disposed) return;
            closing = true;
            var failures = new List<Exception>();
            foreach (var behaviour in owned.ToArray().Reverse())
            {
                try
                {
                    if (!Remove(behaviour))
                        failures.Add(new InvalidOperationException("Owned body behaviour disappeared"));
                }
                catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0) throw new AggregateException(failures);
            disposed = true;
        }
        private void CheckRegistration()
        {
            RuntimeApi.Kernel.CheckThread();
            if (closing) throw new ObjectDisposedException("BodyPipeline");
        }
    }
}

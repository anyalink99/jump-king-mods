using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Simulation
{
    public static class NativeFlightSimulation
    {
        internal const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] StateFields = { "_last_velocity", "_is_on_ground", "_knocked", "<LastScreen>k__BackingField" };
        private static readonly Action<BodyComp, float> Tick = (Action<BodyComp, float>)Delegate.CreateDelegate(
            typeof(Action<BodyComp, float>), typeof(BodyComp).GetMethod("UpdateInternal", Fields));

        internal static T Get<T>(object target, string field)
        { return (T)target.GetType().GetField(field, Fields).GetValue(target); }
        internal static void Set(object target, string field, object value)
        { target.GetType().GetField(field, Fields).SetValue(target, value); }
        public static void Validate()
        {
            RuntimeApi.Kernel.CheckThread();
            foreach (string field in StateFields.Concat(new[] { "m_behaviours", "m_blockBehaviours", "m_blockBehaviourLookup", "m_behaviourContext" }))
                if (typeof(BodyComp).GetField(field, Fields) == null) throw new InvalidOperationException("Missing BodyComp contract: " + field);
            if (Tick == null) throw new InvalidOperationException("Missing native body update");
        }

        private sealed class Branch
        {
            internal INativeFlightWorld World;
            internal int Tick;
            internal Wind Wind;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BodyComp, Branch> shadows = new System.Runtime.CompilerServices.ConditionalWeakTable<BodyComp, Branch>();
        /// <summary>Advance an isolated body created by CreateShadow; never pass a live player body.</summary>
        public static void AdvanceShadow(BodyComp shadow, float delta)
        {
            RuntimeApi.Kernel.CheckThread();
            Branch branch;
            if (shadow == null || !shadows.TryGetValue(shadow, out branch)) throw new ArgumentException("Body is not an isolated native-flight branch");
            if (delta != 1f / 60f) throw new ArgumentException("Native ballistic adapter requires the audited 1/60 physics step");
            NativeItemReadCache.Ensure();
            branch.World.ValidateScreen(); Tick(shadow, delta); branch.Tick++;
        }

        /// <summary>Finish the captured first tick at BeforeXMovement without repeating collision cache, wind or velocity cache.</summary>
        public static void AdvanceShadowFromX(BodyComp shadow)
        {
            RuntimeApi.Kernel.CheckThread();
            Branch branch;
            if (shadow == null || !shadows.TryGetValue(shadow, out branch) || branch.Tick != 0)
                throw new ArgumentException("Continuation requires a new isolated branch captured at BeforeXMovement");
            NativeItemReadCache.Ensure();
            branch.World.ValidateScreen();
            var context = Get<BehaviourContext>(shadow, "m_behaviourContext");
            bool started = false;
            foreach (var stage in Get<LinkedList<IBodyCompBehaviour>>(shadow, "m_behaviours"))
            {
                if (stage is UpdateXPositionFromVelocityBehaviour) started = true;
                if (started && !stage.ExecuteBehaviour(context)) break;
            }
            if (!started) throw new InvalidOperationException("Native X continuation contract unavailable");
            branch.Tick++;
        }

        // Own body/behaviours. Never run prediction on the real player's object.
        public static BodyComp CreateShadow(BodyComp source, INativeFlightWorld world)
        {
            RuntimeApi.Kernel.CheckThread();
            Validate();
            Rectangle hitbox = source.GetHitbox();
            BodyComp body = new BodyComp(source.Position, hitbox.Width, hitbox.Height);
            CopyState(source, body);
            CopyContext(Get<BehaviourContext>(source, "m_behaviourContext"), Get<BehaviourContext>(body, "m_behaviourContext"));
            var branch = new Branch { World = world };
            var lookup = Get<Dictionary<Type, IBlockBehaviour>>(body, "m_blockBehaviourLookup");
            var original = Get<Dictionary<Type, IBlockBehaviour>>(source, "m_blockBehaviourLookup");
            foreach (var pair in lookup)
            {
                IBlockBehaviour existing;
                if (original.TryGetValue(pair.Key, out existing)) CopyScalars(existing, pair.Value);
                ReplaceQuery(pair.Value, world);
            }
            var pipeline = Get<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours");
            foreach (var behaviour in pipeline.ToArray())
            {
                string name = behaviour.GetType().Name;
                if (name == "WindVelocityUpdateBehaviour")
                {
                    var node = pipeline.Find(behaviour);
                    branch.Wind = new Wind(branch) { Enabled = ReadWind(source) };
                    pipeline.AddBefore(node, branch.Wind); pipeline.Remove(node);
                }
                else if (name == "HandlePlayerTeleportBehaviour")
                {
                    var node = pipeline.Find(behaviour); pipeline.AddBefore(node, new Teleport(world)); pipeline.Remove(node);
                }
                else if (name == "WaterParticleSpawningBehaviour" || name == "PlayBumpSFXBehaviour")
                    pipeline.Remove(behaviour);
                else if (name == "CapPositionBehaviour")
                {
                    var node = pipeline.Find(behaviour); pipeline.AddBefore(node, new Cap(world)); pipeline.Remove(node);
                }
                else if (name == "CacheLastScreenBehaviour")
                {
                    var node = pipeline.Find(behaviour); pipeline.AddBefore(node, new CacheScreen(world)); pipeline.Remove(node);
                }
                else ReplaceQuery(behaviour, world);
            }
            shadows.Add(body, branch);
            return body;
        }

        private static void ReplaceQuery(object behaviour, ICollisionQuery world)
        {
            foreach (FieldInfo field in behaviour.GetType().GetFields(Fields))
                if (field.FieldType == typeof(ICollisionQuery)) field.SetValue(behaviour, world);
        }
        private static void CopyScalars(object from, object to)
        {
            if (from.GetType() != to.GetType()) throw new InvalidOperationException("Replaced native block behaviour is not forecastable: " + from.GetType().FullName);
            foreach (FieldInfo field in from.GetType().GetFields(Fields))
                if (!field.IsInitOnly && (field.FieldType.IsValueType || field.FieldType == typeof(string))) field.SetValue(to, field.GetValue(from));
        }
        public static void CopyState(BodyComp from, BodyComp to)
        {
            RuntimeApi.Kernel.CheckThread();
            to.Position = from.Position; to.Velocity = from.Velocity;
            foreach (string name in StateFields) Set(to, name, typeof(BodyComp).GetField(name, Fields).GetValue(from));
        }
        public static bool TryPredict(BodyComp source, INativeFlightWorld world, out BodyComp landing, out int ticks, out string reason)
        {
            landing = null; ticks = 0; reason = null;
            try
            {
                BodyComp ghost = CreateShadow(source, world);
                for (ticks = 1; ticks <= 7200; ticks++)
                {
                    world.ValidateScreen();
                    AdvanceShadow(ghost, 1f / 60f);
                    if (float.IsNaN(ghost.Position.X) || float.IsNaN(ghost.Position.Y)
                        || float.IsInfinity(ghost.Position.X) || float.IsInfinity(ghost.Position.Y)) throw new InvalidOperationException("Non-finite flight");
                    if (ghost.IsOnGround && ghost.LastVelocity.Y >= 0) { landing = ghost; return true; }
                    world.AdvanceCamera(ghost);
                }
                reason = "No landing within 7200 native ticks";
            }
            catch (Exception error) { reason = error.Message; }
            return false;
        }
        public static void Commit(BodyComp landing, BodyComp real, BehaviourContext context)
        {
            RuntimeApi.Kernel.CheckThread();
            // Validate before touching the real body.
            var from = Get<Dictionary<Type, IBlockBehaviour>>(landing, "m_blockBehaviourLookup");
            var to = Get<Dictionary<Type, IBlockBehaviour>>(real, "m_blockBehaviourLookup");
            foreach (var pair in from)
                if (!to.ContainsKey(pair.Key) || pair.Value.GetType() != to[pair.Key].GetType()) throw new InvalidOperationException("Native block state changed during prediction");
            CopyState(landing, real);
            foreach (var pair in from) CopyScalars(pair.Value, to[pair.Key]);
            WriteWind(real, ReadWind(landing));
            BehaviourContext predicted = Get<BehaviourContext>(landing, "m_behaviourContext");
            CopyContext(predicted, context);
        }
        private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", Fields);
        private static void CopyContext(BehaviourContext predicted, BehaviourContext context)
        {
            context.Clear(); foreach (var pair in predicted) context[pair.Key] = pair.Value;
            context.FrameDelta = predicted.FrameDelta;
            foreach (string property in new[] { "CollisionInfo", "LastFrameCollisionInfo" })
                typeof(BehaviourContext).GetProperty(property).SetValue(context, Clone.Invoke(typeof(BehaviourContext).GetProperty(property).GetValue(predicted, null), null), null);
        }
        private static bool ReadWind(BodyComp body)
        {
            foreach (var stage in Get<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours"))
            {
                var wind = stage as Wind;
                if (wind != null) return wind.Enabled;
                if (stage is WindVelocityUpdateBehaviour) return Get<bool>(stage, "m_wind_enabled");
            }
            return false;
        }
        private static void WriteWind(BodyComp body, bool enabled)
        {
            foreach (var stage in Get<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours"))
            {
                var wind = stage as Wind;
                if (wind != null) wind.Enabled = enabled;
                else if (stage is WindVelocityUpdateBehaviour) Set(stage, "m_wind_enabled", enabled);
            }
        }
        private sealed class Wind : IBodyCompBehaviour
        {
            private readonly Branch branch;
            internal bool Enabled;
            internal Wind(Branch value) { branch = value; }
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                var forecast = branch.World as INativeFlightWind;
                if (forecast == null) return true; // Existing wind-free world contract.
                var body = context.BodyComp;
                var collision = branch.World.GetCollisionInfo(body.GetHitbox());
                if (!forecast.WindEnabled) Enabled = false;
                else if (body.IsOnGround || body.LastScreen > branch.World.Screen) Enabled = true;
                if (!body.IsOnBlock(typeof(SnowBlock)) && Enabled && collision.IsInWind())
                    body.Velocity.X += forecast.WindVelocity(branch.Tick);
                return true;
            }
        }
        private sealed class Cap : IBodyCompBehaviour
        {
            private readonly INativeFlightWorld world;
            internal Cap(INativeFlightWorld value) { world = value; }
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                BodyComp body = context.BodyComp; float x = body.Position.X; int width = body.GetHitbox().Width;
                if (world.HasTeleport)
                {
                    // The optional adapter runs at the following native stage.
                    int center = body.GetHitbox().Center.X;
                    if ((center < 0 || center > 480) && !(world is INativeFlightTeleports))
                        throw new InvalidOperationException("Trajectory activates a screen teleport on screen " + (world.Screen + 1));
                    return true;
                }
                body.Position.X = Math.Max(-width / 2f, Math.Min(480 - width / 2, x));
                if (x != body.Position.X) context["CappedXPosition"] = new object();
                return true;
            }
        }
        private sealed class Teleport : IBodyCompBehaviour
        {
            private readonly INativeFlightTeleports world;
            internal Teleport(INativeFlightWorld value) { world = value as INativeFlightTeleports; }
            public bool ExecuteBehaviour(BehaviourContext context)
            { if (world != null) world.HandleTeleport(context); return true; }
        }
        private sealed class CacheScreen : IBodyCompBehaviour
        {
            private readonly INativeFlightWorld world;
            internal CacheScreen(INativeFlightWorld value) { world = value; }
            public bool ExecuteBehaviour(BehaviourContext context)
            { Set(context.BodyComp, "<LastScreen>k__BackingField", world.Screen); return true; }
        }
    }
}

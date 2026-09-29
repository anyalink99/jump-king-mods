using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.Simulation;
using JumpKing;
using JumpKing.API;
using JumpKing.BlockBehaviours;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal sealed class NativeMemory
    {
        internal Vector2 LastVelocity;
        internal int LastScreen, Stable, History0, History1, History2, History3;
        internal bool Knocked, Water, PrevWater, Sand, Ice, Snow, Wind, JumpHeld, CanJump, Charging, WasGrounded, Splat, SplatDone;
        internal float Charge, AirCharge, SplatTime;
        internal bool Subframe, Buffered;
        internal int HeldTicks;
        internal double InputClock = .017;
        internal byte[] Encode()
        {
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(2); w.Write(LastVelocity.X); w.Write(LastVelocity.Y); w.Write(LastScreen); w.Write(Stable);
                w.Write(History0); w.Write(History1); w.Write(History2); w.Write(History3);
                foreach (bool b in new[] { Knocked, Water, PrevWater, Sand, Ice, Snow, Wind, JumpHeld, CanJump, Charging, WasGrounded, Splat, SplatDone }) w.Write(b);
                w.Write(Charge); w.Write(AirCharge); w.Write(SplatTime);
                w.Write(Subframe); w.Write(Buffered); w.Write(HeldTicks); w.Write(InputClock); return stream.ToArray();
            }
        }
        internal static NativeMemory Decode(byte[] data)
        {
            using (var stream = new MemoryStream(data)) using (var r = new BinaryReader(stream))
            {
                if (r.ReadInt32() != 2) throw new NotSupportedException("Native simulation state version");
                var m = new NativeMemory { LastVelocity = new Vector2(r.ReadSingle(), r.ReadSingle()), LastScreen = r.ReadInt32(), Stable = r.ReadInt32(),
                    History0 = r.ReadInt32(), History1 = r.ReadInt32(), History2 = r.ReadInt32(), History3 = r.ReadInt32(),
                    Knocked = r.ReadBoolean(), Water = r.ReadBoolean(), PrevWater = r.ReadBoolean(), Sand = r.ReadBoolean(), Ice = r.ReadBoolean(), Snow = r.ReadBoolean(),
                    Wind = r.ReadBoolean(), JumpHeld = r.ReadBoolean(), CanJump = r.ReadBoolean(), Charging = r.ReadBoolean(), WasGrounded = r.ReadBoolean(),
                    Splat = r.ReadBoolean(), SplatDone = r.ReadBoolean(), Charge = r.ReadSingle(), AirCharge = r.ReadSingle(), SplatTime = r.ReadSingle(),
                    Subframe = r.ReadBoolean(), Buffered = r.ReadBoolean(), HeldTicks = r.ReadInt32(), InputClock = r.ReadDouble() };
                if (stream.Position != stream.Length) throw new InvalidOperationException("Trailing native simulation state"); return m;
            }
        }
    }

    internal sealed class NativePlayer
    {
        internal const string Id = "jumpking.native";
        internal static readonly SimulationRequirement Requirement = new SimulationRequirement(Id, new Version(1, 0));
        private readonly LevelScreen[] screens;
        private readonly byte[] initial;
        private readonly bool snake;
        private readonly double windTickSeconds;
        private readonly bool[] verticalWind;
        private readonly CustomWindProfile[] customWind;
        internal bool ControlsEnabled = true;
        internal NativePlayer(LevelScreen[] map, NativeMemory memory, bool ring, double windClock = 1.0 / 60, bool[] verticalScreens = null, CustomWindProfile[] profiles = null)
        {
            screens = map; initial = memory.Encode(); snake = ring; windTickSeconds = windClock;
            if (verticalScreens != null && verticalScreens.Length != map.Length) throw new ArgumentException("Vertical wind screen count");
            verticalWind = verticalScreens == null ? new bool[map.Length] : (bool[])verticalScreens.Clone();
            if (profiles != null && profiles.Length != map.Length) throw new ArgumentException("Custom wind screen count");
            customWind = profiles == null ? new CustomWindProfile[map.Length] : (CustomWindProfile[])profiles.Clone();
        }
        internal SimulationProvider Provider()
        {
            return new SimulationProvider(Id, NativeWind.AuditedGameSha256, new[] { Requirement },
                Enum.GetValues(typeof(SimulationPhase)).Cast<SimulationPhase>(), seed => {
                    if (seed.Fingerprint != NativeWind.AuditedGameSha256) throw new NotSupportedException("Unaudited Jump King executable");
                    return (byte[])initial.Clone();
                }, Advance);
        }
        internal static T Field<T>(object obj, string name) { return (T)obj.GetType().GetField(name, NativeWorld.Flags).GetValue(obj); }
        internal static void Set(object obj, string name, object value) { obj.GetType().GetField(name, NativeWorld.Flags).SetValue(obj, value); }
        internal static NativeMemory Capture(BodyComp body)
        {
            var wind = Field<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours").Single(b => b.GetType() == typeof(WindVelocityUpdateBehaviour));
            var water = (WaterBlockBehaviour)Block(body, typeof(WaterBlock));
            var ice = Block(body, typeof(IceBlock));
            return new NativeMemory { LastVelocity = body.LastVelocity, LastScreen = body.LastScreen, Knocked = body.IsKnocked,
                Water = water.IsPlayerOnBlock, PrevWater = water.PrevIsPlayerOnBlock, Sand = body.IsOnBlock(typeof(SandBlock)),
                Ice = Field<bool>(ice, "m_isPlayerOnBlock"), Snow = body.IsOnBlock(typeof(SnowBlock)), Wind = Field<bool>(wind, "m_wind_enabled"),
                WasGrounded = body.IsOnGround };
        }
        private void Advance(SimulationTick frame)
        {
            if (frame.Phase == SimulationPhase.BeforeBody)
                frame.SetLocal("body", new Branch(screens, frame.Pose, NativeMemory.Decode(frame.State), snake, (float)frame.Seed.TickSeconds, windTickSeconds, verticalWind, customWind));
            var branch = frame.GetLocal<Branch>(Id, "body");
            if (ControlsEnabled || frame.Phase != SimulationPhase.Controls) branch.Run(frame);
            if (frame.Phase == SimulationPhase.World)
            {
                branch.World.AdvanceCamera(branch.Body);
                var m = branch.Memory; var body = branch.Body;
                m.LastVelocity = body.LastVelocity; m.LastScreen = body.LastScreen; m.Knocked = body.IsKnocked;
                var water = (WaterBlockBehaviour)Block(body, typeof(WaterBlock));
                m.Water = water.IsPlayerOnBlock; m.PrevWater = water.PrevIsPlayerOnBlock;
                m.Ice = branch.Ice.Raw; m.Snow = body.IsOnBlock(typeof(SnowBlock)); m.Sand = body.IsOnBlock(typeof(SandBlock));
                m.Stable = body.IsOnGround && body.Velocity.Y >= 0 && Math.Abs(body.Velocity.X) < .001f &&
                    !frame.Input.Jump && frame.Input.Direction == 0 && !body.IsKnocked && !m.Charging && !m.Splat ? m.Stable + 1 : 0;
                m.Stable = Math.Min(3, m.Stable);
                frame.Pose = new SimulationPose { Position = body.Position, Velocity = body.Velocity, Screen = branch.World.Screen,
                    Width = body.GetHitbox().Width, Height = body.GetHitbox().Height, Grounded = body.IsOnGround, StableLanding = m.Stable >= 3 };
                frame.State = m.Encode();
            }
        }
        internal static IBlockBehaviour Block(BodyComp body, Type type)
        { return Field<Dictionary<Type, IBlockBehaviour>>(body, "m_blockBehaviourLookup")[type]; }
        internal sealed class Branch
        {
            internal readonly BodyComp Body;
            internal readonly NativeMemory Memory;
            internal readonly NativeWorld World;
            internal readonly BehaviourContext Context;
            internal readonly IsolatedIce Ice;
            private readonly Dictionary<SimulationPhase, List<IBodyCompBehaviour>> pipeline = new Dictionary<SimulationPhase, List<IBodyCompBehaviour>>();
            private bool stopped;
            private readonly double windClock;
            private readonly bool[] verticalWind;
            private readonly CustomWindProfile[] customWind;
            internal Branch(LevelScreen[] screens, SimulationPose pose, NativeMemory memory, bool snake, float delta, double clock, bool[] verticalScreens, CustomWindProfile[] profiles)
            {
                windClock = clock; verticalWind = verticalScreens; customWind = profiles;
                Memory = memory; World = new NativeWorld(screens, pose.Screen);
                Body = new BodyComp(pose.Position, pose.Width, pose.Height) { Velocity = pose.Velocity };
                Set(Body, "_is_on_ground", pose.Grounded); Set(Body, "_last_velocity", memory.LastVelocity);
                Set(Body, "_knocked", memory.Knocked); Set(Body, "<LastScreen>k__BackingField", memory.LastScreen);
                var blocks = Field<LinkedList<IBlockBehaviour>>(Body, "m_blockBehaviours");
                var lookup = Field<Dictionary<Type, IBlockBehaviour>>(Body, "m_blockBehaviourLookup");
                var originalIce = lookup[typeof(IceBlock)]; Ice = new IsolatedIce(Body, snake) { Raw = memory.Ice };
                var iceNode = blocks.Find(originalIce); blocks.AddBefore(iceNode, Ice); blocks.Remove(iceNode); lookup[typeof(IceBlock)] = Ice;
                lookup[typeof(SandBlock)].IsPlayerOnBlock = memory.Sand; lookup[typeof(SnowBlock)].IsPlayerOnBlock = memory.Snow;
                var water = (WaterBlockBehaviour)lookup[typeof(WaterBlock)]; water.IsPlayerOnBlock = memory.Water; water.PrevIsPlayerOnBlock = memory.PrevWater;
                foreach (var block in blocks) ReplaceQuery(block);
                Context = Field<BehaviourContext>(Body, "m_behaviourContext"); Context.FrameDelta = delta;
                SimulationPhase phase = SimulationPhase.BeforeBody;
                foreach (var behaviour in Field<LinkedList<IBodyCompBehaviour>>(Body, "m_behaviours"))
                {
                    string name = behaviour.GetType().Name;
                    if (name == "WindVelocityUpdateBehaviour") { phase = SimulationPhase.BeforeX; continue; }
                    if (name == "UpdateXPositionFromVelocityBehaviour") phase = SimulationPhase.AfterX;
                    if (name == "UpdateYPositionFromVelocityBehaviour") phase = SimulationPhase.AfterY;
                    if (name == "ApplyGravityBehaviour") phase = SimulationPhase.AfterGravity;
                    if (name == "CacheLastScreenBehaviour" || name == "WaterParticleSpawningBehaviour" || name == "PlayBumpSFXBehaviour") continue;
                    if (name == "CapPositionBehaviour" || name == "HandlePlayerTeleportBehaviour") continue;
                    ReplaceQuery(behaviour);
                    if (!pipeline.ContainsKey(phase)) pipeline.Add(phase, new List<IBodyCompBehaviour>());
                    pipeline[phase].Add(behaviour);
                }
            }
            private void ReplaceQuery(object value)
            {
                foreach (var field in value.GetType().GetFields(NativeWorld.Flags))
                    if (field.FieldType == typeof(ICollisionQuery)) field.SetValue(value, World);
            }
            internal void Run(SimulationTick frame)
            {
                if (frame.Phase == SimulationPhase.Controls) { Controls(frame); return; }
                if (stopped) return;
                if (frame.Phase == SimulationPhase.BeforeX)
                {
                    var screen = World.Screens[World.Screen];
                    if (!screen.WindEndabled) Memory.Wind = false;
                    else if (Body.IsOnGround || Body.LastScreen > World.Screen) Memory.Wind = true;
                    float force = 0;
                    if (!Body.IsOnBlock(typeof(SnowBlock)) && Memory.Wind && World.GetCollisionInfo(Body.GetHitbox()).IsInWind())
                        force = customWind[World.Screen].Velocity(frame.Tick, windClock, screen);
                    bool vertical = verticalWind[World.Screen];
                    if (vertical) Body.Velocity.Y += force;
                    else Body.Velocity.X += force;
                    if (screen.WindEndabled) frame.Emit(new SimulationEvent(vertical ? "wind-vertical" : "wind", Body.Position, force));
                    Set(Body, "<LastScreen>k__BackingField", World.Screen);
                }
                List<IBodyCompBehaviour> stages;
                if (!pipeline.TryGetValue(frame.Phase, out stages)) return;
                foreach (var behaviour in stages)
                {
                    if (!NativeResolution.Run(behaviour, Context, World, 1.5f)) { stopped = true; break; }
                    if (behaviour is UpdateXPositionFromVelocityBehaviour) CapAndTeleport(frame);
                }
            }
            private void CapAndTeleport(SimulationTick frame)
            {
                var screen = World.Screens[World.Screen];
                if (!screen.CanTeleport)
                {
                    float x = Body.Position.X; int width = Body.GetHitbox().Width;
                    Body.Position.X = Math.Max(-width / 2f, Math.Min(480 - width / 2, x));
                    if (x != Body.Position.X) Context["CappedXPosition"] = new object(); return;
                }
                Point center = Body.GetHitbox().Center;
                if (center.X >= 0 && center.X <= 480) return;
                bool left = center.X < 0;
                int target = screen.IsTwoTeleports ? screen.teleport[left ? 0 : 1].GetIndex0() : screen.teleport.First(t => t.IsEnabled).GetIndex0();
                if (target < 0 || target >= World.Screens.Length) throw new NotSupportedException("Invalid teleport target");
                Body.Position.X += left ? 480 : -480;
                int from = (int)(-(Body.Position.Y - 360f) / 360f);
                int offset = 360 * (target - from); Body.Position.Y = (int)Body.Position.Y - offset;
                center.Y -= offset;
                World.Screen = Math.Max(0, Math.Min(World.Screens.Length - 1, -(int)Math.Floor(center.Y / 360f)));
                Context["TeleportedPlayer"] = new object(); frame.Emit(new SimulationEvent(left ? "teleport-left" : "teleport-right", Body.Position, World.Screen));
            }
            private void Controls(SimulationTick frame)
            {
                var m = Memory; var input = frame.Input; float dt = (float)frame.Seed.TickSeconds;
                if (m.Subframe) m.HeldTicks = m.JumpHeld ? Math.Min(10000, m.HeldTicks + 1) : 0;
                if (input.Jump && !m.JumpHeld) m.CanJump = true;
                if (!input.Jump) m.CanJump = false;
                m.JumpHeld = input.Jump;
                bool ground = Body.IsOnGround || Body.IsOnBlock(typeof(SandBlock));
                if (ground && !m.WasGrounded && Body.LastVelocity.Y == PlayerValues.MAX_FALL)
                { m.Splat = true; m.SplatTime = 0; m.SplatDone = false; m.Charging = false; m.Charge = 0; }
                m.WasGrounded = ground;
                if (m.Splat)
                {
                    if (!Ice.IsPlayerOnBlock) Body.Velocity.X = 0;
                    if (!m.SplatDone && Body.IsOnGround) { m.SplatTime += dt; m.SplatDone = m.SplatTime >= PlayerValues.SPLAT_TIME; }
                    if (m.SplatDone && (input.Jump || input.Direction != 0)) { m.Splat = false; m.SplatTime = 0; }
                    return;
                }
                if (!ground)
                {
                    if (!m.Charging) return;
                    m.AirCharge += dt;
                    if (m.AirCharge > 1f / 30f) { m.Charging = false; m.Charge = 0; return; }
                }
                else m.AirCharge = 0;
                m.History3 = m.History2; m.History2 = m.History1; m.History1 = m.History0; m.History0 = input.Direction;
                if (m.Charging || m.CanJump)
                {
                    if (!m.Charging) { m.Buffered = m.HeldTicks > 0; m.CanJump = false; m.Charge = 0; m.Charging = true; if (!Ice.IsPlayerOnBlock) Body.Velocity.X = 0; }
                    if (!Ice.IsPlayerOnBlock && Body.IsOnGround) Body.Velocity.X = 0;
                    float multiplier = ((WaterBlockBehaviour)Block(Body, typeof(WaterBlock))).GetWaterMultiplier();
                    if (m.Subframe && !m.Buffered && !input.Jump)
                        m.Charge = SubframeTimer.BeforeRelease(m.HeldTicks * m.InputClock, multiplier, (double)(dt * multiplier));
                    m.Charge += dt * multiplier;
                    if (m.Charge >= PlayerValues.JUMP_TIME || !input.Jump)
                    {
                        float power = Math.Min(1, m.Charge / PlayerValues.JUMP_TIME);
                        int direction = new[] { m.History0, m.History1, m.History2, m.History3 }.FirstOrDefault(d => d != 0);
                        m.Charging = false; m.Charge = 0; m.History0 = m.History1 = m.History2 = m.History3 = 0;
                        if (Body.IsOnBlock(typeof(SnowBlock)) && power < .3f) { if (power <= .2f) return; power = .3f; }
                        Body.Velocity.Y = PlayerValues.JUMP * power;
                        Body.Velocity.X = Math.Max(-PlayerValues.SPEED, Math.Min(PlayerValues.SPEED, Body.Velocity.X + direction * PlayerValues.SPEED));
                        frame.Emit(new SimulationEvent("takeoff", Body.Position, power));
                    }
                    return;
                }
                if (!ground || (Body.Velocity.Y < 0 && Body.IsOnBlock(typeof(SandBlock)))) return;
                float target = input.Direction * 1.5f;
                if (Body.IsOnBlock(typeof(SnowBlock))) Body.Velocity.X = 0;
                else if (Ice.IsPlayerOnBlock)
                {
                    if (target != 0 && (Math.Sign(target) != Math.Sign(Body.Velocity.X) || Math.Abs(target) > Math.Abs(Body.Velocity.X)))
                        Body.Velocity.X = Approach(Body.Velocity.X, target, PlayerValues.ICE_FRICTION * 2);
                }
                else Body.Velocity.X = target;
            }
        }
        internal static float Approach(float value, float target, float step)
        { return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step); }
        internal sealed class IsolatedIce : IBlockBehaviour
        {
            private readonly BodyComp body; private readonly bool snake;
            internal bool Raw;
            internal IsolatedIce(BodyComp value, bool ring) { body = value; snake = ring; }
            public float BlockPriority { get { return 2; } }
            public bool IsPlayerOnBlock { get { return snake ? body.IsOnGround : Raw; } set { Raw = value; } }
            public float ModifyXVelocity(float x, BehaviourContext c) { return x; }
            public float ModifyYVelocity(float y, BehaviourContext c) { return y; }
            public float ModifyGravity(float g, BehaviourContext c) { return g; }
            public bool AdditionalXCollisionCheck(AdvCollisionInfo info, BehaviourContext c) { return false; }
            public bool AdditionalYCollisionCheck(AdvCollisionInfo info, BehaviourContext c) { return false; }
            public bool ExecuteBlockBehaviour(BehaviourContext c)
            { if (IsPlayerOnBlock) body.Velocity.X = Approach(body.Velocity.X, 0, PlayerValues.ICE_FRICTION); return true; }
        }
    }
}

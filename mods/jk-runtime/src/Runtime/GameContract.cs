using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using JumpKing.Player;

namespace JKRuntime
{
    // Read-only first-release contract. No jump ownership or physics hooks.
    /// <summary>Reviewed private native-field access. Available means signatures exist; KnownFingerprint separately identifies audited executable behavior.</summary>
    public sealed class GameContract
    {
        private readonly FieldInfo jump;
        private readonly FieldInfo timer;
        public string Fingerprint { get; private set; }
        public bool KnownFingerprint { get; private set; }
        public bool Available { get; private set; }
        public string[] Findings { get { return (string[])findings.Clone(); } }
        private readonly string[] findings;
        internal GameContract()
        {
            var report = new List<string>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            jump = typeof(PlayerEntity).GetField("m_jump_state", flags);
            timer = typeof(JumpState).GetField("m_timer", flags);
            Available = true;
            if (jump == null || jump.FieldType != typeof(JumpState))
            { Available = false; report.Add("missing: PlayerEntity.m_jump_state : JumpState"); }
            if (timer == null || timer.FieldType != typeof(float))
            { Available = false; report.Add("missing: JumpState.m_timer : Single"); }
            if (typeof(BodyComp).GetMethod("GetBehaviourList", Type.EmptyTypes) == null)
            { Available = false; report.Add("missing: BodyComp.GetBehaviourList()"); }
            try
            {
                using (var stream = File.OpenRead(typeof(PlayerEntity).Assembly.Location))
                using (var sha = SHA256.Create())
                    Fingerprint = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception error) { Fingerprint = "unavailable"; report.Add("fingerprint: " + error.Message); }
            KnownFingerprint = Fingerprint == "476f2033b8b614ec97b04311799b2b78239397a2fe8018c77bb45a1f946ffc88";
            report.Add(KnownFingerprint ? "Known installed game fingerprint." : "Unrecognized game fingerprint; structural checks are not a gameplay verification.");
            report.Add(Available ? "Read-only jump/body contract validated." : "Contract capability unavailable; no private writes attempted.");
            findings = report.ToArray();
        }
        /// <summary>Read the current native jump-node slot, including an installed subclass. Never cache it across controller changes.</summary>
        public JumpState GetJumpState(PlayerEntity player)
        {
            Check();
            if (player == null) throw new ArgumentNullException("player");
            return (JumpState)jump.GetValue(player);
        }
        /// <summary>Read the native physics charge timer, not wall-clock input duration.</summary>
        public float ReadChargeTimer(JumpState state)
        {
            Check();
            if (state == null) throw new ArgumentNullException("state");
            return (float)timer.GetValue(state);
        }
        public string[] DescribeBody(BodyComp body)
        {
            Check();
            if (body == null) throw new ArgumentNullException("body");
            var result = new List<string>();
            foreach (var behaviour in body.GetBehaviourList()) result.Add(behaviour.GetType().FullName);
            return result.ToArray();
        }
        private void Check()
        { RuntimeApi.Kernel.CheckThread(); if (!Available) throw new InvalidOperationException(string.Join("; ", findings)); }
    }
}

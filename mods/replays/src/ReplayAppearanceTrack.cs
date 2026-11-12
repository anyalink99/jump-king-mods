using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;

namespace Replays
{
    internal static class ReplayAppearanceTrack
    {
        private static readonly FieldInfo AppliedSkins =
            typeof(Game1).Assembly.GetType(
                "JumpKing.Player.Skins.SkinManager",
                true).GetField(
                "m_applied_skins",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo SkinItem =
            typeof(Game1).Assembly.GetType(
                "JumpKing.Player.Skins.Skin",
                true).GetField(
                "item",
                BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo AppliedSkinsVersion =
            AppliedSkins == null
                ? null
                : AppliedSkins.FieldType.GetField(
                    "_version",
                    BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly int MaximumItem = (int)Items.NULL - 1;
        private static readonly int[] EmptyItems = new int[0];

        internal static void ValidateContract()
        {
            if (AppliedSkins == null
                || SkinItem == null
                || AppliedSkinsVersion == null)
            {
                throw new InvalidOperationException(
                    "Jump King equipped-skin contract is unavailable");
            }
        }

        internal static int CurrentVersion()
        {
            ValidateContract();
            object skins = AppliedSkins.GetValue(null);
            return skins == null
                ? 0
                : (int)AppliedSkinsVersion.GetValue(skins);
        }

        internal static int[] Capture()
        {
            List<int> items = new List<int>(5);
            Capture(items);
            return items.ToArray();
        }

        internal static void Capture(List<int> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            ValidateContract();
            items.Clear();
            IEnumerable skins = AppliedSkins.GetValue(null) as IEnumerable;
            if (skins == null) return;
            foreach (object skin in skins)
            {
                int item = (int)(Items)SkinItem.GetValue(skin);
                if (item < 0 || item > MaximumItem || items.Contains(item))
                    continue;
                items.Add(item);
            }
        }

        internal static void RecordIfChanged(
            ReplayData replay,
            int frame,
            List<int> scratch)
        {
            if (replay == null) throw new ArgumentNullException("replay");
            if (replay.AppearanceEvents == null)
                replay.AppearanceEvents = new List<ReplayAppearanceEvent>();
            Capture(scratch);
            ReplayAppearanceEvent previous = replay.AppearanceEvents.Count == 0
                ? null
                : replay.AppearanceEvents[replay.AppearanceEvents.Count - 1];
            if (previous != null && Same(previous.Items, scratch)) return;
            ReplayAppearanceEvent next = new ReplayAppearanceEvent
            {
                Frame = Math.Max(0, frame),
                Items = scratch.ToArray()
            };
            if (previous != null && previous.Frame == next.Frame)
                replay.AppearanceEvents[replay.AppearanceEvents.Count - 1] = next;
            else replay.AppearanceEvents.Add(next);
        }

        internal static int[] AtFrame(ReplayData replay, int frame)
        {
            if (replay == null || replay.AppearanceEvents == null
                || replay.AppearanceEvents.Count == 0)
            {
                return EmptyItems;
            }
            for (int index = replay.AppearanceEvents.Count - 1;
                index >= 0;
                index--)
            {
                ReplayAppearanceEvent appearance = replay.AppearanceEvents[index];
                if (appearance != null && appearance.Frame <= frame)
                    return appearance.Items ?? EmptyItems;
            }
            return EmptyItems;
        }

        internal static bool Same(int[] left, int[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }

        private static bool Same(int[] left, IList<int> right)
        {
            if (left == null || right == null || left.Length != right.Count)
                return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }
    }
}

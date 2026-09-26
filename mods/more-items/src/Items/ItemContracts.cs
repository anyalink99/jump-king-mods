using System;
using System.Collections.Generic;
using System.IO;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace MoreItems
{
    public sealed class ConsumableHotkey
    {
        public string Label { get; private set; }
        public int[] DefaultBindings { get; private set; }
        public JKRuntime.UI.UiChord[] DefaultChords { get; private set; }

        public ConsumableHotkey(string label, params int[] defaultBindings)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Use" : label;
            DefaultBindings = defaultBindings == null ? new int[0] : (int[])defaultBindings.Clone();
            DefaultChords = JKRuntime.UI.UiChord.FromAlternatives(DefaultBindings);
        }

        private ConsumableHotkey(string label, JKRuntime.UI.UiChord[] defaultChords)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Use" : label;
            DefaultChords = CloneChords(defaultChords);
            DefaultBindings = JKRuntime.UI.UiChord.ToAlternatives(DefaultChords);
        }

        public static ConsumableHotkey FromChords(
            string label,
            params JKRuntime.UI.UiChord[] defaultChords)
        {
            return new ConsumableHotkey(label, defaultChords);
        }

        private static JKRuntime.UI.UiChord[] CloneChords(JKRuntime.UI.UiChord[] chords)
        {
            List<JKRuntime.UI.UiChord> result = new List<JKRuntime.UI.UiChord>();
            foreach (JKRuntime.UI.UiChord chord in chords ?? new JKRuntime.UI.UiChord[0])
            {
                if (chord == null || chord.IsEmpty) continue;
                result.Add(new JKRuntime.UI.UiChord(chord.Buttons));
                if (result.Count == 2) break;
            }
            return result.ToArray();
        }
    }

    public sealed class ConsumableDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string PluralName { get; private set; }
        public string Description { get; private set; }
        public Func<bool> CanUse { get; private set; }
        public Func<bool> Use { get; private set; }
        public Color Color { get; private set; }
        public ConsumableHotkey Hotkey { get; private set; }
        public bool IsUsable { get; private set; }
        public Action<Rectangle> DrawIcon { get; private set; }
        public bool ConsumesOnUse { get; private set; }
        public Func<string> GetActionLabel { get; private set; }
        public Func<bool> IsEnabled { get; private set; }
        public Func<bool> IsEquipped { get; private set; }
        public Func<bool, bool> SetEquipped { get; private set; }
        public bool IsEquipment { get { return SetEquipped != null; } }

        public ConsumableDefinition(
            string id,
            string name,
            string description,
            Func<bool> canUse,
            Func<bool> use,
            Color color,
            string pluralName = null,
            ConsumableHotkey hotkey = null,
            Action<Rectangle> drawIcon = null,
            bool consumesOnUse = true,
            Func<string> getActionLabel = null,
            Func<bool> isEnabled = null,
            Func<bool> isEquipped = null,
            Func<bool, bool> setEquipped = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Item id is required", "id");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required", "name");
            Id = id.Trim();
            Name = name;
            PluralName = string.IsNullOrWhiteSpace(pluralName) ? name + "s" : pluralName;
            Description = description ?? string.Empty;
            CanUse = canUse ?? delegate { return true; };
            IsUsable = use != null;
            Use = use ?? delegate { return false; };
            Color = color;
            Hotkey = hotkey;
            DrawIcon = drawIcon ?? delegate(Rectangle destination) { ConsumableArt.DrawDefault(destination, color); };
            ConsumesOnUse = consumesOnUse;
            GetActionLabel = getActionLabel ?? delegate { return "Use"; };
            IsEnabled = isEnabled ?? delegate { return true; };
            if ((isEquipped == null) != (setEquipped == null))
                throw new ArgumentException(
                    "Equipment requires both state and setter callbacks");
            IsEquipped = isEquipped;
            SetEquipped = setEquipped;
        }
    }
}

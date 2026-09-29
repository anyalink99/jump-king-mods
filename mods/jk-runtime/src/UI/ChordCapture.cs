using System;
using System.Collections.Generic;

namespace JKRuntime.UI
{
    internal sealed class ChordCapture
    {
        private readonly int maximumButtons;
        private readonly List<int> buttons = new List<int>();
        private bool started;
        private int releaseFrames;

        internal ChordCapture(int maximum)
        {
            if (maximum < 1 || maximum > 2) throw new ArgumentOutOfRangeException("maximum");
            maximumButtons = maximum;
        }

        internal bool Update(int[] pressed, out int[] result)
        {
            result = null;
            int[] current = pressed ?? new int[0];
            if (!started)
            {
                if (current.Length == 0) return false;
                started = true;
            }
            foreach (int button in current)
            {
                if (button < 0 || buttons.Contains(button)) continue;
                if (buttons.Count < maximumButtons) buttons.Add(button);
            }
            if (current.Length != 0)
            {
                releaseFrames = 0;
                return false;
            }
            if (buttons.Count == 0 || ++releaseFrames < 3) return false;
            result = buttons.ToArray();
            return true;
        }
    }
}

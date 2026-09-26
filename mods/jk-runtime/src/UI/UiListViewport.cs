using System;

namespace JKRuntime.UI
{
    /// <summary>Scroll position independent of hover selection. One instance per list/page.</summary>
    public sealed class UiListViewport
    {
        private int first;
        private bool initialized;

        /// <summary>Read/clamp the viewport during Draw; selection changes do not scroll it.</summary>
        public int FirstVisible(int count, int visibleRows, int selected)
        {
            Validate(count, visibleRows);
            if (!initialized) FollowSelection(selected, count, visibleRows);
            first = Math.Max(0, Math.Min(first, Math.Max(0, count - visibleRows)));
            return first;
        }

        /// <summary>Explicit keyboard/controller navigation follows its selected row.</summary>
        public void FollowSelection(int selected, int count, int visibleRows)
        {
            Validate(count, visibleRows);
            initialized = true;
            int index = Math.Max(0, Math.Min(selected, count - 1));
            first = Math.Max(0, Math.Min(index - visibleRows / 2, count - visibleRows));
        }

        /// <summary>Scroll by rows (positive is down), retaining selection's visible row.</summary>
        public int Scroll(int rows, int count, int visibleRows, int selected)
        {
            int previous = FirstVisible(count, visibleRows, selected);
            first = (int)Math.Max(0L, Math.Min((long)first + rows, Math.Max(0, count - visibleRows)));
            if (count == 0) return 0;
            return (int)Math.Max(first, Math.Min((long)selected + first - previous, Math.Min(count - 1, first + visibleRows - 1)));
        }

        public void Reset() { first = 0; initialized = false; }
        private static void Validate(int count, int visibleRows)
        {
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            if (visibleRows <= 0) throw new ArgumentOutOfRangeException("visibleRows");
        }
    }
}

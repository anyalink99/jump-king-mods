using System;
using System.Collections.Generic;

namespace JKRuntime.UI
{
    internal sealed class CompactGridLayout
    {
        internal struct Page { internal int First, Last, Height; }
        internal readonly int[] Tops, Heights, PageOf;
        internal readonly Page[] Pages;

        internal CompactGridLayout(int[] heights, int columns, int availableHeight, int gap)
        {
            if (columns < 1 || availableHeight < 1 || gap < 0) throw new ArgumentOutOfRangeException();
            Tops = new int[heights.Length]; Heights = new int[heights.Length]; PageOf = new int[heights.Length];
            var pages = new List<Page>();
            int first = 0, used = 0;
            for (int row = 0; row < heights.Length; row += columns)
            {
                int end = Math.Min(heights.Length, row + columns), tallest = 1;
                for (int i = row; i < end; i++) tallest = Math.Max(tallest, heights[i]);
                if (tallest > availableHeight) throw new ArgumentException("A grid item exceeds the page height");
                if (used != 0 && used + gap + tallest > availableHeight)
                { pages.Add(new Page { First = first, Last = row, Height = used }); first = row; used = 0; }
                int y = used == 0 ? 0 : used + gap;
                for (int i = row; i < end; i++) { Tops[i] = y; Heights[i] = tallest; PageOf[i] = pages.Count; }
                used = y + tallest;
            }
            if (heights.Length > 0) pages.Add(new Page { First = first, Last = heights.Length, Height = used });
            Pages = pages.ToArray();
        }
    }
}

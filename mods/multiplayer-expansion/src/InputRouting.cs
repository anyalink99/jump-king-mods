using System;

namespace MultiplayerExpansion
{
    internal static class InputRouting
    {
        internal static IntPtr Foreground(IntPtr foreground, IntPtr root, IntPtr host, int selected, int role, IntPtr client)
        {
            // inactive observations must stay inactive even if the user switches back before consumption
            return foreground != IntPtr.Zero && host != IntPtr.Zero && root == host && selected == role ? client : IntPtr.Zero;
        }
    }
}

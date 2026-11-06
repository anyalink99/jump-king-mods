namespace MultiplayerExpansion
{
    internal sealed class ClientSwitchKey
    {
        internal const int Key=0x36;
        private bool held=true;
        internal bool Poll(bool enabled,bool focused,bool down)
        {
            if(!enabled || !focused) {held=true;return false;}
            bool pressed=down && !held;held=down;return pressed;
        }
    }
}

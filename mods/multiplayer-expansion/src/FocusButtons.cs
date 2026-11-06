using System;

namespace MultiplayerExpansion
{
    // a held button belongs to the old focus until the device returns to neutral
    internal sealed class FocusButtons
    {
        private bool focused,armed;
        internal int[] Filter(bool selected,int[] buttons)
        {
            buttons=buttons ?? new int[0];
            if(!selected) {focused=armed=false;return new int[0];}
            if(!focused) {focused=true;armed=false;}
            if(buttons.Length==0) armed=true;
            return armed ? buttons : new int[0];
        }
    }
}

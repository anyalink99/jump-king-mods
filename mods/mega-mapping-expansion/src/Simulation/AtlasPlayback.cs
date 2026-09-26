using System;
namespace MegaMappingExpansion
{
    internal static class AtlasPlayback
    {
        internal static int Count(TextureData data)
        {return data.Frames>0?data.Frames:data.Columns*data.Rows*(1+data.Pages.Length);}
        internal static void Frame(TextureData data,float age,out int page,out int cell)
        {
            int perPage=data.Columns*data.Rows;
            int frame=data.Fps<=0?0:(int)((Math.Max(0,(double)age)*data.Fps)%Count(data));
            page=frame/perPage;cell=frame%perPage;
        }
    }
}

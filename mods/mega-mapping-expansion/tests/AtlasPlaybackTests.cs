using System;
namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void AtlasChecks()
        {
            var data=new TextureData{Columns=3,Rows=2,Fps=12,Frames=10,Pages=new[]{new TexturePageData{Path="next.png"}}};
            int page,cell;AtlasPlayback.Frame(data,.5f,out page,out cell);
            Require(page==1&&cell==0,"atlas advances to the next page without a blank frame");
            AtlasPlayback.Frame(data,10/12f+.00001f,out page,out cell);
            Require(page==0&&cell==0,"partial final atlas page wraps before unused cells");
            AtlasPlayback.Frame(data,-1,out page,out cell);Require(page==0&&cell==0,"atlas negative age clamps to the first frame");
            AtlasPlayback.Frame(data,float.MaxValue,out page,out cell);Require(page>=0&&page<2&&cell>=0&&cell<6,"long-running atlas playback avoids integer overflow");
            data.Pages=null;data.Frames=0;Require(AtlasPlayback.Count(data)==6,"omitted pages preserve single-atlas playback");
        }
    }
}

using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void ScriptedViewTests()
    {
        var view=new ScriptedView();var shot=new CameraShot(new Vector2(240,-180),1,true,1,.5);
        view.Advance(shot,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>true,0);
        Check(view.Y>0&&view.Y<360,"Scripted focus interpolates toward an authored screen");
        float held=view.Y;view.Advance(shot,Vector2.Zero,Vector2.Zero,.1,true,(a,b)=>true,0);Near(view.Y,held,"Native pause holds scripted movement");
        for(int i=0;i<12;i++)view.Advance(shot,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>true,0);
        Near(view.Y,360,"Scripted focus reaches the exact screen frame");
        view.Advance(null,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>true,0);Check(view.Active&&view.Y<360&&view.Y>0,"Release returns toward live tracking");
        for(int i=0;i<6;i++)view.Advance(null,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>true,0);
        Check(!view.Active,"Return completion relinquishes camera control");
        view.Advance(shot,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>false,0);Near(view.Y,360,"Disconnected screen IDs cut instead of panning across unrelated rooms");
        view.Reset();Check(!view.Active,"Restart clears scripted presentation state");
    }
}

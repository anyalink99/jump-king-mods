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
        view.Rebase(new Vector2(480,720));
        view.Advance(shot,new Vector2(480,720),Vector2.Zero,0,false,(a,b)=>true,2);
        Near(view.Y,360,"Held authored shot remains on its native point while the player crosses a seam");
        Check(view.Screen==1,"Held shot keeps the authored screen as its render anchor");
        view.Advance(null,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>true,0);Check(view.Active&&view.Y<360&&view.Y>0,"Release returns toward live tracking");
        float returning=view.Y; view.Rebase(new Vector2(480,720));
        view.Advance(null,new Vector2(480,720),new Vector2(480,720),0,false,(a,b)=>true,2);
        Near(view.Y,returning+720,"Return transition rebases with the player at zero elapsed time");
        Check(view.Screen==2,"Returning shot renders the current player's chart");
        for(int i=0;i<6;i++)view.Advance(null,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>true,0);
        Check(!view.Active,"Return completion relinquishes camera control");
        view.Advance(shot,Vector2.Zero,Vector2.Zero,.1,false,(a,b)=>false,0);Near(view.Y,360,"Disconnected screen IDs cut instead of panning across unrelated rooms");
        view.Reset();Check(!view.Active,"Restart clears scripted presentation state");
    }
}

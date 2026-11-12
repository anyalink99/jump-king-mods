using System;
using JKRuntime.World;
using JKRuntime.Gameplay;
using JKRuntime.Presentation;
using JKRuntime.Input;
using JKRuntime.State;

internal static class LegacyConsumer213
{
    private static int Main()
    {
        var world=new WorldRegistry();byte state=7;
        using(world.Register("legacy.fixture",1,()=>new[]{state},bytes=>()=>state=bytes[0])){
            byte[] saved=world.Capture();state=9;world.Apply(saved);if(state!=7)return 1;
        }
        if(new MaterialRegistry().Generation!=0||new SnapshotService().RestoreEpoch!=0)return 2;
        if(ActorVisuals.Valid(new byte[]{255}))return 3;
        bool rejected=false;
        try{NativeInputFrames.Publish(null,new JumpKing.Controller.PadState(),new JumpKing.Controller.PadState());}catch(ArgumentNullException){rejected=true;}
        if(!rejected)return 4;
        Console.WriteLine("[OK] Unchanged API 2.13 consumer: world transaction, materials, state, presentation and input");
        return 0;
    }
}

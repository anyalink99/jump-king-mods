using System;
using MegaMappingExpansion.Api;
public static class MappingExample
{
    public static double HeldFrames(IMappingScene scene){return scene.Signals.Read("charge.heldFrames").Value.X;}
    public static IDisposable ObserveCharge(IMappingScene scene,Action<SignalSample> observer){return scene.Signals.Subscribe("charge.heldFrames",observer);}
    public static ISceneEffect Pulse(IMappingScene scene){return scene.Activate("example.consumer","pulse");}
}
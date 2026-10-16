using System;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal static class PlayerObservations
    {
        internal static void Declare(SignalHub signals)
        {
            foreach(string name in new[]{"moving","stationary","rising","falling","airborne","walking","splat"})
                signals.Declare(new SignalDescriptor("player."+name,SceneValueKind.Boolean,observation:true),SceneValue.Boolean(false));
            signals.Declare(new SignalDescriptor("player.speed",SceneValueKind.Number,"px/s",true),SceneValue.Number(0));
        }
        internal static void Publish(SceneProgram scene,double vx,double vy,bool grounded,bool walking,bool splat,bool emit)
        {
            double speed=Math.Sqrt(vx*vx+vy*vy);
            State(scene,"moving",speed>.001,emit);State(scene,"stationary",speed<=.001,emit);
            State(scene,"rising",!grounded&&vy<-.001,emit);State(scene,"falling",!grounded&&vy>.001,emit);
            State(scene,"airborne",!grounded,emit);State(scene,"walking",grounded&&walking&&!splat,emit);State(scene,"splat",splat,emit);
            scene.Signals.Publish("player.speed",SceneValue.Number(speed),ObservationQuality.Sampled);
        }
        private static void State(SceneProgram scene,string name,bool value,bool emit)
        {
            string id="player."+name;var previous=scene.Signals.Read(id);
            scene.Signals.Publish(id,SceneValue.Boolean(value),ObservationQuality.Sampled);
            if(emit&&previous.Quality!=ObservationQuality.Unavailable&&previous.Value.X!=(value?1:0))
                scene.Signals.Emit(id+(value?".enter":".exit"),"native.player","player",(int)scene.Signals.Read("player.screen").Value.X,scene.GameplayTime);
        }
    }
}

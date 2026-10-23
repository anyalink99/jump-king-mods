using System;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class SceneProgram
    {
        private string[] observedTexts;
        private void ObserveText(bool emit)
        {
            if(observedTexts==null)observedTexts=Components.Values.Where(c=>c.Descriptor.Kind=="text").Select(c=>c.Id).ToArray();
            foreach(string id in observedTexts)
            {
                string text=Properties.Read(id,"value").Text.Replace("\r","");
                int count=Math.Min(text.Length,(int)Properties.Read(id,"visibleCharacters").X);
                var previous=Signals.Read(id+".revealed");bool same=Signals.Read(id+".content").Value.Text==text;
                bool complete=count==text.Length,wasComplete=Signals.Read(id+".complete").Value.X!=0;
                Signals.Publish(id+".content",SceneValue.String(text),ObservationQuality.Exact);
                Signals.Publish(id+".revealed",SceneValue.Integer(count),ObservationQuality.Exact);
                Signals.Publish(id+".complete",SceneValue.Boolean(complete),ObservationQuality.Exact);
                if(!emit||!same||previous.Quality==ObservationQuality.Unavailable)continue;
                string transform=Properties.Read(id,"transform").Text;
                int screen=transform==""?0:(int)Properties.Read(transform,"screen").X;
                if(screen==0)screen=(int)Signals.Read("player.screen").Value.X;
                if(count>previous.Value.X)Signals.Emit(id+".character",id,"",screen,gameplayTime,
                    new SignalSample("from",SceneValue.Integer((int)previous.Value.X),ObservationQuality.Exact,0),new SignalSample("to",SceneValue.Integer(count),ObservationQuality.Exact,0));
                if(complete&&!wasComplete)Signals.Emit(id+".finished",id,"",screen,gameplayTime);
            }
        }
    }
}

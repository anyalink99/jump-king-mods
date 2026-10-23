using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using JKRuntime.UI;
using MegaMappingExpansion.Api;
using MegaMappingExpansion.Construction;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class SceneInspector:ScopedUiPage
    {
        private readonly UiFrame frame=new UiFrame(new Rectangle(12,12,456,336));
        private readonly UiList list=new UiList();
        private readonly Stack<Action> back=new Stack<Action>();
        private NativeScene scene;
        private Action refresh;
        private UiNumberControl numeric;
        private string title="MAPPING INSPECTOR",message="";
        private bool Editable{get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-debug")>=0&&JKRuntime.World.WorldControl.Owner==null;}}
        protected override void OpenPage(JKRuntime.RuntimeScope scope){scene=NativeScene.Current;Root();}
        private void Show(string heading,IEnumerable<UiListItem> items,Action update){UiPointer.CancelCapture(this);title=heading;numeric=null;refresh=update;list.SetItems(items);}
        private void Enter(Action action){var previous=refresh??Root;back.Push(previous);Run(action);}
        private void Run(Action action){try{message="";action();}catch(Exception error){message=error.GetBaseException().Message;ModEntry.Log("Inspector: "+error);}}
        private void Root()
        {
            if(scene==null){Show(title,new[]{new UiListItem("empty","Load a map with a construction scene first.")},Root);return;}
            Show("MAPPING INSPECTOR",new[]{
                new UiListItem("components","Components and effective properties",()=>Enter(Components)),
                new UiListItem("signals","Typed observations and quality",()=>Enter(Signals)),
                new UiListItem("variables","Scene variables",()=>Enter(Variables)),
                new UiListItem("effects","Effect library",()=>Enter(Effects)),
                new UiListItem("active","Active effects and owners",()=>Enter(ActiveEffects)),
                new UiListItem("trees","Control trees",()=>Enter(Trees)),
                new UiListItem("errors","Diagnostics",()=>Enter(Errors)),
                new UiListItem("clear","Clear inspector preview",()=>Run(()=>{scene.Program.Properties.RemoveOwner("inspector");scene.Program.Properties.Compose();}),"Only this inspector's contributions are removed.",Editable),
                new UiListItem("export","Export preview as an effect recipe",()=>Run(Export),"Writes a MappingFragment beside the map. Copy it into your source before rebuilding.",Editable),
                new UiListItem("reload","Reload scene source",()=>Run(()=>{if(!ModEntry.Reload(scene.Root))throw new InvalidOperationException("Reload rejected; inspect the log");scene=NativeScene.Current;back.Clear();Root();}),"Resources and source validate before the current scene is replaced.",Editable)
            },Root);
        }
        private void Components(){Show("COMPONENTS",scene.Gateway.InspectComponents().Select(c=>new UiListItem(c.Id,c.Id+" ["+c.Kind+"]",()=>Enter(()=>Properties(c.Id)))),Components);}
        private void Properties(string id)
        {
            Show(id,scene.Gateway.DescribeProperties(id).Select(p=>new UiListItem(p.Name,p.Name+" = "+scene.Program.Properties.Read(id,p.Name),()=>Enter(()=>Edit(id,p)),p.Kind+"; "+p.Units+"; owner="+scene.Program.Properties.Require(id+"/"+p.Name).Owner+"; "+(p.Bindable?"live "+p.Impact:"structural, reload required"),Editable&&p.Bindable)),()=>Properties(id));
        }
        private void Write(string id,ComponentPropertyInfo field,SceneValue value)
        {
            scene.Program.Properties.Set(new PropertyContribution{Id=id+"."+field.Name,Owner="inspector",Target=id+"/"+field.Name,Mode="set",Priority=100000,Value=value});scene.Program.Properties.Compose();
        }
        private void Edit(string id,ComponentPropertyInfo field)
        {
            if(!Editable||!field.Bindable)return;
            title=id+" / "+field.Name;refresh=()=>Edit(id,field);
            if(field.Kind==SceneValueKind.Number||field.Kind==SceneValueKind.Integer)
            {
                double step=field.Kind==SceneValueKind.Integer||field.Maximum-field.Minimum>100?1:.01;
                numeric=new UiNumberControl(field.Minimum,field.Maximum,step,()=>scene.Program.Properties.Read(id,field.Name).X,value=>Run(()=>Write(id,field,field.Kind==SceneValueKind.Integer?SceneValue.Integer((int)value):SceneValue.Number(value))));return;
            }
            if(field.Kind==SceneValueKind.Vector2)
            {
                Show(title,new[]{new UiListItem("x","X",()=>Coordinate(id,field,false)),new UiListItem("y","Y",()=>Coordinate(id,field,true))},()=>Edit(id,field));return;
            }
            IEnumerable<string> choices=field.Kind==SceneValueKind.Boolean?new[]{"true","false"}:field.Kind==SceneValueKind.Color?new[]{"#FFFFFF","#FFD27A","#91D8FF","#EB718F","#000000"}:field.Choices;
            Show(title,choices.Select(value=>new UiListItem(value,value,()=>Run(()=>Write(id,field,SceneValue.Parse(field.Kind,value))))),()=>Edit(id,field));
        }
        private void Coordinate(string id,ComponentPropertyInfo field,bool y)
        {
            Enter(()=>{title=id+" / "+field.Name+(y?".y":".x");numeric=new UiNumberControl(field.Minimum,field.Maximum,1,()=>{var v=scene.Program.Properties.Read(id,field.Name);return y?v.Y:v.X;},n=>Run(()=>{var v=scene.Program.Properties.Read(id,field.Name);Write(id,field,SceneValue.Vector(y?v.X:n,y?n:v.Y));}));});
        }
        private void Signals(){Show("OBSERVATIONS",scene.Program.Signals.Descriptors.OrderBy(d=>d.Id,StringComparer.Ordinal).Select(d=>{var sample=scene.Program.Signals.Read(d.Id);return new UiListItem(d.Id,d.Id+" = "+sample.Value,null,d.Kind+" "+d.Units+"; quality="+sample.Quality+"; sequence="+sample.Sequence);}),Signals);}
        private void Variables(){Show("VARIABLES",scene.Program.Definition.Variables.Select(v=>new UiListItem(v.Id,v.Id+" = "+scene.Program.ReadVariable(v.Id),null,v.Kind+"; "+v.Scope+" lifetime")),Variables);}
        private void Effects(){Show("EFFECT LIBRARY",scene.Program.Definition.Effects.Select(e=>new UiListItem(e.Id,e.Id,()=>Run(()=>scene.Program.Activate("inspector",e.Id)),e.Clock+"; "+e.Repeat+"; "+e.Duration+"s",Editable)),Effects);}
        private void ActiveEffects(){Show("ACTIVE EFFECTS",scene.Gateway.InspectEffects().Select(e=>new UiListItem(e.Id.ToString(),e.Definition+" / "+e.Owner,()=>Run(()=>{scene.Program.Cancel(e.Id);ActiveEffects();}),double.IsInfinity(e.RemainingSeconds)?"Until cancelled":e.RemainingSeconds.ToString("0.00",CultureInfo.InvariantCulture)+"s",Editable)),ActiveEffects);}
        private void Trees(){Show("CONTROL TREES",scene.Program.InspectTrees().Select((s,i)=>new UiListItem(i.ToString(),s,null,s)),Trees);}
        private void Errors(){Show("DIAGNOSTICS",scene.Program.Errors.Select((s,i)=>new UiListItem(i.ToString(),s,null,s)),Errors);}
        private void Export()
        {
            var values=scene.Program.Properties.Capture().Where(p=>p.Owner=="inspector").Select(p=>new XElement("Set",new XAttribute("target",p.Target),new XAttribute("value",p.Value),new XAttribute("mode",p.Mode))).ToArray();
            var xml=new XDocument(new XElement("MappingFragment",new XAttribute("version",1),new XElement("Effects",new XElement("Effect",new XAttribute("id","inspector-preset"),new XAttribute("priority",100000),values)),new XElement("Rules",new XElement("Rule",new XAttribute("id","inspector-preset"),new XAttribute("event","scene.ready"),new XElement("Action",new XAttribute("op","effect"),new XAttribute("target","inspector-preset"))))));
            string path=Path.Combine(scene.Root,"props/mega-mapping-expansion/preview/inspector-effect.xml");Directory.CreateDirectory(Path.GetDirectoryName(path));xml.Save(path);message="Exported preview/inspector-effect.xml";
        }
        private void Back(){UiPointer.CancelCapture(this);numeric=null;if(back.Count==0)WantsClose=true;else Run(back.Pop());}
        private UiPageCommand[] Commands(){return new[]{new UiPageCommand(UiAction.Confirm,numeric==null?"Select":"Done",()=>{if(numeric!=null)Back();else Run(()=>list.Update(new UiInput{Action=UiAction.Confirm,Confirm=true}));}),new UiPageCommand(UiAction.Secondary,"Refresh",()=>Run(refresh??Root)),new UiPageCommand(UiAction.Cancel,"Back",Back)};}
        public override void Update(UiInput input,float delta)
        {
            if(scene!=NativeScene.Current){scene=NativeScene.Current;back.Clear();Root();}
            foreach(var command in Commands())if(command.Handle(input))return;
            if(numeric!=null)numeric.Update(input);else if(input.Action!=UiAction.None)Run(()=>list.Update(input));
        }
        public override void Draw()
        {
            frame.Draw();var layout=new UiPageLayout(frame.Bounds,Commands(),2);UiTheme.TextLine(UiTheme.FitText(title,layout.Title.Width,false),layout.Title.Location.ToVector2(),UiTheme.Gold,false);
            if(numeric!=null)numeric.Draw(layout.Content);else list.Draw(layout.Content);
            UiTheme.WrappedText(message!=""?message:Editable?list.Description:"Read only. Use -debug to preview changes.",layout.Status,UiTheme.Muted);layout.DrawCommands();
        }
    }
}

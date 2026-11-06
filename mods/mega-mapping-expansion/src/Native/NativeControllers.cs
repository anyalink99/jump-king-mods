using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    // native adapters consume the same composed properties as drawing components
    internal sealed partial class NativeControllers : IDisposable
    {
        private readonly SceneProgram scene;
        private readonly RuntimeScope resources=new RuntimeScope();
        private readonly string[] items,walls;
        private readonly Dictionary<string,JumpKing.MiscEntities.WorldItems.Items> itemTypes=new Dictionary<string,JumpKing.MiscEntities.WorldItems.Items>();
        private bool active,baseline=true,wasEnabled;
        internal NativeControllers(SceneProgram program,string root,NativeSoundPool audioPool)
        {
            scene=program;
            try
            {
                items=Ids("item");walls=Ids("hiddenWall");
                foreach(string id in items)
                {
                    JumpKing.MiscEntities.WorldItems.Items item;
                    if(!Enum.TryParse(Text(id,"name"),false,out item)||!Enum.IsDefined(typeof(JumpKing.MiscEntities.WorldItems.Items),item))
                        throw new System.IO.InvalidDataException(id+": unknown native item "+Text(id,"name"));
                    itemTypes.Add(id,item);
                }
                PrepareAudio(root,audioPool);PrepareNpcs();PrepareEndings();PrepareCamera();
                foreach(string id in walls)
                {
                    int matches=0;var position=scene.Properties.Read(id,"position");
                    for(int screen=1;screen<=JumpKing.Level.LevelManager.TotalScreens;screen++)
                    {
                        JumpKing.Props.RaymanWall.RaymanCollection collection;
                        if(!Game1.instance.contentManager.props.RaymanScreens.TryGetValue("hidden_wall"+screen,out collection))continue;
                        foreach(var wall in collection.walls)
                            if(wall.texture_name==Text(id,"texture")&&(!Flag(id,"matchPosition")||position.X==wall.Position.X&&position.Y==wall.Position.Y-(screen-1)*360))matches++;
                    }
                    if(matches==0)throw new System.IO.InvalidDataException(id+": native hidden wall selector matched no wall");
                    if(matches>1)throw new System.IO.InvalidDataException(id+": hidden wall selector is ambiguous; use a unique texture or canonical position");
                }
            }
            catch{resources.Dispose();throw;}
        }
        private string[] Ids(string kind){return scene.Components.Values.Where(c=>c.Descriptor.Kind==kind).Select(c=>c.Id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();}
        private string Text(string id,string field){return scene.Properties.Read(id,field).Text;}
        private double Number(string id,string field){return scene.Properties.Read(id,field).X;}
        private bool Flag(string id,string field){return Number(id,field)!=0;}
        private void Publish(string id,string field,SceneValue value){scene.Signals.Publish(id+"."+field,value,ObservationQuality.Sampled);}
        private void Event(string id,string suffix,int screen,params SignalSample[] payload)
        {if(active&&!JKRuntime.World.WorldControl.Following)scene.Signals.Emit(id+"."+suffix,id,"player",screen,scene.GameplayTime,payload);}
        internal void Activate(RuntimeScope scope)
        {
            active=true;baseline=true;wasEnabled=false;BindNpcs();ActivateCamera(scope);
            foreach(var voice in voices.Values)scope.Own(voice.Sound.BindToAttempt("mega-mapping-expansion.audio"));
            if(walls.Length>0)scope.Own(JKRuntime.World.NativeWorldInteractions.SubscribeHiddenWalls(Wall));
            if(items.Length>0)scope.Own(JKRuntime.World.NativeWorldInteractions.SubscribeItemPickups(Pickup));
            scope.Defer(delegate{active=false;StopAudio();StopCamera();UnbindNpcs();});
        }
        private void Wall(JKRuntime.World.HiddenWallContact value){ObserveWall(value,true);}
        private void ObserveWall(JKRuntime.World.HiddenWallContact value,bool emit)
        {
            if(!active||(emit&&(!MappingSettings.Enabled||NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen))||JKRuntime.World.WorldControl.Following)return;
            foreach(string id in walls)
            {
                if(Text(id,"texture")!=value.Texture)continue;
                var position=scene.Properties.Read(id,"position");
                if(Flag(id,"matchPosition")&&(position.X!=value.Position.X||position.Y!=value.Position.Y))continue;
                Publish(id,"touching",SceneValue.Boolean(value.Touching));
                if(emit)Event(id,value.Touching?"enter":"exit",value.Screen,new SignalSample("nativeId",SceneValue.String(value.Id),ObservationQuality.Exact,0));
            }
        }
        private void Pickup(JKRuntime.World.NativeItemPickup value)
        {
            if(!active||!MappingSettings.Enabled||JKRuntime.World.WorldControl.Following)return;
            foreach(string id in items)if(Text(id,"name")==value.Item)Event(id,"pickedUp",value.Screen,
                new SignalSample("position",SceneValue.Vector(value.Position.X,value.Position.Y+(value.Screen-1)*360),ObservationQuality.Sampled,0));
        }
        internal void Tick(double delta,bool enabled,bool paused,bool ending)
        {
            if(!active)return;
            if(!JKRuntime.World.WorldControl.Following)
            {
                if((baseline||enabled&&!wasEnabled)&&walls.Length>0)foreach(var wall in JKRuntime.World.NativeWorldInteractions.ReadHiddenWalls())ObserveWall(wall,false);
                foreach(string id in items)
                {
                    int count=JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager.GetItemCount(itemTypes[id]);
                    int previous=(int)scene.Signals.Read(id+".count").Value.X;
                    Publish(id,"count",SceneValue.Integer(count));Publish(id,"owned",SceneValue.Boolean(count>0));
                    Publish(id,"equipped",SceneValue.Boolean(JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager.HasItemEnabled(itemTypes[id])));
                    if(!baseline&&enabled&&!paused&&previous!=count)Event(id,"changed",Camera.CurrentScreenIndex1,
                        new SignalSample("previous",SceneValue.Integer(previous),ObservationQuality.Exact,0),new SignalSample("count",SceneValue.Integer(count),ObservationQuality.Exact,0));
                }
                SampleNpcs(enabled&&!paused);baseline=false;
            }
            TickAudio(delta,enabled,paused);TickCamera(delta,enabled,paused,ending);wasEnabled=enabled;
        }
        internal void ResetTransient()
        {
            baseline=true;StopAudio();StopCamera();
            foreach(var pair in voices)pair.Value.Requested=Flag(pair.Key,"playing")&&!Flag(pair.Key,"loop");
        }
        public void Dispose(){StopAudio();StopCamera();UnbindNpcs();resources.Dispose();}
    }
}

using System;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private Action<int> PoseCommand(CompiledComponent component,Action<ComponentPose,string,int> draw,string gate=null)
        {
            string transform=Text(component.Id,"transform");
            bool hasMaterial=Array.Exists(component.Descriptor.Fields,f=>f.Name=="material");string material=hasMaterial?Text(component.Id,"material"):"";
            PropertySlot visible=gate==null?null:scene.Properties.Require(component.Id+"/"+gate);
            return screen=>{
                if(visible!=null&&visible.Value.X==0)return;
                var pose=transforms.Resolve(transform);if(!pose.Visible||(pose.Screen!=0&&pose.Screen!=screen)||!CaptureReady(material,screen))return;
                draw(pose,material,screen);
            };
        }
        internal Action<int> SpriteCommand(CompiledComponent c){return PoseCommand(c,(pose,material,screen)=>DrawSprite(c,pose,material),"visible");}
        internal Action<int> MeshCommand(CompiledComponent c){return PoseCommand(c,(pose,material,screen)=>DrawMesh(c,pose,material),"visible");}
        internal Action<int> ReceiverCommand(CompiledComponent c){return PoseCommand(c,(pose,material,screen)=>DrawReceiver(c,pose,material,screen));}
        internal Action<int> EmitterCommand(CompiledComponent c){return PoseCommand(c,(pose,material,screen)=>DrawEmitter(c,pose,material,screen),"enabled");}
        internal Action<int> LightCommand(CompiledComponent c){return PoseCommand(c,(pose,material,screen)=>DrawLight(c,pose,screen),"enabled");}
        internal Action<int> TextCommand(CompiledComponent c){return PoseCommand(c,(pose,material,screen)=>{if(!priming||!NativeNarrativeText.IsFont(Text(c.Id,"font")))DrawText(c,pose,material);});}
        internal Action<int> StampCommand(CompiledComponent c){return screen=>DrawStamps(c,screen);}
    }
}

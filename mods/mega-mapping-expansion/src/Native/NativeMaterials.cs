using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JKRuntime;
using JKRuntime.Geometry;
using JKRuntime.State;
using JKRuntime.World;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class MaterialInstance
    {
        internal string Id;
        internal MaterialDefinition Definition;
        internal int Screen;
        internal bool Enabled;
        internal NativePlatform Platform;
        internal NativeMaterials World;
        internal readonly List<IBlock> Blocks=new List<IBlock>();
        internal readonly List<Type> Types=new List<Type>();
        internal readonly Dictionary<Type,string> SourceIds=new Dictionary<Type,string>();
        internal readonly List<Rectangle> Origins=new List<Rectangle>();
        internal readonly List<Action<Rectangle>> Movers=new List<Action<Rectangle>>();
        internal void PrepareMove()
        {
            if(Movers.Count>0)return;
            foreach(var b in Blocks){Origins.Add(b.GetRect());Movers.Add(SlopeClipping.PrepareMove(b));}
        }
        internal void Move(int x,int y)
        {for(int i=0;i<Movers.Count;i++){var r=Origins[i];r.Offset(x,y);Movers[i](r);}}
    }
    // native instances stay in the screen arrays so typed provider queries still see them
    internal sealed partial class NativeMaterials:IDisposable,IStateParticipant
    {
        internal static NativeMaterials Loaded,Current;
        internal readonly MaterialPlan Plan;
        internal readonly List<MaterialInstance> Instances=new List<MaterialInstance>();
        private readonly string root;
        private readonly Dictionary<string,IBlockFactory> factories=new Dictionary<string,IBlockFactory>();
        private readonly Dictionary<string,MaterialInstance> instances=new Dictionary<string,MaterialInstance>();
        private RuntimeScope active;
        private BodyComp player;
        private int blockCount;
        private readonly List<string> diagnostics=new List<string>();
        private readonly Dictionary<int,List<MaterialInstance>> byScreen=new Dictionary<int,List<MaterialInstance>>();
        internal JumpKing.Workshop.Level Level;
        internal LevelTexture Texture;
        private static readonly FieldInfo Factories=typeof(LevelManager).GetField("BlockFactories",OwnedPatches.Members);
        private static readonly FieldInfo Handlers=typeof(BodyComp).GetField("m_blockBehaviours",OwnedPatches.Members);
        private static readonly FieldInfo Lookup=typeof(BodyComp).GetField("m_blockBehaviourLookup",OwnedPatches.Members);
        [ThreadStatic] internal static MaterialSource Sampling;
        [ThreadStatic] private static Rectangle[] sampleArea;
        internal static bool SampleRegion(LevelTexture texture,int screen,int x,int y,out Color color)
        {
            color=Color.Transparent;if(Sampling==null||sampleArea==null)return false;
            var pixel=texture as PixelLevelTexture;int scale=pixel==null?8:480/pixel.Layout.Width(screen);
            var cell=new Rectangle(x*scale,y*scale-screen*360,scale,scale);
            if(sampleArea.Any(r=>r.Intersects(cell)))color=Colour(Sampling.Color);return true;
        }
        private IBlock[] CreateRegion(string material,string id,int screen,Rectangle[] rectangles,JumpKing.Workshop.Level level,LevelTexture texture,int scale)
        {
            var previous=sampleArea;sampleArea=rectangles;
            try{return rectangles.SelectMany(r=>Create(material,id,screen,r,level,texture,r.X/scale,(r.Y+screen*360)/scale,scale)).ToArray();}
            finally{sampleArea=previous;}
        }
        internal NativeMaterials(MaterialPlan plan,string path){Plan=plan;root=path;}
        internal static void Begin(string root,int screens)
        {
            if(Current!=null)Current.Dispose();
            MaterialContacts.Clear();Loaded=new NativeMaterials(MaterialPlan.Read(root,screens),root);
        }
        internal static Color Colour(string text){uint c=Convert.ToUInt32(text.Substring(1),16);return new Color((int)(c>>16),(int)(c>>8&255),(int)(c&255));}
        internal string Sample(Color code,int screen)
        {
            if(Sampling==null)return null;
            var paint=Plan.Find(MovingPlatformScan.Hex(code),screen+1);if(paint==null)return null;
            return Plan.Require(paint.Material).Layers.Any(l=>l.Source==Sampling.Id)?Sampling.Color:"#FF00FF";
        }
        internal IBlockFactory Factory(MaterialSource source,JumpKing.Workshop.Level level)
        {
            IBlockFactory factory;if(factories.TryGetValue(source.Id,out factory))return factory;
            var color=Colour(source.Color);
            var candidates=((IEnumerable<IBlockFactory>)Factories.GetValue(null)).Where(f=>!(f is MovingPlatformFactory)&&
                (source.Factory==""||f.GetType().FullName==source.Factory||f.GetType().Assembly.GetName().Name+":"+f.GetType().FullName==source.Factory)&&f.CanMakeBlock(color,level)).ToArray();
            if(candidates.Length!=1)throw new InvalidDataException("Material source "+source.Id+" needs exactly one factory for "+source.Color+"; found "+candidates.Length+". Set factory to assembly:type when colors overlap.");
            factories.Add(source.Id,factory=candidates[0]);return factory;
        }
        internal IBlock[] Create(string material,string id,int screen,Rectangle bounds,JumpKing.Workshop.Level level,LevelTexture texture,int x,int y,int scale)
        {
            MaterialInstance instance;
            if(!instances.TryGetValue(id,out instance))
            {
                var definition=Plan.Require(material);instance=new MaterialInstance{Id=id,Definition=definition,Enabled=definition.Enabled,Screen=screen+1,World=this};
                instances.Add(id,instance);Instances.Add(instance);
                if(Instances.Count>32768)throw new InvalidDataException("Material instance budget exceeded");
            }
            else if(instance.Definition.Id!=material)throw new InvalidDataException("Material instance ID collision: "+id);
            int first=instance.Blocks.Count;
            foreach(var layer in instance.Definition.Layers)
            {
                var source=Plan.Sources[layer.Source];var factory=Factory(source,level);
                bool native=factory.GetType().Assembly==typeof(BoxBlock).Assembly;
                if(source.Sizing=="cell"&&scale!=8&&!native&&!Api.PixelCollisionFactories.Supports(factory.GetType()))throw new NotSupportedException(source.Id+": factory has not declared fine-cell support; use an explicit rectangular material source when its copy contract applies");
                IBlock block;var previous=Sampling;Sampling=source;
                try
                {
                    // rectangle sources retain encoded parameters, then use Runtime's checked copy contract
                    var sample=bounds;
                    block=NativeWorldGeometry.CreateBlock(factory,Colour(source.Color),sample,level,texture,screen,x,y);
                    if(block==null)throw new NotSupportedException(source.Id+": screen metadata cannot be used as a local material");
                    if(source.Sizing=="rectangle")
                    {
                        string reason;if(!MaterialBlocks.CanCopy(block,out reason))throw new NotSupportedException(source.Id+": "+reason);
                        if(block.GetRect()!=bounds){MaterialBlocks.ValidateDetached(factory,Colour(source.Color),bounds,level,texture,screen,x,y,scale);block=MaterialBlocks.Copy(block,bounds);}
                    }
                }
                finally{Sampling=previous;}
                string previousSource;
                if(instance.SourceIds.TryGetValue(block.GetType(),out previousSource)&&previousSource!=layer.Source)
                    throw new NotSupportedException(id+": sources "+previousSource+" and "+layer.Source+" resolve to the same native block type; the provider must expose independent behavior types or handle their composition");
                if(!instance.SourceIds.ContainsKey(block.GetType())){instance.SourceIds.Add(block.GetType(),layer.Source);instance.Types.Add(block.GetType());}
                if(instance.Definition.Shape!="rectangle")
                {
                    var pair=SlopeClipping.Apply(block,(SlopeType)Enum.Parse(typeof(SlopeType),instance.Definition.Shape));
                    foreach(var shaped in pair)Add(instance,shaped,instance.Definition.Collision!="source");
                }
                else Add(instance,block,instance.Definition.Collision!="source");
            }
            if(instance.Definition.Collision=="solid"||instance.Definition.Collision=="oneWay")
            {
                IBlock support=instance.Definition.Shape=="rectangle"?(IBlock)new BoxBlock(bounds):new SlopeBlock(bounds,(SlopeType)Enum.Parse(typeof(SlopeType),instance.Definition.Shape));
                Add(instance,support,false,true);
            }
            return instance.Blocks.Skip(first).ToArray();
        }
        internal MaterialInstance Platform(NativePlatform platform)
        {
            var definition=Plan.Require(platform.Profile.Material);
            if(definition.Layers.Any(l=>Plan.Sources[l.Source].Sizing!="rectangle"))throw new InvalidDataException("Moving materials require sizing=rectangle sources");
            if(Texture==null)throw new InvalidOperationException("Material source sampler was not captured at map loading");
            string id="platform-"+platform.Definition.Id;
            if(instances.ContainsKey(id))Release(instances[id]);
            int screen=platform.Definition.Screen-1;var pixel=Texture as PixelLevelTexture;int scale=pixel==null?8:480/pixel.Layout.Width(screen);
            var rectangles=platform.Parts.Select(p=>p.Origin).ToArray();
            CreateRegion(definition.Id,id,screen,rectangles,Level,Texture,scale);
            var instance=instances[id];
            var oldArea=sampleArea;var oldSource=Sampling;
            try
            {
                sampleArea=rectangles.Select(r=>{r.Offset(scale,scale);return r;}).ToArray();
                foreach(var layer in definition.Layers)
                {
                    var source=Plan.Sources[layer.Source];Sampling=source;
                    foreach(var part in platform.Parts)MaterialBlocks.ValidateDetached(Factory(source,Level),Colour(source.Color),part.Origin,Level,Texture,screen,part.Origin.X/scale,(part.Origin.Y+screen*360)/scale,scale);
                }
            }
            finally{sampleArea=oldArea;Sampling=oldSource;}
            instance.Platform=platform;instance.PrepareMove();return instance;
        }
        internal void Release(MaterialInstance instance)
        {MaterialInstance current;if(instances.TryGetValue(instance.Id,out current)&&ReferenceEquals(current,instance)){instances.Remove(instance.Id);Instances.Remove(instance);blockCount-=instance.Blocks.Count;stateOrder=null;}}
        internal void Account(int delta){blockCount+=delta;if(blockCount>131072)throw new InvalidDataException("Map exceeds 131072 material colliders");}
        private static void Add(MaterialInstance instance,IBlock block,bool trigger,bool support=false)
        {
            instance.World.Account(1);
            instance.Blocks.Add(block);MaterialBlocks.Identify(block,instance.Id+"/"+block.GetType().FullName);
            MaterialContacts.Bind(block,instance,trigger,support&&instance.Definition.Collision=="oneWay");
        }
        internal void PaintScreen(PixelLevelTexture texture,JumpKing.Workshop.Level level,int screen,List<IBlock> output)
        {
            int width=texture.Layout.Width(screen),height=texture.Layout.Height(screen),scale=480/width;
            foreach(var paint in Plan.Paint.Where(p=>p.Applies(screen+1)))
            {
                var code=Colour(paint.Color);var cells=new bool[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)cells[y*width+x]=texture.Sample(screen,x,y)==code;
                var rects=paint.Mode=="cells"?Enumerable.Range(0,cells.Length).Where(i=>cells[i]).Select(i=>new PlatformRect(i%width,i/width,1,1)).ToList():MaterialGeometry.Compact(cells,width,height);
                var groups=MaterialGeometry.Groups(rects,paint.Mode=="connected");
                foreach(var group in groups)
                {
                    var first=group[0];string id="paint-"+paint.Color.Substring(1).ToLowerInvariant()+"-"+(screen+1)+"-"+first.X+"-"+first.Y;
                    foreach(var r in group)output.AddRange(Create(paint.Material,id,screen,new Rectangle(r.X*scale,(r.Y-screen*height)*scale,r.Width*scale,r.Height*scale),level,texture,r.X,r.Y,scale));
                }
            }
            AreasScreen(texture,level,screen,scale,output);
        }
        internal void AreasScreen(LevelTexture texture,JumpKing.Workshop.Level level,int screen,int scale,List<IBlock> output)
        {
            foreach(var area in Plan.Areas.Where(a=>a.Screen==screen+1))
            {
                var geometry=MaterialGeometry.Compile(area,root);
                if(area.Mode!="overlay")geometry=SelectArea(area,geometry,output,screen);
                output.AddRange(CreateRegion(area.Material,area.Id,screen,geometry.Select(r=>new Rectangle(r.X,r.Y-screen*360,r.Width,r.Height)).ToArray(),level,texture,scale));
            }
        }
        private List<PlatformRect> SelectArea(MaterialArea area,List<PlatformRect> geometry,List<IBlock> output,int screen)
        {
            var selected=new bool[480*360];foreach(var r in geometry)for(int y=r.Y;y<r.Y+r.Height;y++)for(int x=r.X;x<r.X+r.Width;x++)selected[y*480+x]=true;
            var matching=new bool[480*360];var remove=new List<IBlock>();var additions=new List<IBlock>();
            foreach(var block in output.ToArray())
            {
                var r=block.GetRect();var local=new Rectangle(r.X,r.Y+screen*360,r.Width,r.Height);bool touched=false,contained=local.Left>=0&&local.Top>=0&&local.Right<=480&&local.Bottom<=360,rectangular=true;
                Type sourceFactory;Color sourceColor;
                if(area.Select!=""&&(!MaterialBlocks.TryGetSource(block,out sourceFactory,out sourceColor)||sourceColor!=Colour(area.Select)))continue;
                for(int y=Math.Max(0,local.Top);y<Math.Min(360,local.Bottom);y++)for(int x=Math.Max(0,local.Left);x<Math.Min(480,local.Right);x++)
                {
                    Rectangle hit;var contact=block.Intersects(new Rectangle(x,y-screen*360,1,1),out hit);bool solid=area.Mode=="replace"?contact!=BlockCollisionType.NoCollision:contact==BlockCollisionType.Collision_Blocking;
                    rectangular&=contact!=BlockCollisionType.NoCollision;
                    if(solid){matching[y*480+x]=true;touched|=selected[y*480+x];contained&=selected[y*480+x];}
                }
                if(area.Mode=="replace"&&touched)
                {
                    if(!contained)
                    {
                        string reason;
                        if(!rectangular||local.Left<0||local.Top<0||local.Right>480||local.Bottom>360||!MaterialBlocks.CanCopy(block,out reason)||block is SlopeBlock)
                            throw new InvalidDataException(area.Id+": partial replacement requires a rectangular copyable collider; use surfaces for shaped sources");
                        var remaining=new bool[480*360];for(int y=local.Top;y<local.Bottom;y++)for(int x=local.Left;x<local.Right;x++)remaining[y*480+x]=!selected[y*480+x];
                        foreach(var part in MaterialGeometry.Compact(remaining,480,360))
                        {
                            var copy=MaterialBlocks.Copy(block,new Rectangle(part.X,part.Y-screen*360,part.Width,part.Height));
                            MaterialContacts.CopyBinding(block,copy);additions.Add(copy);
                        }
                    }
                    remove.Add(block);
                }
            }
            foreach(var block in remove){output.Remove(block);MaterialContacts.Remove(block);}output.AddRange(additions);
            for(int i=0;i<selected.Length;i++)selected[i]=selected[i]&&(area.Mode=="empty"?!matching[i]:matching[i]);
            return MaterialGeometry.Compact(selected,480,360);
        }
        internal void Prepare()
        {
            byScreen.Clear();
            foreach(var instance in Instances)
            {
                instance.Enabled=instance.Definition.Enabled;
                foreach(var type in instance.Types)MaterialBlocks.PrepareHandler(type);
                if(instance.Platform!=null)continue;
                List<MaterialInstance> list;if(!byScreen.TryGetValue(instance.Screen,out list))byScreen.Add(instance.Screen,list=new List<MaterialInstance>());list.Add(instance);
            }
        }
        internal IDisposable Activate(BodyComp body,bool state=true)
        {
            if(Current!=null||active!=null)throw new InvalidOperationException("Materials already active");
            var scope=new RuntimeScope();active=scope;Current=this;player=body;
            scope.Defer(()=>{player=null;Current=null;active=null;});
            try
            {
                var entity=body.gameObject as PlayerEntity;
                if(entity!=null)foreach(var type in Instances.SelectMany(i=>i.Types).Distinct()){var lease=MaterialBlocks.AcquireHandler(entity,type);if(lease!=null)scope.Own(lease);}
                var hooks=scope.Own(new OwnedPatches("mega-mapping.material-execution"));
                hooks.Add(typeof(BodyComp).GetMethod("Update",OwnedPatches.Members),prefix:Method("BeforeBody"),finalizer:Method("AfterBody"),priority:1001);
                foreach(string name in new[]{"UpdateXPositionFromVelocityBehaviour","UpdateYPositionFromVelocityBehaviour","ResolveXCollisionBehaviour","ResolveYCollisionBehaviour","ApplyGravityBehaviour","ExecuteBlockBehaviours"})
                {
                    var type=typeof(BodyComp).Assembly.GetType("JumpKing.BodyCompBehaviours."+name,true);
                    hooks.Add(type.GetMethod("ExecuteBehaviour"),prefix:Method("BeforePhase"),finalizer:Method("AfterPhase"));
                }
                if(state){scope.Own(GameState.Snapshots.Register(this));scope.Own(WorldRegistry.Shared.Bind<MaterialPacket>(Id,()=>Packet(),s=>Restore(s),s=>Validate(s)));}
                PublishState();
                return scope;
            }
            catch{scope.Dispose();throw;}
        }
        internal IEnumerable<MaterialInstance> Touching(BodyComp body)
        {
            var box=body.GetHitbox();box.Height++;
            int first=1-(int)Math.Floor((box.Bottom-1)/360.0),last=1-(int)Math.Floor(box.Top/360.0);
            var nearby=new List<MaterialInstance>();
            if(byScreen.Count==0)nearby.AddRange(Instances);
            else{for(int s=first;s<=last;s++){List<MaterialInstance> list;if(byScreen.TryGetValue(s,out list))nearby.AddRange(list);}nearby.AddRange(Instances.Where(i=>i.Platform!=null));}
            return nearby.Where(i=>i.Enabled&&i.Blocks.Any(b=>{Rectangle contact;return b.Intersects(box,out contact)!=BlockCollisionType.NoCollision;})).OrderByDescending(i=>i.Definition.Priority).ThenBy(i=>i.Definition.Id,StringComparer.Ordinal).ThenBy(i=>i.Id,StringComparer.Ordinal);
        }
        private sealed class PhaseState
        {internal LinkedList<IBlockBehaviour> List;internal IBlockBehaviour[] Original;}
        private static void BeforePhase(BehaviourContext __0,out PhaseState __state)
        {
            __state=null;var owner=Current;if(owner==null||owner.player!=__0.BodyComp)return;
            var lookup=(Dictionary<Type,IBlockBehaviour>)Lookup.GetValue(__0.BodyComp);
            var selected=new List<IBlockBehaviour>();var edges=new Dictionary<IBlockBehaviour,HashSet<IBlockBehaviour>>();
            foreach(var material in owner.Touching(__0.BodyComp))
            {
                var sequence=new List<IBlockBehaviour>();
                foreach(var type in material.Types){IBlockBehaviour handler;if(lookup.TryGetValue(type,out handler)&&!sequence.Contains(handler)){sequence.Add(handler);if(!edges.ContainsKey(handler)){edges.Add(handler,new HashSet<IBlockBehaviour>());selected.Add(handler);}}}
                for(int i=1;i<sequence.Count;i++)
                {
                    var before=sequence[i-1];var after=sequence[i];
                    if(Reachable(edges,after,before,new HashSet<IBlockBehaviour>()))
                    {string message="Overlapping material order: "+material.Definition.Id+" yields to higher priority (then material ID)";if(owner.diagnostics.Count<64&&!owner.diagnostics.Contains(message)){owner.diagnostics.Add(message);ModEntry.Log(message);}continue;}
                    edges[before].Add(after);
                }
            }
            if(selected.Count<2)return;
            var list=(LinkedList<IBlockBehaviour>)Handlers.GetValue(__0.BodyComp);var old=list.ToArray();var ordered=old.ToArray();int next=0;
            var pending=selected.OrderBy(h=>Array.IndexOf(old,h)).ToList();var sorted=new List<IBlockBehaviour>();
            while(pending.Count>0){var h=pending.First(p=>!pending.Any(q=>edges[q].Contains(p)));sorted.Add(h);pending.Remove(h);}
            for(int i=0;i<ordered.Length;i++)if(selected.Contains(ordered[i]))ordered[i]=sorted[next++];
            if(next!=selected.Count)throw new InvalidOperationException("Material handler registration changed during a physics phase");
            if(old.SequenceEqual(ordered))return;
            __state=new PhaseState{List=list,Original=old};list.Clear();foreach(var h in ordered)list.AddLast(h);
        }
        private static bool Reachable(Dictionary<IBlockBehaviour,HashSet<IBlockBehaviour>> edges,IBlockBehaviour from,IBlockBehaviour to,HashSet<IBlockBehaviour> seen)
        {return from==to||seen.Add(from)&&edges[from].Any(next=>Reachable(edges,next,to,seen));}
        private sealed class BodyFrame{internal BodyComp Body;internal Rectangle Start;}
        private static void BeforeBody(BodyComp __instance,out BodyFrame __state)
        {__state=new BodyFrame{Body=MaterialContacts.Body,Start=MaterialContacts.Start};MaterialContacts.Body=__instance;MaterialContacts.Start=__instance.GetHitbox();if(Current!=null)Current.CheckReset();}
        private static void AfterBody(BodyFrame __state)
        {if(__state==null)return;MaterialContacts.ClearSweeps(MaterialContacts.Body);MaterialContacts.Body=__state.Body;MaterialContacts.Start=__state.Start;}
        private static void AfterPhase(PhaseState __state)
        {
            if(__state==null)return;
            // don't discard a provider registration made during its callback
            var extra=__state.List.Where(h=>!__state.Original.Contains(h)).ToArray();var live=new HashSet<IBlockBehaviour>(__state.List);
            __state.List.Clear();foreach(var h in __state.Original.Where(live.Contains).Concat(extra))__state.List.AddLast(h);
        }
        internal void Command(string target,string command)
        {
            if(WorldControl.Following)throw new InvalidOperationException("A following world cannot command materials");
            if(!new[]{"enable","disable","toggle","reset"}.Contains(command))throw new InvalidDataException("Unknown material command "+command);
            var selected=Instances.Where(i=>i.Id==target||i.Definition.Id==target).ToArray();if(selected.Length==0)throw new InvalidDataException("Unknown material target "+target);
            foreach(var instance in selected)
            {
                bool enabled=command=="reset"?instance.Definition.Enabled:command=="enable"||command=="toggle"&&!instance.Enabled;
                if(enabled&&!instance.Enabled&&player!=null&&instance.Blocks.Any(b=>MaterialContacts.WouldBlock(b,player.GetHitbox())))throw new InvalidOperationException("Cannot enable solid material through the player");
            }
            foreach(var instance in selected)instance.Enabled=command=="reset"?instance.Definition.Enabled:command=="enable"||command=="toggle"&&!instance.Enabled;
            PublishState();
        }
        internal IEnumerable<string> Describe(){return Instances.Select(i=>i.Id+" / "+i.Definition.Id+" / "+(i.Enabled?"enabled":"disabled")+" / "+string.Join(" -> ",i.Types.Select(t=>t.FullName))).Concat(diagnostics);}
        private static MethodInfo Method(string name){return typeof(NativeMaterials).GetMethod(name,OwnedPatches.Members);}
        public void Dispose(){if(active!=null)active.Dispose();}
    }
    internal static class MaterialContacts
    {
        [ThreadStatic] internal static BodyComp Body;
        [ThreadStatic] internal static Rectangle Start;
        [ThreadStatic] private static bool enabling;
        internal static bool WouldBlock(IBlock block,Rectangle body)
        {bool previous=enabling;enabling=true;try{Rectangle hit;return block.Intersects(body,out hit)==BlockCollisionType.Collision_Blocking;}finally{enabling=previous;}}
        private sealed class Binding {internal MaterialInstance Owner;internal bool Trigger,OneWay;internal readonly Dictionary<BodyComp,Rectangle> Swept=new Dictionary<BodyComp,Rectangle>();}
        private static ConditionalWeakTable<IBlock,Binding> bindings=new ConditionalWeakTable<IBlock,Binding>();
        private static readonly HashSet<MethodInfo> patched=new HashSet<MethodInfo>();
        private static OwnedPatches hooks;
        internal static void Clear(){bindings=new ConditionalWeakTable<IBlock,Binding>();}
        internal static void ClearAllSweeps()
        {if(NativeMaterials.Loaded!=null)foreach(var block in NativeMaterials.Loaded.Instances.SelectMany(i=>i.Blocks)){Binding binding;if(bindings.TryGetValue(block,out binding))binding.Swept.Clear();}}
        internal static void Record(MaterialInstance material,IEnumerable<BodyComp> bodies)
        {
            foreach(var block in material.Blocks)
            {
                Binding binding;if(!bindings.TryGetValue(block,out binding))continue;
                foreach(var body in bodies){Rectangle area;var box=body.GetHitbox();if(block.Intersects(box,out area)!=BlockCollisionType.NoCollision){Rectangle previous;binding.Swept[body]=binding.Swept.TryGetValue(body,out previous)?Rectangle.Union(previous,box):box;}}
            }
        }
        internal static void ClearSweeps(BodyComp body)
        {
            if(NativeMaterials.Loaded==null)return;
            foreach(var block in NativeMaterials.Loaded.Instances.SelectMany(i=>i.Blocks)){Binding binding;if(bindings.TryGetValue(block,out binding))binding.Swept.Remove(body);}
        }
        internal static void Bind(IBlock block,MaterialInstance owner,bool trigger,bool oneWay)
        {
            if(block.GetType().IsValueType)throw new NotSupportedException("Materials require reference-type IBlock objects");
            if(hooks==null)hooks=new OwnedPatches("mega-mapping.material-contacts");
            var map=block.GetType().GetInterfaceMap(typeof(IBlock));var method=map.TargetMethods[Array.FindIndex(map.InterfaceMethods,m=>m.Name=="Intersects")];
            method=method.DeclaringType.GetMethods(OwnedPatches.Members|BindingFlags.DeclaredOnly).Single(m=>m.MetadataToken==method.MetadataToken);
            if(!patched.Contains(method)){hooks.Add(method,postfix:typeof(MaterialContacts).GetMethod("After",OwnedPatches.Members),priority:int.MinValue);patched.Add(method);}
            bindings.Add(block,new Binding{Owner=owner,Trigger=trigger,OneWay=oneWay});
        }
        internal static void CopyBinding(IBlock source,IBlock copy)
        {
            Binding binding;if(!bindings.TryGetValue(source,out binding))return;
            binding.Owner.World.Account(1);binding.Owner.Blocks.Add(copy);MaterialBlocks.Identify(copy,MaterialBlocks.IdentityOf(source));Bind(copy,binding.Owner,binding.Trigger,binding.OneWay);
        }
        internal static void Remove(IBlock source)
        {Binding binding;if(bindings.TryGetValue(source,out binding)){binding.Owner.World.Account(-1);binding.Owner.Blocks.Remove(source);bindings.Remove(source);}}
        private static void After(IBlock __instance,Rectangle __0,ref Rectangle __1,ref BlockCollisionType __result)
        {
            Binding binding;if(!bindings.TryGetValue(__instance,out binding))return;
            if(binding.Owner.Platform!=null&&(ReferenceEquals(NativeMovingPlatforms.Ignored,binding.Owner.Platform.Motion)||NativeMovingPlatforms.Launching!=null&&binding.Owner.Platform.Parts.Any(NativeMovingPlatforms.Launching.Contains)))
            {__1=Rectangle.Empty;__result=BlockCollisionType.NoCollision;return;}
            if(!binding.Owner.Enabled&&!enabling){__1=Rectangle.Empty;__result=BlockCollisionType.NoCollision;return;}
            Rectangle swept;
            if(__result==BlockCollisionType.NoCollision&&Body!=null&&binding.Swept.TryGetValue(Body,out swept)&&__0.Intersects(swept))
            {__1=Rectangle.Intersect(__0,swept);__result=BlockCollisionType.Collision_NonBlocking;}
            if(binding.Trigger&&__result!=BlockCollisionType.NoCollision)__result=BlockCollisionType.Collision_NonBlocking;
            if(binding.OneWay&&!enabling&&(Body==null||Body.Velocity.Y<0||Start.Bottom>__instance.GetRect().Top+1))
            {__1=Rectangle.Empty;__result=BlockCollisionType.NoCollision;}
        }
    }
}

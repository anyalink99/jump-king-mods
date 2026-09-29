using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using WardrobePlus;
using WardrobePlus.Advanced;
using JumpKing;
using JumpKing.Player;
using JumpKing.MiscSystems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static partial class WardrobeTests
{
    private static SkinManifest AdvancedSample()
    {
        var m=new SkinManifest{id="test-skin",name="Test skin",defaultProfile="embers"};
        m.effects.Add(new EffectDefinition{id="sparks",count=5,life=.3f,lifeSpread=0,speedSpread=0});
        m.profiles.Add(new EffectProfile{id="embers",rules=new List<EffectRule>{new EffectRule{id="takeoff",trigger="jump",channels=new List<ChannelRule>{new ChannelRule{channel="particles",mode="replace",effects=new List<string>{"sparks"}}}}}});
        return m;
    }
    private static void AdvancedTests()
    {
        LiquidTests();
        SpilledWaterTests();
        var manifest=AdvancedSample();string json=ManifestIO.Write(manifest);
        Check(ManifestIO.Parse(json).effects.Single().id=="sparks","Advanced manifest round trips defaults and resources");
        Reject(()=>ManifestIO.Parse(json.Replace("\"schema\":1","\"schema\":8")),"Future skin schemas are rejected");
        Reject(()=>ManifestIO.Parse(json.Replace("\"count\":5","\"cont\":5")),"Author field typos report an error rather than silently falling back");
        Reject(()=>ManifestIO.Parse(json.Replace("\"effects\":[{","\"effects\":[null,{")),"Null definition entries are rejected");
        Reject(()=>ManifestIO.Asset(output,"../outside.png"),"Advanced resource paths cannot escape the package");
        Reject(()=>ManifestIO.Asset(output,"image.png:stream"),"Alternate data stream paths are rejected");
        var excessive=AdvancedSample();excessive.effects[0].rate=float.NaN;Reject(()=>ManifestIO.Validate(excessive),"Nonfinite emitter rates are rejected");
        excessive=AdvancedSample();excessive.effects[0].count=100000;Reject(()=>ManifestIO.Validate(excessive),"Unbounded particle bursts are rejected");
        var library=new SkinLibrary();var package=new SkinPackage{Manifest=manifest,Root=output,Source="local:test"};library.Packages.Add(manifest.id,package);library.ValidateDependencies();
        var inherited=new[]{library.Profile("test-skin/embers",0)};var selection=new PresentationSelection();var e=new PresentationEvent{Trigger="jump"};
        var result=library.Decide("particles",e,inherited,selection);
        Check(!result.Native && result.Effects.Single().Definition.id=="sparks","A replace rule suppresses only its chosen native channel");
        Check(library.Decide("surfaceSound",e,inherited,selection).Native,"Particle replacement preserves the native sound channel");
        manifest.profiles[0].rules[0].channels[0].mode="add";selection.Particles="test-skin/embers";
        Check(library.Decide("particles",e,inherited,selection).Effects.Count==1,"An explicit inherited profile emits only once");selection.Particles="";manifest.profiles[0].rules[0].channels[0].mode="replace";
        selection.Particles="native";Check(library.Decide("particles",e,inherited,selection).Effects.Count==0,"Explicit native selection bypasses package particles");selection.Particles="";
        manifest.profiles[0].rules.Add(new EffectRule{id="snow",surface="snow",priority=1,channels=new List<ChannelRule>{new ChannelRule{mode="keep"}}});
        e.Surface="snow";Check(library.Decide("particles",e,inherited,selection).Native,"More specific snow rules can restore native surface particles");
        var heavy=new EffectRule{id="boots",equipment="GiantBoots",priority=2,channels=new List<ChannelRule>{new ChannelRule{mode="off"}}};manifest.profiles[0].rules.Add(heavy);
        Check(library.Decide("particles",e,inherited,selection).Native,"Equipment conditions do not grant or assume equipment");e.Equipment=new[]{"GiantBoots"};Check(!library.Decide("particles",e,inherited,selection).Native,"Equipped heavy boots activate their matching rule");
        var missing=new SkinLibrary();missing.Packages.Add(manifest.id,new SkinPackage{Manifest=AdvancedSample()});missing.Packages[manifest.id].Manifest.dependencies.Add(new Dependency{id="absent"});missing.ValidateDependencies();
        Check(missing.Packages.Count==0 && missing.Problems.Count>0,"Missing dependencies remove an incomplete package with diagnostics");
        var cycle=new SkinLibrary();var a=AdvancedSample();var b=AdvancedSample();b.id="other";a.dependencies.Add(new Dependency{id=b.id});b.dependencies.Add(new Dependency{id=a.id});cycle.Packages.Add(a.id,new SkinPackage{Manifest=a});cycle.Packages.Add(b.id,new SkinPackage{Manifest=b});cycle.ValidateDependencies();
        Check(cycle.Packages.Count==0,"Circular libraries are rejected without recursion overflow");
        var data=new WardrobeData();data.Current.Presentation.Particles="test-skin/embers";data.Current.Presentation.ShakeScale=.25f;
        var store=new Store(Path.Combine(output,"advanced-settings"));store.Save(data);var loaded=store.Load();var copy=loaded.Copy();copy.Current.Presentation.Particles="native";
        Check(loaded.Current.Presentation.Particles=="test-skin/embers" && loaded.Current.Presentation.ShakeScale==.25f,"Presentation selection persists and copies independently");
        Check(store.Import(store.Export(loaded.Current)).Presentation.Particles=="test-skin/embers","Outfit recipes carry independent effect selections");
        using(var resources=new PresentationAssets(library))
        {
            var prepared=new PreparedAppearance{Advanced=resources.Retain(),SourceOutfit=new Outfit()};
            using(var actor=new PresentationActor(prepared,new int[0],true))
            {
                var binding=library.ResolveEffect(package,"sparks");actor.Emit(binding,new PresentationEvent());
                Check(actor.Particles.Count==5,"Declarative burst emits its configured particle count");
                var before=actor.Particles[0].Position;actor.Update(.05f);Check(actor.Particles[0].Position!=before,"Particle simulation advances during update");
                for(int i=0;i<8;i++)actor.Update(.05f);Check(actor.Particles.Count==0,"Expired particles leave the bounded pool");
                actor.Emit(binding,new PresentationEvent());actor.Reset();Check(actor.Particles.Count==0 && actor.Time==0,"Restore resets effects and the actor clock");
                manifest.effects[0].count=512;manifest.effects[0].maxAlive=8;actor.Emit(binding,new PresentationEvent());Check(actor.Particles.Count==8,"Emitter limits bound large bursts");
                actor.SetState("rise");actor.Update(.02f);double age=actor.StateTime;actor.SetState("rise");Check(actor.StateTime==age,"Repeated pose observations do not restart a clip");
                actor.SetState("fall");Check(actor.StateTime==0,"State changes begin their own animation clock");
                actor.Reset();manifest.effects[0].count=1;manifest.effects[0].maxAlive=100;manifest.effects[0].cooldown=0;
                var markerClip=new AnimationClip{frames=new List<AnimationFrame>{new AnimationFrame{duration=.01f,effects=new List<string>{"sparks"}},new AnimationFrame{duration=.01f,effects=new List<string>{"sparks"}}}};
                resources.Animations[NativeAppearance.BaseItem]=new AnimationBinding{Package=package,Item=NativeAppearance.BaseItem,Definition=new AnimationSet{clips=new List<AnimationClip>{markerClip}}};
                actor.Update(.055f);Check(actor.Particles.Count==6,"Every crossed frame marker fires, including complete skipped loops");
                actor.SetState("rise");markerClip.transition=.1f;actor.Update(.05f);Check(Math.Abs(actor.Blend(markerClip)-.5f)<.001,"State transitions follow actor time");
            }
            prepared.Dispose();
        }
        var clip=new AnimationClip{frames=new List<AnimationFrame>{new AnimationFrame{duration=.1f},new AnimationFrame{duration=.2f}}};
        Check(PresentationActor.FrameIndex(clip,.15)==1 && PresentationActor.FrameIndex(clip,.35)==0,"Animation honors unequal frame durations and looping");
        clip.loop=false;Check(PresentationActor.FrameIndex(clip,5)==1,"One-shot animation holds its last frame");
        var nativeManifest=AdvancedSample();var nativeClip=new AnimationClip{state="walk",nativeFrames=new List<int>{1,3},frames=new List<AnimationFrame>{new AnimationFrame(),new AnimationFrame()}};
        nativeManifest.animations.Add(new AnimationSet{id="native-walk",clips=new List<AnimationClip>{nativeClip}});
        Check(ManifestIO.Parse(ManifestIO.Write(nativeManifest)).animations[0].clips[0].nativeFrames.SequenceEqual(new[]{1,3}),"Native pose bindings round trip through the skin manifest");
        nativeClip.nativeFrames.RemoveAt(1);Reject(()=>ManifestIO.Validate(nativeManifest),"Incomplete native frame bindings are rejected");
        nativeClip.nativeFrames.Add(13);Reject(()=>ManifestIO.Validate(nativeManifest),"Native pose keys outside the regular atlas are rejected");
        nativeClip.nativeFrames[1]=3;nativeClip.frames[0].effects.Add("sparks");Reject(()=>ManifestIO.Validate(nativeManifest),"Native pose clips reject independently timed frame effects");
    }
    private static void AdvancedGraphicsTests(GraphicsDevice device,SpriteBatch batch,RenderTarget2D target,string compatibilityRepo)
    {
        string packagePath=Environment.GetEnvironmentVariable("WARDROBE_SKIN_TEST");
        if(!string.IsNullOrEmpty(packagePath))RenderAdvancedPackage(packagePath,device,batch,target,compatibilityRepo);
        string eclipsePath=Environment.GetEnvironmentVariable("WARDROBE_ECLIPSE_TEST");
        if(!string.IsNullOrEmpty(eclipsePath))EclipseGraphicsTests(eclipsePath,device,batch,target);
        string variants=Environment.GetEnvironmentVariable("WARDROBE_ECLIPSE_VARIANTS");
        if(!string.IsNullOrEmpty(variants))EclipseVariantPreviews(variants,device,batch,target);
        string vessel=Environment.GetEnvironmentVariable("WARDROBE_VESSEL_TEST");
        if(!string.IsNullOrEmpty(vessel))VesselGraphicsTests(vessel,device,batch,target);
        var original=Controller.Data.Enabled;var oldManager=JumpGame.screenShakeManager;var manager=new ScreenshakeManager();typeof(JumpGame).GetField("_screen_shake_manager",Flags).SetValue(null,manager);
        var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        typeof(PlayerEntity).GetField("m_screen_shake",Flags).SetValue(player,manager.CreateShakeController());
        Controller.Data.Enabled=false;
        try { using(var runtime=new PresentationRuntime(player)) { Check(PresentationRuntime.Live==runtime,"All presentation patches install against the actual native game methods");NativeFeedbackTests(runtime,player); }
            Check(PresentationRuntime.Live==null,"Presentation patches and owned camera controller release cleanly"); }
        finally{Controller.Data.Enabled=original;typeof(JumpGame).GetField("_screen_shake_manager",Flags).SetValue(null,oldManager);}
    }
    private sealed class TestBlock:JumpKing.API.IBlockBehaviour
    {
        public float BlockPriority{get{return 0;}}public bool IsPlayerOnBlock{get;set;}
        public float ModifyXVelocity(float value,JumpKing.BodyCompBehaviours.BehaviourContext context){return value;}
        public float ModifyYVelocity(float value,JumpKing.BodyCompBehaviours.BehaviourContext context){return value;}
        public float ModifyGravity(float value,JumpKing.BodyCompBehaviours.BehaviourContext context){return value;}
        public bool AdditionalXCollisionCheck(JumpKing.Level.AdvCollisionInfo info,JumpKing.BodyCompBehaviours.BehaviourContext context){return true;}
        public bool AdditionalYCollisionCheck(JumpKing.Level.AdvCollisionInfo info,JumpKing.BodyCompBehaviours.BehaviourContext context){return true;}
        public bool ExecuteBlockBehaviour(JumpKing.BodyCompBehaviours.BehaviourContext context){return true;}
    }
    private sealed class TestSound:JumpKing.XnaWrappers.IJKSound
    {
        internal int Calls;internal Action OnPlay;public bool IsLooped{get;set;}public float Volume{get;set;}public TimeSpan Duration{get{return TimeSpan.Zero;}}
        public JumpKing.XnaWrappers.JKSoundState State{get{return default(JumpKing.XnaWrappers.JKSoundState);}}
        public void Play(){Calls++;if(OnPlay!=null)OnPlay();}public void Stop(){}public void Pause(){}public void Resume(){}
    }
    private static void NativeFeedbackTests(PresentationRuntime runtime,PlayerEntity player)
    {
        var body=(BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        var blocks=new Dictionary<Type,JumpKing.API.IBlockBehaviour>();blocks[typeof(JumpKing.Level.BoxBlock)]=new TestBlock{IsPlayerOnBlock=true};
        typeof(BodyComp).GetField("m_blockBehaviourLookup",Flags).SetValue(body,blocks);
        typeof(PlayerEntity).GetField("m_body",Flags).SetValue(player,body);
        typeof(EntityComponent.Entity).GetField("m_components",Flags).SetValue(player,new List<EntityComponent.Component>{body,new InputComponent()});
        var jump=new JumpState(player);var sound=new TestSound();int callbacks=0;jump.RegisterJumpSound<JumpKing.Level.BoxBlock>(sound);jump.RegisterJumpParticleSpawningAction<JumpKing.Level.BoxBlock>(()=>callbacks++);
        var manifest=AdvancedSample();var library=new SkinLibrary();var package=new SkinPackage{Manifest=manifest,Source="native-test",Root=output};library.Packages.Add(manifest.id,package);
        using(var prepared=new PreparedAppearance{SourceOutfit=new Outfit(),Advanced=new PresentationAssets(library)})
        {
            prepared.Resolved[NativeAppearance.BaseItem]=new Resolution{Id="native-test:skin:-1"};
            runtime.Actor=new PresentationActor(prepared,new int[0],true);
            var handleSounds=typeof(JumpState).GetMethod("HandleSounds",Flags);var handleParticles=typeof(JumpState).GetMethod("HandleParticles",Flags);
            object[] arguments={body,new Func<IEnumerable<Type>>(body.OnBlocks)};body.Velocity=new Vector2(3,-7);
            handleSounds.Invoke(jump,arguments);handleParticles.Invoke(jump,arguments);
            Check(sound.Calls==1&&callbacks==0&&runtime.Actor.Particles.Count==5,"Native custom surface sound survives replacement of only the particle callback");
            Check(body.Velocity==new Vector2(3,-7),"Feedback replacements do not change player velocity");
            runtime.Actor.Selection.Particles="native";handleParticles.Invoke(jump,arguments);
            Check(callbacks==1,"Native mode retains a third-party particle spawning callback");
            manifest.profiles[0].rules[0].channels.Add(new ChannelRule{channel="surfaceSound",mode="off"});handleSounds.Invoke(jump,arguments);
            Check(sound.Calls==1,"The surface sound channel can suppress a registered native callback independently");
            blocks.Clear();blocks[typeof(JumpKing.Level.SnowBlock)]=new TestBlock{IsPlayerOnBlock=true};runtime.Actor.Reset();runtime.Actor.Selection.Particles="";
            var doJump=typeof(JumpState).GetMethod("DoJump",Flags);long sequence=PresentationBridge.Sequence;
            doJump.Invoke(jump,new object[]{.1f});
            Check(PresentationBridge.Sequence==sequence&&runtime.Actor.Particles.Count==0&&body.Velocity==new Vector2(3,-7),"An unsuccessful snowy jump emits no takeoff event and preserves native velocity");
            doJump.Invoke(jump,new object[]{.25f});
            Check(PresentationBridge.Sequence==sequence+1&&runtime.Actor.Particles.Count==5&&Math.Abs(runtime.Actor.Charge-.3f)<.001,"A successful snowy jump emits once with the native minimum charge");
            blocks.Clear();blocks[typeof(JumpKing.Level.BoxBlock)]=new TestBlock{IsPlayerOnBlock=true};
            NativeAudioOrder(runtime,player,body,manifest);
            NativeFlightTiming(runtime,player,body);
            runtime.Actor.Dispose();runtime.Actor=null;
        }
    }
    private static void NativeFlightTiming(PresentationRuntime runtime,PlayerEntity player,BodyComp body)
    {
        var actor=runtime.Actor;var sprites=Game1.instance.contentManager.playerSprites;
        var spriteField=typeof(PlayerEntity).GetField("m_sprite",Flags);
        var update=typeof(PresentationRuntime).GetMethod("LateUpdate",Flags);
        int apexEvents=0;Action<PresentationEvent> observed=e=>{if(e.Trigger=="apex")apexEvents++;};
        Action<Sprite> tick=sprite=>{spriteField.SetValue(player,sprite);update.Invoke(runtime,new object[]{1f/60});};
        PresentationEvents.Happened+=observed;
        try
        {
            actor.Reset();body.Velocity=new Vector2(2,-1);tick(sprites.jump_up);
            Check(actor.State=="rise","Live flight follows the native ascent sprite");
            body.Velocity=new Vector2(2,1);tick(sprites.jump_fall);
            bool falling=actor.State=="fall";
            for(int i=0;i<40;i++){tick(sprites.jump_fall);falling &= actor.State=="fall";}
            Check(falling&&apexEvents==1,"Missing apex animation switches to fall on the native descent tick and emits one apex event");
            var apex=new AnimationClip{state="apex",loop=true,frames=new List<AnimationFrame>{new AnimationFrame{duration=.045f},new AnimationFrame{duration=.045f}}};
            actor.Assets.Animations[NativeAppearance.BaseItem]=new AnimationBinding{Item=NativeAppearance.BaseItem,Definition=new AnimationSet{clips=new List<AnimationClip>{apex}}};
            actor.Reset();tick(sprites.jump_up);tick(sprites.jump_fall);
            bool entered=actor.State=="apex",wrapped=false,finished=false,reentered=false;
            for(int i=0;i<40;i++)
            {
                tick(sprites.jump_fall);
                if(actor.State=="apex"){wrapped |= actor.StateTime>=.09;reentered |= finished;}
                if(actor.State=="fall")finished=true;
            }
            Check(entered&&finished&&!wrapped&&!reentered&&apexEvents==2,"An authored looping apex plays once and exits before its first frame can recur");
            tick(sprites.jump_up);tick(sprites.jump_fall);tick(sprites.idle);
            Check(actor.State=="idle","A native landing interrupts the optional apex clip immediately");
        }
        finally{PresentationEvents.Happened-=observed;actor.Assets.Animations.Clear();actor.Reset();}
    }
    private static void NativeAudioOrder(PresentationRuntime runtime,PlayerEntity player,BodyComp body,SkinManifest manifest)
    {
        manifest.profiles[0].rules.Clear();manifest.effects.Add(new EffectDefinition{id="audio-accent",kind="sound",native="jump"});
        foreach(string trigger in new[]{"jump","land","splat"})manifest.profiles[0].rules.Add(new EffectRule{id="aligned-audio-"+trigger,trigger=trigger,
            channels=new List<ChannelRule>{new ChannelRule{channel="surfaceSound",mode="add",effects=new List<string>{"audio-accent"}}}});
        var jump=new JumpState(player);var ground=new IsOnGround(player);var fail=new FailState(player);
        var sound=new TestSound();jump.RegisterJumpSound<JumpKing.Level.BoxBlock>(sound);ground.RegisterLandSound<JumpKing.Level.BoxBlock>(sound);fail.RegisterFailSound<JumpKing.Level.BoxBlock>(sound);
        var order=new List<string>();bool nativeBeforeAccent=true;Action<PresentationEvent> observed=e=>order.Add("published");
        sound.OnPlay=()=>{order.Add("native");nativeBeforeAccent &= !runtime.Actor.Trace.Any(x=>x.Contains("aligned-audio"));};
        PresentationEvents.Happened+=observed;
        try
        {
            foreach(var node in new PlayerNode[]{jump,ground,fail})
            {
                runtime.Actor.Reset();runtime.Actor.Trace.Clear();order.Clear();double time=runtime.Actor.Time;
                var method=node.GetType().GetMethod("HandleSounds",Flags);
                method.Invoke(node,new object[]{body,new Func<IEnumerable<Type>>(()=>{order.Add("lookup");return body.OnBlocks();})});
                Check(nativeBeforeAccent && order.SequenceEqual(new[]{"lookup","native","published"}) && runtime.Actor.Time==time
                    && runtime.Actor.Trace.Any(x=>x.Contains("aligned-audio")),node.GetType().Name+": sound stays at the native call site before cosmetic accents and observers, without waiting for another tick");
            }
            order.Clear();sound.OnPlay=()=>{throw new InvalidOperationException("Native sound failure");};
            Reject(()=>typeof(JumpState).GetMethod("HandleSounds",Flags).Invoke(jump,new object[]{body,new Func<IEnumerable<Type>>(body.OnBlocks)}),"A foreign native sound exception is not swallowed by cosmetic dispatch");
            Check(order.Count==0,"A failed native sound does not publish a completed cosmetic event");
            sound.OnPlay=null;
        }
        finally{PresentationEvents.Happened-=observed;}
    }
    private static void RenderAdvancedPackage(string path,GraphicsDevice device,SpriteBatch batch,RenderTarget2D target,string compatibilityRepo)
    {
        var catalog=new Catalog();var skin=new JumpKing.Workshop.Collection(path);string source=Catalog.SourceId(skin,NativeAppearance.BaseItem,true);
        string parent=Catalog.PackageId(skin);
        catalog.Roots.Add(Catalog.PackageId(skin),path);catalog.Advanced=SkinLibrary.Discover(catalog);
        catalog.Sets.Add(new SetSource{Id=parent,Name="Ashen King"});
        foreach(var entry in skin.Info.Reskins)catalog.Sources.Add(new Source{Id=Catalog.SourceId(skin,(int)entry.skin,true),Name="Ashen King",Item=(int)entry.skin,Asset=Path.Combine(path,entry.name),ParentId=parent,Collection=true});
        var outfit=new Outfit{ParentId=parent,ParentName="Ashen King"};
        Check(skin.Info.Reskins.Length==1 && catalog.Sources.Single().Item==NativeAppearance.BaseItem,"Ashen King is a native body-only collection");
        var migrated=new WardrobeData();string legacy=parent+":skin:"+NativeAppearance.BaseItem;
        migrated.Current.Set(new AppearanceChoice{Item=NativeAppearance.BaseItem,Mode=ChoiceMode.Source,SourceId=legacy,Locked=true});
        migrated.Current.Set(new AppearanceChoice{Item=0,Mode=ChoiceMode.OriginalGame});
        migrated.Current.SetFit(new FitAdjustment{BaseId=legacy,SourceId=legacy,Item=NativeAppearance.BaseItem,X=2});
        migrated.Favorites.Add(legacy);migrated.Presets.Add(migrated.Current.Copy());migrated.Presets[0].ParentId="another-collection";
        Check(catalog.UpgradeCollections(migrated) && migrated.Current.ParentId==parent && migrated.Current.Choice(NativeAppearance.BaseItem).Mode==ChoiceMode.Inherit
            && migrated.Current.Choice(NativeAppearance.BaseItem).Locked && migrated.Current.Choice(0).Mode==ChoiceMode.OriginalGame
            && migrated.Current.Fits.Single().BaseId==source && migrated.Favorites.Single()==source
            && migrated.Presets[0].ParentId=="another-collection" && migrated.Presets[0].Choice(NativeAppearance.BaseItem).SourceId==source,
            "Promoting a body skin preserves outfit overrides, locks, fits, favorites and existing collection parents");
        Check(!catalog.UpgradeCollections(migrated) && catalog.Find(legacy,NativeAppearance.BaseItem).Id==source,"Collection migration is idempotent and old recipes still resolve");
        using(var prepared=PreparedAppearance.Build(outfit,catalog,true))using(var actor=new PresentationActor(prepared,new int[0],true))
        {
            Check(prepared.Advanced.Prepared&&prepared.Advanced.Sounds.Count==1&&prepared.Advanced.Animations.Count==1,"Ashen King loads PNG, charge WAV, visor shader and native XNB assets through the runtime");
            var sprite=NativeAppearance.Frames(prepared.Base.regular)[0];
            Check(!prepared.Advanced.Packages.Single().Manifest.effects.Any(e=>e.kind=="shake"),"Ashen King adds no screen shake");
            Check(!prepared.Advanced.Animations[NativeAppearance.BaseItem].Definition.clips.Any(c=>c.state=="apex"),"Ashen King follows native flight timing without an intermediate apex pose");
            AdvancedCadenceTest(actor,sprite,device,batch);
            AdvancedVisorTest(actor,sprite,device,batch);
            if(compatibilityRepo!=null)AdvancedNativeDrawTest(prepared,device,batch,target,compatibilityRepo);
            device.SetRenderTarget(target);device.Clear(new Color(18,22,30));batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null);
            PresentationDraw.Current=actor;
            try{
                for(int i=0;i<ManifestIO.States.Length;i++)
                {
                    actor.Reset();actor.SetState(ManifestIO.States[i]);actor.Position=new Vector2(48+(i%5)*94,110+(i/5)*160);actor.Flipped=i%2==1;
                    actor.Trigger(i==6?"land":"jump");for(int tick=0;tick<8;tick++)actor.Update(1f/60);
                    double time=actor.Time;int count=actor.Particles.Count;
                    PresentationDraw.Particles(actor,"back",actor.Position,2,false,Color.White);
                    sprite.Draw(new Rectangle((int)actor.Position.X,(int)actor.Position.Y,sprite.source.Width*2,sprite.source.Height*2),actor.Flipped?SpriteEffects.FlipHorizontally:SpriteEffects.None);
                    PresentationDraw.Particles(actor,"front",actor.Position,2,false,Color.White);
                    Check(actor.Time==time&&actor.Particles.Count==count,"Drawing "+actor.State+" leaves simulation unchanged");
                    batch.DrawString(Game1.instance.contentManager.font.MenuFontSmall,actor.State,new Vector2(actor.Position.X-25,actor.Position.Y+12),Color.White);
                }
            }finally{PresentationDraw.Current=null;batch.End();device.SetRenderTarget(null);}
            using(var file=File.Create(Path.Combine(output,"ashen-king-states.png")))target.SaveAsPng(file,target.Width,target.Height);
            var pixels=new Color[target.Width*target.Height];target.GetData(pixels);Check(pixels.Count(c=>c.R>35&&c.G>30&&c.B>30&&c.R<170)>2000,"Advanced material draws the full armor silhouette, not only particles and attachments");
            using(var other=new PresentationActor(prepared,new int[0],true)){Check(other.Time==0&&other.Particles.Count==0,"Independent preview and replay actors do not share clocks or particles");}
            var previous=Controller.Active;Controller.Active=prepared;
            try
            {
                using(var replay=(PresentationActor)PresentationBridge.CreateActor(true))
                {
                    var e=new PresentationEvent{Trigger="jump",X=60,Y=80};string json=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(e);
                    PresentationBridge.Advance(replay,0,0,60,80,2,-5,"rise",false,new int[0],new[]{"{broken",json},true);
                    Check(replay.Particles.Count>0&&replay.Silent&&!replay.AllowShake,"Replay bridge tolerates a malformed event and emits valid events on a silent independent actor");
                    double time=replay.Time;int count=replay.Particles.Count;PresentationBridge.Pause(replay,true);Check(replay.Time==time&&replay.Particles.Count==count,"Replay pause does not simulate another frame");
                    using(PresentationBridge.Playback())Check(PresentationBridge.PlayingReplay,"Viewer scope suspends the live cosmetic actor");
                    Check(!PresentationBridge.PlayingReplay,"Viewer scope restores live cosmetic ownership");
                }
            }finally{Controller.Active=previous;}
        }
    }
    private static void AdvancedCadenceTest(PresentationActor actor,Sprite sprite,GraphicsDevice device,SpriteBatch batch)
    {
        using(var target=new RenderTarget2D(device,64,64))
        {
            var pixels=new Color[64*64];bool visible=true,stable=true,opaque=true;int draws=0;
            PresentationDraw.Current=actor;
            try
            {
                actor.Reset();
                foreach(var state in ManifestIO.States)
                {
                    actor.SetState(state);
                    for(int tick=0;tick<72;tick++)
                    {
                        Color[] first=null;double time=actor.Time;
                        for(int subframe=0;subframe<4;subframe++)
                        {
                            device.SetRenderTarget(target);device.Clear(Color.Transparent);
                            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            sprite.Draw(new Vector2(32,56),tick%2==0?SpriteEffects.None:SpriteEffects.FlipHorizontally);
                            batch.End();device.SetRenderTarget(null);target.GetData(pixels);draws++;
                            visible &= pixels.Count(c=>c.A>0)>=300;
                            opaque &= pixels.All(c=>c.A==0||c.A==255);
                            if(first==null)first=(Color[])pixels.Clone();else stable &= first.SequenceEqual(pixels);
                        }
                        stable &= actor.Time==time;actor.Update(1f/60);
                    }
                }
                Check(visible&&opaque,"All "+draws+" Ashen King draws retain opaque body coverage, including state entry and complete animation loops");
                Check(stable,"Four renders per simulation tick (240 Hz / 60 Hz) preserve identical poses and actor clocks");
                var binding=actor.Assets.Animations[NativeAppearance.BaseItem];var texture=actor.Assets.Texture(binding.Package,binding.Definition.clips[0].texture);
                var idle=binding.Definition.clips.Find(c=>c.state=="idle").frames[0];var reference=new Color[idle.width*idle.height];
                texture.GetData(0,new Rectangle(idle.x,idle.y,idle.width,idle.height),reference,0,reference.Length);
                bool standing=true;foreach(var frame in binding.Definition.clips.Find(c=>c.state=="land").frames)
                {var landed=new Color[reference.Length];texture.GetData(0,new Rectangle(frame.x,frame.y,frame.width,frame.height),landed,0,landed.Length);standing &= reference.Select(c=>c.A).SequenceEqual(landed.Select(c=>c.A));}
                Check(standing,"Every landing frame preserves the standing silhouette");
            }
            finally{PresentationDraw.Current=null;device.SetRenderTarget(null);actor.Reset();}
        }
    }
    private static void AdvancedVisorTest(PresentationActor actor,Sprite sprite,GraphicsDevice device,SpriteBatch batch)
    {
        using(var target=new RenderTarget2D(device,64,64))using(var preview=new RenderTarget2D(device,480,128))
        {
            PresentationDraw.Current=actor;actor.Reset();actor.SetState("charge");
            try
            {
                var frames=new List<Color[]>();
                foreach(float charge in new[]{0f,.25f,.5f,.75f,1f})
                {
                    actor.Charge=charge;device.SetRenderTarget(target);device.Clear(Color.Transparent);
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);sprite.Draw(new Vector2(32,56));batch.End();device.SetRenderTarget(null);
                    var pixels=new Color[64*64];target.GetData(pixels);frames.Add(pixels);
                }
                int changed=Enumerable.Range(0,64*64).Count(i=>frames[0][i]!=frames[4][i]);
                int slit=48*64+29;
                Check(changed==10 && frames.All(f=>f.Select(c=>c.A).SequenceEqual(frames[0].Select(c=>c.A)))
                    && frames.All(f=>f[50*64+31]==f[slit] && f[50*64+32]==f[slit])
                    && frames[0][slit]==new Color(15,13,18) && frames[2][slit].R>190 && frames[2][slit].G<60
                    && frames[4][slit].R==255 && frames[4][slit].G>220,
                    "Actual charge heats the complete T-shaped visor, including both bottom stem pixels, while preserving body alpha");
                var sound=actor.Assets.Library.Decide("surfaceSound",new PresentationEvent{Trigger="jump",Surface="normal"},actor.Profiles,actor.Selection);
                Check(sound.Native&&sound.Effects.Count==0,"Ordinary takeoff keeps native audio without an electronic accent");
                bool nativeImpacts=true;
                foreach(string trigger in new[]{"jump","land","splat"})foreach(string surface in new[]{"normal","snow","ice","water","sand"})foreach(string channel in new[]{"surfaceSound","equipmentSound"})
                {var decision=actor.Assets.Library.Decide(channel,new PresentationEvent{Trigger=trigger,Surface=surface,Equipment=new[]{"GiantBoots"}},actor.Profiles,actor.Selection);nativeImpacts &= decision.Native&&decision.Effects.Count==0;}
                Check(nativeImpacts,"Ashen King preserves native takeoff, landing and splat audio for every surface and heavy boots");
                device.SetRenderTarget(preview);device.Clear(new Color(18,22,30));
                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                for(int i=0;i<5;i++)
                {
                    actor.Charge=i/4f;
                    sprite.Draw(new Rectangle(48+i*96,92,96,96));
                    batch.DrawString(Game1.instance.contentManager.font.MenuFontSmall,(i*25)+"%",new Vector2(34+i*96,105),Color.White);
                }
                batch.End();device.SetRenderTarget(null);
                using(var file=File.Create(Path.Combine(output,"ashen-king-charge.png")))preview.SaveAsPng(file,preview.Width,preview.Height);
            }
            finally{PresentationDraw.Current=null;device.SetRenderTarget(null);actor.Reset();actor.Charge=0;}
        }
    }
    private static void AdvancedNativeDrawTest(PreparedAppearance prepared,GraphicsDevice device,SpriteBatch batch,RenderTarget2D target,string compatibilityRepo)
    {
        var smooth=System.Reflection.Assembly.LoadFrom(Path.Combine(compatibilityRepo,"build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll"));
        var context=smooth.GetType("SmoothCamera.RenderContext",true);
        var originalEnabled=Controller.Data.Enabled;var originalManager=JumpGame.screenShakeManager;
        var screen=typeof(Camera).GetField("_current_screen",Flags);var originalScreen=screen.GetValue(null);var originalOffset=Camera.Offset;
        var manager=new ScreenshakeManager();typeof(JumpGame).GetField("_screen_shake_manager",Flags).SetValue(null,manager);
        var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        var body=(BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));body.Position=new Vector2(231,-500);
        typeof(PlayerEntity).GetField("m_body",Flags).SetValue(player,body);
        typeof(PlayerEntity).GetField("m_screen_shake",Flags).SetValue(player,manager.CreateShakeController());
        var layered=NativeAppearance.Layer(prepared,new int[0]);typeof(PlayerEntity).GetField("m_sprite",Flags).SetValue(player,NativeAppearance.Frames(layered.regular)[0]);
        Controller.Data.Enabled=false;screen.SetValue(null,0);Camera.Offset=Vector2.Zero;
        try
        {
            using(var runtime=new PresentationRuntime(player))using(var patches=new JKRuntime.OwnedPatches("WardrobeTests.AdvancedCamera"))
            {
                runtime.Actor=new PresentationActor(prepared,new int[0],true);
                runtime.Actor.Position=body.Position+new Vector2(9,26);
                patches.Add(typeof(PlayerEntity).GetMethod("Draw"),transpiler:smooth.GetType("SmoothCamera.NativeDrawQueries",true).GetMethod("Rewrite",Flags));
                patches.Add(typeof(Camera).GetMethod("TransformVector2"),postfix:context.GetMethod("Vector",Flags));
                // exercise the same entry point and native layered wrappers as live play
                bool visible=true,stable=true;var pixels=new Color[target.Width*target.Height];
                foreach(var state in ManifestIO.States)
                {
                    runtime.Actor.SetState(state);
                    for(int tick=0;tick<4;tick++)
                    {
                        Color[] reference=null;
                        for(int subframe=0;subframe<4;subframe++)
                        {
                            device.SetRenderTarget(target);device.Clear(Color.Transparent);batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            using((IDisposable)Activator.CreateInstance(context,Flags,null,new object[]{2,0,0,-1},null))player.Draw();
                            batch.End();device.SetRenderTarget(null);target.GetData(pixels);
                            visible &= pixels.Count(c=>c.A==255)>=300;
                            if(reference==null)reference=(Color[])pixels.Clone();else stable &= reference.SequenceEqual(pixels);
                            stable &= PresentationDraw.Current==null;
                            if(tick==1&&subframe==0)
                            {
                                var expected=(Color[])pixels.Clone();double stateTime=runtime.Actor.StateTime;long eventSequence=PresentationBridge.Sequence;
                                using((IDisposable)Activator.CreateInstance(context,Flags,null,new object[]{2,0,0,-1},null))
                                {
                                    var frame=JKRuntime.Presentation.PlayerAppearance.Resolve(player);
                                    var captured=JKRuntime.Presentation.AppearanceCapture.Read(frame,256);
                                    using(var frozen=captured.CreateTexture(device))
                                    {
                                        device.SetRenderTarget(target);device.Clear(Color.Transparent);batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                                        batch.Draw(frozen,captured.TopLeft(Camera.TransformVector2(frame.WorldAnchor)),Color.White);batch.End();device.SetRenderTarget(null);
                                        var actual=new Color[pixels.Length];target.GetData(actual);
                                        Check(actual.SequenceEqual(expected),"Runtime capture matches advanced "+state+" under the current camera context");
                                    }
                                }
                                Check(PresentationDraw.Current==null&&runtime.Actor.StateTime==stateTime&&PresentationBridge.Sequence==eventSequence,"Appearance capture preserves animation context, time and event history");
                            }
                        }
                        runtime.Actor.Update(1f/60);
                    }
                }
                Check(visible&&stable,"Native PlayerEntity.Draw, layered sprites and Smooth Camera retain the body at four draws per tick and restore draw context");
            }
        }
        finally{Controller.Data.Enabled=originalEnabled;typeof(JumpGame).GetField("_screen_shake_manager",Flags).SetValue(null,originalManager);screen.SetValue(null,originalScreen);Camera.Offset=originalOffset;device.SetRenderTarget(null);}
    }
}

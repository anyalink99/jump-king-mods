using System;
using System.IO;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Presentation;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace Metroidvania
{
    [RuntimeModule("metroidvania","Metroidvania",MapSuspendable=true)]
    public static class ModEntry
    {
        private static SettingsFile<Preferences> settings;
        private static CollisionWorld prepared;
        private static EncounterFile encounters;
        private static bool registered;
        private static RuntimeScope worldScope;
        private static WorldResources worldResources;
        private sealed class WorldResources : IDisposable
        {
            internal IDisposable Art;
            internal QuestAudio Audio;
            internal EnemyArt Enemies;
            internal void Clear(){if(Audio!=null){Audio.Dispose();Audio=null;}if(Enemies!=null){Enemies.Dispose();Enemies=null;}if(Art!=null){Art.Dispose();Art=null;}}
            public void Dispose(){Clear();}
        }
        internal static CharacterStore Store;
        internal static GameSession Session;
        internal static string Status="";
        internal static Preferences Preferences {get{return settings.Value;}}
        [BeforeLevelLoad] public static void Load()
        {
            using(RuntimeApi.MeasureStartup("metroidvania.catalogue-load"))ContentCatalog.Prepare();
            if(settings==null){string root=PackageHost.GetDataDirectory(typeof(ModEntry).Assembly);settings=new SettingsFile<Preferences>(Path.Combine(root,"Metroidvania.Settings.xml"),()=>new Preferences(),Metroidvania.Preferences.Validate);Store=new CharacterStore(Path.Combine(root,"Metroidvania.Character.xml"));}
            if(registered)return;Inputs.Register();
            ActorVisuals.Register("metroidvania.equipment",1,VisualPhase.Attachment,p=>Session!=null&&ReferenceEquals(p,Session.Player)?Session.VisualState:null,()=>new EquipmentVisual());
            registered=true;
        }
        [BeforeAttempt] public static void Prepare(RuntimeScope scope)
        {
            prepared=null;encounters=null;scope.Defer(delegate{prepared=null;encounters=null;});
            using(RuntimeApi.MeasureStartup("metroidvania.character-read"))Store.Reload();
            if(!Preferences.Enabled||!Store.Writable){if(worldResources!=null)worldResources.Clear();return;}
            PrepareArt();
            using(RuntimeApi.MeasureStartup("metroidvania.encounters-read")){
                GameSession.ValidateNative();prepared=new CollisionWorld();encounters=EncounterFile.Read(Game1.instance.contentManager.root,prepared.Screens);
                foreach(var spawn in encounters.Enemies){var definition=worldResources.Enemies.Prepare(spawn.Type??ContentCatalog.DefaultEnemy);worldResources.Audio.Ensure(definition);}
            }
        }
        [OnWorldReady] public static void PrepareWorld(RuntimeScope scope)
        {
            worldScope=scope;worldResources=scope.Own(new WorldResources());scope.Defer(delegate{worldScope=null;worldResources=null;});if(Preferences.Enabled)PrepareArt();
        }
        private static void PrepareArt()
        {
            if(worldScope==null)throw new InvalidOperationException("Equipment world was not prepared");
            if(worldResources.Art==null)using(RuntimeApi.MeasureStartup("metroidvania.art-load"))worldResources.Art=EquipmentArt.Acquire();
            if(worldResources.Audio==null)using(RuntimeApi.MeasureStartup("metroidvania.audio-load"))worldResources.Audio=new QuestAudio();
            if(worldResources.Enemies==null)using(RuntimeApi.MeasureStartup("metroidvania.enemies-load"))worldResources.Enemies=new EnemyArt();
            worldResources.Audio.Ensure(worldResources.Enemies.Definition);
        }
        [OnLevelStart] public static void Start(ModuleContext context)
        {
            if(!Preferences.Enabled||prepared==null||encounters==null||!Store.Writable)return;
            var player=GameLoop.m_player;if(player==null)throw new InvalidOperationException("Metroidvania needs the active king");
            Session=context.Track(new GameSession(player,prepared,encounters,worldResources.Audio,worldResources.Enemies));var current=Session;
            context.Track(new RuntimeScope()).Defer(delegate{if(ReferenceEquals(Session,current))Session=null;});Inputs.Activate(context,current);
        }
        internal static void SavePreferences(Preferences next){settings.Save(next);}
        [MainMenuItemSetting] public static TextButton MainPage(object factory,GuiFormat format)
        {return new TextButton("Character",UIApi.CreateMenuPage(factory,new UiPageStack(p=>new CharacterPage(p))));}
        [PauseMenuItemSetting] public static TextButton Pause(object factory,GuiFormat format){return MainPage(factory,format);}
    }
}

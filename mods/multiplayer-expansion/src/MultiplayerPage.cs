using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Steamworks;

namespace MultiplayerExpansion
{
    internal sealed class MultiplayerPage : ScopedUiPage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(12, 12, 456, 336));
        private readonly UiList list = new UiList();
        private readonly UiPageCommand[] commands;
        internal enum Section { Root, Host, Debug, Network }
        private readonly UiPageStack pages;
        private readonly Section section;
        private bool debug {get{return section==Section.Debug;}}
        private bool network {get{return section==Section.Network;}}
        internal MultiplayerPage(UiPageStack stack,Section current=Section.Root)
        { pages=stack;section=current;
          commands = new[] { new UiPageCommand(UiAction.Cancel, "Back", ()=>WantsClose=true) }; Refresh(); }
        private void Open(Section child) {pages.Push(new MultiplayerPage(pages,child));}
        private static int Next(int value,int[] choices) {int index=Array.IndexOf(choices,value);return choices[(index+1)%choices.Length];}
        private void Refresh()
        {
            var rows = new List<UiListItem>();
            if(network)
            {
                var options=NetworkLab.Read();bool enabled=NetworkLab.Available;
                rows.Add(new UiListItem("delay","One-way delay: "+options.Delay+" ms",()=>NetworkLab.Change(x=>x.Delay=Next(x.Delay,new[]{0,30,60,100,150,250,500})),"Applied independently in both directions.",enabled));
                rows.Add(new UiListItem("jitter","Jitter: +/- "+options.Jitter+" ms",()=>NetworkLab.Change(x=>x.Jitter=Next(x.Jitter,new[]{0,10,30,60,100,200,300})),"Variable delivery times can reorder packets.",enabled));
                rows.Add(new UiListItem("loss","Packet loss: "+options.Loss+"%",()=>NetworkLab.Change(x=>x.Loss=Next(x.Loss,new[]{0,1,5,10,20,50})),"Drop packets after transport delivery, before the game receives them.",enabled));
                rows.Add(new UiListItem("duplicate","Duplicates: "+options.Duplicate+"%",()=>NetworkLab.Change(x=>x.Duplicate=Next(x.Duplicate,new[]{0,1,5,10,25})),"Deliver selected packets twice, with independent jitter.",enabled));
                rows.Add(new UiListItem("reset","Reset network conditions",()=>NetworkLab.Change(x=>{x.Delay=x.Jitter=x.Loss=x.Duplicate=0;x.Seed++;}),"Clear pending packets and restore a clean link.",enabled));
                rows.Add(new UiListItem("record",options.Recording==0 ? "Record network (up to 2 min)" : "Stop recording",()=>NetworkLab.Change(x=>x.Recording=x.Recording==0 ? DateTime.UtcNow.Ticks : 0),"Record both clients' extension packets. Up to 4 MiB per file; three files per client.",enabled));
                rows.Add(new UiListItem("replay","Verify last packet recording",NetworkLab.VerifyTrace,"Replay this client's network snapshots twice and compare results. Does not control the game.",enabled));
            }
            else if (debug)
            {
                rows.Add(new UiListItem("local", "Start two clients: Local", () => NativeSession.Start("Local"), "Two independent games, connected locally."));
                rows.Add(new UiListItem("steam", "Start two clients: Steam", () => NativeSession.Start("Steam"), "Two games over Steam loopback sockets."));
                rows.Add(new UiListItem("switch", "Switch input client", NativeSession.Switch, "6 switches normal controls. Other client: [ left, ] right, \\ jump."));
                rows.Add(new UiListItem("stop", "Stop / cancel client 2", NativeSession.Stop, "Close the extra client and return to one game."));
                rows.Add(new UiListItem("network","Network test conditions",()=>Open(Section.Network),"Delay, jitter, loss, duplicates and packet recording."));
            }
            else if(section==Section.Host)
            {
                rows.Add(new UiListItem("mode", "Mode: " + InteractionSettings.Name(AdvancedSession.Rules), delegate {
                    var current = AdvancedSession.Rules;
                    AdvancedSession.SetRules(current == InteractionRules.Ghosts ? AdvancedSession.SolidRules : InteractionRules.Ghosts); Refresh();
                }, "Ghosts: pass through. Solid: stand on heads, collide and bounce. Multiplayer's Ghost opacity remembers each mode.", AdvancedSession.CanEdit));
                var pushRules = AdvancedSession.Rules == InteractionRules.Ghosts ? AdvancedSession.SolidRules : AdvancedSession.Rules;
                rows.Add(new UiListItem("world", "World synchronization: "+(WorldSession.Enabled?"On":"Off"),
                    ()=>{WorldSession.Set(!WorldSession.Enabled);Refresh();},
                    "Host runs the shared world; players trigger events. Independent of Ghosts/Solid. "+WorldSession.Status,AdvancedSession.CanEdit));
                rows.Add(new UiListItem("push", "Push other players: " + ((pushRules & InteractionRules.Push) != 0 ? "On" : "Off"),
                    () => { AdvancedSession.SetRules(AdvancedSession.Rules ^ InteractionRules.Push); Refresh(); },
                    "Walking pushes in Solid mode. Remembered when switching modes.", AdvancedSession.CanEdit && AdvancedSession.Rules != InteractionRules.Ghosts));
                rows.Add(new UiListItem("kicks","Allow kicks: "+(PlayerActions.Kicks ? "On" : "Off"),()=>PlayerActions.Set(!PlayerActions.Kicks,PlayerActions.Teleports),
                    "A forward shockwave adds momentum. 72 px reach, 0.55 s cooldown; terrain blocks it.",AdvancedSession.CanEdit));
                rows.Add(new UiListItem("teleports","Allow teleport: "+(PlayerActions.Teleports ? "On" : "Off"),()=>PlayerActions.Set(PlayerActions.Kicks,!PlayerActions.Teleports),
                    "Players may teleport to free space near someone on the same map.",AdvancedSession.CanEdit));
            }
            else
            {
                rows.Add(new UiListItem("host","Host Settings",()=>Open(Section.Host),"Shared rules, player contacts, world state and allowed actions."));
                rows.Add(new UiListItem("binds","Binds",()=>pages.Push(new UiBindingsPage("Binds","Kick sends a forward shockwave. Keyboard default: K.",PlayerActionBindings.KickId){ShowList=true}),"Set primary and secondary Kick bindings for your input device."));
                if (NativeMod.DebugEnabled) rows.Add(new UiListItem("debug", "Two-client test tools",()=>Open(Section.Debug),"Local / Steam test connection and input selection."));
            }
            list.SetItems(rows);
        }
        public override void Update(UiInput input, float delta)
        { foreach (var command in commands) if (command.Handle(input)) return; Refresh(); list.Update(input); }
        public override void Draw()
        {
            frame.Draw();
            var layout = new UiPageLayout(frame.Bounds, commands, 4);
            UiTheme.TextLine(network ? "NETWORK TEST CONDITIONS" : debug ? "MULTIPLAYER TEST TOOLS" : section==Section.Host ? "HOST SETTINGS" : "MULTIPLAYER EXPANSION", layout.Title.Location.ToVector2(), UiTheme.Gold, false);
            list.Draw(layout.Content);
            UiTheme.WrappedText((network ? NetworkLab.Status : debug ? NativeSession.Status : PlayerActions.Status!="" ? PlayerActions.Status : AdvancedSession.Status) + "\n" + list.Description, layout.Status, UiTheme.Muted);
            layout.DrawCommands();
        }
    }
}

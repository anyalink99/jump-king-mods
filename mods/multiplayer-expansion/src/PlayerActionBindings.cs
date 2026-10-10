using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JKRuntime;
using JKRuntime.UI;
using JumpKing.Controller;
using Microsoft.Xna.Framework.Input;

namespace MultiplayerExpansion
{
    internal static class PlayerActionBindings
    {
        internal const string KickId="multiplayer-expansion.kick";
        private static bool registered;
        private static readonly Dictionary<string,int[][]> devices=new Dictionary<string,int[][]>(StringComparer.Ordinal);
        private static string PathName { get { return Path.Combine(NativeMod.Package,"kick-binds.txt"); } }
        internal static bool Available {get{
            var pad=ControllerManager.instance==null ? null : ControllerManager.instance.GetMain();
            return pad!=null && pad.IsValid && pad.IsConnected && !pad.GetBind().disabled;
        }}
        private static PadInstance CurrentPad()
        {
            var pad=ControllerManager.instance==null ? null : ControllerManager.instance.GetMain();
            if(pad==null || !pad.IsValid || !pad.IsConnected) throw new InvalidOperationException("No active input device");
            return pad;
        }
        internal static int[][] Normalize(int[][] value)
        { return (value ?? new int[0][]).Take(2).Select(c=>new UiChord((c ?? new int[0]).Where(b=>b>=0).Distinct().Take(2).ToArray()).Buttons).ToArray(); }
        internal static int[][] Default(IPad pad)
        {return SecondaryControls.IsKeyboard(pad) ? new[]{new[]{(int)Keys.K}} : new int[0][];}
        internal static void Register()
        {
            if(registered) return;
            if(File.Exists(PathName) && new FileInfo(PathName).Length<=16384)
                foreach(string line in File.ReadAllLines(PathName).Take(64)) {
                    try {
                        var pieces=line.Split(' ');
                        if(pieces.Length!=2) continue;
                        string device=Encoding.UTF8.GetString(Convert.FromBase64String(pieces[0]));
                        var chords=pieces[1].Split('/').Select(c=>c=="-" ? new int[0] : c.Split(',').Select(int.Parse).ToArray()).ToArray();
                        if(device.Length<=256) devices[device]=Normalize(chords);
                    } catch(FormatException) { } catch(OverflowException) { }
                }
            UIApi.RegisterBinding(new UiBindingDefinition(KickId,"Multiplayer Expansion","Kick",Get,Set,delegate {Save(CurrentPad().GetPad().GetSaveIdentifier(),null);})
                {Mode=new UiBindingModeOption(UiBindingMode.Press,"One kick per press; host cooldown applies")});
            registered=true;
        }
        internal static UiChord[] Get()
        {
            var pad=CurrentPad();int[][] value;
            if(!devices.TryGetValue(pad.GetPad().GetSaveIdentifier(),out value))
                value=Default(pad.GetPad());
            return value.Select(c=>c.Length==0 ? null : new UiChord(c)).ToArray();
        }
        private static void Set(UiChord[] chords)
        {Save(CurrentPad().GetPad().GetSaveIdentifier(),Normalize((chords ?? new UiChord[0]).Select(c=>c==null ? null : c.Buttons).ToArray()));}
        private static void Save(string device,int[][] value)
        {
            var next=new Dictionary<string,int[][]>(devices,StringComparer.Ordinal);
            if(value==null) next.Remove(device);else next[device]=value;
            if(device.Length>256 || next.Count>64) throw new InvalidOperationException("Too many saved input devices");
            string text=string.Join("\n",next.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Key))+" "+
                (p.Value.Length==0 ? "-" : string.Join("/",p.Value.Select(c=>c.Length==0 ? "-" : string.Join(",",c))))));
            if(Encoding.UTF8.GetByteCount(text)>16384) throw new InvalidOperationException("Saved bindings exceed 16 KiB");
            string temp=PathName+".tmp";File.WriteAllText(temp,text);
            if(File.Exists(PathName)) File.Replace(temp,PathName,PathName+".bak");else File.Move(temp,PathName);
            devices.Clear();foreach(var pair in next) devices.Add(pair.Key,pair.Value);
        }
        internal static void Activate(ModuleContext context)
        {
            var scope=context.Track(new UiRegistrationScope("anyalink.multiplayer-expansion.world"));
            scope.RegisterInputAction(UiInputActionDefinition.FromChords(KickId,"Kick",100,Get,()=>PlayerActions.CanKick,PlayerActions.Kick));
            context.Track(new RuntimeScope()).Defer(PlayerEffects.Reset);
        }
    }
}

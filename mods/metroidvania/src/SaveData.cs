using System;
using System.Collections.Generic;
using System.IO;
using JKRuntime.Settings;

namespace Metroidvania
{
    public sealed class SavedBinding
    {
        public string Device, Action;
        public int[] Primary=new int[0], Secondary=new int[0];
        public SavedBinding Copy(){return new SavedBinding{Device=Device,Action=Action,Primary=(int[])Primary.Clone(),Secondary=(int[])Secondary.Clone()};}
    }
    public sealed class Preferences
    {
        public int Version=1;
        public bool Enabled;
        public SavedBinding[] Bindings=new SavedBinding[0];
        public Preferences Copy(){var p=(Preferences)MemberwiseClone();p.Bindings=new SavedBinding[Bindings.Length];for(int i=0;i<Bindings.Length;i++)p.Bindings[i]=Bindings[i].Copy();return p;}
        public static void Validate(Preferences p)
        {
            if(p==null||p.Version!=1||p.Bindings==null||p.Bindings.Length>128)throw new InvalidDataException("Unsupported Metroidvania settings");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in p.Bindings) {
                if(b==null||string.IsNullOrEmpty(b.Device)||b.Device.Length>256||!Inputs.Known(b.Action)||!seen.Add(b.Device+"\n"+b.Action))throw new InvalidDataException("Invalid saved binding");
                foreach(var chord in new[]{b.Primary,b.Secondary}){if(chord==null||chord.Length>2)throw new InvalidDataException("Invalid chord");foreach(int button in chord)if(button<0||button>65535)throw new InvalidDataException("Invalid button");}
            }
        }
    }
    internal sealed class CharacterStore
    {
        private SettingsFile<CharacterProfile> file;
        private readonly string path;
        internal CharacterProfile Current {get;private set;}
        internal int Revision {get;private set;}
        internal bool Dirty {get;private set;}
        internal string Error {get;private set;}
        internal bool Writable {get{return file.CanSave;}}
        internal CharacterStore(string name){path=name;Reload();}
        internal void Reload()
        {
            if(Dirty)return;
            file=new SettingsFile<CharacterProfile>(path,()=>new CharacterProfile(),CharacterProfile.Validate);Current=file.Value;Error=file.CanSave?"":file.Error;Revision++;
        }
        internal bool Edit(Func<CharacterProfile,bool> edit)
        {
            if(!Writable){Error=file.Error;return false;}
            var next=Current.Copy();if(!edit(next))return false;CharacterProfile.Validate(next);Current=next;Dirty=true;Revision++;return true;
        }
        internal bool Save()
        {
            if(!Dirty)return Writable;
            try{file.Save(Current);Dirty=false;Error="";return true;}
            catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is InvalidOperationException))throw;Error="Character not saved: "+e.Message;return false;}
        }
    }
}

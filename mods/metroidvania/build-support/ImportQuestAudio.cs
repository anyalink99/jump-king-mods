using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using FMOD;
using FMOD.Studio;

internal static class ImportQuestAudio
{
    public sealed class Cue {public string id,@event,event_guid;}
    public sealed class Recipe {public string[] banks;public Cue[] audio;}
    private static string[] Banks;
    private static void Ok(RESULT result){if(result!=RESULT.OK)throw new InvalidDataException("FMOD: "+result);}
    public static int Main(string[] args)
    {
        try {
            string quest=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]),scratch=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);Directory.CreateDirectory(scratch);
            var recipe=new JavaScriptSerializer().Deserialize<Recipe>(File.ReadAllText(args[3]));Banks=recipe.banks;
            var receipts=new List<object>();
            foreach(var cue in recipe.audio) {
                string name=cue.id,path=cue.@event,raw=Path.Combine(scratch,name+".wav");
                path=Render(quest,path,cue.event_guid,raw);int frames=ConvertWave(raw,Path.Combine(output,name+".wav"));
                receipts.Add(new {name=name,event_path=path,frames=frames,sample_rate=48000,channels=1,sha256=Hash(Path.Combine(output,name+".wav"))});Console.WriteLine(name+" "+frames+" samples");
            }
            var banks=new List<object>();foreach(string bank in Banks){string path=Path.Combine(quest,"JKQ_Data","StreamingAssets",bank);banks.Add(new{name=bank,sha256=Hash(path)});}
            File.WriteAllText(Path.Combine(output,"source-receipt.json"),new JavaScriptSerializer().Serialize(new{actor="Niotid",fmod_version=FMOD.VERSION.number,banks=banks,clips=receipts}));return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static string Hash(string path){using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    private static string Render(string quest,string path,string guid,string output)
    {
        FMOD.Studio.System studio;Ok(FMOD.Studio.System.create(out studio));IntPtr filename=Marshal.StringToHGlobalAnsi(output);
        try {
            FMOD.System core;Ok(studio.getCoreSystem(out core));Ok(core.setOutput(OUTPUTTYPE.WAVWRITER_NRT));Ok(core.setSoftwareFormat(48000,SPEAKERMODE.STEREO,0));Ok(core.setDSPBufferSize(512,4));
            var settings=new FMOD.ADVANCEDSETTINGS{cbSize=Marshal.SizeOf(typeof(FMOD.ADVANCEDSETTINGS)),randomSeed=47291};Ok(core.setAdvancedSettings(ref settings));
            Ok(studio.initialize(64,FMOD.Studio.INITFLAGS.SYNCHRONOUS_UPDATE|FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS,FMOD.INITFLAGS.STREAM_FROM_UPDATE,filename));
            foreach(string bankName in Banks){Bank bank;Ok(studio.loadBankFile(Path.Combine(quest,"JKQ_Data","StreamingAssets",bankName),LOAD_BANK_FLAGS.NORMAL,out bank));}
            EventDescription description;if(string.IsNullOrEmpty(guid))Ok(studio.getEvent(path,out description));else Ok(studio.getEventByID(FMOD.GUID.Parse(guid),out description));
            Ok(description.getPath(out path));bool oneshot;Ok(description.isOneshot(out oneshot));if(!oneshot)throw new InvalidDataException("Refusing a looping event: "+path);
            Ok(description.loadSampleData());Ok(studio.flushSampleLoading());
            EventInstance instance;Ok(description.createInstance(out instance));Ok(instance.start());
            for(int block=0;block<750;block++) {
                Ok(studio.update());PLAYBACK_STATE state;Ok(instance.getPlaybackState(out state));
                if(block>2&&state==PLAYBACK_STATE.STOPPED){for(int tail=0;tail<12;tail++)Ok(studio.update());Ok(instance.release());return path;}
            }
            instance.stop(STOP_MODE.IMMEDIATE);instance.release();throw new InvalidDataException("Event exceeded eight seconds: "+path);
        }finally{studio.release();Marshal.FreeHGlobal(filename);}
    }
    private static int ConvertWave(string input,string output)
    {
        int format=0,channels=0,rate=0,bits=0;byte[] data=null;
        using(var reader=new BinaryReader(File.OpenRead(input))){
            if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Missing RIFF");reader.ReadInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Missing WAVE");
            while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string id=new string(reader.ReadChars(4));int length=reader.ReadInt32();long end=reader.BaseStream.Position+length;
                if(length<0||end>reader.BaseStream.Length)throw new InvalidDataException("Truncated wave");
                if(id=="fmt "){format=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();if(format==65534){reader.ReadInt16();reader.ReadInt16();reader.ReadInt32();format=reader.ReadInt16();}}
                else if(id=="data")data=reader.ReadBytes(length);
                reader.BaseStream.Position=end+(length&1);
            }
        }
        if(data==null||channels<1||channels>2||rate!=48000||!(format==3&&bits==32||format==1&&bits==16))throw new InvalidDataException("Unsupported FMOD capture format: "+format+"/"+bits);
        int stride=channels*bits/8,frames=data.Length/stride;var pcm=new short[frames];int first=frames,last=-1;
        for(int i=0;i<frames;i++){
            float sample=0;for(int c=0;c<channels;c++)sample+=format==3?BitConverter.ToSingle(data,i*stride+c*4):BitConverter.ToInt16(data,i*stride+c*2)/32768f;
            sample/=channels;pcm[i]=(short)Math.Max(short.MinValue,Math.Min(short.MaxValue,Math.Round(sample*32767)));
            if(Math.Abs((int)pcm[i])>8){first=Math.Min(first,i);last=i;}
        }
        if(last<first)throw new InvalidDataException("Silent event capture");first=Math.Max(0,first-144);last=Math.Min(frames-1,last+480);int count=last-first+1;
        using(var writer=new BinaryWriter(File.Create(output))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);for(int i=first;i<=last;i++)writer.Write(pcm[i]);}return count;
    }
}

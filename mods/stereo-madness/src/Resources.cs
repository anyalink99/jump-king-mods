using System;
using System.Collections.Generic;
using System.IO;
using JumpKing;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace StereoMadness
{
    internal sealed class Resources : IDisposable
    {
        internal Course Course;
        internal Texture2D Background, Pixel;
        internal readonly Dictionary<string, Texture2D> Sprites = new Dictionary<string, Texture2D>();
        internal JKSound Music, Death, Finish;
        internal float[] Pulse;
        private byte[] musicPcm;
        private int musicRate, musicChannels;
        private JKSound seekVoice;
        private JKSound originalMusic;
        private readonly List<IDisposable> owned = new List<IDisposable>();
        private T Own<T>(T value) where T : IDisposable { owned.Add(value); return value; }
        internal static Resources Load(string directory)
        {
            var r = new Resources();
            try {
                r.Course = Course.Load(Path.Combine(directory, "course.xml"));
                r.Pulse = Array.ConvertAll(File.ReadAllLines(Path.Combine(directory, "pulse.txt")), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
                r.Background = r.Own(LoadTexture(Path.Combine(directory, "background.png")));
                r.Pixel = r.Own(new Texture2D(Game1.instance.GraphicsDevice, 1, 1));
                r.Pixel.SetData(new[] { Color.White });
                foreach (string file in Directory.GetFiles(Path.Combine(directory, "sprites"), "*.png"))
                    r.Sprites.Add(Path.GetFileName(file), r.Own(LoadTexture(file)));
                r.Music = r.Own(LoadSound(Path.Combine(directory, "music.wav"), SoundType.Music));
                r.originalMusic=r.Music;
                using (var reader = new BinaryReader(File.OpenRead(Path.Combine(directory,"music.wav")))) {
                    if (new string(reader.ReadChars(4))!="RIFF") throw new InvalidDataException("Expected PCM music WAV");
                    reader.ReadInt32(); reader.ReadChars(4);
                    while (reader.BaseStream.Position+8 <= reader.BaseStream.Length) {
                        string id=new string(reader.ReadChars(4)); int size=reader.ReadInt32(); long next=reader.BaseStream.Position+size+(size&1);
                        if (id=="fmt ") {
                            int format=reader.ReadInt16(); r.musicChannels=reader.ReadInt16(); r.musicRate=reader.ReadInt32();
                            reader.ReadInt32(); reader.ReadInt16(); int bits=reader.ReadInt16();
                            if (format!=1 || bits!=16) throw new InvalidDataException("Expected 16-bit PCM music");
                        } else if (id=="data") r.musicPcm=reader.ReadBytes(size);
                        reader.BaseStream.Position=next;
                    }
                    if (r.musicPcm==null || r.musicRate<=0 || r.musicChannels<1 || r.musicChannels>2) throw new InvalidDataException("Invalid PCM music");
                }
                r.Death = r.Own(LoadSound(Path.Combine(directory, "death.wav"), SoundType.SFX));
                r.Finish = r.Own(LoadSound(Path.Combine(directory, "finish.wav"), SoundType.SFX));
                return r;
            } catch { r.Dispose(); throw; }
        }
        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture;
            using (var stream = File.OpenRead(path)) texture = Texture2D.FromStream(Game1.instance.GraphicsDevice, stream);
            try {
                var pixels = new Color[texture.Width * texture.Height]; texture.GetData(pixels);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.FromNonPremultiplied(pixels[i].ToVector4());
                texture.SetData(pixels); return texture;
            } catch { texture.Dispose(); throw; }
        }
        private static JKSound LoadSound(string path, SoundType type)
        {
            SoundEffect sound; using (var stream = File.OpenRead(path)) sound = SoundEffect.FromStream(stream);
            try { return new JKSound(sound, type); } catch { sound.Dispose(); throw; }
        }
        public void Dispose()
        { if (seekVoice!=null) seekVoice.Dispose(); for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose(); owned.Clear(); }
        internal void SeekMusic(double seconds)
        {
            Music.Stop();
            if (seconds<=0) {
                if (seekVoice!=null) { seekVoice.Dispose(); seekVoice=null; }
                Music=originalMusic; Music.Play(); return;
            }
            int frameSize=musicChannels*2;
            int offset=(int)Math.Min(musicPcm.Length-frameSize, Math.Max(0, seconds)*musicRate*frameSize);
            offset-=offset%frameSize;
            var remaining=new byte[musicPcm.Length-offset];
            Buffer.BlockCopy(musicPcm,offset,remaining,0,remaining.Length);
            var sound=new SoundEffect(remaining,musicRate,(AudioChannels)musicChannels);
            var next=new JKSound(sound,SoundType.Music);
            if (seekVoice!=null) seekVoice.Dispose();
            Music=seekVoice=next; Music.Play();
        }
    }
}

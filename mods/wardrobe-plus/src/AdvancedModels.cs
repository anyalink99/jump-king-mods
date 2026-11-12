using System;
using System.Collections.Generic;

namespace WardrobePlus.Advanced
{
    // public authoring contract. JSON uses these field names, no package code is executed
    public sealed class SkinManifest
    {
        public int schema = 1;
        public string id = "", name = "", version = "1.0.0", minimumWardrobe = "2.0.0";
        public List<Dependency> dependencies = new List<Dependency>();
        public List<EffectDefinition> effects = new List<EffectDefinition>();
        public List<EffectProfile> profiles = new List<EffectProfile>();
        public List<AnimationSet> animations = new List<AnimationSet>();
        public List<MaterialDefinition> materials = new List<MaterialDefinition>();
        public string defaultProfile = "";
    }
    public sealed class Dependency { public string id = "", minimumVersion = "1.0.0"; }
    public sealed class EffectProfile
    {
        public string id = "", name = "";
        public List<EffectRule> rules = new List<EffectRule>();
    }
    public sealed class EffectRule
    {
        public string id = "", trigger = "jump", surface = "*", equipment = "*";
        public float minCharge, maxCharge = 1, minSpeed, maxSpeed = 10000;
        public int priority;
        public List<ChannelRule> channels = new List<ChannelRule>();
    }
    public sealed class ChannelRule
    {
        public string channel = "particles", mode = "add";
        public List<string> effects = new List<string>();
        public float scale = 1;
    }
    public sealed class EffectDefinition
    {
        public string id = "", kind = "particles", native = "", texture = "", anchor = "feet";
        public string space = "world", layer = "front", blend = "alpha";
        public int count = 8, maxAlive = 128, columns = 1, rows = 1;
        public float rate, duration = .5f, life = .6f, lifeSpread = .2f;
        public float speed = 24, speedSpread = 12, angle = -90, spread = 110;
        public float gravity = 70, drag, rotation, spin, size = 1, endSize = .25f;
        public float offsetX, offsetY, fps = 12;
        public string color = "#FFFFFFFF", endColor = "#FFFFFF00";
        public List<string> sounds = new List<string>();
        public float volume = .6f, pitch, pitchSpread, cooldown = .05f;
        public int voices = 4;
        public bool loop;
        public bool liquidScale;
        public string collision = "none";
        public string stopOn = "chargeEnd";
        public float shakeX = 1, shakeY = 2, frequency = 25;
        public string waveform = "noise";
    }
    public sealed class AnimationSet
    {
        public string id = "", item = "NULL", material = "";
        public List<AnimationClip> clips = new List<AnimationClip>();
        public List<Attachment> attachments = new List<Attachment>();
        public List<EquipmentAnchor> equipmentAnchors = new List<EquipmentAnchor>();
    }
    public sealed class EquipmentAnchor {public string item="Cap",anchor="head";public float nativeX,nativeY=-28;}
    public sealed class AnimationClip
    {
        public string state = "idle", texture = "";
        public bool loop = true;
        public float transition;
        public List<int> nativeFrames = new List<int>();
        public List<AnimationFrame> frames = new List<AnimationFrame>();
    }
    public sealed class AnimationFrame
    {
        public int x, y, width = 32, height = 32;
        public float duration = .1f, originX = 16, originY = 32;
        public List<Anchor> anchors = new List<Anchor>();
        public List<string> effects = new List<string>();
    }
    public sealed class Anchor { public string id = "feet"; public float x, y, rotation; }
    public sealed class Attachment
    {
        public string id = "", parent = "", anchor = "back", texture = "", layer = "back";
        public int x, y, width = 16, height = 16;
        public float originX = 8, originY, offsetX, offsetY, rotation, inertia, stiffness = 30, damping = 8;
    }
    public sealed class MaterialDefinition
    {
        public string id = "", kind = "original", mask = "", texture = "", shader = "";
        public string color = "#FFFFFFFF";
        public float strength = 1, scrollX, scrollY, pulse, pulseSpeed = 1;
        public List<string> palette = new List<string>();
    }
    public sealed class PresentationSelection
    {
        // Empty = selected outfit's packages, "native" disables advanced output in that channel
        public string Animation = "", Particles = "", SurfaceSound = "", EquipmentSound = "", Shake = "";
        public float ParticleScale = 1, SoundVolume = 1, ShakeScale = 1;
        public PresentationSelection Copy() { return (PresentationSelection)MemberwiseClone(); }
        public string For(string channel)
        {
            switch (channel) { case "particles": return Particles; case "surfaceSound": return SurfaceSound;
                case "equipmentSound": return EquipmentSound; case "shake": return Shake; default: return Animation; }
        }
        public void Set(string channel, string value)
        {
            switch (channel) { case "particles": Particles = value; break; case "surfaceSound": SurfaceSound = value; break;
                case "equipmentSound": EquipmentSound = value; break; case "shake": Shake = value; break; default: Animation = value; break; }
        }
    }
    public sealed class PresentationEvent
    {
        public string Trigger = "jump", Surface = "normal";
        public string[] Equipment = new string[0];
        public float Charge, Speed, X, Y, VelocityX, VelocityY;
        public bool Flipped;
        public double Time;
        public long Sequence;
        public PresentationEvent Copy() { var result = (PresentationEvent)MemberwiseClone(); result.Equipment = (string[])Equipment.Clone(); return result; }
    }
}

using System;

namespace MegaMappingExpansion.Api
{
    /// <summary>Game-thread access to the active construction scene. Own returned leases in the caller's attempt scope.</summary>
    public interface IMappingScene
    {
        bool Available { get; }
        long Generation { get; }
        IMappingSignals Signals { get; }
        ISceneEffect Activate(string owner,string effectId);
        SceneValue ReadVariable(string id);
        void WriteVariable(string id,SceneValue value);
        void Emit(string id,string source,string actor,int screen,params SignalSample[] payload);
        ComponentInfo[] InspectComponents();
        ComponentPropertyInfo[] DescribeProperties(string component);
        EffectInfo[] InspectEffects();
        string[] InspectErrors();
    }
    public sealed class ComponentInfo
    {
        public string Id{get;private set;}
        public string Kind{get;private set;}
        public string[] Tags{get;private set;}
        public string[] Properties{get;private set;}
        public SceneValue[] Values{get;private set;}
        public string[] Owners{get;private set;}
        public ComponentInfo(string id,string kind,string[] tags,string[] properties,SceneValue[] values,string[] owners)
        {Id=id;Kind=kind;Tags=(string[])tags.Clone();Properties=(string[])properties.Clone();Values=(SceneValue[])values.Clone();Owners=(string[])owners.Clone();}
    }
    public sealed class ComponentPropertyInfo
    {
        public string Name{get;private set;}
        public SceneValueKind Kind{get;private set;}
        public SceneValue Default{get;private set;}
        public string Units{get;private set;}
        public double Minimum{get;private set;}
        public double Maximum{get;private set;}
        public bool Bindable{get;private set;}
        public string Impact{get;private set;}
        public string Reference{get;private set;}
        public string[] Choices{get;private set;}
        public ComponentPropertyInfo(string name,SceneValue value,string units,double min,double max,bool bindable,string impact,string reference,string[] choices)
        {Name=name;Kind=value.Kind;Default=value;Units=units;Minimum=min;Maximum=max;Bindable=bindable;Impact=impact;Reference=reference;Choices=(string[])choices.Clone();}
    }
}

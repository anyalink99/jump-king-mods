using System;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    // The same typed graph is used by the XML loader, cache and runtime compiler.
    public abstract class BehaviorNodeData { }
    public abstract class BehaviorBranchData : BehaviorNodeData
    {
        protected BehaviorBranchData() { Children = new BehaviorNodeData[0]; }
        [XmlElement("Sequencer", typeof(BehaviorSequence))]
        [XmlElement("Selector", typeof(BehaviorSelector))]
        [XmlElement("ParallelAll", typeof(BehaviorParallel))]
        [XmlElement("Repeat", typeof(BehaviorRepeat))]
        [XmlElement("Invert", typeof(BehaviorInvert))]
        [XmlElement("Subtree", typeof(BehaviorCall))]
        [XmlElement("PauseNode", typeof(BehaviorWait))]
        [XmlElement("WaitEvent", typeof(BehaviorWaitEvent))]
        [XmlElement("EmitEvent", typeof(BehaviorEmit))]
        [XmlElement("CheckFlag", typeof(BehaviorCheckFlag))]
        [XmlElement("WaitFlag", typeof(BehaviorWaitFlag))]
        [XmlElement("SetFlag", typeof(BehaviorSetFlag))]
        [XmlElement("IncrementFlag", typeof(BehaviorIncrement))]
        [XmlElement("ActivateEffect", typeof(BehaviorEffect))]
        [XmlElement("ClearEffects", typeof(BehaviorClear))]
        [XmlElement("PlayEventSFX", typeof(BehaviorSound))]
        public BehaviorNodeData[] Children { get; set; }
    }
    public sealed class BehaviorTreeData : BehaviorBranchData
    {
        public BehaviorTreeData() { Retrigger = "ignore"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("event")] public string Event { get; set; }
        [XmlAttribute("stopEvent")] public string StopEvent { get; set; }
        [XmlAttribute("screen")] public int Screen { get; set; }
        [XmlAttribute("once")] public bool Once { get; set; }
        [XmlAttribute("retrigger")] public string Retrigger { get; set; }
        [XmlAttribute("requiresFlag")] public string RequiresFlag { get; set; }
        [XmlAttribute("equals")] public string EqualsValue { get; set; }
    }
    public sealed class BehaviorSequence : BehaviorBranchData { }
    public sealed class BehaviorSelector : BehaviorBranchData { }
    public sealed class BehaviorParallel : BehaviorBranchData { }
    public sealed class BehaviorRepeat : BehaviorBranchData
    {
        // Zero explicitly means repeat until cancellation, at most once per tick.
        [XmlAttribute("count")] public int Count { get; set; }
    }
    public sealed class BehaviorInvert : BehaviorBranchData { }
    public sealed class BehaviorCall : BehaviorNodeData
    { [XmlAttribute("tree")] public string Tree { get; set; } }
    public sealed class BehaviorWait : BehaviorNodeData
    { [XmlAttribute("seconds")] public double Seconds { get; set; } }
    public sealed class BehaviorWaitEvent : BehaviorNodeData
    { [XmlAttribute("event")] public string Event { get; set; } }
    public sealed class BehaviorEmit : BehaviorNodeData
    { [XmlAttribute("event")] public string Event { get; set; } }
    public abstract class BehaviorFlag : BehaviorNodeData
    {
        [XmlAttribute("flag")] public string Flag { get; set; }
        [XmlAttribute("value")] public string Value { get; set; }
    }
    public sealed class BehaviorCheckFlag : BehaviorFlag { }
    public sealed class BehaviorWaitFlag : BehaviorFlag { }
    public sealed class BehaviorSetFlag : BehaviorFlag { }
    public sealed class BehaviorIncrement : BehaviorNodeData
    {
        [XmlAttribute("flag")] public string Flag { get; set; }
        [XmlAttribute("amount")] public int Amount { get; set; }
    }
    public sealed class BehaviorEffect : BehaviorNodeData
    { [XmlAttribute("effect")] public string Effect { get; set; } }
    public sealed class BehaviorClear : BehaviorNodeData { }
    public sealed class BehaviorSound : BehaviorNodeData
    { [XmlAttribute("sound")] public string Sound { get; set; } }
}

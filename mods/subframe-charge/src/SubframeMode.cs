using System;

namespace SubframeCharge
{
    public enum SubframeMode { Charge, AllInputs, InputsAndPhysics }
    internal static class SubframeModes
    {
        internal static bool Valid(SubframeMode value) { return value>=SubframeMode.Charge && value<=SubframeMode.InputsAndPhysics; }
        internal static string Label(SubframeMode value)
        {
            switch(value) {
                case SubframeMode.Charge: return "Charge";
                case SubframeMode.AllInputs: return "All Inputs";
                case SubframeMode.InputsAndPhysics: return "Inputs + Physics";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }
    }
}

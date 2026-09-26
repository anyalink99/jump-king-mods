using System;

namespace JKRuntime.Modules
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RuntimeModuleAttribute : Attribute
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string[] Requires { get; set; }
        public string[] Provides { get; set; }
        public string[] Before { get; set; }
        public string[] After { get; set; }
        /// <summary>Permit the map to suppress this package's world, attempt and activation callbacks.</summary>
        public bool MapSuspendable { get; set; }
        public RuntimeModuleAttribute(string id, string name) { Id = id; Name = name; }
    }
    [AttributeUsage(AttributeTargets.Method)] public sealed class BeforeLevelLoadAttribute : Attribute { }
    /// <summary>Prepare loaded-world resources once before its first attempt. Signature: public static void Method(RuntimeScope scope).</summary>
    [AttributeUsage(AttributeTargets.Method)] public sealed class OnWorldReadyAttribute : Attribute { }
    /// <summary>Prepare before every attempt, including same-world restarts. Own resources in the supplied RuntimeScope; do not create player behavior.</summary>
    [AttributeUsage(AttributeTargets.Method)] public sealed class BeforeAttemptAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class OnLevelStartAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class OnLevelEndAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class OnLevelUnloadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class MainMenuItemSettingAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class PauseMenuItemSettingAttribute : Attribute { }
}

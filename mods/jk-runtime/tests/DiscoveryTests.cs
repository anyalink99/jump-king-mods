using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.UI;
using JumpKing.Controller;
using JumpKing.Mods;

namespace JKRuntime
{
    internal static class DiscoveryTests
    {
        private sealed class MetadataAssembly : Assembly
        {
            internal readonly string Name;
            internal Type[] Types = new Type[0];
            internal int TypeReads, ResourceReads;
            internal bool Partial, Dynamic, ResourceFailure;
            internal MetadataAssembly(string name) { Name = name; }
            public override string FullName { get { return Name; } }
            public override bool IsDynamic { get { return Dynamic; } }
            public override AssemblyName GetName() { return new AssemblyName(Name); }
            public override Type[] GetTypes()
            {
                TypeReads++;
                if (Partial) throw new ReflectionTypeLoadException(Types, new Exception[] { new FileNotFoundException("Fixture dependency is not loaded yet") });
                return Types;
            }
            public override Stream GetManifestResourceStream(string name)
            {
                ResourceReads++;
                if (ResourceFailure) throw new IOException("Fixture resource failure");
                return null;
            }
        }
        public sealed class LegacySettings
        {
            public IDictionary KeyBindings { get; private set; }
            public LegacySettings(string key, int button) { KeyBindings = new Hashtable { { key, new[] { button } } }; }
        }
        private static class LateHolder
        {
            internal static object Value;
            internal static bool Broken;
            internal static int Reads;
            public static LegacySettings Settings
            { get { Reads++; if (Broken) throw new IOException("Not ready"); return (LegacySettings)Value; } }
        }
        private static class OtherHolder { public static LegacySettings Settings { get { return null; } } }
        private static class MetadataHolder { public static LegacySettings Settings { get { return new LegacySettings("Ready", 67); } } }
        private sealed class DeferredType : TypeDelegator
        {
            internal bool Broken = true;
            internal DeferredType() : base(typeof(MetadataHolder)) { }
            public override PropertyInfo[] GetProperties(BindingFlags flags)
            { return base.GetProperties(flags).Select(p => (PropertyInfo)new DeferredProperty(p, this)).ToArray(); }
        }
        private sealed class DeferredProperty : PropertyInfo
        {
            private readonly PropertyInfo source;
            private readonly DeferredType owner;
            internal DeferredProperty(PropertyInfo value, DeferredType type) { source = value; owner = type; }
            public override Type PropertyType { get { if (owner.Broken) throw new FileNotFoundException("Optional property dependency"); return source.PropertyType; } }
            public override string Name { get { return source.Name; } }
            public override Type DeclaringType { get { return source.DeclaringType; } }
            public override Type ReflectedType { get { return source.ReflectedType; } }
            public override PropertyAttributes Attributes { get { return source.Attributes; } }
            public override bool CanRead { get { return source.CanRead; } }
            public override bool CanWrite { get { return source.CanWrite; } }
            public override MethodInfo[] GetAccessors(bool nonPublic) { return source.GetAccessors(nonPublic); }
            public override MethodInfo GetGetMethod(bool nonPublic) { return source.GetGetMethod(nonPublic); }
            public override MethodInfo GetSetMethod(bool nonPublic) { return source.GetSetMethod(nonPublic); }
            public override ParameterInfo[] GetIndexParameters() { return source.GetIndexParameters(); }
            public override object GetValue(object obj, BindingFlags flags, Binder binder, object[] index, System.Globalization.CultureInfo culture) { return source.GetValue(obj, flags, binder, index, culture); }
            public override void SetValue(object obj, object value, BindingFlags flags, Binder binder, object[] index, System.Globalization.CultureInfo culture) { source.SetValue(obj, value, flags, binder, index, culture); }
            public override object[] GetCustomAttributes(bool inherit) { return source.GetCustomAttributes(inherit); }
            public override object[] GetCustomAttributes(Type type, bool inherit) { return source.GetCustomAttributes(type, inherit); }
            public override bool IsDefined(Type type, bool inherit) { return source.IsDefined(type, inherit); }
        }
        private static void Check(bool result, string name)
        { if (!result) throw new Exception(name); Console.WriteLine("PASS: " + name); }
        private static MetadataAssembly Add(string name, params Type[] types)
        {
            var assembly = new MetadataAssembly(name) { Types = types };
            ModLoader.Instance.LoadedMods.Add(new ModAssembly(assembly, new JumpKingModAttribute(name)));
            return assembly;
        }
        private static void Main()
        {
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager,
                new List<PadInstance> { (PadInstance)FormatterServices.GetUninitializedObject(typeof(PadInstance)) });
            ControllerManager.instance = manager;
            var foreign = Add("fixture.foreign", typeof(LateHolder));
            int initialTypeReads = foreign.TypeReads;
            AutomaticBindings.Refresh(); AutomaticBindings.Refresh();
            Check(foreign.TypeReads == initialTypeReads + 1 && LateHolder.Reads == 2,
                "immutable metadata is scanned once while null containers remain retryable");
            LateHolder.Broken = true; AutomaticBindings.Refresh(); LateHolder.Broken = false;
            LateHolder.Value = new LegacySettings("Jump", 65); AutomaticBindings.Refresh();
            var binding = UIApi.GetBindings().Single(b => b.Id == "auto.fixture.foreign.Jump");
            var page = new UiBindingsPage("Binds", null, binding.Id);
            page.OnOpen();
            Check(ReferenceEquals(page.Selected(), binding), "focused pages reuse the existing automatic binding adapter and profile ID");
            Check(binding.GetBindings().SequenceEqual(new[] { 65 }), "a settings getter becoming ready is still discovered after a failure");
            LateHolder.Value = new LegacySettings("Jump", 66);
            Check(binding.GetBindings().SequenceEqual(new[] { 66 }), "registered adapters read replacement settings objects");
            int reads = LateHolder.Reads;
            for (int i = 0; i < 20; i++) AutomaticBindings.Refresh();
            Check(foreign.TypeReads == initialTypeReads + 1 && LateHolder.Reads == reads,
                "warm menu rebuilds neither enumerate settled types nor re-register settled containers");

            var late = Add("fixture.late", typeof(OtherHolder)); int lateReads = late.TypeReads;
            AutomaticBindings.Refresh();
            Check(late.TypeReads == lateReads + 1, "a mod added after the first menu is inspected");
            var partial = Add("fixture.partial", typeof(OtherHolder)); partial.Partial = true;
            int partialReads = partial.TypeReads;
            AutomaticBindings.Refresh(); partial.Partial = false; AutomaticBindings.Refresh(); AutomaticBindings.Refresh();
            Check(partial.TypeReads == partialReads + 2, "partially loadable metadata retries and only caches a complete scan");
            var dynamic = Add("fixture.dynamic"); dynamic.Dynamic = true;
            int dynamicReads = dynamic.TypeReads;
            AutomaticBindings.Refresh(); dynamic.Types = new[] { typeof(OtherHolder) }; AutomaticBindings.Refresh();
            Check(dynamic.TypeReads == dynamicReads + 2, "dynamic assemblies can expose new types after discovery");

            var deferred = new DeferredType();
            var metadata = Add("fixture.metadata", deferred);
            int metadataReads = metadata.TypeReads;
            AutomaticBindings.Refresh();
            Check(UIApi.GetBindings().Any(b => b.Id == "auto.fixture.foreign.Jump"), "Missing property metadata does not abort healthy bindings");
            deferred.Broken = false;
            AutomaticBindings.Refresh(); AutomaticBindings.Refresh();
            Check(metadata.TypeReads == metadataReads + 2 && UIApi.GetBindings().Any(b => b.Id == "auto.fixture.metadata.Ready"),
                "A member dependency appearing later retries the incomplete scan and then caches it");

            PackageHost.Discover();
            int resources = foreign.ResourceReads;
            for (int i = 0; i < 100; i++) PackageHost.Discover();
            Check(resources == 1 && foreign.ResourceReads == resources, "foreign assemblies without SDK manifests are probed once");
            int dynamicResources = dynamic.ResourceReads;
            PackageHost.Discover();
            Check(dynamic.ResourceReads == dynamicResources + 1, "dynamic missing resources are not frozen");
            var later = Add("fixture.later-resource"); PackageHost.Discover();
            Check(later.ResourceReads == 1, "newly loaded assemblies still participate in package discovery");
            var retry = Add("fixture.retry-resource"); retry.ResourceFailure = true;
            try { PackageHost.Discover(); throw new Exception("Resource failure was swallowed"); } catch (IOException) { }
            retry.ResourceFailure = false; PackageHost.Discover(); PackageHost.Discover();
            Check(retry.ResourceReads == 2, "failed resource access is retried before negative caching");
            Console.WriteLine("[OK] Runtime menu discovery lifecycle and retry checks");
        }
    }
}

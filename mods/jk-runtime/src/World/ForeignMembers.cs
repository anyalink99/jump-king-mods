using System;
using System.Reflection;

namespace JKRuntime.World
{
    internal static class ForeignMembers
    {
        internal static FieldInfo Field(Type type,string name)
        {for(var t=type;t!=null;t=t.BaseType){var field=t.GetField(name,OwnedPatches.Members|BindingFlags.DeclaredOnly);if(field!=null)return field;}return null;}
        internal static MethodInfo Method(Type type,string name) {return type.GetMethod(name,OwnedPatches.Members);}
        internal static MethodInfo PropertyGetter(Type type,string name) {var p=type.GetProperty(name,OwnedPatches.Members);return p==null?null:p.GetGetMethod(true);}
        internal static MethodInfo PropertySetter(Type type,string name) {var p=type.GetProperty(name,OwnedPatches.Members);return p==null?null:p.GetSetMethod(true);}
    }
}

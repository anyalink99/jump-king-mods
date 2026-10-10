using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace JKRuntime.World
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WorldFieldAttribute : Attribute { }

    public sealed class WorldStateCodec
    {
        private readonly Type type;
        private readonly bool declared;
        private readonly Dictionary<Type,FieldInfo[]> fields=new Dictionary<Type,FieldInfo[]>();
        private sealed class SetMethods {internal MethodInfo Add,Clear;}
        private readonly Dictionary<Type,SetMethods> sets=new Dictionary<Type,SetMethods>();
        public readonly ulong Schema;
        public WorldStateCodec(Type root) : this(root,false) { }
        public WorldStateCodec(Type root,bool declaredOnly)
        {
            if(root==null)throw new ArgumentNullException("root");
            declared=declaredOnly;type=root;var description=new StringBuilder();Describe(root,description,new HashSet<Type>(),0);
            ulong hash=14695981039346656037UL;foreach(byte b in Encoding.UTF8.GetBytes(description.ToString())) {hash^=b;hash*=1099511628211UL;}Schema=hash;
        }
        private static bool Scalar(Type t) {return t==typeof(bool)||t==typeof(int)||t==typeof(long)||t==typeof(ulong)||t==typeof(float)||t==typeof(double)||t==typeof(string)||t.IsEnum;}
        private bool ArrayType(Type t) {return declared && t.IsArray && t.GetArrayRank()==1;}
        private static bool Set(Type t) {return t.IsGenericType && t.GetGenericTypeDefinition()==typeof(HashSet<>);}
        private static bool Dictionary(Type t) {return t.IsGenericType && t.GetGenericTypeDefinition()==typeof(Dictionary<,>);}
        private void Describe(Type t,StringBuilder text,HashSet<Type> path,int depth)
        {
            if(depth>8 || !path.Add(t)) throw new InvalidDataException("Recursive world data");
            text.Append(t.FullName).Append(';');if(declared)text.Append("declared;");
            if(!Scalar(t)) {
                if(ArrayType(t))Describe(t.GetElementType(),text,path,depth+1);
                else if(Set(t)||Dictionary(t)) {
                    if(Set(t) && !sets.ContainsKey(t)) sets.Add(t,new SetMethods{Add=t.GetMethod("Add"),Clear=t.GetMethod("Clear")});
                    foreach(var a in t.GetGenericArguments()) Describe(a,text,path,depth+1);
                }
                else {
                    if(t.IsArray || t.IsPointer || t.IsInterface || t.IsAbstract || typeof(Delegate).IsAssignableFrom(t) || t.Assembly==typeof(object).Assembly)
                        throw new InvalidDataException("Unsupported world field: "+t.FullName);
                    var list=t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray();
                    if(declared) {
                        var inherited=new List<FieldInfo>();
                        for(var current=t;current!=null&&current!=typeof(object);current=current.BaseType)
                            inherited.AddRange(current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(f=>Attribute.IsDefined(f,typeof(WorldFieldAttribute))));
                        list=inherited.OrderBy(f=>f.DeclaringType.FullName,StringComparer.Ordinal).ThenBy(f=>f.Name,StringComparer.Ordinal).ToArray();
                        if(list.Any(f=>f.IsInitOnly))throw new InvalidDataException("Readonly declared world field");
                    }
                    if(list.Length==0 || list.Length>128) throw new InvalidDataException("Invalid world object: "+t.FullName);
                    fields[t]=list;foreach(var f in list) {if(declared)text.Append(f.DeclaringType.FullName).Append(':');text.Append(f.Name);Describe(f.FieldType,text,path,depth+1);}
                }
            }
            path.Remove(t);
        }
        public byte[] Capture(object value)
        {
            using(var s=new MemoryStream()) using(var w=new BinaryWriter(s)) {int budget=4096;Write(w,type,value,ref budget);if(s.Length>48000) throw new InvalidDataException("World data too large");return s.ToArray();}
        }
        public object Decode(byte[] bytes)
        {
            if(bytes==null || bytes.Length>48000) throw new InvalidDataException("World data too large");
            using(var s=new MemoryStream(bytes,false)) using(var r=new BinaryReader(s)) {int budget=4096;object result=Read(r,type,ref budget);if(s.Position!=s.Length)throw new InvalidDataException("Trailing field data");return result;}
        }
        private void Write(BinaryWriter w,Type t,object v,ref int budget)
        {
            if(--budget<0) throw new InvalidDataException("World data budget");
            if(!t.IsValueType) {w.Write(v!=null);if(v==null)return;}
            if(t==typeof(bool))w.Write((bool)v);else if(t==typeof(int))w.Write((int)v);else if(t==typeof(long))w.Write((long)v);else if(t==typeof(ulong))w.Write((ulong)v);
            else if(t.IsEnum)w.Write(Convert.ToInt64(v));else if(t==typeof(float))w.Write((float)v);else if(t==typeof(double))w.Write((double)v);
            else if(t==typeof(string)) {if(((string)v).Length>256)throw new InvalidDataException("World string too long");w.Write((string)v);}
            else if(ArrayType(t)) {
                var array=(Array)v;if(array.Length>512)throw new InvalidDataException("World array too large");w.Write((ushort)array.Length);
                foreach(var value in array)Write(w,t.GetElementType(),value,ref budget);
            }
            else if(Dictionary(t)) {
                var d=(IDictionary)v;if(d.Count>512)throw new InvalidDataException("World collection too large");w.Write((ushort)d.Count);var a=t.GetGenericArguments();
                foreach(DictionaryEntry pair in d){Write(w,a[0],pair.Key,ref budget);Write(w,a[1],pair.Value,ref budget);}
            } else if(Set(t)) {
                int count=(int)t.GetProperty("Count").GetValue(v,null);if(count>512)throw new InvalidDataException("World collection too large");w.Write((ushort)count);
                foreach(object item in (IEnumerable)v)Write(w,t.GetGenericArguments()[0],item,ref budget);
            } else foreach(var f in fields[t])Write(w,f.FieldType,f.GetValue(v),ref budget);
        }
        private object Read(BinaryReader r,Type t,ref int budget)
        {
            if(--budget<0)throw new InvalidDataException("World data budget");
            if(!t.IsValueType && !r.ReadBoolean()){
                if(!declared && t!=type && t!=typeof(string))throw new InvalidDataException("Missing nested world object");return null;
            }
            if(t==typeof(bool))return r.ReadBoolean();if(t==typeof(int))return r.ReadInt32();if(t==typeof(long))return r.ReadInt64();if(t==typeof(ulong))return r.ReadUInt64();
            if(t.IsEnum)return Enum.ToObject(t,r.ReadInt64());
            if(t==typeof(float)){float f=r.ReadSingle();if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException("Invalid world number");return f;}
            if(t==typeof(double)){double f=r.ReadDouble();if(double.IsNaN(f)||double.IsInfinity(f))throw new InvalidDataException("Invalid world number");return f;}
            if(t==typeof(string)){string v=r.ReadString();if(v.Length>256)throw new InvalidDataException("World string too long");return v;}
            if(ArrayType(t)) {
                int length=r.ReadUInt16();if(length>512)throw new InvalidDataException("World array too large");
                var result=Array.CreateInstance(t.GetElementType(),length);for(int i=0;i<length;i++)result.SetValue(Read(r,t.GetElementType(),ref budget),i);return result;
            }
            if(Dictionary(t)||Set(t)) {
                int count=r.ReadUInt16();if(count>512)throw new InvalidDataException("World collection too large");var a=t.GetGenericArguments();object result=Activator.CreateInstance(t);
                for(int i=0;i<count;i++) {
                    object key=Read(r,a[0],ref budget);
                    if(Dictionary(t)){var d=(IDictionary)result;if(key==null||d.Contains(key))throw new InvalidDataException("Duplicate world key");d.Add(key,Read(r,a[1],ref budget));}
                    else if(!(bool)sets[t].Add.Invoke(result,new[]{key}))throw new InvalidDataException("Duplicate world value");
                }
                return result;
            }
            object instance=t.IsValueType ? Activator.CreateInstance(t) : FormatterServices.GetUninitializedObject(t);foreach(var f in fields[t])f.SetValue(instance,Read(r,f.FieldType,ref budget));return instance;
        }
        public void Apply(object target,object state)
        {
            if(target==null||state==null||target.GetType()!=type||state.GetType()!=type)throw new ArgumentException("World state and target must match the codec type");
            if(type.IsValueType||Scalar(type))throw new InvalidOperationException("Attach a state object, not a scalar or boxed value");
            Merge(type,target,state);
        }
        private object Merge(Type t,object target,object value)
        {
            if(value==null || target==null || Scalar(t) || t.IsValueType)return value;
            if(ArrayType(t)) {
                var dest=(Array)target;var source=(Array)value;
                if(dest.Length!=source.Length)return source;
                for(int i=0;i<dest.Length;i++)dest.SetValue(Merge(t.GetElementType(),dest.GetValue(i),source.GetValue(i)),i);
            }
            else if(Dictionary(t)) {
                var dest=(IDictionary)target;var source=(IDictionary)value;var remove=new List<object>();foreach(object key in dest.Keys)if(!source.Contains(key))remove.Add(key);foreach(var key in remove)dest.Remove(key);
                foreach(DictionaryEntry pair in source) dest[pair.Key]=Merge(t.GetGenericArguments()[1],dest.Contains(pair.Key)?dest[pair.Key]:null,pair.Value);
            } else if(Set(t)) {var methods=sets[t];methods.Clear.Invoke(target,null);foreach(var item in (IEnumerable)value)methods.Add.Invoke(target,new[]{item});}
            else foreach(var f in fields[t]) {object next=Merge(f.FieldType,f.GetValue(target),f.GetValue(value));if(!ReferenceEquals(next,f.GetValue(target)))f.SetValue(target,next);}
            return target;
        }
    }
}

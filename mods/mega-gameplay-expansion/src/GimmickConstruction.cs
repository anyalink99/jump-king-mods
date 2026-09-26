using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // A bounded interpreter for object construction, not an execution sandbox for
    // arbitrary mod startup. Foreign IL never runs directly. Stores remain in this
    // invocation; native calls are restricted to explicit value/collection contracts.
    internal sealed class GimmickConstruction
    {
        private sealed class Record
        {
            internal Type Type;
            internal readonly Dictionary<FieldInfo, object> Fields = new Dictionary<FieldInfo, object>();
        }
        private sealed class Address
        {
            internal Func<object> Read;
            internal Action<object> Write;
        }
        private readonly Dictionary<FieldInfo, object> statics = new Dictionary<FieldInfo, object>();
        private readonly Dictionary<object, Dictionary<FieldInfo, object>> writes = new Dictionary<object, Dictionary<FieldInfo, object>>();
        private readonly HashSet<Type> initialized = new HashSet<Type>();
        private readonly HashSet<object> owned = new HashSet<object>();
        private readonly Dictionary<object, object> collections = new Dictionary<object, object>();
        private readonly Dictionary<Record, object> materialized = new Dictionary<Record, object>();
        private readonly Stack<Type> initializing = new Stack<Type>();
        private readonly HashSet<Assembly> assemblies = new HashSet<Assembly>();
        private int steps;
        private bool externalWrites;
        internal GimmickConstruction(Assembly provider) { assemblies.Add(provider); assemblies.Add(typeof(JumpKing.Level.BoxBlock).Assembly); }
        private static object Value(object value) { var address = value as Address; return address == null ? value : address.Read(); }
        private static Type TypeOf(object value) { value = Value(value); var record = value as Record; return record == null ? (value == null ? null : value.GetType()) : record.Type; }
        private object Default(Type type)
        {
            if (!type.IsValueType) return null;
            if (type.IsPrimitive || type.IsEnum || PureValue(type)) return Activator.CreateInstance(type);
            return new Record { Type = type };
        }
        private static bool PureValue(Type type)
        { return type == typeof(Color) || type == typeof(Rectangle) || type == typeof(Point) || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4); }
        private static bool Collection(Type type)
        {
            if (type == typeof(ArrayList)) return true;
            if (!type.IsGenericType) return false;
            Type definition = type.GetGenericTypeDefinition();
            return definition == typeof(HashSet<>) || definition == typeof(List<>) || definition == typeof(Dictionary<,>);
        }
        private object Import(object value)
        {
            if (value == null || !Collection(value.GetType()) || owned.Contains(value)) return value;
            object copy; if (collections.TryGetValue(value, out copy)) return copy;
            Type type = value.GetType();
            if (type.GetGenericArguments().Any(t => !(t.IsPrimitive || t.IsEnum || PureValue(t) || t == typeof(string))))
                throw new InvalidOperationException("Collection holds external objects: " + type);
            copy = Activator.CreateInstance(type); owned.Add(copy); collections[value] = copy;
            if (copy is IDictionary) foreach (DictionaryEntry pair in (IDictionary)value) ((IDictionary)copy).Add(pair.Key, pair.Value);
            else foreach (object item in (IEnumerable)value)
            {
                if (item != null && !(item.GetType().IsPrimitive || item.GetType().IsEnum || PureValue(item.GetType()) || item is string))
                    throw new InvalidOperationException("Collection holds external objects: " + type);
                type.GetMethod("Add").Invoke(copy, new[] { item });
            }
            return copy;
        }
        private void Initialize(Type type)
        {
            if (!initialized.Add(type) || type.TypeInitializer == null) return;
            initializing.Push(type);
            try { Execute(type.TypeInitializer, null, new object[0], 0); }
            finally { initializing.Pop(); }
        }
        private object Read(FieldInfo field, object target)
        {
            target = Value(target); object result;
            if (field.IsLiteral) return field.GetRawConstantValue();
            if (field.IsStatic)
            {
                if (field.DeclaringType == typeof(string) && field.Name == "Empty") return "";
                if (PureValue(field.DeclaringType)) return field.GetValue(null);
                Initialize(field.DeclaringType);
                return statics.TryGetValue(field, out result) ? result : Default(field.FieldType);
            }
            if (target == null) throw new InvalidOperationException("Construction needs unavailable context: " + field);
            var record = target as Record;
            if (record != null) return record.Fields.TryGetValue(field, out result) ? result : Default(field.FieldType);
            Dictionary<FieldInfo, object> changed;
            if (writes.TryGetValue(target, out changed) && changed.TryGetValue(field, out result)) return result;
            return Import(field.GetValue(target));
        }
        private void Write(FieldInfo field, object target, object value)
        {
            target = Value(target); value = Coerce(Value(value), field.FieldType);
            if (field.IsStatic)
            {
                if (initializing.Count != 0 && initializing.Peek() != field.DeclaringType)
                    throw new InvalidOperationException("Static initializer writes another type: " + field);
                if (initializing.Count == 0) externalWrites = true;
                statics[field] = value; return;
            }
            var record = target as Record; if (record != null) { record.Fields[field] = value; return; }
            if (target == null) throw new InvalidOperationException("Null construction target");
            if (PureValue(target.GetType())) { field.SetValue(target, value); return; }
            externalWrites = true;
            Dictionary<FieldInfo, object> changed;
            if (!writes.TryGetValue(target, out changed)) writes.Add(target, changed = new Dictionary<FieldInfo, object>());
            changed[field] = value;
        }
        private static object Coerce(object value, Type type)
        {
            if (type.IsByRef) type = type.GetElementType();
            if (value == null || value is Record || value is Address || type.IsInstanceOfType(value)) return value;
            if (type.IsEnum) return Enum.ToObject(type, value);
            if (type == typeof(bool)) return Number(value) != 0;
            if (type == typeof(ulong) && value is long) return unchecked((ulong)(long)value);
            if (type == typeof(uint) && (value is int || value is long)) return unchecked((uint)Convert.ToInt64(value));
            if (type == typeof(long) && value is ulong) return unchecked((long)(ulong)value);
            if (type.IsPrimitive) return Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
            return value;
        }
        private static double Number(object value) { value = Value(value); return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture); }
        private static bool True(object value)
        { value = Value(value); return value != null && (!(value is IConvertible) || value is string || Number(value) != 0); }
        internal object Empty(Type type) { return new Record { Type = type }; }
        internal object Construct(ConstructorInfo constructor, params object[] args)
        {
            object value = New(constructor, args, 0);
            if (externalWrites) throw new InvalidOperationException("Constructor requires external state writes");
            return Materialize(value);
        }
        internal object Run(MethodInfo method, object target, params object[] args) { return Materialize(Execute(method, target, args, 0)); }
        private object New(ConstructorInfo constructor, object[] args, int depth)
        {
            Type type = constructor.DeclaringType;
            if (PureValue(type) || Collection(type))
            {
                if (Collection(type) && args.Length != 0 && !(args.Length == 1 && constructor.GetParameters()[0].ParameterType == typeof(int) && Number(args[0]) >= 0 && Number(args[0]) <= 16384))
                    throw new InvalidOperationException("Unsupported collection constructor");
                object result = constructor.Invoke(ConvertArguments(constructor, args)); owned.Add(result); return result;
            }
            if (!assemblies.Contains(type.Assembly)) throw new InvalidOperationException("Construction dependency is not supported: " + type.FullName);
            Initialize(type);
            var record = new Record { Type = type }; Execute(constructor, record, args, depth + 1); return record;
        }
        private object[] ConvertArguments(MethodBase method, object[] args)
        {
            var parameters = method.GetParameters(); var result = new object[args.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Coerce(Value(args[i]), parameters[i].ParameterType);
            return result;
        }
        private object Invoke(MethodBase method, object target, object[] args, int depth)
        {
            object actual = Value(target); Type type = method.DeclaringType;
            if (method is ConstructorInfo)
            {
                if (type == typeof(object)) return null;
                if (PureValue(type)) { ((Address)target).Write(((ConstructorInfo)method).Invoke(ConvertArguments(method, args))); return null; }
                return Execute(method, actual, args, depth + 1);
            }
            if (type == typeof(Type) && method.Name == "GetTypeFromHandle") return args[0];
            if (type == typeof(Type) && (method.Name == "op_Equality" || method.Name == "op_Inequality"))
                return Equals(Value(args[0]), Value(args[1])) == (method.Name == "op_Equality");
            if (type == typeof(object) && method.Name == "GetType") return TypeOf(actual);
            if (type == typeof(EntityComponent.Entity) && method.Name == "GetComponent" && method.IsGenericMethod && actual is EntityComponent.Entity)
            {
                Type component = ((MethodInfo)method).GetGenericArguments()[0];
                var values = (IEnumerable<EntityComponent.Component>)typeof(EntityComponent.Entity).GetField("m_components", Gimmicks.Members).GetValue(actual);
                return values.FirstOrDefault(c => c.GetType() == component);
            }
            if (type == typeof(Array) && method.Name == "get_Length" && actual is Array) return ((Array)actual).Length;
            if (type == typeof(Enum) && method.Name == "GetValues" && args[0] is Type && ((Type)args[0]).IsEnum)
            { var values = Enum.GetValues((Type)args[0]); owned.Add(values); return values; }
            if (type == typeof(JumpKing.Level.Sampler.LevelTexture) && method.Name == "GetColor")
                return ((JumpKing.Level.Sampler.LevelTexture)actual).GetColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]));
            bool pure = PureValue(type) || type == typeof(Math)
                || (type == typeof(Convert) && args.All(a => Value(a) == null || Value(a).GetType().IsPrimitive || Value(a) is string))
                || (type == typeof(string) && new[] { "op_Equality", "op_Inequality", "Concat", "get_Length", "Contains", "StartsWith", "EndsWith" }.Contains(method.Name) && args.All(a => a == null || a is string))
                || (Collection(type) && owned.Contains(actual) && new[] { "Contains", "ContainsKey", "Add", "Remove", "get_Item", "set_Item", "get_Count", "Clear" }.Contains(method.Name));
            if (pure)
            {
                object result = ((MethodInfo)method).Invoke(actual, ConvertArguments(method, args));
                var address = target as Address; if (address != null && actual != null && actual.GetType().IsValueType) address.Write(actual);
                return result;
            }
            if (!assemblies.Contains(type.Assembly)) throw new InvalidOperationException("Construction call is not supported: " + type.FullName + "." + method.Name);
            if (method.IsVirtual && actual != null && TypeOf(actual) != type)
            {
                if (type.IsInterface) { var map = TypeOf(actual).GetInterfaceMap(type); method = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, method)]; }
                else method = TypeOf(actual).GetMethod(method.Name, Gimmicks.Members, null, method.GetParameters().Select(p => p.ParameterType).ToArray(), null) ?? method;
            }
            return Execute(method, actual, args, depth + 1);
        }
        private object Materialize(object value)
        {
            value = Value(value); var record = value as Record; if (record == null) return value;
            object existing; if (materialized.TryGetValue(record, out existing)) return existing;
            // The initializer was already interpreted and checked: external calls
            // and writes into other types were rejected before actual allocation.
            Initialize(record.Type);
            object result = FormatterServices.GetUninitializedObject(record.Type);
            materialized.Add(record, result);
            foreach (var field in record.Fields) field.Key.SetValue(result, Coerce(Materialize(field.Value), field.Key.FieldType));
            return result;
        }
        private object Execute(MethodBase method, object target, object[] supplied, int depth)
        {
            if (depth > 32) throw new InvalidOperationException("Construction call-depth limit");
            var body = method.GetMethodBody(); if (body == null || body.ExceptionHandlingClauses.Count != 0) throw new InvalidOperationException("Unsupported construction body: " + method);
            var code = GimmickIl.Read(method); var offsets = code.Select((i, n) => new { i.Offset, n }).ToDictionary(i => i.Offset, i => i.n);
            var args = method.IsStatic ? supplied : new[] { target }.Concat(supplied).ToArray();
            var locals = body.LocalVariables.Select(l => Default(l.LocalType)).ToArray(); var stack = new Stack<object>();
            for (int pc = 0; pc < code.Length; pc++)
            {
                if (++steps > 30000) throw new InvalidOperationException("Construction instruction limit");
                var instruction = code[pc]; string op = instruction.Code.Name; object operand = instruction.Operand;
                if (op == "nop" || op == "readonly." || op == "constrained.") continue;
                if (op.StartsWith("ldarg") || op.StartsWith("ldloc") || op.StartsWith("stloc") || op.StartsWith("starg"))
                {
                    bool argument = op.Contains("arg"); object[] slots = argument ? args : locals;
                    int index = operand == null ? int.Parse(op.Substring(op.LastIndexOf('.') + 1)) : (int)operand;
                    if (op.StartsWith("st")) slots[index] = Value(stack.Pop());
                    else if (op.StartsWith("ldarga") || op.StartsWith("ldloca")) stack.Push(new Address { Read = () => slots[index], Write = v => slots[index] = v });
                    else stack.Push(slots[index]); continue;
                }
                if (op.StartsWith("ldc.i4")) { stack.Push(operand ?? (object)(op == "ldc.i4.m1" ? -1 : int.Parse(op.Substring(7)))); continue; }
                if (op == "ldc.i8" || op == "ldc.r4" || op == "ldc.r8" || op == "ldstr" || op == "ldtoken") { stack.Push(operand); continue; }
                if (op == "ldnull") { stack.Push(null); continue; }
                if (op == "dup") { stack.Push(stack.Peek()); continue; }
                if (op == "pop") { stack.Pop(); continue; }
                if (op == "ret") return method is MethodInfo && ((MethodInfo)method).ReturnType != typeof(void) ? Value(stack.Pop()) : null;
                if (op == "call" || op == "callvirt" || op == "newobj")
                {
                    var called = (MethodBase)operand; var parameters = new object[called.GetParameters().Length];
                    for (int i = parameters.Length - 1; i >= 0; i--) parameters[i] = stack.Pop();
                    object receiver = op == "newobj" || called.IsStatic ? null : stack.Pop();
                    object result = op == "newobj" ? New((ConstructorInfo)called, parameters, depth) : Invoke(called, receiver, parameters, depth);
                    if (op == "newobj" || (called is MethodInfo && ((MethodInfo)called).ReturnType != typeof(void))) stack.Push(result); continue;
                }
                if (op == "ldfld" || op == "ldsfld" || op == "ldflda" || op == "ldsflda" || op == "stfld" || op == "stsfld")
                {
                    var field = (FieldInfo)operand; bool store = op.StartsWith("st"); object value = store ? stack.Pop() : null;
                    object receiver = field.IsStatic ? null : stack.Pop();
                    if (store) Write(field, receiver, value);
                    else if (op.EndsWith("a")) stack.Push(new Address { Read = () => Read(field, receiver), Write = v => Write(field, receiver, v) });
                    else stack.Push(Read(field, receiver)); continue;
                }
                if (op == "initobj") { ((Address)stack.Pop()).Write(Default((Type)operand)); continue; }
                if (op == "ldobj" || op.StartsWith("ldind")) { stack.Push(Value(stack.Pop())); continue; }
                if (op == "stobj" || op.StartsWith("stind")) { object value = stack.Pop(); ((Address)stack.Pop()).Write(Value(value)); continue; }
                if (op == "box" || op == "unbox.any") { stack.Push(Coerce(Value(stack.Pop()), (Type)operand)); continue; }
                if (op == "castclass" || op == "isinst")
                { object value = Value(stack.Pop()); bool fits = value == null || ((Type)operand).IsAssignableFrom(TypeOf(value)); if (!fits && op == "castclass") throw new InvalidOperationException("Construction cast failed"); stack.Push(fits ? value : null); continue; }
                if (op == "newarr") { int size = Convert.ToInt32(stack.Pop()); if (size < 0 || size > 16384) throw new InvalidOperationException("Construction array limit"); var array = Array.CreateInstance((Type)operand, size); owned.Add(array); stack.Push(array); continue; }
                if (op == "ldlen") { stack.Push(((Array)Value(stack.Pop())).Length); continue; }
                if (op.StartsWith("ldelem") || op.StartsWith("stelem"))
                {
                    bool store = op.StartsWith("st"); object value = store ? stack.Pop() : null; int index = Convert.ToInt32(stack.Pop()); var array = (Array)Value(stack.Pop());
                    if (store) {
                        if (!owned.Contains(array)) throw new InvalidOperationException("External array write");
                        if (Value(value) is Record) throw new InvalidOperationException("Construction requires an object graph inside a typed array");
                        array.SetValue(Coerce(Value(value), array.GetType().GetElementType()), index);
                    }
                    else stack.Push(array.GetValue(index)); continue;
                }
                if (op == "br" || op == "br.s") { pc = offsets[(int)operand] - 1; continue; }
                if (op.StartsWith("brtrue") || op.StartsWith("brfalse")) { if (True(stack.Pop()) == op.StartsWith("brtrue")) pc = offsets[(int)operand] - 1; continue; }
                if (op == "switch") { int index = Convert.ToInt32(stack.Pop()); var branches = (int[])operand; if (index >= 0 && index < branches.Length) pc = offsets[branches[index]] - 1; continue; }
                if (op.StartsWith("conv."))
                {
                    object value = Value(stack.Pop()); double number = Number(value);
                    if (op == "conv.r4") stack.Push((float)number); else if (op == "conv.r8" || op == "conv.r.un") stack.Push(number);
                    else if (op == "conv.u8") stack.Push(unchecked((ulong)number)); else if (op == "conv.i8") stack.Push(unchecked((long)number));
                    else if (op == "conv.u1") stack.Push(unchecked((int)(byte)number)); else if (op == "conv.u4") stack.Push(unchecked((uint)number));
                    else if (op == "conv.i4") stack.Push(unchecked((int)number)); else throw new InvalidOperationException("Unsupported conversion: " + op); continue;
                }
                if (op == "neg") { stack.Push(-Number(stack.Pop())); continue; }
                if (op == "not") { stack.Push(~Convert.ToInt64(stack.Pop())); continue; }
                if (new[] { "add", "sub", "mul", "div", "rem", "and", "or", "xor", "shl", "shr", "shr.un" }.Contains(op))
                {
                    object right = Value(stack.Pop()), left = Value(stack.Pop()); double a = Number(left), b = Number(right);
                    object result;
                    if (op == "and" || op == "or" || op == "xor" || op.StartsWith("sh"))
                    { long x = Convert.ToInt64(left), y = Convert.ToInt64(right); result = op == "and" ? x & y : op == "or" ? x | y : op == "xor" ? x ^ y : op == "shl" ? x << (int)y : x >> (int)y; }
                    else { double n = op == "add" ? a + b : op == "sub" ? a - b : op == "mul" ? a * b : op == "div" ? a / b : a % b; result = left is float || right is float ? (object)(float)n : left is double || right is double ? n : (object)(long)n; }
                    stack.Push(result); continue;
                }
                if (op.StartsWith("beq") || op.StartsWith("bne") || op.StartsWith("bge") || op.StartsWith("bgt") || op.StartsWith("ble") || op.StartsWith("blt") || op == "ceq" || op.StartsWith("cgt") || op.StartsWith("clt"))
                {
                    object right = Value(stack.Pop()), left = Value(stack.Pop()); bool equal = Equals(left, right);
                    bool numeric = left is IConvertible && right is IConvertible && !(left is string) && !(right is string);
                    if (numeric) equal = Number(left) == Number(right);
                    bool yes = op.StartsWith("beq") || op == "ceq" ? equal : op.StartsWith("bne") ? !equal
                        : op.StartsWith("bge") ? Number(left) >= Number(right) : op.StartsWith("ble") ? Number(left) <= Number(right)
                        : op.StartsWith("bgt") || op.StartsWith("cgt") ? (numeric ? Number(left) > Number(right) : left != null && right == null) : Number(left) < Number(right);
                    if (op.StartsWith("b")) { if (yes) pc = offsets[(int)operand] - 1; } else stack.Push(yes ? 1 : 0); continue;
                }
                if (op == "throw") throw new InvalidOperationException("Provider rejected the requested construction");
                throw new InvalidOperationException("Construction instruction is not supported: " + op + " in " + method.DeclaringType.FullName + "." + method.Name);
            }
            throw new InvalidOperationException("Construction did not return");
        }
    }
}

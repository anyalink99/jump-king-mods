using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace MegaGameplayExpansion
{
    internal sealed class GimmickInstruction
    {
        internal int Offset, Next;
        internal OpCode Code;
        internal object Operand;
    }
    internal static class GimmickIl
    {
        private static readonly Dictionary<short, OpCode> codes = MakeCodes();
        private static readonly Dictionary<MethodBase, GimmickInstruction[]> cache = new Dictionary<MethodBase, GimmickInstruction[]>();
        private static Dictionary<short, OpCode> MakeCodes()
        {
            var result = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.FieldType == typeof(OpCode)) { var code = (OpCode)field.GetValue(null); result[code.Value] = code; }
            return result;
        }
        internal static GimmickInstruction[] Read(MethodBase method)
        {
            GimmickInstruction[] found; if (cache.TryGetValue(method, out found)) return found;
            var body = method.GetMethodBody(); if (body == null) throw new InvalidOperationException("No managed body: " + method);
            byte[] bytes = body.GetILAsByteArray(); var result = new List<GimmickInstruction>();
            Type[] types = method.DeclaringType == null ? null : method.DeclaringType.GetGenericArguments();
            Type[] args = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (int at = 0; at < bytes.Length; )
            {
                var item = new GimmickInstruction { Offset = at }; int code = bytes[at++];
                if (code == 254) code = 0xfe00 | bytes[at++]; item.Code = codes[(short)code];
                int token;
                switch (item.Code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: item.Operand = (int)(sbyte)bytes[at++]; break;
                    case OperandType.ShortInlineVar: item.Operand = (int)bytes[at++]; break;
                    case OperandType.InlineVar: item.Operand = (int)BitConverter.ToUInt16(bytes, at); at += 2; break;
                    case OperandType.InlineI: item.Operand = BitConverter.ToInt32(bytes, at); at += 4; break;
                    case OperandType.InlineI8: item.Operand = BitConverter.ToInt64(bytes, at); at += 8; break;
                    case OperandType.ShortInlineR: item.Operand = BitConverter.ToSingle(bytes, at); at += 4; break;
                    case OperandType.InlineR: item.Operand = BitConverter.ToDouble(bytes, at); at += 8; break;
                    case OperandType.ShortInlineBrTarget: item.Operand = at + 1 + (sbyte)bytes[at]; at++; break;
                    case OperandType.InlineBrTarget: item.Operand = at + 4 + BitConverter.ToInt32(bytes, at); at += 4; break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(bytes, at); at += 4; var targets = new int[count]; int end = at + count * 4;
                        for (int i = 0; i < count; i++) { targets[i] = end + BitConverter.ToInt32(bytes, at); at += 4; } item.Operand = targets; break;
                    default:
                        token = BitConverter.ToInt32(bytes, at); at += 4;
                        if (item.Code.OperandType == OperandType.InlineString) item.Operand = method.Module.ResolveString(token);
                        else if (item.Code.OperandType == OperandType.InlineField) item.Operand = method.Module.ResolveField(token, types, args);
                        else if (item.Code.OperandType == OperandType.InlineMethod) item.Operand = method.Module.ResolveMethod(token, types, args);
                        else if (item.Code.OperandType == OperandType.InlineType) item.Operand = method.Module.ResolveType(token, types, args);
                        else if (item.Code.OperandType == OperandType.InlineTok) item.Operand = method.Module.ResolveMember(token, types, args);
                        else throw new InvalidOperationException("Unsupported IL operand: " + item.Code);
                        break;
                }
                item.Next = at; result.Add(item);
            }
            found = result.ToArray(); cache[method] = found; return found;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class GraphSnapshot
    {
        internal string Identity;
        internal SceneValue[] Values, Memory, Velocity;
        internal bool[] Initialized;
        internal bool[] Computed;
        internal ObservationQuality[] Quality,MemoryQuality;
    }
    internal sealed partial class ValueGraph
    {
        private readonly GraphInstruction[] code;
        private readonly Dictionary<string, int> ids;
        private readonly int[] order;
        private readonly string identity;
        private readonly SignalHub signals;
        private SceneValue[] values, memory, velocity;
        private bool[] initialized;
        private bool[] computed;
        private ObservationQuality[] quality,memoryQuality;
        private readonly bool[] changed;
        private readonly long[] inputSequence;
        private bool force=true;
        private static readonly Func<double,double,double> DivideValues=Divide,MinimumValues=Math.Min,MaximumValues=Math.Max;
        private readonly Dictionary<string,string> faults=new Dictionary<string,string>(StringComparer.Ordinal);
        internal string[] Errors{get{return faults.Select(p=>p.Key+": "+p.Value).ToArray();}}
        internal ValueGraph(ValueNodeData[] source, SignalHub hub)
        {
            if (source == null || source.Length > 4096) throw new InvalidDataException("Value graph supports at most 4096 nodes");
            signals = hub; ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null || string.IsNullOrWhiteSpace(source[i].Id) || source[i].Id.Length > 128 || ids.ContainsKey(source[i].Id))
                    throw new InvalidDataException("Value graph requires unique node IDs");
                ids.Add(source[i].Id, i);
            }
            code = new GraphInstruction[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var data = source[i]; ValueOperation op;
                if (!Enum.TryParse<ValueOperation>(data.Operation, true, out op) || !Enum.IsDefined(typeof(ValueOperation), op)) throw new InvalidDataException(data.Id + ": unknown operation " + data.Operation);
                if (!Enum.IsDefined(typeof(SceneClock), data.Clock)) throw new InvalidDataException(data.Id + ": unknown clock");
                if(!Enum.IsDefined(typeof(ObservationQuality),data.MinimumQuality))throw new InvalidDataException(data.Id+": unknown observation quality");
                var inputs = string.IsNullOrWhiteSpace(data.Inputs) ? new int[0] : data.Inputs.Split(';').Select(RequireIndex).ToArray();
                code[i] = new GraphInstruction(data, op, inputs);
            }
            var visits = new byte[code.Length]; var sorted = new List<int>();
            for (int i = 0; i < code.Length; i++) Visit(i, visits, sorted, 0);
            order = sorted.ToArray();
            foreach (int i in order) Infer(code[i]);
            foreach (var instruction in code.Where(n => n.Op == ValueOperation.Delay))
                if (code[instruction.Inputs[0]].Port.Kind != instruction.Port.Kind) throw new InvalidDataException(instruction.Data.Id + ": delay input type differs from its initial value");
            values = Array.ConvertAll(code, n => Zero(n.Port.Kind)); memory = (SceneValue[])values.Clone(); velocity = (SceneValue[])values.Clone(); initialized = new bool[code.Length];
            quality=new ObservationQuality[code.Length];memoryQuality=new ObservationQuality[code.Length];
            computed=new bool[code.Length];
            changed=new bool[code.Length];inputSequence=new long[code.Length];
            // an owned fingerprint rejects same-sized graphs with different state layouts
            var text = string.Join("\n", code.Select(n => n.Data.Id + "|" + n.Op + "|" + n.Data.Inputs + "|" + n.Data.Source + "|" + n.Data.Kind + "|" + n.Data.Value + "|" + n.Data.Units + "|" + n.Data.Clock + "|" + n.Data.Curve+"|"+n.Data.MinimumQuality));
            using (var hash = System.Security.Cryptography.SHA256.Create()) identity = Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));
        }
        private int RequireIndex(string id) { int value; if (!ids.TryGetValue(id.Trim(), out value)) throw new InvalidDataException("Unknown graph input: " + id); return value; }
        private void Visit(int index, byte[] visits, List<int> sorted, int depth)
        {
            if (depth > 128 || visits[index] == 1) throw new InvalidDataException(code[index].Data.Id + ": cyclic or excessively deep graph; explicit Delay is required for feedback");
            if (visits[index] == 2) return;
            visits[index] = 1;
            if (code[index].Op != ValueOperation.Delay) foreach (int input in code[index].Inputs) Visit(input, visits, sorted, depth + 1);
            visits[index] = 2; sorted.Add(index);
        }
        private void Infer(GraphInstruction n)
        {
            int count = n.Inputs.Length; var p = n.Inputs.Select(i => code[i].Port).ToArray();
            Func<int, SceneValueKind, bool> has = (i, type) => i < p.Length && p[i] != null && p[i].Kind == type;
            Action<bool> need = condition => { if (!condition) throw new InvalidDataException(n.Data.Id + ": invalid inputs for " + n.Op); };
            var output = new GraphPort(SceneValueKind.Number, n.Data.Units);
            switch (n.Op)
            {
                case ValueOperation.Constant: need(count == 0); output.Kind = n.Initial.Kind; break;
                case ValueOperation.Pass: need(count==1);output.Kind=p[0].Kind;output.Units=p[0].Units;break;
                case ValueOperation.Input:
                    need(count == 0); var descriptor = signals.Describe(n.Data.Source); output.Kind = descriptor.Kind; output.Units = descriptor.Units; break;
                case ValueOperation.Delay: need(count == 1); output.Kind = n.Initial.Kind; break;
                case ValueOperation.Vector: need(count == 2 && has(0, SceneValueKind.Number) && has(1, SceneValueKind.Number)); output.Kind = SceneValueKind.Vector2; break;
                case ValueOperation.X: case ValueOperation.Y: case ValueOperation.Length: case ValueOperation.Angle:
                    need(count == 1 && has(0, SceneValueKind.Vector2)); output.Units = n.Op == ValueOperation.Angle ? "degrees" : p[0].Units; break;
                case ValueOperation.Normalize: need(count == 1 && has(0, SceneValueKind.Vector2)); output.Kind = SceneValueKind.Vector2; break;
                case ValueOperation.Dot: need(count == 2 && has(0, SceneValueKind.Vector2) && has(1, SceneValueKind.Vector2)); break;
                case ValueOperation.Add: case ValueOperation.Subtract: case ValueOperation.Minimum: case ValueOperation.Maximum:
                    need(count == 2 && p[0].Kind == p[1].Kind && Numeric(p[0].Kind));
                    need(p[0].Units == p[1].Units || p[0].Units == "" || p[1].Units == ""); output.Kind = p[0].Kind; output.Units = p[0].Units == "" ? p[1].Units : p[0].Units; break;
                case ValueOperation.Multiply: case ValueOperation.Divide:
                    need(count == 2 && Numeric(p[0].Kind) && (p[1].Kind == p[0].Kind || has(1, SceneValueKind.Number)));
                    output.Kind = p[0].Kind; output.Units = n.Data.Units == "" ? p[0].Units : n.Data.Units; break;
                case ValueOperation.Select:
                    need(count == 3 && has(0, SceneValueKind.Boolean) && p[1].Kind == p[2].Kind); output.Kind = p[1].Kind; output.Units = p[1].Units; break;
                case ValueOperation.Equal: need(count == 2 && p[0].Kind == p[1].Kind); output.Kind = SceneValueKind.Boolean; break;
                case ValueOperation.Less: case ValueOperation.Greater:
                    need(count == 2 && has(0, SceneValueKind.Number) && has(1, SceneValueKind.Number)); output.Kind = SceneValueKind.Boolean; break;
                case ValueOperation.And: case ValueOperation.Or:
                    need(count == 2 && has(0, SceneValueKind.Boolean) && has(1, SceneValueKind.Boolean)); output.Kind = SceneValueKind.Boolean; break;
                case ValueOperation.Not: need(count == 1 && has(0, SceneValueKind.Boolean)); output.Kind = SceneValueKind.Boolean; break;
                case ValueOperation.Available:need(count==1);output.Kind=SceneValueKind.Boolean;break;
                case ValueOperation.Quality:need(count==1);output.Kind=SceneValueKind.Integer;break;
                case ValueOperation.Number: need(count == 1 && (has(0, SceneValueKind.Integer)||has(0,SceneValueKind.Number))); output.Units=p[0].Units; break;
                case ValueOperation.Integer: need(count == 1 && has(0, SceneValueKind.Number)); output.Kind = SceneValueKind.Integer; break;
                case ValueOperation.Format: need(count == 1); output.Kind = SceneValueKind.Text; break;
                case ValueOperation.Clamp: need(count == 3 && Numeric(p[0].Kind) && has(1, SceneValueKind.Number) && has(2, SceneValueKind.Number)); output.Kind = p[0].Kind; output.Units = p[0].Units; break;
                case ValueOperation.Remap: need(count == 5 && p.All(q => q.Kind == SceneValueKind.Number)); break;
                case ValueOperation.Smooth: case ValueOperation.Spring:
                    need(count == (n.Op == ValueOperation.Spring ? 3 : 2) && Numeric(p[0].Kind) && has(1, SceneValueKind.Number) && (count == 2 || has(2, SceneValueKind.Number)));
                    output.Kind = p[0].Kind; output.Units = p[0].Units; break;
                case ValueOperation.Integrate: need(count == 1 && Numeric(p[0].Kind)); need(n.Initial.Kind == p[0].Kind); output.Kind = p[0].Kind; break;
                case ValueOperation.Hysteresis: need(count == 3 && p.All(q => q.Kind == SceneValueKind.Number)); output.Kind = SceneValueKind.Boolean; break;
                case ValueOperation.Curve: need(count == 1 && has(0, SceneValueKind.Number) && n.Curve.Length >= 4); break;
                case ValueOperation.Wrap: case ValueOperation.Power: need(count == 2 && p.All(q => q.Kind == SceneValueKind.Number)); break;
                default: need(count == 1 && has(0, SceneValueKind.Number)); output.Units = n.Data.Units; break;
            }
            n.Port = output;
        }
        private static bool Numeric(SceneValueKind kind) { return kind == SceneValueKind.Number || kind == SceneValueKind.Vector2; }
        internal static SceneValue Zero(SceneValueKind kind)
        {
            if (kind == SceneValueKind.Vector2) return SceneValue.Vector(0, 0);
            if (kind == SceneValueKind.Boolean) return SceneValue.Boolean(false);
            if (kind == SceneValueKind.Integer) return SceneValue.Integer(0);
            if (kind == SceneValueKind.Text) return SceneValue.String("");
            return kind == SceneValueKind.Color ? SceneValue.Color(0, 0, 0, 0) : SceneValue.Number(0);
        }
        internal GraphPort Describe(string id) { return code[RequireIndex(id)].Port; }
        internal SceneValue Read(string id) { return values[RequireIndex(id)]; }
        internal bool Available(string id){return quality[RequireIndex(id)]!=ObservationQuality.Unavailable;}
        internal bool HasValue(string id){return computed[RequireIndex(id)];}
        internal void Tick(double gameplayDelta, double presentationDelta){EvaluateFrame(gameplayDelta,presentationDelta,false);}
        internal void RefreshPure(){EvaluateFrame(0,0,true);}
        private void EvaluateFrame(double gameplayDelta,double presentationDelta,bool pureOnly)
        {
            if (!SceneValue.Finite(gameplayDelta) || !SceneValue.Finite(presentationDelta) || gameplayDelta < 0 || presentationDelta < 0 || gameplayDelta > 1 || presentationDelta > 1)
                throw new ArgumentOutOfRangeException("Graph deltas must be finite seconds in 0..1");
            foreach (int i in order)
            {
                var n = code[i]; double delta = n.Data.Clock == SceneClock.Gameplay ? gameplayDelta : presentationDelta;
                changed[i]=false;
                if(Stateful(n.Op)&&((pureOnly)||(delta==0&&computed[i])))continue;
                bool dirty=force||(!computed[i]&&!faults.ContainsKey(n.Data.Id));foreach(int input in n.Inputs)dirty|=changed[input];
                ObservationQuality observed=ObservationQuality.Exact;
                if(n.Op==ValueOperation.Input){var sample=signals.Read(n.Data.Source);dirty|=inputSequence[i]!=sample.Sequence;inputSequence[i]=sample.Sequence;observed=sample.Quality;if(observed<n.Data.MinimumQuality)observed=ObservationQuality.Unavailable;}
                else if(n.Op==ValueOperation.Delay)observed=initialized[i]?memoryQuality[i]:ObservationQuality.Exact;
                else if(n.Op!=ValueOperation.Available&&n.Op!=ValueOperation.Quality)
                {
                    if(n.Op==ValueOperation.Select)observed=quality[n.Inputs[0]]==ObservationQuality.Unavailable?ObservationQuality.Unavailable:quality[n.Inputs[values[n.Inputs[0]].X!=0?1:2]];
                    else foreach(int input in n.Inputs)if(quality[input]<observed)observed=quality[input];
                }
                var oldQuality=quality[i];
                if(faults.ContainsKey(n.Data.Id)&&!dirty)continue;
                quality[i]=observed;if(observed==ObservationQuality.Unavailable){changed[i]=oldQuality!=observed;continue;}
                if(!dirty&&!Stateful(n.Op)&&oldQuality==observed)continue;
                var oldMemory=memory[i];var oldVelocity=velocity[i];bool oldInitialized=initialized[i];
                var oldValue=values[i];bool oldComputed=computed[i];
                try{values[i]=Evaluate(i,delta);computed[i]=true;faults.Remove(n.Data.Id);changed[i]=!oldComputed||!oldValue.Equals(values[i])||oldQuality!=observed;}
                catch(Exception error){memory[i]=oldMemory;velocity[i]=oldVelocity;initialized[i]=oldInitialized;quality[i]=ObservationQuality.Unavailable;changed[i]=oldQuality!=ObservationQuality.Unavailable;faults[n.Data.Id]=error.GetBaseException().Message;}
            }
            foreach (int i in order) if (code[i].Op == ValueOperation.Delay && quality[code[i].Inputs[0]]!=ObservationQuality.Unavailable && (code[i].Data.Clock == SceneClock.Gameplay ? gameplayDelta : presentationDelta) > 0)
            { memory[i] = values[code[i].Inputs[0]];memoryQuality[i]=quality[code[i].Inputs[0]]; initialized[i] = true; }
            force=false;
        }
        private SceneValue Evaluate(int index, double delta)
        {
            var n = code[index]; var input = n.Inputs;
            SceneValue a = input.Length > 0 ? values[input[0]] : n.Initial, b = input.Length > 1 ? values[input[1]] : SceneValue.Number(0);
            switch (n.Op)
            {
                case ValueOperation.Constant: return n.Initial;
                case ValueOperation.Input: return signals.Read(n.Data.Source).Value;
                case ValueOperation.Pass:return a;
                case ValueOperation.Delay: return initialized[index] ? memory[index] : n.Initial;
                case ValueOperation.Add: return Zip(a, b, (l, r) => l + r);
                case ValueOperation.Subtract: return Zip(a, b, (l, r) => l - r);
                case ValueOperation.Multiply: return Zip(a, b, (l, r) => l * r);
                case ValueOperation.Divide: return Zip(a, b, DivideValues);
                case ValueOperation.Minimum: return Zip(a, b, MinimumValues);
                case ValueOperation.Maximum: return Zip(a, b, MaximumValues);
                case ValueOperation.Vector: return SceneValue.Vector(a.X, b.X);
                case ValueOperation.X: return SceneValue.Number(a.X);
                case ValueOperation.Y: return SceneValue.Number(a.Y);
                case ValueOperation.Length: return SceneValue.Number(Math.Sqrt(a.X * a.X + a.Y * a.Y));
                case ValueOperation.Normalize: double length = Math.Sqrt(a.X * a.X + a.Y * a.Y); return length <= 1e-12 ? SceneValue.Vector(0, 0) : SceneValue.Vector(a.X / length, a.Y / length);
                case ValueOperation.Dot: return SceneValue.Number(a.X * b.X + a.Y * b.Y);
                case ValueOperation.Angle: return SceneValue.Number(Math.Atan2(a.Y, a.X) * 180 / Math.PI);
                case ValueOperation.Sin: return SceneValue.Number(Math.Sin(a.X * Math.PI * 2));
                case ValueOperation.Cos: return SceneValue.Number(Math.Cos(a.X * Math.PI * 2));
                case ValueOperation.Abs: return SceneValue.Number(Math.Abs(a.X));
                case ValueOperation.Floor: return SceneValue.Number(Math.Floor(a.X));
                case ValueOperation.Round: return SceneValue.Number(Math.Round(a.X, MidpointRounding.AwayFromZero));
                case ValueOperation.Power: return SceneValue.Number(Math.Pow(a.X, b.X));
                case ValueOperation.Wrap: if (b.X <= 0) throw new InvalidOperationException(n.Data.Id + ": wrap period must be positive"); return SceneValue.Number(a.X - Math.Floor(a.X / b.X) * b.X);
                case ValueOperation.Clamp:
                    if (b.X > values[input[2]].X) throw new InvalidOperationException(n.Data.Id + ": reversed clamp range");
                    return Clamp(a,b.X,values[input[2]].X);
                case ValueOperation.Remap:
                    double span = values[input[2]].X - b.X; if (Math.Abs(span) < 1e-12) throw new InvalidOperationException(n.Data.Id + ": empty remap input range");
                    return SceneValue.Number(values[input[3]].X + (a.X - b.X) / span * (values[input[4]].X - values[input[3]].X));
                case ValueOperation.Curve:
                    double[] curve = n.Curve; if (a.X <= curve[0]) return SceneValue.Number(curve[1]);
                    for (int k = 2; k < curve.Length; k += 2) if (a.X <= curve[k]) return SceneValue.Number(curve[k - 1] + (curve[k + 1] - curve[k - 1]) * (a.X - curve[k - 2]) / (curve[k] - curve[k - 2]));
                    return SceneValue.Number(curve[curve.Length - 1]);
                case ValueOperation.Select: return a.X != 0 ? b : values[input[2]];
                case ValueOperation.Less: return SceneValue.Boolean(a.X < b.X);
                case ValueOperation.Greater: return SceneValue.Boolean(a.X > b.X);
                case ValueOperation.Equal: return SceneValue.Boolean(a.Equals(b));
                case ValueOperation.And: return SceneValue.Boolean(a.X != 0 && b.X != 0);
                case ValueOperation.Or: return SceneValue.Boolean(a.X != 0 || b.X != 0);
                case ValueOperation.Not: return SceneValue.Boolean(a.X == 0);
                case ValueOperation.Available:return SceneValue.Boolean(quality[input[0]]!=ObservationQuality.Unavailable);
                case ValueOperation.Quality:return SceneValue.Integer((int)quality[input[0]]);
                case ValueOperation.Number: return SceneValue.Number(a.X);
                case ValueOperation.Integer:
                    if (a.X < int.MinValue || a.X > int.MaxValue) throw new OverflowException(n.Data.Id + ": integer conversion overflow"); return SceneValue.Integer((int)Math.Floor(a.X));
                case ValueOperation.Format: return SceneValue.String(a.ToString());
                case ValueOperation.Smooth:
                    if (b.X < 0) throw new InvalidOperationException(n.Data.Id + ": response must be nonnegative");
                    if (!initialized[index]) { memory[index] = a; initialized[index] = true; }
                    if (delta > 0) {double t=1-Math.Exp(-b.X*delta);memory[index]=Linear(memory[index],1-t,a,t);}
                    return memory[index];
                case ValueOperation.Spring:
                    if (b.X < 0 || values[input[2]].X < 0) throw new InvalidOperationException(n.Data.Id + ": spring coefficients must be nonnegative");
                    if (!initialized[index]) { memory[index] = a; velocity[index] = Zero(a.Kind); initialized[index] = true; }
                    int steps = Math.Max(1, (int)Math.Ceiling(delta * 240)); double step = delta / steps,drag=Math.Exp(-values[input[2]].X*step);
                    for (int s = 0; s < steps; s++)
                    {
                        var offset = Linear(a,b.X*step,memory[index],-b.X*step);
                        velocity[index] = Linear(velocity[index],drag,offset,drag);
                        memory[index] = Linear(memory[index],1,velocity[index],step);
                    }
                    return memory[index];
                case ValueOperation.Integrate:
                    if (!initialized[index]) { memory[index] = n.Initial; initialized[index] = true; }
                    if (delta > 0) memory[index] = Linear(memory[index],1,a,delta); return memory[index];
                case ValueOperation.Hysteresis:
                    if (b.X > values[input[2]].X) throw new InvalidOperationException(n.Data.Id + ": reversed hysteresis thresholds");
                    bool previous = initialized[index] && memory[index].X != 0;
                    if (delta > 0) { memory[index] = SceneValue.Boolean(previous ? a.X > b.X : a.X >= values[input[2]].X); initialized[index] = true; }
                    return initialized[index] ? memory[index] : SceneValue.Boolean(false);
                default: throw new InvalidOperationException("Unimplemented graph operation: " + n.Op);
            }
        }
        private static double Divide(double a,double b){if(Math.Abs(b)<1e-12)throw new InvalidOperationException("division by zero");return a/b;}
        private static SceneValue Linear(SceneValue a,double gainA,SceneValue b,double gainB)
        {return a.Kind==SceneValueKind.Vector2?SceneValue.Vector(a.X*gainA+b.X*gainB,a.Y*gainA+(b.Kind==SceneValueKind.Vector2?b.Y:b.X)*gainB):SceneValue.Number(a.X*gainA+b.X*gainB);}
        internal static SceneValue Clamp(SceneValue value,double minimum,double maximum)
        {double x=Math.Max(minimum,Math.Min(maximum,value.X)),y=Math.Max(minimum,Math.Min(maximum,value.Y));return value.Kind==SceneValueKind.Vector2?SceneValue.Vector(x,y):SceneValue.Number(x);}
        internal static SceneValue Map(SceneValue value, Func<double, double> action)
        { return value.Kind == SceneValueKind.Vector2 ? SceneValue.Vector(action(value.X), action(value.Y)) : SceneValue.Number(action(value.X)); }
        internal static SceneValue Zip(SceneValue a, SceneValue b, Func<double, double, double> action)
        { return a.Kind == SceneValueKind.Vector2 ? SceneValue.Vector(action(a.X, b.X), action(a.Y, b.Kind == SceneValueKind.Vector2 ? b.Y : b.X)) : SceneValue.Number(action(a.X, b.X)); }
        internal GraphSnapshot Capture()
        { return new GraphSnapshot { Identity = identity, Values = (SceneValue[])values.Clone(), Memory = (SceneValue[])memory.Clone(), Velocity = (SceneValue[])velocity.Clone(), Initialized = (bool[])initialized.Clone(),Computed=(bool[])computed.Clone(),Quality=(ObservationQuality[])quality.Clone(),MemoryQuality=(ObservationQuality[])memoryQuality.Clone() }; }
        internal void Validate(GraphSnapshot state)
        {
            if (state == null || state.Identity != identity || state.Values == null || state.Memory == null || state.Velocity == null || state.Initialized == null
                ||state.Computed==null||state.Computed.Length!=code.Length|| state.Quality==null||state.MemoryQuality==null||state.Quality.Length!=code.Length||state.MemoryQuality.Length!=code.Length
                || state.Values.Length != code.Length || state.Memory.Length != code.Length || state.Velocity.Length != code.Length || state.Initialized.Length != code.Length)
                throw new InvalidDataException("Graph snapshot belongs to another program");
            for (int i = 0; i < code.Length; i++) if (state.Values[i].Kind != code[i].Port.Kind || state.Memory[i].Kind != code[i].Port.Kind || state.Velocity[i].Kind != code[i].Port.Kind)
                throw new InvalidDataException("Graph snapshot value type differs from its program");
            if(state.Quality.Any(q=>!Enum.IsDefined(typeof(ObservationQuality),q))||state.MemoryQuality.Any(q=>!Enum.IsDefined(typeof(ObservationQuality),q)))throw new InvalidDataException("Graph snapshot has invalid observation quality");
        }
        internal void Restore(GraphSnapshot state)
        { Validate(state); values = (SceneValue[])state.Values.Clone(); memory = (SceneValue[])state.Memory.Clone(); velocity = (SceneValue[])state.Velocity.Clone(); initialized = (bool[])state.Initialized.Clone();computed=(bool[])state.Computed.Clone();quality=(ObservationQuality[])state.Quality.Clone();memoryQuality=(ObservationQuality[])state.MemoryQuality.Clone();faults.Clear();force=true; }
    }
}

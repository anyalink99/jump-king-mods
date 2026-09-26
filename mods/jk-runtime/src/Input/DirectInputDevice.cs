using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq.Expressions;
using JumpKing.Controller;

namespace JKRuntime.Input
{
    public interface IDirectInputDevice : IDisposable
    {
        int[] ReadButtons();
    }
    public interface IDirectInputChargeDevice
    {
        bool ReadJump(DirectInputJumpBinding binding);
    }

    // Own connection and own native decoder. Never update, unacquire, dispose
    // or change properties on the game's joystick/SlimPad instances.
    public sealed class DirectInputDevice : IDirectInputDevice, IDirectInputChargeDevice
    {
        private object directInput;
        private object joystick;
        private IPad decoder;
        private Action update;
        private Func<bool> poll;
        private DirectInputJumpBinding fastBinding;
        private Action readState;
        private Func<bool> matchJump;
        private static readonly System.Collections.Generic.HashSet<Guid> Owned = new System.Collections.Generic.HashSet<Guid>();
        private Guid ownedId;
        private bool ownsLease;

        public static IDirectInputDevice Open(Guid id, IntPtr window)
        {
            DirectInputDevice result = new DirectInputDevice();
            try
            {
                lock (Owned)
                {
                    if (!Owned.Add(id)) throw new InvalidOperationException("Previous SFC DirectInput connection is still shutting down");
                    result.ownedId = id;
                    result.ownsLease = true;
                }
                Assembly game = typeof(PadInstance).Assembly;
                Type slimType = game.GetType("JumpKing.Controller.Slim.SlimPad", true);
                const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo joystickField = slimType.GetField("m_joystick", Private);
                Type joystickType = joystickField.FieldType;
                Assembly slimDX = joystickType.Assembly;
                Type inputType = slimDX.GetType("SlimDX.DirectInput.DirectInput", true);
                Type cooperative = slimDX.GetType("SlimDX.DirectInput.CooperativeLevel", true);
                result.directInput = Activator.CreateInstance(inputType);
                result.joystick = Activator.CreateInstance(joystickType, result.directInput, id);
                CheckResult(joystickType.GetMethod("SetCooperativeLevel", new[] { typeof(IntPtr), cooperative })
                    .Invoke(result.joystick, new[] { (object)window, Enum.Parse(cooperative, "Background, Nonexclusive", true) }));

                // Use the installed game's axis range setup and button/axis/POV
                // conversion, including its exact integer rounding/dead zones.
                // Skip SlimPad's constructor: it references the global manager.
                object slim = FormatterServices.GetUninitializedObject(slimType);
                joystickField.SetValue(slim, result.joystick);
                slimType.GetMethod("SetUpAxises", Private).Invoke(slim, null);
                CheckResult(joystickType.GetMethod("Acquire").Invoke(result.joystick, null));
                slimType.GetField("_connected", Private).SetValue(slim, true);
                result.decoder = (IPad)Activator.CreateInstance(
                    game.GetType("JumpKing.Controller.LegacyPad", true), new[] { slim });
                result.update = CreateUpdate(result.joystick, slim);
                MethodInfo pollMethod = joystickType.GetMethod("Poll");
                result.poll = Expression.Lambda<Func<bool>>(Expression.Property(
                    Expression.Call(Expression.Constant(result.joystick, joystickType), pollMethod), "IsSuccess")).Compile();
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public static bool TryGetDeviceId(PadInstance instance, out Guid id)
        {
            id = Guid.Empty;
            try
            {
                // Wrappers forwarding the native GUID and physical-binding API
                // are supported too; opening the actual DI GUID validates it.
                return instance != null && instance.GetPad() != null
                    && Guid.TryParse(instance.GetPad().GetSaveIdentifier(), out id) && id != Guid.Empty;
            }
            catch { return false; }
        }

        public static Action CreateUpdate(object joystick, object slim)
        {
            Type joystickType = joystick.GetType();
            Type stateType = joystickType.Assembly.GetType("SlimDX.DirectInput.JoystickState", true);
            Type nativeState = typeof(IPad).Assembly.GetType("JumpKing.Controller.Slim.SlimPadState", true);
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            ParameterExpression state = Expression.Variable(stateType, "freshState");
            MethodInfo read = joystickType.GetMethod("GetCurrentState", new[] { stateType.MakeByRefType() });
            Expression success = Expression.Property(Expression.Call(Expression.Constant(joystick, joystickType), read, state), "IsSuccess");
            // Check HRESULT even if another mod disables SlimDX exceptions.
            return Expression.Lambda<Action>(Expression.Block(new[] { state },
                // SlimDX's ref overload fills an existing object; it does not
                // allocate one (unlike the parameterless GetCurrentState).
                Expression.Assign(state, Expression.Constant(Activator.CreateInstance(stateType), stateType)),
                Expression.IfThen(Expression.Not(success), Expression.Throw(Expression.New(
                    typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }),
                    Expression.Constant("DirectInput GetCurrentState failed")))),
                Expression.Assign(Expression.Field(Expression.Constant(slim, slim.GetType()),
                    slim.GetType().GetField("m_current_state", Private)),
                    Expression.New(nativeState.GetConstructor(Private, null, new[] { stateType }, null), state)),
                Expression.Empty())).Compile();
        }

        public int[] ReadButtons()
        {
            if (!poll()) throw new InvalidOperationException("DirectInput Poll failed");
            update(); // Fresh GetCurrentState on OUR handle, not game-frame cache.
            return decoder.GetPressedButtons();
        }

        public bool ReadJump(DirectInputJumpBinding binding)
        {
            if (!ReferenceEquals(binding, fastBinding))
            {
                Type type = joystick.GetType().Assembly.GetType("SlimDX.DirectInput.JoystickState", true);
                object state = Activator.CreateInstance(type);
                ParameterExpression local = Expression.Variable(type, "state");
                Expression success = Expression.Property(Expression.Call(Expression.Constant(joystick),
                    joystick.GetType().GetMethod("GetCurrentState", new[] { type.MakeByRefType() }), local), "IsSuccess");
                readState = Expression.Lambda<Action>(Expression.Block(new[] { local },
                    Expression.Assign(local, Expression.Constant(state, type)),
                    Expression.IfThen(Expression.Not(success), Expression.Throw(Expression.New(
                        typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }),
                        Expression.Constant("DirectInput GetCurrentState failed")))))).Compile();
                matchJump = CreateMatcher(state, binding);
                fastBinding = binding;
            }
            if (!poll()) throw new InvalidOperationException("DirectInput Poll failed");
            readState();
            return matchJump();
        }

        // Allocation-free evaluation of only bound controls. Installed-game
        // contract tests compare this with LegacyPad across axes/POVs/buttons.
        // The unusual integer /100 BEFORE the deadzone is intentional.
        public static Func<bool> CreateMatcher(object state, DirectInputJumpBinding binding)
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            Type type = state.GetType();
            Expression owner = Expression.Constant(state, type);
            Func<string, Expression> field = name => Expression.Field(owner, type.GetField(name, Fields));
            Expression any = Expression.Constant(false);
            foreach (int[] chord in binding.Alternatives)
            {
                Expression all = Expression.Constant(chord.Length != 0);
                foreach (int code in chord)
                {
                    Expression pressed = Expression.Constant(false);
                    if (code > 0 && code < 1000)
                    {
                        Expression buttons = field("pressedButtons");
                        pressed = Expression.AndAlso(Expression.GreaterThan(Expression.ArrayLength(buttons), Expression.Constant(code - 1)),
                            Expression.ArrayIndex(buttons, Expression.Constant(code - 1)));
                    }
                    else if (code >= 1000 && code < 1012)
                    {
                        string[] names = { "x", "y", "z", "rx", "ry", "rz" };
                        Expression value = Expression.Divide(field(names[(code - 1000) / 2]), Expression.Constant(100));
                        pressed = code % 2 == 0 ? Expression.GreaterThan(value, Expression.Constant(0))
                            : Expression.LessThan(value, Expression.Constant(0));
                    }
                    else if (code >= 10000)
                    {
                        int index = (code - 10000) / 4;
                        Expression povs = field("povs");
                        Expression angle = Expression.ArrayIndex(povs, Expression.Constant(index));
                        Func<int, Expression> above = n => Expression.GreaterThan(angle, Expression.Constant(n));
                        Func<int, Expression> below = n => Expression.LessThan(angle, Expression.Constant(n));
                        Expression direction = code % 4 == 0 ? Expression.OrElse(above(27000), below(9000))
                            : code % 4 == 1 ? Expression.AndAlso(above(9000), below(27000))
                            : code % 4 == 2 ? above(18000) : Expression.AndAlso(above(0), below(18000));
                        pressed = Expression.AndAlso(Expression.GreaterThan(Expression.ArrayLength(povs), Expression.Constant(index)),
                            Expression.AndAlso(Expression.GreaterThanOrEqual(angle, Expression.Constant(0)), direction));
                    }
                    all = Expression.AndAlso(all, pressed);
                }
                any = Expression.OrElse(any, all);
            }
            return Expression.Lambda<Func<bool>>(any).Compile();
        }

        private static void CheckResult(object result)
        {
            if (result != null && !(bool)result.GetType().GetProperty("IsSuccess").GetValue(result, null))
                throw new InvalidOperationException("DirectInput operation failed: " + result);
        }

        public void Dispose()
        {
            if (joystick != null)
            {
                try { joystick.GetType().GetMethod("Unacquire").Invoke(joystick, null); } catch { }
                try { ((IDisposable)joystick).Dispose(); } catch { }
                joystick = null;
            }
            if (directInput != null)
            {
                try { ((IDisposable)directInput).Dispose(); } catch { }
                directInput = null;
            }
            if (ownsLease)
            {
                lock (Owned) Owned.Remove(ownedId);
                ownsLease = false;
            }
        }
    }
}

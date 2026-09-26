using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime.Input
{
    // Own SharpDX Controller, not MonoGame's shared connection/timeout arrays.
    // Pure conversion still uses this installed MonoGame's exact deadzones and
    // trigger/button mapping; no second, subtly different analog implementation.
    public static class NativeXInputReader
    {
        public static Func<GamePadState> Create(int slot)
        {
            const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
            Type controller = typeof(GamePad).GetField("_controllers", Hidden).FieldType.GetElementType();
            Type userIndex = controller.Assembly.GetType("SharpDX.XInput.UserIndex", true);
            Type stateType = controller.Assembly.GetType("SharpDX.XInput.State", true);
            object own = Activator.CreateInstance(controller, Enum.ToObject(userIndex, slot));
            ParameterExpression state = Expression.Variable(stateType, "state");
            Expression read = Expression.Call(Expression.Constant(own),
                controller.GetMethod("GetState", new[] { stateType.MakeByRefType() }), state);
            return Expression.Lambda<Func<GamePadState>>(Expression.Block(new[] { state },
                Expression.Condition(read, Convert(Expression.Field(state, "Gamepad")),
                    Expression.Default(typeof(GamePadState))))).Compile();
        }

        public static Func<object, GamePadState> CreateDecoder(Type gamepadType)
        {
            ParameterExpression boxed = Expression.Parameter(typeof(object), "gamepad");
            return Expression.Lambda<Func<object, GamePadState>>(
                Convert(Expression.Convert(boxed, gamepadType)), boxed).Compile();
        }

        private static Expression Convert(Expression pad)
        {
            ConstructorInfo vector = typeof(Vector2).GetConstructor(new[] { typeof(float), typeof(float) });
            Func<string, Expression> axis = name => Expression.Divide(
                Expression.Convert(Expression.Field(pad, name), typeof(float)), Expression.Constant(32767f));
            Func<string, Expression> trigger = name => Expression.Divide(
                Expression.Convert(Expression.Field(pad, name), typeof(float)), Expression.Constant(255f));
            ConstructorInfo sticks = typeof(GamePadThumbSticks).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Vector2), typeof(Vector2), typeof(GamePadDeadZone), typeof(GamePadDeadZone) }, null);
            Expression thumbs = Expression.New(sticks,
                Expression.New(vector, axis("LeftThumbX"), axis("LeftThumbY")),
                Expression.New(vector, axis("RightThumbX"), axis("RightThumbY")),
                Expression.Constant(GamePadDeadZone.IndependentAxes), Expression.Constant(GamePadDeadZone.IndependentAxes));
            Expression buttons = Expression.Call(typeof(GamePad).GetMethod("ConvertToButtons",
                BindingFlags.Static | BindingFlags.NonPublic), Expression.Field(pad, "Buttons"),
                Expression.Field(pad, "LeftTrigger"), Expression.Field(pad, "RightTrigger"));
            MethodInfo direction = typeof(GamePad).GetMethod("ConvertToButtonState", BindingFlags.Static | BindingFlags.NonPublic);
            Type flags = Expression.Field(pad, "Buttons").Type;
            Func<string, Expression> dpad = name => Expression.Call(direction, Expression.Field(pad, "Buttons"),
                Expression.Constant(Enum.Parse(flags, name), flags));
            return Expression.New(typeof(GamePadState).GetConstructor(new[] {
                typeof(GamePadThumbSticks), typeof(GamePadTriggers), typeof(GamePadButtons), typeof(GamePadDPad) }),
                thumbs,
                Expression.New(typeof(GamePadTriggers).GetConstructor(new[] { typeof(float), typeof(float) }),
                    trigger("LeftTrigger"), trigger("RightTrigger")), buttons,
                Expression.New(typeof(GamePadDPad).GetConstructor(new[] { typeof(ButtonState), typeof(ButtonState), typeof(ButtonState), typeof(ButtonState) }),
                    dpad("DPadUp"), dpad("DPadDown"), dpad("DPadLeft"), dpad("DPadRight")));
        }
    }
}

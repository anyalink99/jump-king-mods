using JKRuntime.Input;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using JumpKing;
using JumpKing.Util;
using Microsoft.Xna.Framework;

namespace SubframeCharge
{
    internal static class JumpPercentIntegration
    {
        private static readonly IDisposable jumpSubscription = JKRuntime.Gameplay.JumpEvents.Subscribe(OnJump);
        private static void OnJump(JKRuntime.Gameplay.JumpResult result)
        {
            if (result.Provider != "subframe-charge") return;
            double? seconds = result.HoldMilliseconds.HasValue ? (double?)(result.HoldMilliseconds.Value / 1000) : null;
            if (result.Evidence == JKRuntime.Gameplay.JumpEvidence.BufferedNative) RecordBuffered();
            else if (result.ObservationOnly)
                RecordObservedLaunch(result.PredictedFrameCount.HasValue ? (ChargeResult?)new ChargeResult(result.PredictedFrameCount.Value, 0) : null, seconds, result.Automatic);
            else
            {
                RecordMeasuredLaunch(result.CorrectedFrameCount.HasValue && result.CorrectedTimer.HasValue
                    ? (ChargeResult?)new ChargeResult(result.CorrectedFrameCount.Value, result.CorrectedTimer.Value) : null, seconds, result.Automatic);
                if (result.Evidence == JKRuntime.Gameplay.JumpEvidence.BufferedHold) measurementText += " (buffered)";
            }
        }
        private const string TargetAssemblyName = "JumpKingLastJumpValue";
        private const string CalculatorTypeName =
            "JumpKingLastJumpValue.Models.JumpChargeCalc";

        private static PropertyInfo jumpFramesProperty;
        private static PropertyInfo jumpPercentageProperty;
        private static bool integrationLogged;
        private static bool failureLogged;
        private static Assembly targetAssembly;
        private static bool displayPatched;
        private static bool displayFailed;
        private static long nextDisplayAttempt;
        private static PropertyInfo preferencesProperty;
        private static PropertyInfo displayTypeProperty;
        private const string PendingMeasurementText = "SFC: -";
        private static string measurementText = PendingMeasurementText;
        private static ChargeResult? fractionalDisplay;

        internal static string MeasurementText { get { return measurementText; } }

        internal static void ResetMeasurement(bool enabled)
        {
            measurementText = PendingMeasurementText;
            fractionalDisplay = null;
        }

        internal static void RecordLaunch(int? correctedStep, double? seconds, bool automatic)
        {
            fractionalDisplay = null;
            bool measured = seconds.HasValue && seconds.Value >= 0
                && !double.IsNaN(seconds.Value) && !double.IsInfinity(seconds.Value);
            measurementText = measured
                ? "SFC: " + (seconds.Value * 1000).ToString("0.##", CultureInfo.InvariantCulture)
                    + " ms" + (automatic ? " (max)" : "")
                : "SFC: Not supported";
            // Missing edges must never fabricate a correction to native Jump%.
            if (measured && correctedStep.HasValue)
            {
                ApplyCorrectedCharge(correctedStep.Value);
            }
        }

        internal static void RecordCharging(double? seconds)
        {
            var previous = fractionalDisplay;
            RecordLaunch(null, seconds, false);
            fractionalDisplay = previous;
        }

        internal static void RecordBuffered()
        {
            // Native power and Jump% are authoritative for tick-aligned buffers.
            measurementText = "SFC: Buffered";
            fractionalDisplay = null;
        }

        internal static void RecordMeasuredLaunch(ChargeResult? charge, double? seconds, bool automatic)
        {
            RecordLaunch(null, seconds, automatic);
            if (charge.HasValue && seconds.HasValue && seconds.Value >= 0
                && !double.IsNaN(seconds.Value) && !double.IsInfinity(seconds.Value))
            {
                ApplyChargeValues(charge.Value.Frames, charge.Value.Strength);
                if (!charge.Value.WholeFrames.HasValue) fractionalDisplay = charge;
            }
        }

        internal static void RecordObservedLaunch(ChargeResult? predicted, double? seconds, bool automatic)
        {
            RecordLaunch(null, seconds, automatic);
            // The native Jump% postfix has already run inside base.MyRun.
            // Read its actual count; never overwrite it in observation mode.
            if (!predicted.HasValue || automatic || !seconds.HasValue
                || seconds.Value < 0 || double.IsNaN(seconds.Value) || double.IsInfinity(seconds.Value)) return;
            try
            {
                if (TryResolve())
                {
                    int actual = (int)jumpFramesProperty.GetValue(null, null);
                    if (actual != predicted.Value.ExactFrames)
                        measurementText += " (would " + predicted.Value.ExactFrames.ToString("0.##", CultureInfo.InvariantCulture) + "f)";
                }
            }
            catch (Exception error)
            {
                LogIncompatibleOnce("native comparison unavailable: " + error.GetType().Name);
            }
        }

        internal static bool ShowMeasurement
        {
            get { SettingsStore.EnsureLoaded(); return SettingsStore.Current.ShowMeasurement; }
        }

        internal static void EnsureDisplayHook()
        {
            if (displayPatched || displayFailed || Stopwatch.GetTimestamp() < nextDisplayAttempt)
            {
                return;
            }
            nextDisplayAttempt = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            if (!TryResolve())
            {
                return;
            }
            try
            {
                Type entry = targetAssembly.GetType("JumpKingLastJumpValue.JumpKingLastJumpValue", true);
                preferencesProperty = entry.GetProperty("Preferences", BindingFlags.Public | BindingFlags.Static);
                displayTypeProperty = preferencesProperty.PropertyType.GetProperty("DisplayType");
                Type draw = targetAssembly.GetType("JumpKingLastJumpValue.Models.GameLoopDraw", true);
                MethodInfo original = draw.GetMethod("DrawText", BindingFlags.NonPublic | BindingFlags.Static);
                if (original == null || displayTypeProperty == null)
                {
                    throw new MissingMemberException("Jump% DrawText/DisplayType contract");
                }

                // Use Jump%'s own Harmony dependency, never ship a competing version.
                Assembly harmonyAssembly = null;
                foreach (AssemblyName reference in targetAssembly.GetReferencedAssemblies())
                {
                    if (reference.Name == "0Harmony")
                    {
                        harmonyAssembly = Assembly.Load(reference);
                        break;
                    }
                }
                if (harmonyAssembly == null)
                {
                    throw new MissingMemberException("Jump% Harmony dependency");
                }
                Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
                Type methodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);
                object harmony = Activator.CreateInstance(harmonyType, new object[] { "SubframeCharge.JumpPercent.Display" });
                MethodInfo postfix = typeof(JumpPercentIntegration).GetMethod("DrawMeasurement", BindingFlags.NonPublic | BindingFlags.Static);
                object patchMethod = Activator.CreateInstance(methodType, new object[] { postfix });
                MethodInfo patch = null;
                foreach (MethodInfo candidate in harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (candidate.Name == "Patch" && parameters.Length >= 3
                        && parameters[0].ParameterType == typeof(MethodBase)
                        && parameters[2].Name == "postfix" && parameters[2].ParameterType == methodType)
                    {
                        patch = candidate;
                        break;
                    }
                }
                if (patch == null) throw new MissingMethodException("Harmony.Patch");
                object[] arguments = new object[patch.GetParameters().Length];
                arguments[0] = original;
                arguments[1] = Activator.CreateInstance(methodType, new object[] {
                    typeof(JumpPercentIntegration).GetMethod("DrawFractionalFrames", BindingFlags.NonPublic | BindingFlags.Static) });
                arguments[2] = patchMethod;
                patch.Invoke(harmony, arguments);
                displayPatched = true;
                DiagnosticLog.Write("Jump% SF Charge measurement display installed");
            }
            catch (Exception error)
            {
                displayFailed = true;
                DiagnosticLog.Write("Jump% measurement display unavailable: " + error);
            }
        }

        internal static string FrameLabel(int frames, float percentage)
        {
            if (fractionalDisplay.HasValue)
            {
                var charge = fractionalDisplay.Value;
                if (frames == charge.Frames && percentage == charge.Strength)
                    return charge.ExactFrames.ToString("0.##", CultureInfo.InvariantCulture) + " frames";
                fractionalDisplay = null;
            }
            return frames + " frames";
        }

        // Jump% stores an integer counter. Keep its API compatible and render
        // the exact value only for our fractional result, using its native style.
        private static bool DrawFractionalFrames()
        {
            if (!fractionalDisplay.HasValue) return true;
            try
            {
                if (Convert.ToInt32(displayTypeProperty.GetValue(preferencesProperty.GetValue(null, null), null)) == 0) return true;
                string label = FrameLabel((int)jumpFramesProperty.GetValue(null, null), (float)jumpPercentageProperty.GetValue(null, null));
                if (!fractionalDisplay.HasValue) return true;
                TextHelper.DrawString(Game1.instance.contentManager.font.MenuFont, label,
                    new Vector2(12f, 26f), Color.White, Vector2.Zero, true);
                return false;
            }
            catch { return true; }
        }

        private static void DrawMeasurement()
        {
            if (!ShowMeasurement) return;
            // Called only when Jump% itself draws its text: same visibility,
            // pause handling, font, coordinates, scale and draw ordering.
            try
            {
                object preferences = preferencesProperty.GetValue(null, null);
                bool frames = Convert.ToInt32(displayTypeProperty.GetValue(preferences, null)) != 0;
                string nativeText = frames
                    ? FrameLabel((int)jumpFramesProperty.GetValue(null, null), (float)jumpPercentageProperty.GetValue(null, null))
                    : "Last Jump: " + (Convert.ToSingle(jumpPercentageProperty.GetValue(null, null)) * 100f).ToString("0.00") + "%";
                var font = Game1.instance.contentManager.font.MenuFont;
                string label = "  |  " + measurementText;
                float x = 12f + font.MeasureString(nativeText).X;
                TextHelper.DrawString(font, label, new Vector2(x, 26f), Color.White, Vector2.Zero, true);
            }
            catch (Exception error)
            {
                if (!displayFailed)
                {
                    DiagnosticLog.Write("Jump% measurement drawing failed: " + error);
                    displayFailed = true;
                }
            }
        }

        internal static void ApplyCorrectedCharge(int chargeStep)
        {
            ApplyChargeValues(ToJumpPercentFrames(chargeStep), ToJumpPercentPercentage(chargeStep));
        }

        private static void ApplyChargeValues(int frames, float percentage)
        {
            if (!TryResolve())
            {
                return;
            }

            try
            {
                jumpFramesProperty.SetValue(
                    null,
                    frames,
                    null);
                jumpPercentageProperty.SetValue(
                    null,
                    percentage,
                    null);
                if (!integrationLogged)
                {
                    DiagnosticLog.Write(
                        "Jump% integration active frames="
                        + frames
                        + " percentage="
                        + percentage
                            .ToString("F6"));
                    integrationLogged = true;
                }
            }
            catch (Exception error)
            {
                if (!failureLogged)
                {
                    DiagnosticLog.Write(
                        "Jump% integration failed: " + error.Message);
                    failureLogged = true;
                }
                jumpFramesProperty = null;
                jumpPercentageProperty = null;
            }
        }

        internal static int ToJumpPercentFrames(int chargeStep)
        {
            ValidateStep(chargeStep);
            return chargeStep - 1;
        }

        internal static float ToJumpPercentPercentage(int chargeStep)
        {
            ValidateStep(chargeStep);
            return ChargeQuantizer.StrengthForStep(chargeStep);
        }

        private static bool TryResolve()
        {
            if (jumpFramesProperty != null
                && jumpPercentageProperty != null)
            {
                return true;
            }

            targetAssembly = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                AssemblyName name;
                try
                {
                    name = assemblies[index].GetName();
                }
                catch
                {
                    continue;
                }
                if (string.Equals(
                    name.Name,
                    TargetAssemblyName,
                    StringComparison.Ordinal))
                {
                    targetAssembly = assemblies[index];
                    break;
                }
            }
            if (targetAssembly == null)
            {
                return false;
            }

            Type calculator = targetAssembly.GetType(
                CalculatorTypeName,
                false);
            if (calculator == null)
            {
                LogIncompatibleOnce("calculator type is missing");
                return false;
            }

            const BindingFlags Flags = BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            PropertyInfo frames = calculator.GetProperty(
                "JumpFrames",
                Flags);
            PropertyInfo percentage = calculator.GetProperty(
                "JumpPercentage",
                Flags);
            if (!CanWrite(frames, typeof(int))
                || !CanWrite(percentage, typeof(float)))
            {
                LogIncompatibleOnce("charge properties are incompatible");
                return false;
            }

            jumpFramesProperty = frames;
            jumpPercentageProperty = percentage;
            return true;
        }

        private static bool CanWrite(PropertyInfo property, Type valueType)
        {
            return property != null
                && property.PropertyType == valueType
                && property.GetSetMethod(true) != null
                && property.GetSetMethod(true).IsStatic;
        }

        private static void LogIncompatibleOnce(string reason)
        {
            if (failureLogged)
            {
                return;
            }
            DiagnosticLog.Write(
                "Jump% integration unavailable: " + reason);
            failureLogged = true;
        }

        private static void ValidateStep(int chargeStep)
        {
            if (chargeStep < ChargeQuantizer.MinimumStep
                || chargeStep > ChargeQuantizer.MaximumStep)
            {
                throw new ArgumentOutOfRangeException("chargeStep");
            }
        }
    }
}

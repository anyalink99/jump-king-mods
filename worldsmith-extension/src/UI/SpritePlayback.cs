using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorldsmithExtension
{
    internal static class SpritePlayback
    {
        // native StartAnimation can run both on Loaded and on Visibility changes
        internal static bool Start(object __instance)
        {
            var evt = Engine.Type("JKWorldsmith.Models.DeltaManager").GetEvent("OnUpdate");
            var method = __instance.GetType().GetMethod("ChangeSprite", BindingFlags.Instance | BindingFlags.NonPublic);
            var callback = Delegate.CreateDelegate(evt.EventHandlerType, __instance, method);
            evt.RemoveEventHandler(null, callback);
            evt.AddEventHandler(null, callback);
            return false;
        }

        internal static bool Tick(object __instance, float delta)
        {
            var image = (Image)__instance;
            if (!image.IsVisible || !image.IsLoaded)
            {
                Engine.Set(__instance, "m_timer", 0d);
                return false;
            }

            return Advance(__instance, delta);
        }

        internal static bool Advance(object __instance, float delta)
        {
            var image = (Image)__instance;
            var property = (DependencyProperty)Engine.Get(__instance.GetType(), "FramesProperty");
            var frames = image.GetValue(property) as IList;
            if (frames == null || frames.Count < 2)
                return false;
            var durations = (List<float>)Engine.Get(__instance, "Floats");
            bool variable = durations != null && durations.Count > 0;
            double timer = (double)Engine.Get(__instance, "m_timer") + Math.Max(0, Math.Min(0.5, delta));
            int index = (int)Engine.Get(__instance, "Index"), timeIndex = variable ? (int)Engine.Get(__instance, "FloatIndex") : 0;
            if (variable)
            {
                double cycle = 0;
                foreach (float value in durations)
                {
                    if (value <= 0 || Single.IsNaN(value) || Single.IsInfinity(value))
                        return false;
                    cycle += value;
                }

                double cycles = Math.Floor(timer / cycle);
                index = (index + (int)((cycles % frames.Count) * durations.Count % frames.Count)) % frames.Count;
                timer %= cycle;
                for (int i = 0; i < durations.Count && timer > durations[timeIndex]; i++)
                {
                    timer -= durations[timeIndex];
                    timeIndex = (timeIndex + 1) % durations.Count;
                    index = (index + 1) % frames.Count;
                }

                Engine.Set(__instance, "FloatIndex", timeIndex);
            }
            else
            {
                float interval = (float)Engine.Get(__instance, "TimerInterval");
                if (interval <= 0 || Single.IsNaN(interval) || Single.IsInfinity(interval))
                    return false;
                double steps = Math.Floor(timer / interval);
                index = (index + (int)(steps % frames.Count)) % frames.Count;
                timer %= interval;
            }

            Engine.Set(__instance, "m_timer", timer);
            Engine.Set(__instance, "indexChangedPrivately", true);
            Engine.Set(__instance, "Index", index);
            // DeltaManager already runs on the render dispatcher
            // update once, don't queue a job for every missed animation frame
            image.Source = frames[index] as ImageSource;
            return false;
        }
    }
}

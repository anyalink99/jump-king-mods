using System;
using System.Collections;
using System.Windows;

namespace WorldsmithExtension
{
    internal static class AudioSettings
    {
        internal static void Loading(object __instance)
        {
            // native Load clears file paths but leaves the old project's sound types behind
            Application.Current.Dispatcher.Invoke(new Action(() => ((IList)Engine.Get(__instance.GetType(), "SpecialInfo")).Clear()));
        }

        internal static bool Current(object __instance, int screen, ref object __result)
        {
            __result = null;
            if (screen < 1) return false;
            int end = 0;
            foreach (object section in (IList)Engine.Get(__instance, "Sections"))
            {
                int length = (int)Engine.Get(section, "Screens");
                if (length < 1) throw new InvalidOperationException("Audio sections must cover at least one screen.");
                end = checked(end + length);
                if (screen <= end) { __result = section; break; }
            }
            return false;
        }

        internal static bool Set(object __instance, object ambienceObject, int screen)
        {
            if (screen < 1) throw new ArgumentOutOfRangeException("screen");
            if (ambienceObject == null) throw new ArgumentNullException("ambienceObject");
            object current = null;
            Current(__instance, screen, ref current);
            if (current != null)
            {
                ((IList)Engine.Get(current, "Ambience")).Add(ambienceObject);
                return false;
            }
            var sections = (IList)Engine.Get(__instance, "Sections");
            int covered = 0;
            foreach (object section in sections) covered = checked(covered + (int)Engine.Get(section, "Screens"));
            // this screen is past the last section, append the gap and sound instead of shifting everything
            object gap = screen > covered + 1 ? Section(screen - covered - 1, null) : null;
            object added = Section(1, ambienceObject);
            if (gap != null) sections.Add(gap);
            sections.Add(added);
            return false;
        }

        static object Section(int length, object ambience)
        {
            object data = Activator.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.AmbienceSave"));
            Engine.Set(data, "screens", length);
            Engine.Set(data, "ambience", Array.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.Ambience"), 0));
            object section = Engine.Call(Activator.CreateInstance(Engine.Type("JKWorldsmith.Shared.Structs.AmbienceSaveObject")), "FromStruct", data);
            if (ambience != null) ((IList)Engine.Get(section, "Ambience")).Add(ambience);
            return section;
        }
    }
}

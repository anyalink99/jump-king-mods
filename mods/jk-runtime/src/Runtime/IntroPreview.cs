using System;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.GameManager.TitleScreen;
using JumpKing.SaveThread;

namespace JKRuntime
{
    internal static class IntroPreview
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static FieldInfo title, preview;
        private static MethodInfo apply;
        internal static void Prepare()
        {
            // bind once with the shared hooks, before a map's first intro
            title = typeof(JumpGame).GetField("m_title_screen", Flags);
            preview = typeof(GameTitleScreen).GetField("m_folly", Flags);
            if (title == null || title.FieldType != typeof(GameTitleScreen) || preview == null
                || !typeof(Entity).IsAssignableFrom(preview.FieldType)) return;
            apply = preview.FieldType.GetMethod("ApplySave", Flags, null, new[] { typeof(SaveState) }, null);
            if (apply != null && apply.ReturnType != typeof(void)) apply = null;
        }
        internal static void Refresh()
        {
            if (apply == null || JumpGame.instance == null || SaveManager.instance == null || LevelDebugState.instance != null) return;
            var screen = title.GetValue(JumpGame.instance);
            var player = screen == null ? null : preview.GetValue(screen) as Entity;
            if (player == null || !player.IsAlive) return;
            // the new-game menu can omit ReloadSaveNode and leave the old map's preview
            // use the settled in-memory save; GetDefault would reread level_settings.xml
            apply.Invoke(player, new object[] { SaveManager.instance.GetSaveFile() });
        }
    }
}

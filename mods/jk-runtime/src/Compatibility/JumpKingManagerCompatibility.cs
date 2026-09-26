using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using EntityComponent;
using JumpKing;
using JumpKing.Level;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Player;
using JKRuntime.Geometry;

namespace JKRuntime.Compatibility
{
    internal static class JumpKingManagerCompatibility
    {
        private const string Hash = "F97C0F9412DEA2456305B12DB1D42FB92C066FED3A1CDD1D8BAC77AB62AAF275";
        private static OwnedPatches patches;
        private static Type managerType;
        private static FieldInfo activeForm;
        private static bool worldReady;
        private static readonly ConditionalWeakTable<Form, ManagerView> views = new ConditionalWeakTable<Form, ManagerView>();
        private static readonly List<WeakReference> forms = new List<WeakReference>();
        internal static ManagerMap Map { get; private set; }
        internal static string Status { get; private set; }

        internal static void Prepare()
        {
            worldReady = true;
            Apply();
        }

        internal static void Apply()
        {
            try
            {
                if (!ModCompatibility.Setting.Value)
                {
                    Map = null; RefreshForms(false);
                    if (patches != null) { patches.Dispose(); patches = null; }
                    Status = "Disabled"; return;
                }
                if (patches == null && !Install()) return;
                if (!worldReady)
                {
                    Map = null; RefreshForms(true); Status = "Waiting: no loaded map"; return;
                }
                var settingsType = typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.LocationText.LocationTextManager", true);
                var property = settingsType.GetProperty("SETTINGS", OwnedPatches.Members);
                if (property == null || property.PropertyType != typeof(LocationSettings))
                    throw new MissingMemberException("Loaded map location settings unavailable");
                var settings = (LocationSettings)property.GetValue(null, null);
                Map = new ManagerMap(settings.locations, NativeWorldGeometry.ReadScreens().Length);
                RefreshForms(true);
                Status = "Active: loaded-map areas and screens";
            }
            catch (Exception error)
            {
                Map = null;
                // Keep the adapted browser empty on failure, never expose stale
                // destinations as if they belonged to the newly loaded map.
                try { RefreshForms(patches != null && ModCompatibility.Setting.Value); }
                catch (Exception cleanup) { RuntimeJournal.Record("jk-runtime", "manager-ui-cleanup", cleanup.ToString()); }
                Status = "Unavailable: " + error.GetBaseException().Message;
                RuntimeJournal.Record("jk-runtime", "manager-compatibility", error.ToString());
            }
        }

        private static bool Install()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "JumpKingManager").ToArray();
            if (assemblies.Length == 0) { Status = "Not needed: Jump King Manager not loaded"; return false; }
            if (assemblies.Length != 1 || !KnownBuild(assemblies[0]))
            { Status = "Unsupported: unreviewed Jump King Manager build"; return false; }
            managerType = assemblies[0].GetType("JumpKingManager.Manager", true);
            activeForm = assemblies[0].GetType("JumpKingManager.ModEntry", true).GetField("JKManager", OwnedPatches.Members);
            if (activeForm == null || !typeof(Form).IsAssignableFrom(managerType)) throw new MissingMemberException("Manager form contract changed");
            var candidate = new OwnedPatches("jk-runtime.compatibility.jump-king-manager");
            try
            {
                candidate.Add(managerType.GetConstructor(Type.EmptyTypes), postfix: Callback("Constructed"));
                candidate.Add(managerType.GetMethod("btnSpecificLevel_Click", OwnedPatches.Members), prefix: Callback("SpecificLevel"));
                candidate.Add(managerType.GetMethod("btnSave_Click", OwnedPatches.Members), postfix: Callback("Saved"));
                candidate.Add(activeForm.DeclaringType.GetMethod("OnLevelStart", OwnedPatches.Members), postfix: Callback("Started"));
                patches = candidate;
                return true;
            }
            catch { candidate.Dispose(); throw; }
        }
        private static MethodInfo Callback(string name) { return typeof(JumpKingManagerCompatibility).GetMethod(name, OwnedPatches.Members); }
        private static bool KnownBuild(Assembly assembly)
        {
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == Hash;
        }

        // Called on world exit before native loading replaces the locations. An
        // existing foreign form must never keep destinations from the old map.
        internal static void ClearWorld()
        {
            worldReady = false;
            Map = null;
            if (patches != null)
            {
                Status = "Waiting: no loaded map";
                Guard(delegate { RefreshForms(ModCompatibility.Setting.Value); });
            }
        }
        private static void Started()
        { Guard(delegate { RefreshForms(patches != null && ModCompatibility.Setting.Value); }); }
        private static void Constructed(Form __instance) { Guard(delegate { Attach(__instance); }); }
        private static void Guard(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                Status = "Unavailable: " + error.GetBaseException().Message;
                RuntimeJournal.Record("jk-runtime", "manager-compatibility", error.ToString());
            }
        }
        private static void Attach(Form form)
        {
            if (form == null || form.IsDisposed) return;
            ManagerView view;
            if (!views.TryGetValue(form, out view))
            {
                view = new ManagerView(form); views.Add(form, view); forms.Add(new WeakReference(form));
            }
            view.Refresh(Map, patches != null && ModCompatibility.Setting.Value);
        }
        private static void RefreshForms(bool enabled)
        {
            var current = activeForm == null ? null : activeForm.GetValue(null) as Form;
            if (current != null) Attach(current);
            for (int i = forms.Count - 1; i >= 0; i--)
            {
                var form = forms[i].Target as Form;
                if (form == null || form.IsDisposed) { forms.RemoveAt(i); continue; }
                ManagerView view;
                if (views.TryGetValue(form, out view)) view.Refresh(Map, enabled);
            }
        }
        private static bool SpecificLevel(Form __instance)
        {
            ManagerView view;
            if (!views.TryGetValue(__instance, out view) || !view.Enabled) return true;
            // Manager's hotkey worker also invokes this handler. Match its UI
            // marshaling, but avoid its cast to the hard-coded vanilla enum.
            if (__instance.InvokeRequired)
                __instance.BeginInvoke(new Action(delegate { SpecificLevel(__instance); }));
            else view.NavigateSelected();
            return false;
        }
        private static void Saved(Form __instance)
        {
            if (__instance.InvokeRequired) return;
            ManagerView view;
            if (views.TryGetValue(__instance, out view)) view.RefreshSavedLabel();
        }
        internal static void Navigate(Form form, ManagerMap source, int index)
        {
            try { NavigateCore(form, source, index); }
            catch (Exception error)
            {
                RuntimeJournal.Record("jk-runtime", "manager-navigation", error.ToString());
                MessageBox.Show(form, "Could not navigate to this screen: " + error.GetBaseException().Message, "Jump King Manager");
            }
        }
        private static void NavigateCore(Form form, ManagerMap source, int index)
        {
            if (source == null || !ReferenceEquals(source, Map) || !ModCompatibility.Setting.Value) return;
            RuntimeApi.Kernel.CheckThread();
            var player = EntityManager.instance == null ? null : EntityManager.instance.Find<PlayerEntity>();
            if (player == null) return;
            var screens = NativeWorldGeometry.ReadScreens();
            if (index < 0 || index >= screens.Length) return;
            var box = player.m_body.GetHitbox();
            Microsoft.Xna.Framework.Vector2 position;
            if (!ManagerMap.TryPosition(screens[index], box.Width, box.Height, out position))
            {
                MessageBox.Show(form, "No clear arrival position was found on this screen.", "Jump King Manager"); return;
            }
            player.m_body.Position = position;
            player.m_body.Velocity = Microsoft.Xna.Framework.Vector2.Zero;
            Camera.UpdateCamera(player.m_body.GetHitbox().Center);
        }
    }
}

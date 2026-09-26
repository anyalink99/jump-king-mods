using System;
using System.Collections.Generic;
using EntityComponent;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Player;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal sealed class ModalHost : Entity, IForeground
    {
        private sealed class Entry
        {
            internal UiPageSession Session;
            internal bool DimBackground;
        }

        private RuntimeScope suspension;
        private readonly List<Entry> pages = new List<Entry>();
        private InputContextLease inputContext;
        private readonly List<UiPageSession> retiring = new List<UiPageSession>();
        private float releaseRetry;
        internal static ModalHost Instance { get; private set; }
        internal bool IsOpen { get { return pages.Count > 0; } }
        internal int Depth { get { return pages.Count; } }

        internal ModalHost()
        {
            Instance = this;
            GoToFront();
        }

        internal bool Open(IUiPage value, UiModalOptions options)
        {
            if (value == null || pages.Exists(p => ReferenceEquals(p.Session.Page, value))) return false;
            bool first = pages.Count == 0;
            Entry entry = new Entry
            {
                Session = new UiPageSession(value),
                DimBackground = options == null || options.DimBackground
            };
            if (!first) pages[pages.Count - 1].Session.Resume();
            pages.Add(entry);
            try
            {
                GoToFront();
                if (first)
                {
                    SuspendPlayer();
                    inputContext = InputContextLease.AcquireModal();
                }
                ControllerManager.instance.MenuController.ConsumePadPresses();
                entry.Session.Open();
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Could not open modal page: " + error.Message);
                while (pages.Contains(entry) && !ReferenceEquals(pages[pages.Count - 1], entry)) CloseTop(true);
                bool removed = pages.Remove(entry);
                if (removed) Retire(entry.Session);
                if (pages.Count == 0) TryReleaseModalContext();
                return false;
            }
        }

        internal void Close()
        {
            if (pages.Count == 0) { TryReleaseModalContext(); return; }
            CloseTop(true);
        }

        protected override void Update(float delta)
        {
            if (pages.Count == 0)
            {
                releaseRetry -= delta;
                if (releaseRetry <= 0 && (suspension != null || inputContext != null || retiring.Count > 0)) { releaseRetry = 1; TryReleaseModalContext(); }
                return;
            }
            PadState state = ControllerManager.instance.MenuController.GetPadState();
            UiInput input = inputContext == null
                ? UiInputRouter.Read(state, false)
                : inputContext.ReadModal(state);
            if (input.Action != UiAction.None) UiInputRouter.Consume();
            Entry entry = pages[pages.Count - 1];
            try
            {
                entry.Session.Update(input, delta, UiPageInput.IsReleased()
                    && (inputContext == null || inputContext.IsCancelReleased()));
            }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Modal update failed: " + error.Message);
                // A callback may have opened children before throwing.
                while (pages.Contains(entry)) Close();
                return;
            }
            if (pages.Count > 0
                && ReferenceEquals(entry, pages[pages.Count - 1])
                && entry.Session.ReadyToClose)
            {
                Close();
            }
        }

        public void ForegroundDraw()
        {
            if (pages.Count == 0) return;
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].DimBackground)
                {
                    Game1.spriteBatch.Draw(
                        pixel,
                        new Rectangle(0, 0, 480, 360),
                        new Color(0, 0, 0, i == 0 ? 190 : 150));
                }
                try
                {
                    pages[i].Session.Draw();
                }
                catch (Exception error)
                {
                    Console.WriteLine("[JK Runtime UI] Modal draw failed: " + error.Message);
                    while (pages.Count > i) CloseTop(true);
                    break;
                }
            }
        }

        protected override void OnDestroy()
        {
            while (pages.Count > 0) CloseTop(true);
            TryReleaseModalContext();
            if (Instance == this) Instance = null;
        }

        private void SuspendPlayer()
        {
            if (suspension != null) throw new InvalidOperationException("Previous modal suspension has not been released");
            PlayerEntity player = Find<PlayerEntity>();
            if (player == null) return;
            suspension = SuspendComponents(player.GetComponents());
        }

        internal static RuntimeScope SuspendComponents(IEnumerable<Component> components)
        {
            var scope = new RuntimeScope();
            try
            {
                foreach (Component component in components)
                    scope.Own(Gameplay.ComponentSuspension.Acquire("jk.ui.modal", component));
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        private void CloseTop(bool notifyPage)
        {
            int index = pages.Count - 1;
            UiPageSession closing = pages[index].Session;
            pages.RemoveAt(index);
            try
            {
                if (notifyPage) Retire(closing);
            }
            finally
            {
                ControllerManager.instance.MenuController.ConsumePadPresses();
                if (pages.Count == 0) TryReleaseModalContext();
                else pages[pages.Count - 1].Session.Resume();
            }
        }

        private void Retire(UiPageSession session)
        {
            try { session.Dispose(); }
            catch (Exception error)
            {
                retiring.Add(session);
                Console.WriteLine("[JK Runtime UI] Modal page cleanup pending: " + error);
            }
        }

        private void TryReleaseModalContext()
        {
            try { ReleaseModalContext(); }
            catch (Exception error) { Console.WriteLine("[JK Runtime UI] Modal cleanup pending retry: " + error); }
        }
        private void ReleaseModalContext()
        {
            var failures = new List<Exception>();
            for (int i = retiring.Count - 1; i >= 0; i--)
                try { retiring[i].Dispose(); retiring.RemoveAt(i); }
                catch (Exception error) { failures.Add(error); }
            try { if (suspension != null) suspension.Dispose(); suspension = null; }
            catch (Exception error) { failures.Add(error); }
            try { if (inputContext != null) inputContext.Dispose(); inputContext = null; }
            catch (Exception error) { failures.Add(error); }
            if (failures.Count != 0) throw new AggregateException("Modal input cleanup incomplete", failures);
        }
    }
}

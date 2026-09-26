using System;
using System.Collections.Generic;

namespace JKRuntime.UI
{
    /// <summary>Owned nested navigation for either host. The root factory runs on each open.
    /// Only the top page receives input and draws. Push children from Update callbacks;
    /// release boundaries prevent opening/closing input from reaching another page.</summary>
    public sealed class UiPageStack : IUiPage, IUiPageInputPolicy
    {
        private readonly Func<UiPageStack, IUiPage> createRoot;
        private readonly List<UiPageSession> pages = new List<UiPageSession>();
        private bool open, drawing, transitioning;
        public bool WantsClose { get; private set; }
        public bool HandlesCancel { get { return true; } }
        /// <summary>Number of owned pages, including the root.</summary>
        public int Depth { get { return pages.Count; } }
        public UiPageStack(Func<UiPageStack, IUiPage> rootFactory)
        { if (rootFactory == null) throw new ArgumentNullException("rootFactory"); createRoot = rootFactory; }
        public void OnOpen()
        {
            OnClose(); open = true; WantsClose = false;
            try { Push(createRoot(this)); }
            catch (Exception error)
            {
                try { OnClose(); }
                catch (Exception cleanup) { throw new AggregateException("Page stack open and cleanup failed", error, cleanup); }
                throw;
            }
        }
        /// <summary>Open a child immediately, retaining parent state. Duplicate instances,
        /// navigation during Draw/open/close, and pushing into a closed stack are rejected.</summary>
        public void Push(IUiPage page)
        {
            if (!open || WantsClose) throw new InvalidOperationException("Page stack is closed");
            if (drawing || transitioning) throw new InvalidOperationException("Navigate from Update, not Draw or lifecycle callbacks");
            if (page == null) throw new ArgumentNullException("page");
            if (ReferenceEquals(page, this) || pages.Exists(p => ReferenceEquals(p.Page, page)))
                throw new InvalidOperationException("A page instance cannot own itself or be opened twice");
            transitioning = true;
            try
            {
                if (pages.Count > 0) pages[pages.Count - 1].Resume();
                var session = new UiPageSession(page);
                pages.Add(session);
                session.Open();
            }
            finally { transitioning = false; }
        }
        public void Update(UiInput input, float delta)
        {
            if (!open || WantsClose || pages.Count == 0) return;
            var top = pages[pages.Count - 1];
            top.Update(input, delta, UiPageInput.IsReleased());
            if (!open || pages.Count == 0 || !ReferenceEquals(top, pages[pages.Count - 1]) || !top.ReadyToClose) return;
            transitioning = true;
            try
            {
                top.Dispose(); pages.RemoveAt(pages.Count - 1);
                if (pages.Count == 0) WantsClose = true;
                else pages[pages.Count - 1].Resume();
            }
            finally { transitioning = false; }
        }
        public void Draw()
        {
            if (!open || pages.Count == 0) return;
            drawing = true;
            try { pages[pages.Count - 1].Draw(); }
            finally { drawing = false; }
        }
        public void OnClose()
        {
            if (transitioning || drawing) throw new InvalidOperationException("Reentrant page stack lifecycle");
            open = false; WantsClose = true;
            var failures = new List<Exception>();
            transitioning = true;
            try
            {
                for (int i = pages.Count - 1; i >= 0; i--)
                    try { pages[i].Dispose(); pages.RemoveAt(i); }
                    catch (Exception error) { failures.Add(error); }
            }
            finally { transitioning = false; }
            if (failures.Count != 0) throw new AggregateException("Page stack cleanup incomplete", failures);
        }
    }
}

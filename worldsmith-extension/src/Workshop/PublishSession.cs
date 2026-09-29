using System;

namespace WorldsmithExtension
{
    // all transitions run on the owning dispatcher, background work returns through run
    internal sealed class PublishSession
    {
        readonly Publisher publisher;
        readonly OperationCoordinator operations;
        readonly Action<Action, Action<Exception>> run;
        WorkshopEntry pending;
        IDisposable lease;
        internal event Action Changed;
        internal event Action<WorkshopEntry> DetailsChanged;
        internal ulong ItemId { get; private set; }
        internal WorkshopEntry Linked { get; private set; }
        internal bool DetailsLoaded { get { return ItemId == 0 || Linked != null; } }
        internal bool NeedsPersistence { get; private set; }
        internal bool Busy { get { return lease != null; } }
        internal string Content { get; private set; }
        internal bool CanPublish { get { return !Busy && Content != null && DetailsLoaded; } }

        internal PublishSession(ulong id, WorkshopEntry entry, Publisher workflow,
            OperationCoordinator coordinator, Action<Action, Action<Exception>> background)
        {
            ItemId = id; Linked = entry; publisher = workflow; operations = coordinator; run = background;
        }

        void Notify() { if (Changed != null) Changed(); }
        void Finish()
        {
            var held = lease; lease = null;
            if (held != null) held.Dispose();
            var arrived = pending; pending = null;
            if (arrived != null) AcceptDetails(arrived);
            Notify();
        }

        internal void AcceptDetails(WorkshopEntry entry)
        {
            if (entry == null || entry.Id != ItemId || DetailsLoaded) return;
            if (Busy) { pending = entry; return; }
            Linked = entry;
            if (DetailsChanged != null) DetailsChanged(entry);
            Notify();
        }

        internal void Relink(WorkshopEntry entry)
        {
            if (Busy) throw new InvalidOperationException("Wait for publication work before changing its item.");
            ItemId = entry.Id; Linked = entry; pending = null;
            if (DetailsChanged != null) DetailsChanged(entry);
            Notify();
        }

        internal void InvalidateBuild()
        {
            if (Busy) throw new InvalidOperationException("Wait for publication work before changing the build.");
            Content = null; Notify();
        }

        internal void Build(string destination, Action build, Action<Exception> finished)
        {
            if (Busy) throw new InvalidOperationException("Publication work is still running.");
            lease = operations.Enter("the project build");
            Content = null;
            try
            {
                Notify();
                run(build, error =>
                {
                    if (error == null) Content = destination;
                    Finish();
                    finished(error);
                });
            }
            catch { Finish(); throw; }
        }

        internal void Publish(PublishRequest request, ulong owner, Action<ulong, PublishRequest> persist,
            Action<string, bool> finished)
        {
            if (!CanPublish) throw new InvalidOperationException("Build the project and load its Workshop details before publishing.");
            request.Id = ItemId; request.Content = Content;
            request.ValidateFields();
            lease = operations.Enter("the Workshop publication");
            try
            {
                Notify();
                if (ItemId != 0)
                {
                    persist(ItemId, request);
                    NeedsPersistence = false;
                }
                publisher.Start(request, value =>
                {
                    // keep the assigned identity in memory even if persistence fails
                    ItemId = value;
                    NeedsPersistence = true;
                    Linked = Metadata(request, owner);
                    Notify();
                    persist(value, request);
                    NeedsPersistence = false;
                }, (message, ok) =>
                {
                    if (ok) Linked = Metadata(request, owner);
                    Finish();
                    finished(message, ok);
                });
            }
            catch { Finish(); throw; }
        }

        internal static WorkshopEntry Metadata(PublishRequest request, ulong owner)
        {
            return new WorkshopEntry { Id = request.Id, Owner = owner, Title = request.Title,
                Description = request.Description, Tags = request.Tags, Category = WorkshopLibrary.CategoryOf(request.Tags), Visibility = request.Visibility };
        }
        internal string Progress() { return publisher.Progress(); }
    }
}

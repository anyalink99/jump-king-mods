using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WorldsmithExtension
{
    internal static class WorkflowTests
    {
        sealed class Queue
        {
            readonly System.Collections.Generic.Queue<Action> work = new System.Collections.Generic.Queue<Action>();
            internal int Runs;
            internal void Schedule(Action action, Action<Exception> done)
            {
                work.Enqueue(() =>
                {
                    Runs++;
                    Exception failure = null;
                    try { action(); } catch (Exception error) { failure = error; }
                    done(failure);
                });
            }
            internal void Next() { work.Dequeue()(); }
        }
        sealed class Steam : IWorkshop
        {
            internal int Creates, Submits;
            internal Action<ulong, string, bool> Created;
            internal Action<string, bool> Submitted;
            public void Create(Action<ulong, string, bool> result) { Creates++; Created = result; }
            public void Submit(PublishRequest request, Action<string, bool> result) { Submits++; Submitted = result; }
            public string Progress() { return "Steam pending"; }
        }
        static void Reject<T>(Action work, Action<bool, string> assert, string name) where T : Exception
        {
            try { work(); } catch (T) { assert(true, name); return; }
            throw new Exception(name);
        }

        internal static void Run(string directory, Action<bool, string> assert)
        {
            var operations = new OperationCoordinator();
            var lease = operations.Enter("build");
            Reject<InvalidOperationException>(() => operations.Enter("download"), assert, "conflicting operation rejected");
            lease.Dispose(); lease.Dispose();
            assert(!operations.Busy, "lease released idempotently");
            var queue = new Queue(); var api = new Steam();
            var session = new PublishSession(42, null, new Publisher(api, queue.Schedule), operations, queue.Schedule);
            int applied = 0;
            session.DetailsChanged += item => applied++;
            session.Build("prepared", () => {}, error => assert(error == null, "build completion succeeds"));
            session.AcceptDetails(new WorkshopEntry { Id = 42, Owner = 100, Title = "Remote title", Category = "Level", Tags = new[] { "Level" } });
            assert(!session.DetailsLoaded && session.Busy, "metadata deferred while building");
            queue.Next();
            assert(session.CanPublish && applied == 1 && !operations.Busy, "metadata arriving during build enables publication after completion");
            session.AcceptDetails(new WorkshopEntry { Id = 42, Title = "Unrequested overwrite" });
            assert(applied == 1 && session.Linked.Title == "Remote title", "catalog refresh does not overwrite an initialized draft");
            session.Relink(new WorkshopEntry { Id = 43, Title = "Other item" });
            assert(session.ItemId == 43 && applied == 2, "explicit relink replaces metadata");
            session.Build("broken", () => { throw new IOException("compiler failure"); }, error => assert(error is IOException, "build failure delivered"));
            queue.Next();
            assert(!session.Busy && !operations.Busy && session.Content == null, "failed build clears content and releases ownership");

            string root = Path.Combine(directory, "workflow-source"), content = Path.Combine(directory, "workflow-build", "content");
            Directory.CreateDirectory(root); Directory.CreateDirectory(content);
            File.WriteAllText(Path.Combine(root, "source.txt"), "original");
            File.WriteAllText(Path.Combine(content, "built.txt"), "compiled");
            Action receipt = () => BuildReceipt.Save(root, content, Files.Fingerprint(root));
            receipt();
            api = new Steam(); queue = new Queue();
            session = new PublishSession(0, null, new Publisher(api, queue.Schedule), operations, queue.Schedule);
            session.Build(content, () => {}, error => {}); queue.Next();
            var request = new PublishRequest { Root = root, Title = "Test map", Tags = new[] { "Level" }, Visibility = 2 };
            bool success = false;
            session.Publish(request, 100, (id, draft) => { throw new IOException("disk full"); }, (message, ok) => success = ok);
            assert(api.Creates == 0 && session.Busy && operations.Busy, "verification queued before any Steam request");
            queue.Next();
            assert(api.Creates == 1, "creation follows successful background verification");
            api.Created(501, null, false);
            assert(!success && !session.Busy && session.ItemId == 501 && session.NeedsPersistence && !operations.Busy, "persistence failure retains assigned identity and releases operation");
            int checks = queue.Runs;
            session.Publish(request, 100, (id, draft) => assert(id == 501, "retry persists assigned identity"), (message, ok) => success = ok);
            queue.Next();
            assert(queue.Runs == checks + 1 && api.Submits == 1 && api.Creates == 1, "retry verifies once and updates instead of creating a duplicate");
            api.Submitted(null, false);
            assert(success && !operations.Busy && !session.NeedsPersistence, "successful retry releases lease");

            api = new Steam(); queue = new Queue();
            session = new PublishSession(0, null, new Publisher(api, queue.Schedule), operations, queue.Schedule);
            session.Build(content, () => {}, error => {}); queue.Next();
            session.Publish(request, 100, (id, draft) => {}, (message, ok) => success = ok);
            queue.Next();
            File.WriteAllText(Path.Combine(root, "source.txt"), "edited during creation");
            api.Created(502, null, false);
            assert(session.Busy && api.Submits == 0, "new item waits for a second background check after creation");
            queue.Next();
            assert(!success && api.Submits == 0 && session.ItemId == 502 && !operations.Busy, "edits during item creation prevent submission without losing the ID");

            string mixed = Path.Combine(directory, "mixed-plan"); Directory.CreateDirectory(mixed);
            File.WriteAllText(Path.Combine(mixed, "level.xnb"), "compiled collision");
            assert(BuildPlan.Inspect(mixed, "Level").Intent == BuildIntent.PreservePackage, "compiled-only maps preserve content");
            File.WriteAllText(Path.Combine(mixed, "preview.png"), "preview");
            assert(BuildPlan.Inspect(mixed, "Level").Intent == BuildIntent.PreservePackage, "preview image does not make a compiled map editable");
            File.WriteAllText(Path.Combine(mixed, "visual_level.png"), "source collision");
            assert(BuildPlan.Inspect(mixed, "Level").Intent == BuildIntent.CompileSources, "editable mixed map uses the common compilation plan");
            string mod = Path.Combine(directory, "mod-plan"); Directory.CreateDirectory(mod);
            assert(BuildPlan.Inspect(mod, "Mod").Intent == BuildIntent.BuildMod, "compiled mods always use package validation");
            File.WriteAllText(Path.Combine(mod, "Example.csproj"), "<Project/>");
            assert(BuildPlan.Inspect(mod, "Mod").Intent == BuildIntent.BuildMod, "source mods share the mod build workflow");

            string input = Path.Combine(directory, "install-input"), installed = Path.Combine(directory, "install-target"), backup = Path.Combine(directory, "install-rollback");
            Directory.CreateDirectory(input); Directory.CreateDirectory(installed);
            foreach (string name in new[] { "a.txt", "b.txt", "c.txt" })
            {
                File.WriteAllText(Path.Combine(input, name), "new"); File.WriteAllText(Path.Combine(installed, name), "old");
            }
            var restored = new List<string>();
            Reject<IOException>(() => ModInstallation.Install(input, installed, backup, text => {}, (source, destination) =>
            {
                if (source.StartsWith(backup, StringComparison.OrdinalIgnoreCase))
                {
                    restored.Add(Path.GetFileName(source));
                    if (Path.GetFileName(source) == "b.txt") throw new IOException("restore blocked");
                }
                else if (Path.GetFileName(source) == "c.txt") throw new IOException("install failed");
                File.Copy(source, destination, true);
            }), assert, "rollback reports a restoration failure");
            assert(restored.Contains("a.txt") && restored.Contains("b.txt") && File.ReadAllText(Path.Combine(installed, "a.txt")) == "old", "rollback continues restoring earlier files after one restore fails");

            var target = new Overloads();
            assert((string)NativeMembers.Method(typeof(Overloads), "Call", typeof(string)).Invoke(target, new object[] { "value" }) == "string", "native adapter selects exact overload");
            Reject<MissingMethodException>(() => NativeMembers.Method(typeof(Overloads), "Missing", typeof(string)), assert, "missing native contract reports a member error");
            Reject<MissingMethodException>(() => NativeMembers.Method(typeof(Overloads), "Call", new Type[] { null }), assert, "ambiguous null overload requires an explicit signature");
        }
        sealed class Overloads
        {
            public string Call(string value) { return "string"; }
            public string Call(object value) { return "object"; }
        }
    }
}

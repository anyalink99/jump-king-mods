using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using EntityComponent;
using JKRuntime.Gameplay;
using JKRuntime.Settings;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace JKRuntime
{
    internal static class LifecycleSafetyTests
    {
        private sealed class Behaviour : IBodyCompBehaviour
        {
            internal bool FailComparison;
            public bool ExecuteBehaviour(BehaviourContext context) { return true; }
            public override bool Equals(object other)
            {
                if (FailComparison) throw new InvalidOperationException("comparison failure");
                return ReferenceEquals(this, other);
            }
            public override int GetHashCode() { return base.GetHashCode(); }
        }

        private static void Check(bool value, string message)
        { if (!value) throw new Exception(message); }

        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new Exception("Expected " + typeof(T).Name);
        }

        private static void RejectWorker(Action action)
        {
            Exception failure = null;
            var worker = new Thread(delegate() {
                try { action(); } catch (Exception error) { failure = error; }
            });
            worker.Start();
            Check(worker.Join(5000), "Worker did not finish");
            Check(failure is InvalidOperationException, "Off-thread operation was not rejected");
        }

        private static void BodyLifetime()
        {
            var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
            var anchor = new Behaviour();
            var list = new LinkedList<IBodyCompBehaviour>();
            list.AddLast(anchor);
            typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(body, list);
            var pipeline = new BodyPipeline(body, false);
            var healthy = new Behaviour();
            var failing = new Behaviour();
            Throws<ArgumentOutOfRangeException>(() => pipeline.Register((BodyPhase)999, healthy));
            Check(list.Count == 1, "Invalid phase changed the body");
            RejectWorker(() => new BodyPipeline(body, false));
            RejectWorker(() => pipeline.RegisterBefore(healthy, anchor));
            Check(pipeline.RegisterBefore(healthy, anchor), "Healthy registration failed");
            Check(pipeline.RegisterBefore(failing, anchor), "Failing fixture registration failed");
            RejectWorker(() => pipeline.Remove(healthy));
            RejectWorker(pipeline.Dispose);
            failing.FailComparison = true;
            var error = Throws<AggregateException>(pipeline.Dispose);
            Check(error.Flatten().InnerExceptions.Any(e => e.Message == "comparison failure"),
                "Cleanup failure was hidden");
            Check(!list.Any(b => ReferenceEquals(b, healthy)), "Failure prevented unrelated cleanup");
            Check(list.Any(b => ReferenceEquals(b, failing)), "Failed release lost its native registration");
            Throws<ObjectDisposedException>(() => pipeline.RegisterBefore(new Behaviour(), anchor));
            failing.FailComparison = false;
            pipeline.Dispose();
            pipeline.Dispose();
            Check(list.Count == 1 && ReferenceEquals(list.First.Value, anchor), "Retry did not restore the body");
            Throws<ObjectDisposedException>(() => pipeline.RegisterBefore(new Behaviour(), anchor));
        }

        private static void JumpThreadOwnership()
        {
            var result = new JumpResult("test.jump", JumpEvidence.Unavailable, null,
                null, null, null, true, false, 0f, -1f);
            int notifications = 0;
            IDisposable lease = JumpEvents.Subscribe(r => notifications++);
            RejectWorker(() => JumpEvents.Subscribe(r => { }));
            RejectWorker(() => JumpEvents.Publish(result));
            RejectWorker(lease.Dispose);
            Check(notifications == 0, "Worker delivered an event");
            Throws<ArgumentNullException>(() => JumpEvents.Publish(null));
            JumpEvents.Publish(result);
            Check(notifications == 1, "Rejected disposal lost the live listener");
            lease.Dispose();
            lease.Dispose();
            JumpEvents.Publish(result);
            Check(notifications == 1, "Disposed listener received an event");
        }

        private static void CommandCancellation(bool failDuringDrain)
        {
            var manager = new EntityManager();
            Commands.Start();
            int cancelled = 0, ran = 0;
            if (failDuringDrain)
                Commands.Enqueue(() => { throw new InvalidOperationException("command failure"); });
            Commands.Enqueue(() => ran++, () => {
                cancelled++;
                Throws<InvalidOperationException>(() => Commands.Enqueue(() => ran++));
                Throws<InvalidOperationException>(Commands.Start);
                Throws<InvalidOperationException>(Commands.Stop);
                throw new InvalidOperationException("first cancellation failure");
            });
            Commands.Enqueue(() => ran++, () => { cancelled++; });
            Commands.Enqueue(() => ran++, () => {
                cancelled++;
                throw new InvalidOperationException("last cancellation failure");
            });
            if (failDuringDrain)
            {
                Commands.Drain();
                Check(Commands.LastError.Contains("command failure"), "Original command failure was lost");
                Commands.Stop();
            }
            else Throws<AggregateException>(Commands.Stop);
            Check(cancelled == 3 && ran == 0, "Cancellation stopped early or executed pending work");
            Check(Commands.LastError.Contains("first cancellation failure")
                && Commands.LastError.Contains("last cancellation failure"), "Cancellation errors were lost");
            Check(!manager.Entities.Any(e => e.IsAlive), "Failed cancellation left the command pump alive");
            Commands.Stop();
            Commands.Drain();
            Check(cancelled == 3 && ran == 0, "Cancelled work was retained or cancelled twice");
            Throws<InvalidOperationException>(() => Commands.Enqueue(() => ran++));
            Throws<InvalidOperationException>(Commands.Start);
        }

        public static int Main(string[] args)
        {
            try
            {
                // Command failures intentionally poison the process. Exercise the
                // drain path in a separate invocation of this same executable.
                RuntimeApi.Kernel.CheckThread();
                BodyLifetime();
                JumpThreadOwnership();
                CommandCancellation(args.Length != 0 && args[0] == "drain");
                Console.WriteLine("[OK] Body cleanup retry, invalid phases, game-thread events and exhaustive command cancellation");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}

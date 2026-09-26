using System;

namespace JKRuntime
{
    // One owner per SDK module. Preparation never publishes gameplay services.
    internal sealed class PreparationLifetime
    {
        private readonly string id;
        private readonly string worldStage, attemptStage;
        private readonly Action<RuntimeScope> worldReady, beforeAttempt;
        private RuntimeScope world, attempt;
        private bool worldReadyCompleted, attemptPrepared;
        internal Exception Error { get; private set; }
        internal object Inspect()
        { return new { module = id, worldReady = worldReadyCompleted, attemptPrepared = attemptPrepared, attemptResources = attempt != null,
            worldResources = world != null, error = Error == null ? null : Error.ToString() }; }

        internal PreparationLifetime(string owner, Action<RuntimeScope> prepareWorld, Action<RuntimeScope> prepareAttempt)
        {
            id = owner; worldStage = "module.world-ready:" + owner; attemptStage = "module.prepare-attempt:" + owner;
            worldReady = prepareWorld; beforeAttempt = prepareAttempt;
        }
        internal void EnsurePrepared() { if (!attemptPrepared) Prepare(); }

        internal void Prepare()
        {
            Error = null;
            try
            {
                ReleaseAttempt();
                // A failed world cleanup must finish before replacing its resources.
                if (!worldReadyCompleted && world != null) ReleaseWorld();
                if (!worldReadyCompleted && worldReady != null)
                {
                    world = new RuntimeScope();
                    using (StartupTrace.Measure(worldStage)) worldReady(world);
                    worldReadyCompleted = true;
                }
                if (beforeAttempt != null)
                {
                    attempt = new RuntimeScope();
                    using (StartupTrace.Measure(attemptStage)) beforeAttempt(attempt);
                }
            }
            catch (Exception failure)
            {
                Error = failure;
                try { ReleaseAttempt(); if (!worldReadyCompleted) ReleaseWorld(); }
                catch (Exception cleanup) { Error = new AggregateException("Preparation and rollback failed", failure, cleanup); }
            }
            finally { attemptPrepared = true; }
        }

        internal void ThrowIfFailed()
        { if (Error != null) throw new InvalidOperationException("Module preparation failed: " + id, Error); }

        internal void ReleaseAttempt()
        { attemptPrepared = false; if (attempt != null) { attempt.Dispose(); attempt = null; } }

        internal void ReleaseWorld()
        {
            // Keep failed scopes available for retry, including a cancelled intro.
            worldReadyCompleted = false;
            ReleaseAttempt();
            if (world != null) { world.Dispose(); world = null; }
        }
    }
}

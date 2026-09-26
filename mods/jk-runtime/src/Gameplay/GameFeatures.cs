using System;

namespace JKRuntime.Gameplay
{
    public enum MovementMode { Vanilla, AirControl, VariableJump }
    public interface IPlayerForm
    {
        bool Morphed { get; }
        bool Attached { get; }
        int JumpSequence { get; }
        bool OwnsSprite(object sprite);
    }
    public interface IAirThrust { bool Enabled { get; } bool Active { get; } }
    public interface IMovementRules { MovementMode Mode { get; } }

    public static class GameFeatures
    {
        private static IPlayerForm form;
        private static IAirThrust thrust;
        private static IMovementRules movement;
        public static bool IsMorphed { get { RuntimeApi.Kernel.CheckThread(); return form != null && form.Morphed; } }
        public static bool IsAttached { get { RuntimeApi.Kernel.CheckThread(); return form != null && form.Attached; } }
        public static int FormJumpSequence { get { RuntimeApi.Kernel.CheckThread(); return form == null ? 0 : form.JumpSequence; } }
        public static bool FormOwnsSprite(object sprite) { RuntimeApi.Kernel.CheckThread(); return form != null && form.OwnsSprite(sprite); }
        public static bool ThrustEnabled { get { RuntimeApi.Kernel.CheckThread(); return thrust != null && thrust.Enabled; } }
        public static bool ThrustActive { get { RuntimeApi.Kernel.CheckThread(); return thrust != null && thrust.Active; } }
        public static MovementMode Movement { get { RuntimeApi.Kernel.CheckThread(); return movement == null ? MovementMode.Vanilla : movement.Mode; } }
        public static IDisposable RegisterForm(IPlayerForm provider)
        {
            RuntimeApi.Kernel.CheckThread();
            if (provider == null || form != null) throw new InvalidOperationException("Player form is already owned or invalid");
            form = provider; return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); if (form != provider) throw new InvalidOperationException("Form ownership lost"); form = null; });
        }
        public static IDisposable RegisterThrust(IAirThrust provider)
        {
            RuntimeApi.Kernel.CheckThread();
            if (provider == null || thrust != null) throw new InvalidOperationException("Air thrust is already owned or invalid");
            thrust = provider; return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); if (thrust != provider) throw new InvalidOperationException("Thrust ownership lost"); thrust = null; });
        }
        public static IDisposable RegisterMovement(IMovementRules provider)
        {
            RuntimeApi.Kernel.CheckThread();
            if (provider == null || movement != null) throw new InvalidOperationException("Movement rules are already owned or invalid");
            movement = provider; return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); if (movement != provider) throw new InvalidOperationException("Movement ownership lost"); movement = null; });
        }
    }

    // Native charge decoration is a single owned role, separate from a
    // replacement controller. Changes release it before editing the native tree.
    public static class JumpSlot
    {
        private static Action install, uninstall;
        private static Action controllerInstall, controllerUninstall;
        private static Func<bool> controllerPermitsCharge;
        private static bool controllerAttached, controllerBlocksCharge;
        private static bool busy, poisoned, attached, inPolicy;
        private static readonly System.Collections.Generic.HashSet<object> suspensions = new System.Collections.Generic.HashSet<object>();

        /// <summary>True while a controller has reserved jumping without native charge decoration.</summary>
        public static bool ChargePolicySuspended { get { RuntimeApi.Kernel.CheckThread(); return suspensions.Count != 0 || controllerBlocksCharge; } }

        /// <summary>Hold while replacing native jumping. Acquire before graph edits and release after restoring them,
        /// inside Recompose. Independent leases compose; registration/settings changes cannot resume charge until
        /// the last lease is released. Outside Recompose, acquisition/release performs composition automatically.</summary>
        public static IDisposable SuspendChargePolicy(string owner)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (poisoned || inPolicy) throw new InvalidOperationException("Charge suspension cannot change during a policy callback or after failed cleanup");
            object token = new object();
            Action add = delegate { suspensions.Add(token); };
            if (busy) add(); else Recompose(add);
            return new ActionLease(delegate {
                RuntimeApi.Kernel.CheckThread();
                if (poisoned || inPolicy) throw new InvalidOperationException("Unsafe charge suspension release");
                Action remove = delegate { suspensions.Remove(token); };
                if (busy) remove(); else Recompose(remove);
            });
        }

        private static void Detach()
        {
            inPolicy = true;
            try
            {
                if (attached) { uninstall(); attached = false; }
                if (controllerAttached) { controllerUninstall(); controllerAttached = controllerBlocksCharge = false; }
            }
            catch { poisoned = true; throw; }
            finally { inPolicy = false; }
        }

        private static void Attach()
        {
            if (suspensions.Count != 0) return;
            inPolicy = true;
            try
            {
                if (controllerInstall != null && !controllerAttached)
                {
                    try { controllerInstall(); controllerAttached = true; controllerBlocksCharge = controllerPermitsCharge != null && !controllerPermitsCharge(); }
                    catch (Exception acquireError)
                    {
                        try { controllerUninstall(); controllerAttached = controllerBlocksCharge = false; }
                        catch (Exception releaseError) { poisoned = true; throw new AggregateException("Controller policy acquisition and cleanup failed", acquireError, releaseError); }
                        throw;
                    }
                }
                if (controllerBlocksCharge || install == null || attached) return;
                try { install(); attached = true; }
                catch (Exception acquireError)
                {
                    try { uninstall(); }
                    catch (Exception releaseError) { poisoned = true; throw new AggregateException("Charge policy acquisition and cleanup failed", acquireError, releaseError); }
                    throw;
                }
            }
            finally { inPolicy = false; }
        }
        public static IDisposable RegisterChargePolicy(Action acquire, Action release)
        {
            RuntimeApi.Kernel.CheckThread();
            if (acquire == null || release == null || install != null || busy || poisoned)
                throw new InvalidOperationException("Native charge policy cannot be acquired");
            install = acquire; uninstall = release;
            busy = true;
            try { Detach(); Attach(); }
            catch { install = uninstall = null; throw; }
            finally { busy = false; }
            return new ActionLease(delegate {
                RuntimeApi.Kernel.CheckThread();
                if (busy) throw new InvalidOperationException("Charge policy release during composition");
                busy = true;
                try { Detach(); } finally { busy = false; }
                install = uninstall = null;
                Attach();
            });
        }
        /// <summary>Own a reversible base controller policy below native charge decoration. Exclusive controller reservations suspend both policies; changes restore the base before decorating charge.</summary>
        public static IDisposable RegisterControllerPolicy(Action acquire, Action release)
        { return RegisterControllerPolicy(acquire, release, null); }
        /// <summary>A base controller that removes native jumping can explicitly suspend charge decoration. The predicate is evaluated after acquisition, never on the gameplay hot path.</summary>
        public static IDisposable RegisterControllerPolicy(Action acquire, Action release, Func<bool> permitsCharge)
        {
            RuntimeApi.Kernel.CheckThread();
            if (acquire == null || release == null || controllerInstall != null || busy || poisoned)
                throw new InvalidOperationException("Base controller policy cannot be acquired");
            try { Recompose(delegate { controllerInstall = acquire; controllerUninstall = release; controllerPermitsCharge = permitsCharge; }); }
            catch (Exception failure)
            {
                if (poisoned) throw; // Keep uncertain ownership for diagnostics; never overwrite the graph.
                // A failed charge decorator may follow a successful base install.
                // Remove that base before dropping its callbacks, then restore the previous policy.
                try
                {
                    Recompose(delegate { controllerInstall = controllerUninstall = null; controllerPermitsCharge = null; });
                }
                catch (Exception cleanup) { throw new AggregateException("Base registration and previous-policy restoration failed", failure, cleanup); }
                throw;
            }
            return new ActionLease(delegate { Recompose(delegate { controllerInstall = controllerUninstall = null; controllerPermitsCharge = null; }); });
        }
        public static void Recompose(Action changeController)
        {
            RuntimeApi.Kernel.CheckThread();
            if (changeController == null) throw new ArgumentNullException("changeController");
            if (busy || poisoned) throw new InvalidOperationException("Unsafe jump composition; restart after a cleanup failure");
            busy = true;
            try
            {
                Detach();
                Exception changeError = null;
                try { changeController(); } catch (Exception error) { changeError = error; }
                try { Attach(); }
                catch (Exception attachError)
                {
                    if (changeError != null) throw new AggregateException("Controller change and charge policy restoration failed", changeError, attachError);
                    throw;
                }
                if (changeError != null) throw new AggregateException("Controller change failed; charge policy restored or suspended", changeError);
            }
            finally { busy = false; }
        }
        public static void Refresh() { Recompose(delegate { }); }
    }
}

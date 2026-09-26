using System;
using JKRuntime;
using JKRuntime.Gameplay;

namespace HammerKing
{
    internal static class HammerInstaller
    {
        private static HammerController controller;
        private sealed class Lifetime : IDisposable { public void Dispose() { Uninstall(); } }

        internal static void Register(JKRuntime.ModuleContext context)
        {
            context.Track(new Lifetime());
            context.Track(RuntimeApi.Mechanics.Register("more-items",
                new MechanicDefinition("more-items.hammer", new Version(1, 0),
                    MechanicEffects.Movement | MechanicEffects.Collision | MechanicEffects.Presentation,
                    MechanicComposition.Exclusive, "player.controller", null, "hammer-king.state"),
                delegate { return new MechanicState(true, MoreItems.HammerDefinition.IsEnabledForPlayer(), controller != null,
                    MechanicSource.Equipment, controller == null ? "Hammer unequipped" : "Hammer equipped; mouse control active"); }));
        }

        internal static void Apply()
        {
            JumpSlot.Recompose(delegate
            {
                if (!MoreItems.HammerDefinition.IsEnabledForPlayer()) { Remove(); return; }
                if (controller != null) return;
                var player = EntityComponent.EntityManager.instance.Find<JumpKing.Player.PlayerEntity>();
                if (player == null) throw new InvalidOperationException("Hammer requires a live player");
                controller = HammerController.Install(player);
            });
        }

        private static void Remove()
        {
            if (controller == null) return;
            controller.Dispose(); controller = null;
        }

        internal static void Uninstall() { JumpSlot.Recompose(Remove); }
    }
}

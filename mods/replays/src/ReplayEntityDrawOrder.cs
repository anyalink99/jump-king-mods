using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;

namespace Replays
{
    internal static class ReplayEntityDrawOrder
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo EntitiesField =
            typeof(EntityManager).GetField("entities", InstanceMembers);
        private static readonly FieldInfo EntitiesLockField =
            typeof(EntityManager).GetField("entitiesLock", InstanceMembers);

        internal static void ValidateContract()
        {
            if (EntitiesField == null || EntitiesLockField == null)
                throw new InvalidOperationException(
                    "Jump King entity draw-order contract is unavailable");
        }

        internal static void PlaceImmediatelyBefore(
            Entity underlay,
            Entity overlay)
        {
            if (underlay == null) throw new ArgumentNullException("underlay");
            if (overlay == null) throw new ArgumentNullException("overlay");
            ValidateContract();
            EntityManager manager = EntityManager.instance;
            List<Entity> entities = manager == null
                ? null
                : EntitiesField.GetValue(manager) as List<Entity>;
            object sync = manager == null
                ? null
                : EntitiesLockField.GetValue(manager);
            if (entities == null || sync == null)
                throw new InvalidOperationException(
                    "Jump King entity list is unavailable");

            lock (sync)
            {
                int underlayIndex = entities.IndexOf(underlay);
                int overlayIndex = entities.IndexOf(overlay);
                if (underlayIndex < 0 || overlayIndex < 0)
                    throw new InvalidOperationException(
                        "Could not place the racing ghost below the player");
                if (underlayIndex + 1 == overlayIndex) return;
                entities.RemoveAt(underlayIndex);
                overlayIndex = entities.IndexOf(overlay);
                entities.Insert(overlayIndex, underlay);
            }
        }
    }
}

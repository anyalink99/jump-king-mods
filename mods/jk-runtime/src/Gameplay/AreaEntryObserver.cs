using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using JumpKing;

namespace JKRuntime.Gameplay
{
    public sealed class AreaEntryObserver
    {
        private const string ComponentTypeName =
            "JumpKing.MiscSystems.LocationText.LocationComp";

        private static readonly Type ComponentType =
            typeof(Game1).Assembly.GetType(ComponentTypeName);
        private static readonly MethodInfo UpdateCurrentLocationMethod =
            GetMethod("UpdateCurrentLocation");
        private static readonly MethodInfo CheckIfNewScreenMethod =
            GetMethod("CheckIfNewScreen");
        private static readonly FieldInfo NewScreenAvailableField =
            GetField("m_new_screen_available");

        private Component component;

        public static void ValidateContract()
        {
            if (ComponentType == null
                || UpdateCurrentLocationMethod == null
                || CheckIfNewScreenMethod == null
                || NewScreenAvailableField == null)
            {
                throw new MissingMemberException(
                    "Jump King area-transition contract is unavailable");
            }
        }

        public void Update()
        {
            if (component == null)
            {
                component = FindComponent();
            }
            if (component == null)
            {
                return;
            }

            UpdateCurrentLocationMethod.Invoke(component, null);
            bool entered = (bool)CheckIfNewScreenMethod.Invoke(
                component,
                null);
            if (entered)
            {
                NewScreenAvailableField.SetValue(component, true);
            }
        }

        private static Component FindComponent()
        {
            EntityManager manager = EntityManager.instance;
            if (manager == null)
            {
                return null;
            }
            IReadOnlyList<Entity> entities = manager.Entities;
            for (int entityIndex = 0;
                entityIndex < entities.Count;
                entityIndex++)
            {
                Component[] components = entities[entityIndex]
                    .GetComponents();
                for (int componentIndex = 0;
                    componentIndex < components.Length;
                    componentIndex++)
                {
                    if (ComponentType.IsInstanceOfType(
                        components[componentIndex]))
                    {
                        return components[componentIndex];
                    }
                }
            }
            return null;
        }

        private static MethodInfo GetMethod(string name)
        {
            MethodInfo method = ComponentType == null
                ? null
                : ComponentType.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            return method;
        }

        private static FieldInfo GetField(string name)
        {
            FieldInfo field = ComponentType == null
                ? null
                : ComponentType.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            return field;
        }
    }
}

using System;
using System.Collections.Generic;

namespace JKRuntime.UI
{
    public sealed class UiRegistrationScope : IDisposable
    {
        private readonly string ownerId;
        private readonly List<Action> unregister = new List<Action>();
        private bool disposed, closing, releasing;

        public UiRegistrationScope(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner id is required", "owner");
            ownerId = owner.Trim();
        }

        public string OwnerId { get { return ownerId; } }

        public void RegisterBinding(UiBindingDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "binding",
                delegate { UIApi.RegisterBinding(definition); },
                UIApi.UnregisterBinding);
        }

        public void RegisterInteraction(WorldInteraction definition)
        {
            Register(definition == null ? null : definition.Id,
                "interaction",
                delegate { UIApi.RegisterInteraction(definition); },
                UIApi.UnregisterInteraction);
        }

        public void RegisterInputAction(UiInputActionDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "input",
                delegate { UIApi.RegisterInputAction(definition); },
                UIApi.UnregisterInputAction);
        }

        public void RegisterDebugAction(UiDebugActionDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "debug",
                delegate { UIApi.RegisterDebugAction(definition); },
                UIApi.UnregisterDebugAction);
        }

        public void RegisterCurrency(UiCurrencyDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "currency",
                delegate { UIApi.RegisterCurrency(definition); },
                UIApi.UnregisterCurrency);
        }

        public void RegisterMerchantOffer(MerchantOfferDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "offer",
                delegate { UIApi.RegisterMerchantOffer(definition); },
                UIApi.UnregisterMerchantOffer);
        }

        public void RegisterMerchant(MerchantDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "interaction",
                delegate { UIApi.RegisterMerchant(definition); },
                UIApi.UnregisterMerchant);
        }

        public void RegisterInventoryItem(UiInventoryItemDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "inventory-item",
                delegate { UIApi.RegisterInventoryItem(definition); },
                UIApi.UnregisterInventoryItem);
        }

        public void RegisterMainMenuItem(UiMainMenuItemDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "main-menu-item",
                delegate { UIApi.RegisterMainMenuItem(definition); },
                UIApi.UnregisterMainMenuItem);
        }

        public void RegisterPauseMenuItem(UiPauseMenuItemDefinition definition)
        {
            Register(definition == null ? null : definition.Id,
                "pause-menu-item",
                delegate { UIApi.RegisterPauseMenuItem(definition); },
                UIApi.UnregisterPauseMenuItem);
        }

        public void RegisterModSettingLabel(string settingId, string label)
        {
            Register(
                settingId,
                "setting-label",
                delegate { UIApi.RegisterModSettingLabel(settingId, label); },
                UIApi.UnregisterModSettingLabel);
        }

        public void Dispose()
        {
            if (disposed) return;
            if (releasing) throw new InvalidOperationException("Reentrant UI registration cleanup");
            closing = releasing = true;
            var errors = new List<Exception>();
            try
            {
            for (int i = unregister.Count - 1; i >= 0; i--)
            {
                try { unregister[i](); unregister.RemoveAt(i); }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            }
            finally { releasing = false; }
            if (errors.Count != 0) throw new AggregateException("UI registration cleanup incomplete: " + ownerId, errors);
            disposed = true;
        }

        private void Register(string id, string category, Action register, Action<string> remove)
        {
            if (closing) throw new ObjectDisposedException("UiRegistrationScope");
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Registration id is required", "id");
            register();
            string captured = id;
            object token = UIApi.SetRegistrationOwner(category, captured, ownerId);
            unregister.Add(
                delegate
                {
                    if (UIApi.IsRegistrationOwner(category, captured, token)) remove(captured);
                });
        }
    }
}

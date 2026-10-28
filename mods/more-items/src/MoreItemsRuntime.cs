namespace MoreItems
{
    internal static class MoreItemsRuntime
    {
        private static BargainburgMerchantGuard merchant;
        private static bool installed;

        internal static void Install()
        {
            Uninstall();
            installed = true;
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.inventory-activate")) ItemInventory.Reload();
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.item-modules")) ItemModuleRegistry.StartRuntime();
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.world-items")) ItemWorldLoader.Load();
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.merchant")) InstallMerchant();
        }

        internal static void Uninstall()
        {
            installed = false;
            var release = new JKRuntime.RuntimeScope();
            release.Defer(delegate { if (merchant != null && merchant.IsAlive) merchant.Destroy(); merchant = null; });
            release.Defer(MoreItemsApi.ClearWorldObjects);
            release.Defer(ItemModuleRegistry.StopRuntime);
            release.Dispose();
        }

        internal static bool MerchantAvailable()
        {
            return installed
                && merchant != null
                && merchant.IsAvailable();
        }

        private static void InstallMerchant()
        {
            if (merchant != null && merchant.IsAlive) merchant.Destroy();
            merchant = installed
                ? new BargainburgMerchantGuard(new Microsoft.Xna.Framework.Rectangle(10, -4761, 80, 50))
                : null;
        }
    }
}

using JKRuntime.Modules;
using Microsoft.Xna.Framework;
using MoreItems;

namespace MoreItemsExample
{
    [RuntimeModule("example.items", "More Items Example", After = new[] { "more-items" })]
    public static class ExampleEntry
    {
        private static bool enabled = true;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            MoreItemsApi.RegisterModule(new ItemModuleDefinition(
                "example.token",
                "Token",
                delegate { return enabled; },
                delegate(bool value) { enabled = value; }));
            MoreItemsApi.Register(new ConsumableDefinition(
                "example.token",
                "Token",
                "A persistent example currency.",
                null,
                null,
                new Color(244, 194, 58),
                "Tokens",
                null,
                null,
                true,
                null,
                delegate { return enabled; }));
            MoreItemsApi.RegisterCurrency("example.tokens", "example.token", 1);
        }
    }
}

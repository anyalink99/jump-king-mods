namespace JumpKingJetpack
{
    internal static class ThrustState
    {
        public static bool Enabled
        {
            get
            {
                return MoreItems.JetpackDefinition.IsEnabledForPlayer();
            }
        }

        public static bool Active { get; private set; }

        internal static void SetActive(bool active)
        {
            Active = active;
        }
    }
}

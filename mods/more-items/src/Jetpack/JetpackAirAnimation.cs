namespace JumpKingJetpack
{
    internal enum JetpackAirSprite
    {
        Up,
        Fall
    }

    internal sealed class JetpackAirAnimation
    {
        internal JetpackAirSprite Select(float verticalVelocity)
        {
            if (verticalVelocity <= 0f)
            {
                return JetpackAirSprite.Up;
            }
            return JetpackAirSprite.Fall;
        }
    }
}

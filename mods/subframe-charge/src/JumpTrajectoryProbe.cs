using JKRuntime.Input;
using EntityComponent;
using JumpKing.Player;

namespace SubframeCharge
{
    internal sealed class JumpTrajectoryProbe : Component
    {
        private readonly BodyComp body;
        private bool active;
        private bool sawUpwardVelocity;
        private int jumpNumber;
        private float chargeStep;
        private int lateUpdateFrames;
        private float startY;
        private float minimumY;
        private float launchVelocityY;
        private float previousVelocityY;
        private string launchMode;

        internal JumpTrajectoryProbe(BodyComp playerBody)
        {
            body = playerBody;
            previousVelocityY = body == null ? 0f : body.Velocity.Y;
        }

        internal void RecordLaunch(float step, float velocityY)
        {
            BeginJump("precision", step, velocityY);
        }

        private void BeginJump(string mode, float step, float velocityY)
        {
            jumpNumber++;
            launchMode = mode;
            chargeStep = step;
            lateUpdateFrames = 0;
            startY = body.Position.Y;
            minimumY = startY;
            launchVelocityY = velocityY;
            sawUpwardVelocity = velocityY < 0f;
            active = true;
        }

        protected override void LateUpdate(float delta)
        {
            JumpPercentIntegration.EnsureDisplayHook();
            if (body == null || !body.Enabled)
            {
                return;
            }

            if (!active
                && previousVelocityY >= 0f
                && body.Velocity.Y < 0f)
            {
                BeginJump("vanilla", 0, body.Velocity.Y);
            }
            previousVelocityY = body.Velocity.Y;
            if (!active)
            {
                return;
            }

            lateUpdateFrames++;
            if (body.Position.Y < minimumY)
            {
                minimumY = body.Position.Y;
            }
            if (body.Velocity.Y < 0f)
            {
                sawUpwardVelocity = true;
                return;
            }
            if (!sawUpwardVelocity)
            {
                return;
            }

            DiagnosticLog.Write(
                "trajectory jump=" + jumpNumber
                + " mode=" + launchMode
                + " step=" + (chargeStep == 0
                    ? "unknown"
                    : chargeStep.ToString())
                + " launchVelocityY=" + launchVelocityY.ToString("F6")
                + " startY=" + startY.ToString("F6")
                + " apexY=" + minimumY.ToString("F6")
                + " rise=" + (startY - minimumY).ToString("F6")
                + " framesToApex=" + lateUpdateFrames);
            active = false;
        }
    }
}

namespace JumpKingJetpack
{
    internal enum JetpackFlamePhase
    {
        Off,
        Ignition,
        Sustained,
        Shutdown,
    }

    internal sealed class JetpackFlameAnimation
    {
        internal const int TransitionUpdatesPerFrame = 3;
        internal const int SustainedUpdatesPerFrame = 5;

        private JetpackFlamePhase phase;
        private int frame;
        private int update;

        internal JetpackFlamePhase Phase
        {
            get { return phase; }
        }

        internal int Frame
        {
            get { return frame; }
        }

        internal bool Visible
        {
            get { return phase != JetpackFlamePhase.Off; }
        }

        internal void Update(bool thrustActive)
        {
            if (thrustActive)
            {
                UpdateActive();
            }
            else
            {
                UpdateInactive();
            }
        }

        private void UpdateActive()
        {
            if (phase == JetpackFlamePhase.Off
                || phase == JetpackFlamePhase.Shutdown)
            {
                phase = JetpackFlamePhase.Ignition;
                frame = 0;
                update = 0;
                return;
            }

            update++;
            if (phase == JetpackFlamePhase.Ignition)
            {
                if (update < TransitionUpdatesPerFrame)
                {
                    return;
                }
                update = 0;
                frame++;
                if (frame >= JetpackFlameData.IgnitionFrameCount)
                {
                    phase = JetpackFlamePhase.Sustained;
                    frame = 0;
                }
                return;
            }

            if (update >= SustainedUpdatesPerFrame)
            {
                update = 0;
                frame = (frame + 1) % JetpackFlameData.FrameCount;
            }
        }

        private void UpdateInactive()
        {
            if (phase == JetpackFlamePhase.Off)
            {
                return;
            }
            if (phase != JetpackFlamePhase.Shutdown)
            {
                phase = JetpackFlamePhase.Shutdown;
                frame = 0;
                update = 0;
                return;
            }

            update++;
            if (update < TransitionUpdatesPerFrame)
            {
                return;
            }
            update = 0;
            frame++;
            if (frame >= JetpackFlameData.ShutdownFrameCount)
            {
                phase = JetpackFlamePhase.Off;
                frame = 0;
            }
        }
    }
}

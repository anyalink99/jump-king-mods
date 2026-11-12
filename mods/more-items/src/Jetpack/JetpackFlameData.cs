using System;

namespace JumpKingJetpack
{
    internal static class JetpackFlameData
    {
        internal const int Width = 11;
        internal const int Height = 21;
        internal const int FrameCount = 4;
        internal const int IgnitionFrameCount = 2;
        internal const int ShutdownFrameCount = 2;

        private static readonly string[][] IgnitionFrames =
        {
            new[]
            {
                "....ywy....",
                "...oywyo...",
                "...oyyyo...",
                "....oyo....",
                "....oro....",
                ".....r.....",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
            },
            new[]
            {
                "....ywy....",
                "...oywyo...",
                "..ooyyyoo..",
                "...oyyyo...",
                "...ooyoo...",
                "..rooyoor..",
                "...rooor...",
                "...rroor...",
                "....roor...",
                "....rrr....",
                ".....rr....",
                ".....r.....",
                ".....r.....",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
            },
        };

        private static readonly string[][] Frames =
        {
            new[]
            {
                "....ywy....",
                "...oywyo...",
                "...oywyo...",
                "...oyyyo...",
                "..ooyyyoo..",
                "...oyyyo...",
                "...ooyoo...",
                "..rooyoor..",
                "...rooor...",
                "...rooor...",
                "..rrooor...",
                "...rroor...",
                "...rroor...",
                "....rorr...",
                "....rrr....",
                "....rrr....",
                ".....rr....",
                ".....r.....",
                ".....r.....",
                "...........",
                "...........",
            },
            new[]
            {
                "...oywyo...",
                "...oywyo...",
                "..ooywyoo..",
                "...oyyyo...",
                "...oyyyo...",
                "..ooyyyo...",
                "...ooyoo...",
                "...rooor...",
                "..rrooorr..",
                "...rooor...",
                "...rroor...",
                "....roor...",
                "...rroor...",
                "....rror...",
                "....rrr....",
                ".....rr....",
                "....rr.....",
                ".....r.....",
                "....r......",
                "...........",
                "...........",
            },
            new[]
            {
                "....ywy....",
                "...oywyo...",
                "..oywwwyo..",
                "...oywyo...",
                "..ooyyyoo..",
                "..ooyyyoo..",
                "...ooyoo...",
                "..roooyoor.",
                "...rooor...",
                "..rrooor...",
                "...rroorr..",
                "...rroor...",
                "....roor...",
                "...rroor...",
                "....rrr....",
                "....rr.....",
                ".....rr....",
                "....rr.....",
                ".....r.....",
                ".....r.....",
                "...........",
            },
            new[]
            {
                "...oywyo...",
                "...oywyo...",
                "...oywyo...",
                "..ooyyyoo..",
                "...oyyyo...",
                "..ooyyyoo..",
                "...ooyoo...",
                "...rooor...",
                "..rrooor...",
                "...rooor...",
                "..rrooor...",
                "...rroor...",
                "...rroor...",
                "....rror...",
                "...rrr.....",
                "....rr.....",
                "....rr.....",
                ".....rr....",
                ".....r.....",
                "......r....",
                "...........",
            },
        };

        private static readonly string[][] ShutdownFrames =
        {
            new[]
            {
                "...........",
                "...........",
                "...........",
                "...........",
                "...ooyoo...",
                "..roooyoor.",
                "...rooor...",
                "..rrooor...",
                "...rroorr..",
                "...rroor...",
                "....roor...",
                "...rroor...",
                "....rrr....",
                "....rr.....",
                ".....rr....",
                "....rr.....",
                ".....r.....",
                ".....r.....",
                "...........",
                "...........",
                "...........",
            },
            new[]
            {
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...........",
                "...rooor...",
                "....roor...",
                "...rroor...",
                "....rrr....",
                "....rr.....",
                ".....rr....",
                "....rr.....",
                ".....r.....",
                ".....r.....",
                "...........",
                "...........",
                "...........",
            },
        };

        internal static string[] GetIgnitionFrame(int frame)
        {
            return CopyFrame(IgnitionFrames, frame, "frame");
        }

        internal static string[] GetFrame(int frame)
        {
            return CopyFrame(Frames, frame, "frame");
        }

        internal static string[] GetShutdownFrame(int frame)
        {
            return CopyFrame(ShutdownFrames, frame, "frame");
        }

        private static string[] CopyFrame(
            string[][] source,
            int frame,
            string parameterName)
        {
            if (frame < 0 || frame >= source.Length)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
            string[] result = new string[Height];
            Array.Copy(source[frame], result, Height);
            return result;
        }
    }
}

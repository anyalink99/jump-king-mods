using System;

namespace JumpKingJetpack
{
    internal static class JetpackSpriteData
    {
        internal const int Width = 12;
        internal const int Height = 17;

        private static readonly string[] Body =
        {
            "............",
            "............",
            ".....DD.....",
            "....DHHDD...",
            "...DHLMLD...",
            "...DLMMMD...",
            "...DBCCBD...",
            "...DBBBBD...",
            "...DSMMMD...",
            "...DSMMMD...",
            "...DSMSSD...",
            "...DMMMMDD..",
            "...DMMSSSD..",
            "....DSSSD...",
            ".....DDD....",
            "....DDDDD...",
            ".....DDD....",
        };

        internal static string[] GetBody()
        {
            string[] result = new string[Body.Length];
            Array.Copy(Body, result, Body.Length);
            return result;
        }
    }
}

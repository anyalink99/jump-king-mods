using System;
using System.Reflection;
using JumpKing.SaveThread;

namespace MorphBallMod
{
    [Serializable]
    public sealed class MorphSaveState : ISaveable<MorphSaveState>
    {
        public bool Morphed { get; set; }

        public MorphSaveState GetDefault()
        {
            return new MorphSaveState();
        }
    }

    internal static class MorphStateStore
    {
        private const string FileName = "morph_ball.sav";
        private const string SaveFolder = "Saves";
        private static readonly Type SaveLubeType =
            typeof(SaveManager).Assembly.GetType(
                "JumpKing.SaveThread.SaveLube",
                false);
        private static readonly Type EncryptModeType =
            SaveLubeType == null
                ? null
                : SaveLubeType.GetNestedType(
                "EncryptMode",
                BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly object NoEncryption =
            EncryptModeType == null
                ? null
                : Enum.ToObject(EncryptModeType, 0);
        private static readonly MethodInfo LoadMethod = FindMethod("Load");
        private static readonly MethodInfo SaveMethod = FindMethod("Save");

        internal static void ValidateContract()
        {
            if (SaveLubeType == null
                || EncryptModeType == null
                || LoadMethod == null
                || SaveMethod == null)
            {
                throw new MissingMemberException(
                    "Jump King save-state contract is unavailable");
            }
        }

        internal static bool Load()
        {
            ValidateContract();
            if (SaveManager.instance != null
                && SaveManager.instance.IsNewGame)
            {
                Save(false);
                return false;
            }
            MorphSaveState state = LoadMethod
                .MakeGenericMethod(typeof(MorphSaveState))
                .Invoke(
                    null,
                    new[] { SaveFolder, FileName, NoEncryption })
                as MorphSaveState;
            return state != null && state.Morphed;
        }

        internal static void Save(bool morphed)
        {
            ValidateContract();
            SaveMethod
                .MakeGenericMethod(typeof(MorphSaveState))
                .Invoke(
                    null,
                    new object[]
                    {
                        SaveFolder,
                        FileName,
                        new MorphSaveState { Morphed = morphed },
                        NoEncryption
                    });
        }

        private static MethodInfo FindMethod(string name)
        {
            if (SaveLubeType == null)
            {
                return null;
            }
            MethodInfo match = null;
            MethodInfo[] methods = SaveLubeType.GetMethods(
                BindingFlags.Public | BindingFlags.Static);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo candidate = methods[index];
                if (candidate.Name != name
                    || !candidate.IsGenericMethodDefinition
                    || candidate.GetParameters().Length
                        != (name == "Load" ? 3 : 4))
                {
                    continue;
                }
                if (match != null)
                {
                    return null;
                }
                match = candidate;
            }
            return match;
        }
    }
}

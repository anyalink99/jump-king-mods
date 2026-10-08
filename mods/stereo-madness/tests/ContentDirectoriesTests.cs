using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

internal static class ContentDirectoriesTests
{
    private static int Main(string[] args)
    {
        try
        {
            string source = Path.GetFullPath(args[0]);
            string restored = Path.Combine(args[1], "file-only-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(restored);
            // Steam delivers files; local recursive copies hide missing empty directories
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(restored, file.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
            }
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            GC.SuppressFinalize(game);
            typeof(Game).GetField("_services", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, new GameServiceContainer());
            typeof(Game1).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, game);
            game.contentManager = new JKContentManager();
            game.contentManager.root = restored;
            // the installed game's actual NPC loader must accept the downloaded map
            game.contentManager.oldMan.Load(game.contentManager);
            foreach (string folder in new[] { "props/hidden wall props", "props/hidden_walls", "props/messages", "screens/scrolling" })
                UltraContent.LoadXmlFiles<object>(null, Path.Combine(restored, folder));
            UltraContent.LoadXmlFiles<object>(null, Path.Combine(restored, "props/textures/raven"), ".ravset");
            using (var content = new ContentManager(new GameServiceContainer(), ""))
            {
                foreach (string folder in new[] { "screens/foreground", "screens/background", "screens/masks", "screens/scrolling/textures", "props/hidden_walls/textures" })
                {
                    // these are empty in Stereo Madness; a direct .keep would be loaded as an asset
                    if (content.LoadContent<object>(Path.Combine(restored, folder)).Count != 0)
                        throw new Exception("Unexpected assets in " + folder);
                }
            }
            Console.WriteLine("[OK] Native content loaders accept file-only Stereo Madness package: " + restored);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

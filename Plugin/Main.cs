using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace NamespacedItems.Plugin
{
    [BepInPlugin(Guid, Name, Version)]
    public class Main : BaseUnityPlugin
    {
        public const string
            Name = "NamespacedItems",
            Author = "KcumSquad",
            Guid = $"{Author}.{Name}",
            Version = "1.0.0.0";

        internal static ManualLogSource log;
        internal readonly Harmony harmony;
        internal readonly Assembly assembly;
        public readonly string modFolder;

        Main()
        {
            log = Logger;
            harmony = new Harmony(Guid);
            assembly = Assembly.GetExecutingAssembly();
            modFolder = Path.GetDirectoryName(assembly.Location);

            harmony.PatchAll(assembly);
        }
    }
}
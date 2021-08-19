using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace NegotiateIDs
{
    [HarmonyPatch]
    class Patches
    {
        static readonly Dictionary<Assembly, string> cachedNames = new()
        {
            { typeof(InventoryItem).Assembly, "CoreMuck" }
        };
        static readonly HashSet<Assembly> cachedIgnore = new(new[] { Assembly.GetExecutingAssembly() });
        static readonly Regex unwantedNames = new(@"^(UnityEngine|BepInEx|Mono(\.Cecil)?)(\.\w+)?$"
            + @"|^(Facepunch\.Steamworks|Unity)\.\w+$"
            + @"|^(0Harmony|0Harmony20|Assembly-CSharp-firstpass)$"
            + $"|^(NavMeshComponents|netstandard|mscorlib)$");
        [HarmonyPatch(typeof(ScriptableObject), "CreateScriptableObjectInstanceFromType")]
        [HarmonyPatch(typeof(ScriptableObject), "CreateScriptableObjectInstanceFromName")]
        [HarmonyPostfix, HarmonyPriority(Priority.High)]
        static ScriptableObject CreateScriptableObject(ScriptableObject result)
        {
            if (result is InventoryItem orig)
            {
                if (orig is InventoryItemWithGUID) return result;
                string name = null;
                var trace = new StackTrace();
                foreach (var frame in trace.GetFrames())
                {
                    var method = frame.GetMethod();
                    if (method == null) break;
                    var assembly = method.DeclaringType.Assembly;
                    if (cachedIgnore.Contains(assembly)) continue;
                    if (cachedNames.TryGetValue(assembly, out name)) break;
                    if (unwantedNames.IsMatch(assembly.GetName().Name))
                    {
                        cachedIgnore.Add(assembly);
                        continue;
                    }
                }
                var item = ScriptableObject.CreateInstance<InventoryItemWithGUID>();
                orig.Copy(item, orig.amount);
                ScriptableObject.Destroy(orig);
                item.plugin = name ?? "unknown";
                return item;
            }
            else return result;
        }
    }
}
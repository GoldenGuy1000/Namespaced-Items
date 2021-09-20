using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

#pragma warning disable CS0618 // InventoryItem is obsolete

namespace NamespacedItems.Plugin
{
    [HarmonyPatch]
    static class Patches
    {

        [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.ToNamespacedItem))]
        [HarmonyTranspiler, HarmonyPriority(Priority.First)]
        static IEnumerable<CodeInstruction> ToNamespacedItem(IEnumerable<CodeInstruction> instructions) =>
            new CodeMatcher(instructions)
                .Start()
                .Insert(
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldarg_1),
                    new(OpCodes.Ldarg_2),
                    Transpilers.EmitDelegate<Func<InventoryItem, string, string, INamespacedItem>>(Implementations.ToNamespacedItem),
                    new(OpCodes.Ret)
                )
                .InstructionEnumeration();



        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.InitAllItems))]
        [HarmonyTranspiler, HarmonyPriority(Priority.First)]
        static IEnumerable<CodeInstruction> InitAllItems(IEnumerable<CodeInstruction> instructions) =>
            new CodeMatcher(instructions)
                .Start()
                .Insert(
                    new(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate<Action<ItemManager>>(Implementations.InitAllItems),
                    new(OpCodes.Ret)
                )
                .InstructionEnumeration();
    }
}
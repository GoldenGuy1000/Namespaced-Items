using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NegotiateIDsPatcher
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                yield return "Assembly-CSharp.dll";
            }
        }

        // private static readonly ManualLogSource log = Logger.CreateLogSource("");

        public static void Patch(AssemblyDefinition assembly)
        {
            var InventoryItem = assembly.MainModule.Types.Single(type => type.FullName == "InventoryItem");
            var VirtualCopy = InventoryItem.Methods.Single(method => method.Name == "Copy");
            VirtualCopy.Name = "VirtualCopy";
            VirtualCopy.IsVirtual = true;
            VirtualCopy.IsNewSlot = true;
            var NewCopy = new MethodDefinition("Copy", MethodAttributes.Public | MethodAttributes.HideBySig, assembly.MainModule.TypeSystem.Void);
            NewCopy.Parameters.Add(new ParameterDefinition("item", ParameterAttributes.None, InventoryItem));
            NewCopy.Parameters.Add(new ParameterDefinition("amount", ParameterAttributes.None, assembly.MainModule.TypeSystem.Int32));
            var il = NewCopy.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Callvirt, VirtualCopy);
            il.Emit(OpCodes.Ret);
            InventoryItem.Methods.Add(NewCopy);
        }
    }
}
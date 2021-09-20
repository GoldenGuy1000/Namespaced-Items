using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NamespacedItems.Preloader
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

        // do not use this for actual data, it probably doesn't do the length correctly and will fuck up after 127 i assume
        public static CustomAttribute Obsolete(string message)
        {
            var blob = new byte[message.Length + 5];
            var i = 0;
            blob[i++] = 1;
            blob[i++] = 0;
            blob[i++] = (byte)message.Length; // this will *definitely* break after 255 length
            foreach (var ch in message)
            {
                blob[i++] = (byte)ch;
            }
            blob[i++] = 0;
            blob[i++] = 0;
            return new CustomAttribute(ObsoleteConstructor, blob);
        }

        static MethodReference ObsoleteConstructor;
        static MethodReference TupleElementNamesAttributeConstructor;
        static TypeReference Enum;
        static TypeReference BinaryWriter;
        static TypeReference BinaryReader;
        static TypeReference Nullable;
        static TypeReference IEnumerable;
        static TypeReference Dictionary;
        static TypeReference Queue;
        static TypeReference HashSet;
        static TypeReference ValueTuple2;

        static TypeReference GameObject;
        static TypeReference Vector3;
        static TypeReference Quaternion;
        static TypeReference Material;
        static TypeReference Mesh;
        static TypeReference Sprite;

        static TypeDefinition Weakness;
        static TypeDefinition InventoryItem;
        static TypeDefinition BowComponent;
        static TypeDefinition ArmorComponent;
        static TypeDefinition ItemManager;
        static TypeDefinition Packet;

        static TypeDefinition INamespacedItem;
        static TypeDefinition IBowItem;
        static TypeDefinition IArmorItem;
        static TypeDefinition IMeleeItem;
        static TypeDefinition IResourceHarvestItem;
        static TypeDefinition IArrowItem;
        static TypeDefinition IDataItem;
        static TypeDefinition IFoodItem;
        static TypeDefinition IFuelItem;
        static TypeDefinition IBuildableItem;
        static TypeDefinition IEnemyProjectileItem;

        static TypeDefinition HarvestTool;
        static TypeDefinition ArmorSlot;
        static TypeDefinition IArmorSet;

        static MethodDefinition CachedNamespacedItem;

        public static void Patch(AssemblyDefinition assembly)
        {
            ClearWeirdEmptyShit(assembly);
            GetImportantTypes(assembly);
            AddImportantTypes(assembly);
            MakeStuffObsolete();
            EditImportantStuff(assembly);
        }

        static GenericInstanceType MakeGenericType(this TypeReference type, params TypeReference[] generics)
        {
            var instance = new GenericInstanceType(type);
            foreach (var arg in generics) instance.GenericArguments.Add(arg);
            return instance;
        }

        static TypeDefinition GetTypeDefinition(this ModuleDefinition module, string name) => module.Types.Single(type => type.FullName == name);
        static TypeDefinition GetNestedTypeDefinition(this TypeDefinition type, string name) => type.NestedTypes.Single(ntype => ntype.Name == name);
        static TypeDefinition AddPublicInterface(this ModuleDefinition module, string name, TypeReference implements = null)
        {
            var type = module.AddPublicType(name, TypeAttributes.Interface | TypeAttributes.Abstract);
            if (implements is not null) type.Interfaces.Add(new InterfaceImplementation(implements));
            return type;
        }
        static TypeDefinition AddPublicType(this ModuleDefinition module, string name, TypeAttributes attributes, TypeReference baseType = null)
        {
            var type = new TypeDefinition("", name, attributes | TypeAttributes.Public, baseType);
            module.Types.Add(type);
            return type;
        }

        static PropertyDefinition AddGetProperty(this TypeDefinition def, string name, TypeReference type)
        {
            var prop = new PropertyDefinition(name, PropertyAttributes.None, type);
            prop.GetMethod = new MethodDefinition(
                $"get_{name}",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Abstract |
                MethodAttributes.Virtual,
                type);
            def.Methods.Add(prop.GetMethod);
            def.Properties.Add(prop);
            return prop;
        }

        static PropertyDefinition AddConcreteGetProperty(this TypeDefinition def, string name, TypeReference type)
        {
            var prop = new PropertyDefinition(name, PropertyAttributes.None, type);
            prop.GetMethod = new MethodDefinition(
                $"get_{name}",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName,
                type);
            def.Methods.Add(prop.GetMethod);
            def.Properties.Add(prop);
            return prop;
        }

        static PropertyDefinition AddGetSetProperty(this TypeDefinition def, string name, TypeReference type)
        {
            var prop = new PropertyDefinition(name, PropertyAttributes.None, type);
            prop.GetMethod = new MethodDefinition(
                $"get_{name}",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Abstract |
                MethodAttributes.Virtual,
                type);
            prop.SetMethod = new MethodDefinition(
                $"set_{name}",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Abstract |
                MethodAttributes.Virtual,
                def.Module.TypeSystem.Void);
            prop.SetMethod.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, type));
            def.Methods.Add(prop.GetMethod);
            def.Methods.Add(prop.SetMethod);
            def.Properties.Add(prop);
            return prop;
        }

        static MethodDefinition AddInterfaceMethod(this TypeDefinition def, string name, TypeReference returnType, params ParameterDefinition[] parameters)
        {
            var method = new MethodDefinition(
                name,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Abstract |
                MethodAttributes.Virtual,
                returnType);
            foreach (var param in parameters) method.Parameters.Add(param);
            def.Methods.Add(method);
            return method;
        }

        static MethodDefinition AddPublicMethod(this TypeDefinition def, string name, TypeReference returnType, params ParameterDefinition[] parameters)
        {
            var method = new MethodDefinition(
                name,
                MethodAttributes.Public |
                MethodAttributes.HideBySig,
                returnType);
            foreach (var param in parameters) method.Parameters.Add(param);
            def.Methods.Add(method);
            return method;
        }

        static TypeDefinition AddEnum(this ModuleDefinition module, string name, params string[] elements)
        {
            var type = module.AddPublicType(name, TypeAttributes.Sealed, Enum);
            type.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.RTSpecialName | FieldAttributes.SpecialName, module.TypeSystem.Int32));
            var i = 0;
            foreach (var element in elements)
            {
                var field = new FieldDefinition(element, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal, type);
                field.Constant = i++;
                type.Fields.Add(field);
            }
            return type;
        }

        static TypeReference GetUnityType(this ModuleDefinition module, string name) => module.GetTypeReferences().Single(type => type.FullName == $"UnityEngine.{name}");
        static MethodDefinition GetMethod(this TypeDefinition type, string name) => type.Methods.Single(m => m.Name == name);
        static FieldDefinition GetField(this TypeDefinition type, string name)
        {
            return type.Fields.Single(f => f.Name == name);
        }

        static void ReturnDefaultFor(this ILProcessor il, TypeReference type)
        {
            // emulate C# default
            if (type == type.Module.TypeSystem.Void)
            {
                // void: don't return any value
                il.Emit(OpCodes.Ret);
            }
            else if (type.IsValueType)
            {
                // value type: have to init local
                var i = (byte)il.Body.Variables.Count;
                il.Body.Variables.Add(new VariableDefinition(type));
                il.Emit(OpCodes.Ldloca_S, i);
                il.Emit(OpCodes.Initobj, type);

                // and then load and return it
                if (i < 4) // micro optimization: emit correct short form instruction
                {
                    il.Emit(i switch
                    {
                        0 => OpCodes.Ldloc_0,
                        1 => OpCodes.Ldloc_1,
                        2 => OpCodes.Ldloc_2,
                        3 => OpCodes.Ldloc_3,
                        _ => default, // silence warning
                    });
                }
                else
                {
                    il.Emit(OpCodes.Ldloc_S, i);
                }
                il.Emit(OpCodes.Ret);
            }
            else
            {
                // reference type: just return a null pointer
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }
        }

        static void ClearBody(this MethodDefinition method)
        {
            var il = method.Body.GetILProcessor();
            while (method.Body.Instructions.Any())
            {
                il.Remove(method.Body.Instructions.First());
            }
            il.Body.Variables.Clear();

            // dummy local will be unused when overwritten and shows a message when decompiled with ILSpy
            il.Body.Variables.Add(new VariableDefinition(method.Module.TypeSystem.String));
            il.Emit(OpCodes.Ldstr, "This method is implemented in NamespacedItems.Plugin.Implementations.");
            il.Emit(OpCodes.Stloc_0);
            il.ReturnDefaultFor(method.ReturnType);
        }

        static void AddEmptyBody(this MethodDefinition method)
        {
            var il = method.AddNewBody();
            il.Body.Variables.Clear();

            // dummy local will be unused when overwritten and shows a message when decompiled with ILSpy
            il.Body.Variables.Add(new VariableDefinition(method.Module.TypeSystem.String));
            il.Emit(OpCodes.Ldstr, "This method is implemented in NamespacedItems.Plugin.Implementations.");
            il.Emit(OpCodes.Stloc_0);
            il.ReturnDefaultFor(method.ReturnType);
        }

        static ILProcessor AddNewBody(this MethodDefinition method)
        {
            if (!method.HasBody) method.Body = new MethodBody(method);
            return method.Body.GetILProcessor();
        }

        static void FixNamespacedAsProperty(this FieldDefinition field, string propName)
        {
            field.CustomAttributes.Add(Obsolete($"Use {propName}."));

            if (!field.FieldType.IsArray)
            {
                var prop = field.DeclaringType.AddConcreteGetProperty(propName, INamespacedItem);
                var il = prop.GetMethod.AddNewBody();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Call, CachedNamespacedItem);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                var prop = field.DeclaringType.AddConcreteGetProperty(propName, new ArrayType(INamespacedItem));
                var il = prop.GetMethod.AddNewBody();
                il.Body.Variables.Add(new VariableDefinition(new ArrayType(INamespacedItem)));
                il.Body.Variables.Add(new VariableDefinition(field.Module.TypeSystem.Int32));

                /*

                this is a lot of IL emit and basically i just compiled roughly this code and copied the output:
        
                    var arr = new INamespacedItem[field.Length];
                    for (var i = 0; i < arr.Length; i++) arr[i] = field[i].CachedNamespacedItem();
                    return arr;

                arr = local 0
                i = local 1

                hopefully you're not mad at me for this, but in case you think this is awful, at least i've tried to comment it so it's easy to understand

                */

                // need to jump to these
                var condition = il.Create(OpCodes.Ldloc_1);
                var bodyStart = il.Create(OpCodes.Ldloc_0);

                // get length of existing array
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Ldlen);
                il.Emit(OpCodes.Conv_I4);
                // create new array with length and store to local 0
                il.Emit(OpCodes.Newarr, INamespacedItem);
                il.Emit(OpCodes.Stloc_0);

                // start for loop: set index to 0
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stloc_1);

                // check length before executing even once
                il.Emit(OpCodes.Br_S, condition);

                {
                    // loop body: add new array and index to top of stack for later
                    il.Append(bodyStart); // ldloc.0
                    il.Emit(OpCodes.Ldloc_1);
                    {
                        // load old array
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldfld, field);
                        // element at index from local 1
                        il.Emit(OpCodes.Ldloc_1);
                        il.Emit(OpCodes.Ldelem_Any, field.FieldType);
                    }
                    // actually convert the fucking item finally
                    il.Emit(OpCodes.Call, CachedNamespacedItem);

                    // set the element in new array
                    // here stack looks like:
                    // the item
                    // index (local 1)
                    // new array (local 0)
                    il.Emit(OpCodes.Stelem_Any, field.FieldType);
                }

                // add 1 to index (local 1)
                il.Emit(OpCodes.Ldloc_1);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Stloc_1);

                // load index first
                il.Append(condition); // ldloc.1
                {
                    // then load arr len
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldlen);
                    il.Emit(OpCodes.Conv_I4);
                }
                // and go back to the body if index is < length
                il.Emit(OpCodes.Blt_S, bodyStart);

                // load resulting new arr and return it
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ret);
            }
        }

        // i am doing this for my own good so i don't go crazy clearing empty method shit
        static void ClearWeirdEmptyShit(AssemblyDefinition assembly)
        {
            foreach (var type in assembly.MainModule.Types) RecurseType(type);
            void RecurseType(TypeDefinition type)
            {
                foreach (var nest in type.NestedTypes) RecurseType(nest);
                foreach (var method in type.Methods)
                {
                    if (method.HasBody)
                    {
                        var body = method.Body;
                        var locals = body.Variables.Count;
                        var used = new bool[locals];
                        foreach (var inst in body.Instructions)
                        {
                            int loc;
                            if (inst.OpCode == OpCodes.Ldloc_0) loc = 0;
                            else if (inst.OpCode == OpCodes.Ldloc_1) loc = 1;
                            else if (inst.OpCode == OpCodes.Ldloc_2) loc = 2;
                            else if (inst.OpCode == OpCodes.Ldloc_3) loc = 3;
                            else if (inst.OpCode == OpCodes.Stloc_0) loc = 0;
                            else if (inst.OpCode == OpCodes.Stloc_1) loc = 1;
                            else if (inst.OpCode == OpCodes.Stloc_2) loc = 2;
                            else if (inst.OpCode == OpCodes.Stloc_3) loc = 3;
                            else if (inst.OpCode == OpCodes.Ldloc_S
                                || inst.OpCode == OpCodes.Ldloc
                                || inst.OpCode == OpCodes.Stloc_S
                                || inst.OpCode == OpCodes.Stloc
                                || inst.OpCode == OpCodes.Ldloca
                                || inst.OpCode == OpCodes.Ldloca_S)
                            {
                                switch (inst.Operand)
                                {
                                    case int x:
                                        loc = x;
                                        break;
                                    case byte y:
                                        loc = y;
                                        break;
                                    case VariableDefinition var:
                                        loc = var.Index;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                            else continue;

                            used[loc] = true;
                        }


                        // why the fuck does the game have methods that don't fit this filter
                        if (used.All(x => x)) continue;

                        var mapping = Enumerable.Repeat(-1, locals).ToList();

                        var i = 0;
                        for (var j = 0; j < locals; j++)
                        {
                            if (used[j])
                            {
                                mapping[j] = i++;
                            }
                        }

                        foreach (var local in Enumerable.Range(0, locals).Where(i => !used[i]).Select(i => body.Variables[i]).ToArray())
                        {
                            body.Variables.Remove(local);
                        }


                        foreach (var inst in body.Instructions)
                        {
                            int loc;
                            var set = false;
                            var addr = false;
                            if (inst.OpCode == OpCodes.Ldloc_0) loc = 0;
                            else if (inst.OpCode == OpCodes.Ldloc_1) loc = 1;
                            else if (inst.OpCode == OpCodes.Ldloc_2) loc = 2;
                            else if (inst.OpCode == OpCodes.Ldloc_3) loc = 3;
                            else if (inst.OpCode == OpCodes.Ldloc_S
                                || inst.OpCode == OpCodes.Ldloc)
                            {
                                switch (inst.Operand)
                                {
                                    case int x:
                                        loc = x;
                                        break;
                                    case byte y:
                                        loc = y;
                                        break;
                                    case VariableDefinition var:
                                        loc = var.Index;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                            else
                            {
                                set = true;
                                if (inst.OpCode == OpCodes.Stloc_0) loc = 0;
                                else if (inst.OpCode == OpCodes.Stloc_1) loc = 1;
                                else if (inst.OpCode == OpCodes.Stloc_2) loc = 2;
                                else if (inst.OpCode == OpCodes.Stloc_3) loc = 3;
                                else if (inst.OpCode == OpCodes.Stloc_S
                                  || inst.OpCode == OpCodes.Stloc)
                                {
                                    switch (inst.Operand)
                                    {
                                        case int x:
                                            loc = x;
                                            break;
                                        case byte y:
                                            loc = y;
                                            break;
                                        case VariableDefinition var:
                                            loc = var.Index;
                                            break;
                                        default:
                                            continue;
                                    }
                                }
                                else
                                {
                                    set = false;
                                    addr = true;
                                    if (inst.OpCode == OpCodes.Ldloca
                                || inst.OpCode == OpCodes.Ldloca_S)
                                    {
                                        switch (inst.Operand)
                                        {
                                            case int x:
                                                loc = x;
                                                break;
                                            case byte y:
                                                loc = y;
                                                break;
                                            case VariableDefinition var:
                                                loc = var.Index;
                                                break;
                                            default:
                                                continue;
                                        }

                                    }
                                    else continue;
                                }
                            }

                            loc = mapping[loc];
                            if (addr)
                            {
                                inst.OpCode = loc <= byte.MaxValue ? OpCodes.Ldloca_S : OpCodes.Ldloca;
                                inst.Operand = body.Variables[loc];
                            }
                            else
                            {
                                switch (loc)
                                {
                                    case <= byte.MaxValue when addr:
                                        inst.OpCode = OpCodes.Ldloca_S;
                                        inst.Operand = body.Variables[loc];
                                        break;
                                    case > byte.MaxValue when addr:
                                        inst.OpCode = OpCodes.Ldloca;
                                        inst.Operand = body.Variables[loc];
                                        break;
                                    case 0:
                                        inst.OpCode = set ? OpCodes.Stloc_0 : OpCodes.Ldloc_0;
                                        inst.Operand = null;
                                        break;
                                    case 1:
                                        inst.OpCode = set ? OpCodes.Stloc_1 : OpCodes.Ldloc_1;
                                        inst.Operand = null;
                                        break;
                                    case 2:
                                        inst.OpCode = set ? OpCodes.Stloc_2 : OpCodes.Ldloc_2;
                                        inst.Operand = null;
                                        break;
                                    case 3:
                                        inst.OpCode = set ? OpCodes.Stloc_3 : OpCodes.Ldloc_3;
                                        inst.Operand = null;
                                        break;
                                    case <= byte.MaxValue:
                                        inst.OpCode = set ? OpCodes.Stloc_S : OpCodes.Ldloc_S;
                                        inst.Operand = body.Variables[loc];
                                        break;
                                    default:
                                        inst.OpCode = set ? OpCodes.Stloc : OpCodes.Ldloc;
                                        inst.Operand = body.Variables[loc];
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        static void GetImportantTypes(AssemblyDefinition assembly)
        {
            var ObsoleteAttribute = assembly.MainModule.ImportReference(typeof(ObsoleteAttribute));
            var ObsoleteConstructorDefinition = ObsoleteAttribute.Resolve().Methods.Single(m => m.IsConstructor && m.Parameters.Count == 1);
            ObsoleteConstructor = assembly.MainModule.ImportReference(ObsoleteConstructorDefinition);

            var TupleElementNamesAttribute = assembly.MainModule.ImportReference(typeof(TupleElementNamesAttribute));
            var TupleElementNamesAttributeConstructorDefinition = TupleElementNamesAttribute.Resolve().Methods.Single(m => m.IsConstructor && m.Parameters.Count == 1);
            TupleElementNamesAttributeConstructor = assembly.MainModule.ImportReference(TupleElementNamesAttributeConstructorDefinition);

            Enum = assembly.MainModule.ImportReference(typeof(Enum));
            BinaryWriter = assembly.MainModule.ImportReference(typeof(BinaryWriter));
            BinaryReader = assembly.MainModule.ImportReference(typeof(BinaryReader));
            Nullable = assembly.MainModule.ImportReference(typeof(Nullable<>));
            IEnumerable = assembly.MainModule.ImportReference(typeof(IEnumerable<>));
            Dictionary = assembly.MainModule.ImportReference(typeof(Dictionary<,>));
            Queue = assembly.MainModule.ImportReference(typeof(Queue<>));
            HashSet = assembly.MainModule.ImportReference(typeof(HashSet<>));
            ValueTuple2 = assembly.MainModule.ImportReference(typeof(ValueTuple<,>));

            GameObject = assembly.MainModule.GetUnityType(nameof(GameObject));
            Vector3 = assembly.MainModule.GetUnityType(nameof(Vector3));
            Quaternion = assembly.MainModule.GetUnityType(nameof(Quaternion));
            Material = assembly.MainModule.GetUnityType(nameof(Material));
            Mesh = assembly.MainModule.GetUnityType(nameof(Mesh));
            Sprite = assembly.MainModule.GetUnityType(nameof(Sprite));

            Weakness = assembly.MainModule.GetTypeDefinition("MobType").NestedTypes.Single(type => type.Name == nameof(Weakness));
            InventoryItem = assembly.MainModule.GetTypeDefinition(nameof(InventoryItem));
            BowComponent = assembly.MainModule.GetTypeDefinition(nameof(BowComponent));
            ArmorComponent = assembly.MainModule.GetTypeDefinition(nameof(ArmorComponent));
            ItemManager = assembly.MainModule.GetTypeDefinition(nameof(ItemManager));
            Packet = assembly.MainModule.GetTypeDefinition(nameof(Packet));
        }

        static void MakeStuffObsolete()
        {
            InventoryItem.CustomAttributes.Add(Obsolete("This class is only used for serializing vanilla item data. Use INamespacedItem instead."));
            ArmorComponent.CustomAttributes.Add(Obsolete("This class is only used for serializing vanilla item data. Use IArmorItem instead."));
            BowComponent.CustomAttributes.Add(Obsolete("This class is only used for serializing vanilla item data. Use IBowItem instead."));
            // this probably does nothing, everyone uses publicized assemblies anyways, but oh well, what can i do about it? obsolete isn't removed at least
            InventoryItem.IsPublic = false;
            ArmorComponent.IsPublic = false;
            BowComponent.IsPublic = false;
        }

        static void AddImportantTypes(AssemblyDefinition assembly)
        {
            INamespacedItem = assembly.MainModule.AddPublicInterface(nameof(INamespacedItem));
            IBuildableItem = assembly.MainModule.AddPublicInterface(nameof(IBuildableItem), INamespacedItem);
            IDataItem = assembly.MainModule.AddPublicInterface(nameof(IDataItem), INamespacedItem);

            IBowItem = assembly.MainModule.AddPublicInterface(nameof(IBowItem), INamespacedItem);
            IFoodItem = assembly.MainModule.AddPublicInterface(nameof(IFoodItem), INamespacedItem);
            IFuelItem = assembly.MainModule.AddPublicInterface(nameof(IFuelItem), INamespacedItem);

            IMeleeItem = assembly.MainModule.AddPublicInterface(nameof(IMeleeItem), INamespacedItem);
            IResourceHarvestItem = assembly.MainModule.AddPublicInterface(nameof(IResourceHarvestItem), IMeleeItem);

            IArrowItem = assembly.MainModule.AddPublicInterface(nameof(IArrowItem), INamespacedItem);
            IEnemyProjectileItem = assembly.MainModule.AddPublicInterface(nameof(IEnemyProjectileItem), INamespacedItem);

            IArmorItem = assembly.MainModule.AddPublicInterface(nameof(IArmorItem), INamespacedItem);
            IArmorSet = assembly.MainModule.AddPublicInterface(nameof(IArmorSet));

            HarvestTool = assembly.MainModule.AddEnum(nameof(HarvestTool), "Axe", "Pickaxe");
            ArmorSlot = assembly.MainModule.AddEnum(nameof(ArmorSlot), "Helmet", "Torso", "Legs", "Feet");

            INamespacedItem.AddGetProperty("Namespace", assembly.MainModule.TypeSystem.String);
            INamespacedItem.AddGetProperty("Name", assembly.MainModule.TypeSystem.String);
            INamespacedItem.AddGetProperty("DisplayName", assembly.MainModule.TypeSystem.String);
            INamespacedItem.AddGetProperty("CanDespawn", assembly.MainModule.TypeSystem.Boolean);
            INamespacedItem.AddGetProperty("Description", assembly.MainModule.TypeSystem.String);

            INamespacedItem.AddInterfaceMethod("Copy", INamespacedItem);
            INamespacedItem.AddGetSetProperty("Amount", assembly.MainModule.TypeSystem.Int32);
            INamespacedItem.AddGetProperty("MaxAmount", assembly.MainModule.TypeSystem.Int32);
            INamespacedItem.AddGetProperty("Stackable", assembly.MainModule.TypeSystem.Boolean);

            INamespacedItem.AddGetProperty("Sprite", Sprite);
            INamespacedItem.AddGetProperty("DroppedMesh", Mesh);
            INamespacedItem.AddGetProperty("DroppedMaterial", Material);
            INamespacedItem.AddGetProperty("HeldRotationOffset", Vector3);
            INamespacedItem.AddGetProperty("HeldPositionOffset", Vector3);
            INamespacedItem.AddGetProperty("HeldScale", assembly.MainModule.TypeSystem.Single);

            IFoodItem.AddGetProperty("HealthRegen", assembly.MainModule.TypeSystem.Single);
            IFoodItem.AddGetProperty("HungerRegen", assembly.MainModule.TypeSystem.Single);
            IFoodItem.AddGetProperty("StaminaRegen", assembly.MainModule.TypeSystem.Single);

            IBowItem.AddGetProperty("ProjectileSpeed", assembly.MainModule.TypeSystem.Single);
            IBowItem.AddGetProperty("ArrowCount", assembly.MainModule.TypeSystem.Int32);
            IBowItem.AddGetProperty("ArrowAngleDelta", assembly.MainModule.TypeSystem.Single);

            IArrowItem.AddGetProperty("AttackDamage", assembly.MainModule.TypeSystem.Single);
            IArrowItem.AddGetProperty("Prefab", GameObject);
            IArrowItem.AddGetProperty("ArrowMaterial", Material);

            IEnemyProjectileItem.AddGetProperty("Prefab", GameObject);
            IEnemyProjectileItem.AddGetProperty("AttackDamage", assembly.MainModule.TypeSystem.Single);
            IEnemyProjectileItem.AddGetProperty("ProjectileSpeed", assembly.MainModule.TypeSystem.Single);
            IEnemyProjectileItem.AddGetProperty("ColliderDisabledTime", assembly.MainModule.TypeSystem.Single);
            IEnemyProjectileItem.AddGetProperty("RotationOffset", Vector3);

            IMeleeItem.AddGetProperty("AttackDamage", assembly.MainModule.TypeSystem.Single);
            IMeleeItem.AddGetProperty("AttackRange", assembly.MainModule.TypeSystem.Single);
            IMeleeItem.AddGetProperty("AttackTypes", IEnumerable.MakeGenericType(Weakness));

            IResourceHarvestItem.AddGetProperty("HarvestType", HarvestTool);
            IResourceHarvestItem.AddGetProperty("ResourceDamage", assembly.MainModule.TypeSystem.Single);

            IBuildableItem.AddGetProperty("Prefab", GameObject);
            IBuildableItem.AddGetProperty("SnapToGrid", assembly.MainModule.TypeSystem.Boolean);
            IBuildableItem.AddGetProperty("GhostMesh", Mesh);
            IBuildableItem.AddGetProperty("GhostMaterial", Material);

            IFuelItem.AddGetProperty("MaxUses", assembly.MainModule.TypeSystem.Int32);
            IFuelItem.AddGetSetProperty("CurrentUses", assembly.MainModule.TypeSystem.Int32);
            IFuelItem.AddGetProperty("SpeedMultiplier", assembly.MainModule.TypeSystem.Int32);

            IArmorItem.AddGetProperty("Armor", assembly.MainModule.TypeSystem.Int32);
            IArmorItem.AddGetProperty("Slot", ArmorSlot);
            IArmorItem.AddGetProperty("Set", IArmorSet);

            IArmorSet.AddGetProperty("Namespace", assembly.MainModule.TypeSystem.String);
            IArmorSet.AddGetProperty("Name", assembly.MainModule.TypeSystem.String);
            IArmorSet.AddGetProperty("Bonus", assembly.MainModule.TypeSystem.String);

            IArmorSet.AddGetProperty("Helmet", IArmorItem);
            IArmorSet.AddGetProperty("Torso", IArmorItem);
            IArmorSet.AddGetProperty("Legs", IArmorItem);
            IArmorSet.AddGetProperty("Feet", IArmorItem);

            IDataItem.AddInterfaceMethod("Serialize", assembly.MainModule.TypeSystem.Void, new ParameterDefinition("writer", ParameterAttributes.None, BinaryWriter));
            IDataItem.AddInterfaceMethod("Deserialize", assembly.MainModule.TypeSystem.Void, new ParameterDefinition("reader", ParameterAttributes.None, BinaryReader));

            InventoryItem.AddPublicMethod(
                "ToNamespacedItem", INamespacedItem,
                new ParameterDefinition("namespace", ParameterAttributes.None, assembly.MainModule.TypeSystem.String),
                new ParameterDefinition("name", ParameterAttributes.None, assembly.MainModule.TypeSystem.String)
            ).AddEmptyBody();

            CachedNamespacedItem = InventoryItem.AddPublicMethod(nameof(CachedNamespacedItem), INamespacedItem);
            CachedNamespacedItem.AddEmptyBody();
        }

        static void EditImportantStuff(AssemblyDefinition assembly)
        {
            var TwoString = ValueTuple2.MakeGenericType(assembly.MainModule.TypeSystem.String, assembly.MainModule.TypeSystem.String);
            var NamespaceAndName = new CustomAttribute(TupleElementNamesAttributeConstructor, new byte[] {
                // new string[] { "nspace", "name" }
                1, 0, 2, 0, 0, 0, 6, 110, 115, 112, 97, 99, 101, 4, 110, 97, 109, 101, 0, 0
            });

            var BuildManager = assembly.MainModule.GetTypeDefinition("BuildManager");
            {
                var BuildItem = BuildManager.GetMethod("BuildItem");
                BuildItem.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, IBuildableItem);
                BuildItem.Parameters[4] = new ParameterDefinition("rotation", ParameterAttributes.None, Quaternion);
                BuildItem.ClearBody();
            }

            // ItemManager is already in scope
            {
                ItemManager.Methods.Remove(ItemManager.GetMethod("GetItemByName"));

                ItemManager.GetMethod("Awake").ClearBody();
                ItemManager.GetMethod("InitAllItems").ClearBody();

                var allItems = ItemManager.GetField("allItems");
                allItems.FieldType = Dictionary.MakeGenericType(TwoString, INamespacedItem);
                allItems.CustomAttributes.Add(NamespaceAndName);

                var DropItem = ItemManager.GetMethod("DropItem");
                DropItem.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                DropItem.ClearBody();

                var DropItemAtPosition = ItemManager.GetMethod("DropItemAtPosition");
                DropItemAtPosition.Parameters[0] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                DropItemAtPosition.ClearBody();
            }

            var PlayerRagdoll = assembly.MainModule.GetTypeDefinition("PlayerRagdoll");
            {
                var SetArmor = PlayerRagdoll.GetMethod("SetArmor");
                SetArmor.Parameters[0] = new ParameterDefinition("slot", ParameterAttributes.None, ArmorSlot);
                SetArmor.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                SetArmor.ClearBody();

                var WeaponInHand = PlayerRagdoll.GetMethod("WeaponInHand");
                WeaponInHand.Parameters[0] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                WeaponInHand.ClearBody();
            }

            var PlayerStatus = assembly.MainModule.GetTypeDefinition("PlayerStatus");
            {
                var UpdateArmor = PlayerStatus.GetMethod("UpdateArmor");
                UpdateArmor.Parameters[0] = new ParameterDefinition("slot", ParameterAttributes.None, ArmorSlot);
                UpdateArmor.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                UpdateArmor.ClearBody();
            }

            var PreviewPlayer = assembly.MainModule.GetTypeDefinition("PreviewPlayer");
            {
                var SetArmor = PreviewPlayer.GetMethod("SetArmor");
                SetArmor.Parameters[0] = new ParameterDefinition("slot", ParameterAttributes.None, ArmorSlot);
                SetArmor.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                SetArmor.ClearBody();

                var WeaponInHand = PreviewPlayer.GetMethod("WeaponInHand");
                WeaponInHand.Parameters[0] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                WeaponInHand.ClearBody();
            }

            var ProjectileController = assembly.MainModule.GetTypeDefinition("ProjectileController");
            {
                var SpawnProjectileFromPlayer = ProjectileController.GetMethod("SpawnProjectileFromPlayer");
                SpawnProjectileFromPlayer.Parameters[3] = new ParameterDefinition("arrow", ParameterAttributes.None, IArrowItem);
                SpawnProjectileFromPlayer.ClearBody();

                var SpawnMobProjectile = ProjectileController.GetMethod("SpawnMobProjectile");
                SpawnMobProjectile.Parameters[3] = new ParameterDefinition("item", ParameterAttributes.None, IEnemyProjectileItem);
                SpawnMobProjectile.ClearBody();
            }

            var CauldronSync = assembly.MainModule.GetTypeDefinition("CauldronSync");
            {
                var AddMaterial = CauldronSync.GetMethod("AddMaterial");
                AddMaterial.Parameters[1] = new ParameterDefinition("processedItem", ParameterAttributes.None, INamespacedItem);
                AddMaterial.Parameters.RemoveAt(0);
                AddMaterial.ClearBody();
            }

            var FurnaceSync = assembly.MainModule.GetTypeDefinition("FurnaceSync");
            {
                var AddMaterial = FurnaceSync.GetMethod("AddMaterial");
                AddMaterial.Parameters[1] = new ParameterDefinition("processedItem", ParameterAttributes.None, INamespacedItem);
                AddMaterial.Parameters.RemoveAt(0);
                AddMaterial.ClearBody();
            }

            var ChestManager = assembly.MainModule.GetTypeDefinition("ChestManager");
            {
                var UpdateChest = ChestManager.GetMethod("UpdateChest");
                UpdateChest.Parameters[2] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                UpdateChest.ClearBody();
            }

            var OnlinePlayer = assembly.MainModule.GetTypeDefinition("OnlinePlayer");
            {
                var UpdateWeapon = OnlinePlayer.GetMethod("UpdateWeapon");
                UpdateWeapon.Parameters[0] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                UpdateWeapon.ClearBody();
            }

            var PlayerManager = assembly.MainModule.GetTypeDefinition("PlayerManager");
            {
                var SetArmor = PlayerManager.GetMethod("SetArmor");
                SetArmor.Parameters[0] = new ParameterDefinition("slot", ParameterAttributes.None, ArmorSlot);
                SetArmor.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                SetArmor.ClearBody();
            }

            var Player = assembly.MainModule.GetTypeDefinition("Player");
            {
                var UpdateArmor = Player.GetMethod("UpdateArmor");
                UpdateArmor.Parameters[0] = new ParameterDefinition("slot", ParameterAttributes.None, ArmorSlot);
                UpdateArmor.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                UpdateArmor.ClearBody();
            }

            var GrowableFoodZone = assembly.MainModule.GetTypeDefinition("GrowableFoodZone");
            {
                var LocalSpawnEntity = GrowableFoodZone.GetMethod("LocalSpawnEntity");
                LocalSpawnEntity.Parameters[1] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                LocalSpawnEntity.ClearBody();
            }

            var TestPlayerAnimations = assembly.MainModule.GetTypeDefinition("TestPlayerAnimations");
            {
                var UpdateWeapon = TestPlayerAnimations.GetMethod("UpdateWeapon");
                UpdateWeapon.Parameters[0] = new ParameterDefinition("item", ParameterAttributes.None, INamespacedItem);
                UpdateWeapon.ClearBody();
            }

            var UiEvents = assembly.MainModule.GetTypeDefinition("UiEvents");
            {
                var itemsToUnlock = UiEvents.GetField("idsToUnlock");
                itemsToUnlock.Name = "itemsToUnlock";
                itemsToUnlock.FieldType = Queue.MakeGenericType(TwoString);
                itemsToUnlock.CustomAttributes.Add(NamespaceAndName);

                var unlockedSoft = UiEvents.GetField("unlockedSoft");
                unlockedSoft.FieldType = HashSet.MakeGenericType(TwoString);
                unlockedSoft.CustomAttributes.Add(NamespaceAndName);

                var unlockedHard = UiEvents.GetField("unlockedHard");
                unlockedHard.FieldType = HashSet.MakeGenericType(TwoString);
                unlockedHard.CustomAttributes.Add(NamespaceAndName);

                var stationsUnlocked = UiEvents.GetField("stationsUnlocked");
                stationsUnlocked.FieldType = HashSet.MakeGenericType(TwoString);
                stationsUnlocked.CustomAttributes.Add(NamespaceAndName);

                var alertCleared = UiEvents.GetField("alertCleared");
                alertCleared.FieldType = HashSet.MakeGenericType(TwoString);
                alertCleared.CustomAttributes.Add(NamespaceAndName);

                UiEvents.GetMethod("Start").ClearBody();
                UiEvents.GetMethod("Unlock").ClearBody();

                var UnlockItemHard = UiEvents.GetMethod("UnlockItemHard");
                UnlockItemHard.Parameters.Clear();
                UnlockItemHard.Parameters.Add(new ParameterDefinition("namespace", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
                UnlockItemHard.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
                UnlockItemHard.ClearBody();

                var UnlockItemSoft = UiEvents.GetMethod("UnlockItemSoft");
                UnlockItemSoft.Parameters.Clear();
                UnlockItemSoft.Parameters.Add(new ParameterDefinition("namespace", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
                UnlockItemSoft.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
                UnlockItemSoft.ClearBody();

                var CheckNewUnlocks = UiEvents.GetMethod("CheckNewUnlocks");
                CheckNewUnlocks.Parameters.Clear(); // param is unused
                CheckNewUnlocks.ClearBody();

                var CheckProcessedItem = UiEvents.GetMethod("CheckProcessedItem");
                CheckProcessedItem.Parameters.Clear();
                CheckProcessedItem.Parameters.Add(new ParameterDefinition("namespace", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
                CheckProcessedItem.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
                CheckProcessedItem.ClearBody();

                UiEvents.GetMethod("Awake").ClearBody();
                UiEvents.Methods.Remove(UiEvents.GetMethod("Start"));
            }

            // these use GetItemByName and break saving
            assembly.MainModule.GetTypeDefinition("HitBox").GetMethod("ShovelHitGround").ClearBody();
            assembly.MainModule.GetTypeDefinition("OtherInput").GetMethod("GetStationId").ClearBody();
            assembly.MainModule.GetTypeDefinition("InventoryUI").GetMethod("UseMoney").ClearBody();
            assembly.MainModule.GetTypeDefinition("TradeUi").GetMethod("BuySell").ClearBody();

            // i don't remember what the difference was, but above was fixing a different search for InventoryItem than below
            // that's why the sorting kinda restarts, since i finished the first search and started new one

            var AchievementManager = assembly.MainModule.GetTypeDefinition("AchievementManager");
            {
                var ItemCrafted = AchievementManager.GetMethod("ItemCrafted");
                ItemCrafted.Parameters[0].ParameterType = INamespacedItem;
                ItemCrafted.ClearBody();

                var WieldedWeapon = AchievementManager.GetMethod("WieldedWeapon");
                WieldedWeapon.Parameters[0].ParameterType = INamespacedItem;
                WieldedWeapon.ClearBody();

                var EatFood = AchievementManager.GetMethod("EatFood");
                EatFood.Parameters[0].ParameterType = INamespacedItem;
                EatFood.ClearBody();

                var PickupItem = AchievementManager.GetMethod("PickupItem");
                PickupItem.Parameters[0].ParameterType = INamespacedItem;
                PickupItem.ClearBody();
            }

            var Arrow = assembly.MainModule.GetTypeDefinition("Arrow");
            {
                var item = Arrow.Properties.Single(p => p.Name == "item");
                item.PropertyType = INamespacedItem;
                item.GetMethod.ReturnType = INamespacedItem;
                item.SetMethod.Parameters[0].ParameterType = INamespacedItem;
                Arrow.GetField("<item>k__BackingField").FieldType = INamespacedItem;

                Arrow.GetMethod("OnCollisionEnter").ClearBody();
            }

            var Boat = assembly.MainModule.GetTypeDefinition("Boat");
            {
                Boat.GetField("mapItem").FixNamespacedAsProperty("MapItem");
                Boat.GetField("gemMap").FixNamespacedAsProperty("GemMap");
                Boat.GetMethod("CheckForMap").ClearBody();
            }

            var CauldronUI = assembly.MainModule.GetTypeDefinition("CauldronUI");
            {
                var processableFood = CauldronUI.GetField("processableFood");
                processableFood.CustomAttributes.Add(Obsolete("Namespaced Items doesn't use this field."));

                // these methods are noops so i just remove them
                CauldronUI.Methods.Remove(CauldronUI.GetMethod("CanProcess"));
                CauldronUI.Methods.Remove(CauldronUI.GetMethod("FindItemByIngredients"));
            }

            var Chest = assembly.MainModule.GetTypeDefinition("Chest");
            {
                Chest.GetMethod("InitChest").ClearBody();
                // this can make unity think assets are corrupted, but the field should be empty in all serialized assets and only filled by scripts, so it's fine
                Chest.GetField("cells").FieldType = new ArrayType(INamespacedItem);
            }

            var CraftingUI = assembly.MainModule.GetTypeDefinition("CraftingUI");
            var CraftingUI_Tab = CraftingUI.GetNestedTypeDefinition("Tab");
            {
                var items = CraftingUI_Tab.GetField("items");
                items.CustomAttributes.Add(Obsolete("Namespaced Items doesn't use this field."));
            }

            var WoodmanTrades = assembly.MainModule.GetTypeDefinition("WoodmanTrades");
            {
                var trades = WoodmanTrades.GetField("trades");
                trades.CustomAttributes.Add(Obsolete("Namespaced Items doesn't use this field."));

                var WoodmanTrades_Trade = WoodmanTrades.GetNestedTypeDefinition("Trade");
                WoodmanTrades_Trade.CustomAttributes.Add(Obsolete("Namespaced Items doesn't use this class."));
                {
                    WoodmanTrades_Trade.GetField("item").FixNamespacedAsProperty("Item");
                }
            }

            var DragonRainAttack = assembly.MainModule.GetTypeDefinition("DragonRainAttack");
            {
                DragonRainAttack.GetField("fireball").FixNamespacedAsProperty("Fireball");
            }

            var Fireball = assembly.MainModule.GetTypeDefinition("Fireball");
            {
                Fireball.GetField("fireball").FixNamespacedAsProperty("Fireball");
            }

            var GroundSwordAttack = assembly.MainModule.GetTypeDefinition("GroundSwordAttack");
            {
                GroundSwordAttack.GetField("projectile").FixNamespacedAsProperty("Projectile");
            }

            var Guardian = assembly.MainModule.GetTypeDefinition("Guardian");
            {
                Guardian.GetField("gems").FixNamespacedAsProperty("Gems");
            }

            var GuardianSpikes = assembly.MainModule.GetTypeDefinition("GuardianSpikes");
            {
                GuardianSpikes.GetField("attack").FixNamespacedAsProperty("Attack");
            }

            // ItemManager
            {
                var allScriptableItems = ItemManager.GetField("allScriptableItems");
                allScriptableItems.CustomAttributes.Add(Obsolete("This is only for serialized game items. No touchy! Seriously!"));
            }

            var ItemPickedupUI = assembly.MainModule.GetTypeDefinition("ItemPickedupUI");
            {
                var SetItem = ItemPickedupUI.GetMethod("SetItem");
                SetItem.Parameters[0].ParameterType = INamespacedItem;
                SetItem.ClearBody();
            }

            // intentional typo because the game has this typo lmao
            var ItemUnlcokedUI = assembly.MainModule.GetTypeDefinition("ItemUnlcokedUI");
            {
                var SetItem = ItemUnlcokedUI.GetMethod("SetItem");
                SetItem.Parameters[0].ParameterType = INamespacedItem;
                SetItem.ClearBody();
            }

            // PlayerStatus
            {
                var armor = PlayerStatus.GetField("armor");
                armor.FieldType = new ArrayType(INamespacedItem);

                var Eat = PlayerStatus.GetMethod("Eat");
                Eat.Parameters[0].ParameterType = IFoodItem;
                Eat.ClearBody();
            }

            var ProjectileAttackNoGravity = assembly.MainModule.GetTypeDefinition("ProjectileAttackNoGravity");
            {
                ProjectileAttackNoGravity.GetField("projectile").FixNamespacedAsProperty("Projectile");
                ProjectileAttackNoGravity.GetField("predictionProjectile").FixNamespacedAsProperty("PredictionProjectile");
                ProjectileAttackNoGravity.GetField("warningAttack").FixNamespacedAsProperty("WarningAttack");
            }

            var RepairInteract = assembly.MainModule.GetTypeDefinition("RepairInteract");
            {
                RepairInteract.GetField("requirements").FixNamespacedAsProperty("Requirements");
            }

            // CauldronSync
            {
                var CanProcess = CauldronSync.GetMethod("CanProcess");
                CanProcess.ReturnType = INamespacedItem;
                CanProcess.ClearBody();

                var FindItemByIngredients = CauldronSync.GetMethod("FindItemByIngredients");
                FindItemByIngredients.Parameters.Clear(); // kinda pointless, always passed ingredientCells
                FindItemByIngredients.ReturnType = INamespacedItem;
                FindItemByIngredients.ClearBody();
            }

            var HitableResource = assembly.MainModule.GetTypeDefinition("HitableResource");
            {
                HitableResource.GetField("dropItem").FixNamespacedAsProperty("DropItem");
                HitableResource.GetField("dropExtra").FixNamespacedAsProperty("DropExtra");
            }

            var PickupInteract = assembly.MainModule.GetTypeDefinition("PickupInteract");
            {
                PickupInteract.GetField("item").FixNamespacedAsProperty("Item");
            }

            var Hotbar = assembly.MainModule.GetTypeDefinition("Hotbar");
            {
                Hotbar.GetField("currentItem").FieldType = INamespacedItem;
            }

            var InventoryCell = assembly.MainModule.GetTypeDefinition("InventoryCell");
            {
                InventoryCell.GetField("currentItem").FieldType = INamespacedItem;
                // leave spawnItem alone because it's unused but don't delete to prevent corrupt
                var ForceAddItem = InventoryCell.GetMethod("ForceAddItem");
                ForceAddItem.Parameters[0].ParameterType = INamespacedItem;
                ForceAddItem.ClearBody();

                var SetItem = InventoryCell.GetMethod("SetItem");
                SetItem.Parameters[0].ParameterType = INamespacedItem;
                SetItem.ReturnType = INamespacedItem;
                SetItem.ClearBody();

                var PickupItem = InventoryCell.GetMethod("PickupItem");
                PickupItem.ReturnType = INamespacedItem;
                PickupItem.ClearBody();

                // remove noops
                InventoryCell.Methods.Remove(InventoryCell.GetMethod("AddItemToCauldron"));
                InventoryCell.Methods.Remove(InventoryCell.GetMethod("AddItemToChest"));
                InventoryCell.Methods.Remove(InventoryCell.GetMethod("AddItemToFurnace"));
            }

            var InventoryUI = assembly.MainModule.GetTypeDefinition("InventoryUI");
            {
                InventoryUI.GetField("currentMouseItem").FieldType = INamespacedItem;

                var CanPickup = InventoryUI.GetMethod("CanPickup");
                CanPickup.Parameters[0].ParameterType = INamespacedItem;
                CanPickup.ClearBody();

                var PickupItem = InventoryUI.GetMethod("PickupItem");
                PickupItem.Parameters[0].ParameterType = INamespacedItem;
                PickupItem.ClearBody();


                var PlaceItem = InventoryUI.GetMethod("PlaceItem");
                PlaceItem.Parameters[0].ParameterType = INamespacedItem;
                PlaceItem.ClearBody();

                var DropItemIntoWorld = InventoryUI.GetMethod("DropItemIntoWorld");
                DropItemIntoWorld.Parameters[0].ParameterType = INamespacedItem;
                DropItemIntoWorld.ClearBody();

                var AddItemToInventory = InventoryUI.GetMethod("AddItemToInventory");
                AddItemToInventory.Parameters[0].ParameterType = INamespacedItem;
                AddItemToInventory.ClearBody();

                var IsCraftable = InventoryUI.GetMethod("IsCraftable");
                IsCraftable.Parameters[0].ParameterType = INamespacedItem;
                IsCraftable.ClearBody();

                var CraftItem = InventoryUI.GetMethod("CraftItem");
                CraftItem.Parameters[0].ParameterType = INamespacedItem;
                CraftItem.ClearBody();

                var RemoveItem = InventoryUI.GetMethod("RemoveItem");
                RemoveItem.Parameters[0].ParameterType = INamespacedItem;
                RemoveItem.ClearBody();

                var CanRepair = InventoryUI.GetMethod("CanRepair");
                CanRepair.Parameters[0].ParameterType = new ArrayType(INamespacedItem);
                CanRepair.ClearBody();

                var Repair = InventoryUI.GetMethod("Repair");
                Repair.Parameters[0].ParameterType = new ArrayType(INamespacedItem);
                Repair.ClearBody();

                var HasItem = InventoryUI.GetMethod("HasItem");
                HasItem.Parameters[0].ParameterType = INamespacedItem;
                HasItem.ClearBody();

                var AddArmor = InventoryUI.GetMethod("AddArmor");
                AddArmor.Parameters[0].ParameterType = INamespacedItem;
                AddArmor.ClearBody();
            }

            var UseInventory = assembly.MainModule.GetTypeDefinition("UseInventory");
            {
                var SetWeapon = UseInventory.GetMethod("SetWeapon");
                SetWeapon.Parameters[0].ParameterType = INamespacedItem;
                SetWeapon.ClearBody();
            }

            var LootDrop_LootItems = assembly.MainModule.GetTypeDefinition("LootDrop").GetNestedTypeDefinition("LootItems");
            {
                LootDrop_LootItems.GetField("item").FixNamespacedAsProperty("Item");
            }

            // GrowableFoodZone
            {
                GrowableFoodZone.GetField("spawnItems").FixNamespacedAsProperty("SpawnItems");
            }

            var ShipChest = assembly.MainModule.GetTypeDefinition("ShipChest");
            {
                ShipChest.GetField("spawnLoot").FixNamespacedAsProperty("SpawnLoot");
            }

            var Tutorial_TutorialStep = assembly.MainModule.GetTypeDefinition("Tutorial").GetNestedTypeDefinition("TutorialStep");
            {
                Tutorial_TutorialStep.GetField("item").FixNamespacedAsProperty("Item");
            }

            var TutorialTaskUI = assembly.MainModule.GetTypeDefinition("TutorialTaskUI");
            {
                var SetItem = TutorialTaskUI.GetMethod("SetItem");
                SetItem.Parameters[0].ParameterType = INamespacedItem;
                SetItem.ClearBody();
            }

            // UiEvents
            {
                var AddPickup = UiEvents.GetMethod("AddPickup");
                AddPickup.Parameters[0].ParameterType = INamespacedItem;
                AddPickup.ClearBody();

                var PlaceInInventory = UiEvents.GetMethod("PlaceInInventory");
                PlaceInInventory.Parameters[0].ParameterType = INamespacedItem;
                PlaceInInventory.ClearBody();
            }

            // yet again here i start another search, but it doesn't seem like the last one was sorted in any way lol

            AchievementManager.GetField("gems").FixNamespacedAsProperty("Gems");

            BuildManager.GetMethod("SetNewItem").ClearBody();
            BuildManager.GetMethod("NewestBuild").ClearBody();
            BuildManager.GetMethod("RequestBuildItem").ClearBody();
            BuildManager.GetMethod("CanBuild").ClearBody();

            CauldronUI.GetMethod("CopyChest").ClearBody();
            CauldronUI.GetMethod("UpdateCraftables").ClearBody();
            CauldronUI.GetMethod("ProcessItem").ClearBody();
            CauldronUI.GetMethod("UseFuel").ClearBody();
            CauldronUI.GetMethod("AddMaterial").ClearBody();

            Chest.GetMethod("Start").ClearBody();
            CraftingUI.GetMethod("UpdateCraftables").ClearBody();
            WoodmanTrades.GetMethod("GetTrades").ClearBody();
            DragonRainAttack.GetMethod("SpawnFireBall").ClearBody();
            Fireball.GetMethod("Start").ClearBody();
        }
    }
}

// this file was not generated by any tool
// i wrote it all by hand
// pain
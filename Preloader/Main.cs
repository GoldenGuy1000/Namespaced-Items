using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static byte[] GetCustomAttributeBlob(string message)
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
            return blob;
        }

        static MethodReference ObsoleteConstructor;
        static TypeReference Enum;
        static TypeReference BinaryWriter;
        static TypeReference BinaryReader;
        static TypeReference Nullable;
        static TypeReference IEnumerable;

        static TypeReference GameObject;
        static TypeReference Vector3;
        static TypeReference Material;
        static TypeReference Mesh;

        static TypeDefinition Weakness;
        static TypeDefinition InventoryItem;
        static TypeDefinition BowComponent;
        static TypeDefinition ArmorComponent;

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

        public static void Patch(AssemblyDefinition assembly)
        {
            GetImportantTypes(assembly);
            AddImportantTypes(assembly);
            MakeStuffObsolete();
        }

        static TypeReference MakeGenericType(this TypeReference type, params TypeReference[] generics)
        {
            var instance = new GenericInstanceType(type);
            foreach (var arg in generics) instance.GenericArguments.Add(arg);
            return instance;
        }

        static TypeDefinition GetTypeDefinition(this ModuleDefinition module, string name) => module.Types.Single(type => type.FullName == name);
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

        static void GetImportantTypes(AssemblyDefinition assembly)
        {
            var ObsoleteAttribute = assembly.MainModule.ImportReference(typeof(ObsoleteAttribute));
            var ObsoleteConstructorDefinition = ObsoleteAttribute.Resolve().Methods.Single(meth => meth.IsConstructor && meth.Parameters.Count == 1);
            ObsoleteConstructor = assembly.MainModule.ImportReference(ObsoleteConstructorDefinition);

            Enum = assembly.MainModule.ImportReference(typeof(Enum));
            BinaryWriter = assembly.MainModule.ImportReference(typeof(BinaryWriter));
            BinaryReader = assembly.MainModule.ImportReference(typeof(BinaryReader));
            Nullable = assembly.MainModule.ImportReference(typeof(Nullable<>));
            IEnumerable = assembly.MainModule.ImportReference(typeof(IEnumerable<>));

            GameObject = assembly.MainModule.GetUnityType(nameof(GameObject));
            Vector3 = assembly.MainModule.GetUnityType(nameof(Vector3));
            Material = assembly.MainModule.GetUnityType(nameof(Material));
            Mesh = assembly.MainModule.GetUnityType(nameof(Mesh));

            Weakness = assembly.MainModule.GetTypeDefinition("MobType").NestedTypes.Single(type => type.Name == nameof(Weakness));
            InventoryItem = assembly.MainModule.GetTypeDefinition(nameof(InventoryItem));
            BowComponent = assembly.MainModule.GetTypeDefinition(nameof(BowComponent));
            ArmorComponent = assembly.MainModule.GetTypeDefinition(nameof(ArmorComponent));
        }

        static void MakeStuffObsolete()
        {
            InventoryItem.CustomAttributes.Add(new CustomAttribute(ObsoleteConstructor, GetCustomAttributeBlob("This class is only used for serializing vanilla item data. Use INamespacedItem instead.")));
            ArmorComponent.CustomAttributes.Add(new CustomAttribute(ObsoleteConstructor, GetCustomAttributeBlob("This class is only used for serializing vanilla item data. Use IArmorItem instead.")));
            BowComponent.CustomAttributes.Add(new CustomAttribute(ObsoleteConstructor, GetCustomAttributeBlob("This class is only used for serializing vanilla item data. Use IBowItem instead.")));
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

            // overwrite this method in plugin with harmony
            var ToNamespacedItem = new MethodDefinition("ToNamespacedItem", MethodAttributes.Public | MethodAttributes.HideBySig, INamespacedItem);
            ToNamespacedItem.Parameters.Add(new ParameterDefinition("namespace", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
            ToNamespacedItem.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.None, assembly.MainModule.TypeSystem.String));
            InventoryItem.Methods.Add(ToNamespacedItem);
            ToNamespacedItem.Body.InitLocals = true;
            var il = ToNamespacedItem.Body.GetILProcessor();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

#pragma warning disable CS0618 // InventoryItem is obsolete

namespace NamespacedItems.Plugin
{
    [HarmonyPatch]
    static class Patches
    {
        [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.ToNamespacedItem)), HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ToNamespacedItem(IEnumerable<CodeInstruction> instructions) =>
            new CodeMatcher(instructions)
                .Start()
                .Insert(
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldarg_1),
                    new(OpCodes.Ldarg_2),
                    Transpilers.EmitDelegate<Func<InventoryItem, string, string, INamespacedItem>>(ToNamespacedItem),
                    new(OpCodes.Ret)
                )
                .InstructionEnumeration();

        static Dictionary<(string nspace, string name), INamespacedItem> CachedItems = new();
        static Dictionary<ArmorComponent, IArmorSet> CachedArmors = new();

        static AssemblyBuilder asm;
        static ModuleBuilder module;

        static INamespacedItem ToNamespacedItem(InventoryItem item, string nspace, string name)
        {
            if (CachedItems.TryGetValue((nspace, name), out var cached)) return cached;
            if (module is null)
            {
                var asmname = new AssemblyName("NamespacedGeneratedItems");
                asm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmname, AssemblyBuilderAccess.RunAndSave);
                module = asm.DefineDynamicModule(asmname.Name, asmname.Name + ".dll");
            }

            var original = AccessTools.Field(typeof(BaseNamespacedGeneratedItem), nameof(BaseNamespacedGeneratedItem.original));

            var isFood = item.tag == InventoryItem.ItemTag.Food;
            var isFuel = item.tag == InventoryItem.ItemTag.Fuel;
            var isArmor = item.armorComponent != null;
            var isBow = item.bowComponent != null;
            var isArrow = item.tag == InventoryItem.ItemTag.Arrow;
            var isBuildable = item.buildable;
            var isEnemyProjectile = false;
            if (item.prefab != null && item.prefab.TryGetComponent<EnemyProjectile>(out _))
            {
                isBow = false;
                isArrow = false;
                isEnemyProjectile = true;
            }

            var isResourceHarvest = (item.type & (InventoryItem.ItemType.Axe | InventoryItem.ItemType.Pickaxe)) != 0;
            var isMelee = (item.attackDamage > 1 && !isArrow) || isResourceHarvest;

            var type = module.DefineType($"{nspace}.{name}", TypeAttributes.Public, typeof(BaseNamespacedGeneratedItem), Type.EmptyTypes);

            if (isFood)
            {
                type.AddInterfaceImplementation(typeof(IFoodItem));
                type.AddGetProperty(nameof(IFoodItem.HealthRegen), typeof(float)).OriginalField(nameof(InventoryItem.heal));
                type.AddGetProperty(nameof(IFoodItem.HungerRegen), typeof(float)).OriginalField(nameof(InventoryItem.hunger));
                type.AddGetProperty(nameof(IFoodItem.StaminaRegen), typeof(float)).OriginalField(nameof(InventoryItem.stamina));
            }
            if (isFuel)
            {
                type.AddInterfaceImplementation(typeof(IFuelItem));
                type.AddGetProperty(nameof(IFuelItem.MaxUses), typeof(int)).OriginalField(nameof(InventoryItem.fuel), nameof(InventoryItem.fuel.maxUses));
                type.AddAutoProperty(nameof(IFuelItem.CurrentUses), typeof(int));
                type.AddGetProperty(nameof(IFuelItem.SpeedMultiplier), typeof(int)).OriginalField(nameof(InventoryItem.fuel), nameof(InventoryItem.fuel.speedMultiplier));
            }
            if (isArmor)
            {
                type.AddInterfaceImplementation(typeof(IArmorItem));
                type.AddGetProperty(nameof(IArmorItem.Armor), typeof(int)).OriginalField(nameof(InventoryItem.armor));

                {
                    var il = type.AddGetProperty(nameof(IArmorItem.Slot), typeof(ArmorSlot)).GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, original);
                    il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(InventoryItem), nameof(InventoryItem.tag)));
                    il.Emit(OpCodes.Ldc_I4_4);
                    il.Emit(OpCodes.Sub);
                    il.Emit(OpCodes.Ret);
                }

                if (item.armorComponent == null || item.armorComponent.name == "NormalArmor")
                {
                    var il = type.AddGetProperty(nameof(IArmorItem.Set), typeof(IArmorSet)).GetILGenerator();
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    if (!CachedArmors.ContainsKey(item.armorComponent))
                    {
                        if (GeneratedArmorSet.sets.TryGetValue(item.armorComponent.name, out var setName))
                        {
                            CachedArmors[item.armorComponent] = new GeneratedArmorSet(setName, item.armorComponent.setBonus);
                        }
                        else
                        {
                            throw new InvalidOperationException($"No set name found. Please add the key '{item.armorComponent.name}' to GeneratedArmorSet.sets.");
                        }
                    }
                    var il = type.AddGetProperty(nameof(IArmorItem.Set), typeof(IArmorSet)).GetILGenerator();
                    il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(Patches), nameof(CachedArmors)));
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, original);
                    il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(InventoryItem), nameof(InventoryItem.armorComponent)));
                    il.EmitCall(OpCodes.Call, AccessTools.PropertyGetter(typeof(Dictionary<,>), "Item"), null);
                    il.Emit(OpCodes.Ret);
                }
            }
            if (isBow)
            {
                type.AddInterfaceImplementation(typeof(IBowItem));
                type.AddGetProperty(nameof(IBowItem.ProjectileSpeed), typeof(float)).OriginalField(nameof(InventoryItem.bowComponent), nameof(InventoryItem.bowComponent.projectileSpeed));
                type.AddGetProperty(nameof(IBowItem.ArrowCount), typeof(int)).OriginalField(nameof(InventoryItem.bowComponent), nameof(InventoryItem.bowComponent.nArrows));
                var il = type.AddGetProperty(nameof(IBowItem.ArrowAngleDelta), typeof(float)).GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, original);
                il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(InventoryItem), nameof(InventoryItem.bowComponent)));
                il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(BowComponent), nameof(BowComponent.angleDelta)));
                il.Emit(OpCodes.Conv_R4);
                il.Emit(OpCodes.Ret);
            }
            if (isArrow)
            {
                type.AddInterfaceImplementation(typeof(IArrowItem));
                type.AddGetProperty(nameof(IArrowItem.AttackDamage), typeof(float)).OriginalField(nameof(InventoryItem.attackDamage));
                type.AddGetProperty(nameof(IArrowItem.Prefab), typeof(GameObject)).OriginalField(nameof(InventoryItem.prefab));
                type.AddGetProperty(nameof(IArrowItem.ArrowMaterial), typeof(Material)).OriginalField(nameof(InventoryItem.material));
            }
            if (isEnemyProjectile)
            {
                type.AddInterfaceImplementation(typeof(IEnemyProjectileItem));
                type.AddGetProperty(nameof(IEnemyProjectileItem.Prefab), typeof(GameObject)).OriginalField(nameof(InventoryItem.prefab));
                type.AddGetProperty(nameof(IEnemyProjectileItem.AttackDamage), typeof(float)).OriginalField(nameof(InventoryItem.attackDamage));
                type.AddGetProperty(nameof(IEnemyProjectileItem.ProjectileSpeed), typeof(float)).OriginalField(nameof(InventoryItem.bowComponent), nameof(InventoryItem.bowComponent.projectileSpeed));
                type.AddGetProperty(nameof(IEnemyProjectileItem.ColliderDisabledTime), typeof(float)).OriginalField(nameof(InventoryItem.bowComponent), nameof(InventoryItem.bowComponent.colliderDisabledTime));
                type.AddGetProperty(nameof(IEnemyProjectileItem.RotationOffset), typeof(Vector3)).OriginalField(nameof(InventoryItem.rotationOffset));
            }
            if (isBuildable)
            {
                type.AddInterfaceImplementation(typeof(IBuildableItem));
                type.AddGetProperty(nameof(IBuildableItem.Prefab), typeof(GameObject)).OriginalField(nameof(InventoryItem.prefab));
                type.AddGetProperty(nameof(IBuildableItem.SnapToGrid), typeof(bool)).OriginalField(nameof(InventoryItem.grid));
                type.AddGetProperty(nameof(IBuildableItem.GhostMesh), typeof(Mesh)).OriginalField(nameof(InventoryItem.mesh));
                type.AddGetProperty(nameof(IBuildableItem.GhostMaterial), typeof(Material)).OriginalField(nameof(InventoryItem.material));
            }
            if (isMelee)
            {
                type.AddInterfaceImplementation(typeof(IMeleeItem));
                type.AddGetProperty(nameof(IMeleeItem.AttackDamage), typeof(float)).OriginalField(nameof(InventoryItem.attackDamage));
                type.AddGetProperty(nameof(IMeleeItem.AttackRange), typeof(float)).OriginalField(nameof(InventoryItem.attackRange));
                type.AddGetProperty(nameof(IMeleeItem.AttackTypes), typeof(IEnumerable<MobType.Weakness>)).OriginalField(nameof(InventoryItem.attackTypes));
            }
            if (isResourceHarvest)
            {
                type.AddInterfaceImplementation(typeof(IResourceHarvestItem));
                type.AddGetProperty(nameof(IResourceHarvestItem.ResourceDamage), typeof(float)).OriginalField(nameof(InventoryItem.resourceDamage));
                var il = type.AddGetProperty(nameof(IResourceHarvestItem.HarvestType), typeof(HarvestTool)).GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, original);
                il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(InventoryItem), nameof(InventoryItem.type)));
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Sub);
                il.Emit(OpCodes.Ret);
            }

            var ctor = type.DefineConstructor(
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new[] { typeof(InventoryItem), typeof(string), typeof(string) });
            {
                var il = ctor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_3);
                il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(BaseNamespacedGeneratedItem), new[] { typeof(InventoryItem), typeof(string), typeof(string) }));
                il.Emit(OpCodes.Ret);
            }

            var copy = type.DefineMethod(
                nameof(INamespacedItem.Copy),
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.Virtual,
                typeof(INamespacedItem), Type.EmptyTypes);
            {
                var il = copy.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, original);
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Call, AccessTools.PropertyGetter(typeof(BaseNamespacedGeneratedItem), nameof(BaseNamespacedGeneratedItem.Namespace)), null);
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Call, AccessTools.PropertyGetter(typeof(BaseNamespacedGeneratedItem), nameof(BaseNamespacedGeneratedItem.Name)), null);
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
            }

            var resultType = type.CreateType();
            var resultCtor = AccessTools.Constructor(resultType, new[] { typeof(InventoryItem), typeof(string), typeof(string) });
            var result = (INamespacedItem)resultCtor.Invoke(new object[] { item, nspace, name });
            CachedItems[(nspace, name)] = result;
            return result;
        }

        static MethodBuilder AddGetProperty(this TypeBuilder type, string name, Type returnType)
        {
            var prop = type.DefineProperty(name, PropertyAttributes.None, returnType, Type.EmptyTypes);
            var get = type.DefineMethod(
                $"get_{name}",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                returnType, Type.EmptyTypes);
            prop.SetGetMethod(get);
            return get;
        }

        static void AddAutoProperty(this TypeBuilder type, string name, Type returnType)
        {
            var field = type.DefineField($"<{name}>k__BackingField", returnType, FieldAttributes.Private);
            var get = type.DefineMethod(
                $"get_{name}",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                returnType, Type.EmptyTypes);
            var set = type.DefineMethod(
                $"set_{name}",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                typeof(void), new[] { returnType });
            field.SetCustomAttribute(AccessTools.Constructor(typeof(CompilerGeneratedAttribute), Type.EmptyTypes), new byte[] { 1, 0, 0, 0 });
            get.SetCustomAttribute(AccessTools.Constructor(typeof(CompilerGeneratedAttribute), Type.EmptyTypes), new byte[] { 1, 0, 0, 0 });
            set.SetCustomAttribute(AccessTools.Constructor(typeof(CompilerGeneratedAttribute), Type.EmptyTypes), new byte[] { 1, 0, 0, 0 });
            var getil = get.GetILGenerator();
            getil.Emit(OpCodes.Ldarg_0);
            getil.Emit(OpCodes.Ldfld, field);
            getil.Emit(OpCodes.Ret);
            var setil = set.GetILGenerator();
            setil.Emit(OpCodes.Ldarg_0);
            setil.Emit(OpCodes.Ldarg_1);
            setil.Emit(OpCodes.Stfld, field);
            setil.Emit(OpCodes.Ret);

            var prop = type.DefineProperty(name, PropertyAttributes.None, returnType, Type.EmptyTypes);
            prop.SetGetMethod(get);
            prop.SetSetMethod(set);
        }

        static void OriginalField(this MethodBuilder method, params string[] names)
        {
            var original = AccessTools.Field(typeof(BaseNamespacedGeneratedItem), nameof(BaseNamespacedGeneratedItem.original));
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, original);
            var lastType = original.FieldType;
            foreach (var name in names)
            {
                var field = AccessTools.Field(lastType, name);
                il.Emit(OpCodes.Ldfld, field);
                lastType = field.FieldType;
            }
            il.Emit(OpCodes.Ret);
        }

        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.InitAllItems)), HarmonyPostfix]
        static void InitAllItems(ItemManager __instance)
        {
            foreach (var item in __instance.allScriptableItems)
            {
                try
                {
                    item.ToNamespacedItem("muck", item.id.ToString());
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            asm.Save(asm.GetName().Name + ".dll");
        }
    }
}

public abstract class BaseNamespacedGeneratedItem : INamespacedItem
{
    protected internal readonly InventoryItem original;

    public string Namespace { get; }
    public string Name { get; }
    public string DisplayName => original.name;
    public string Description => original.description;
    public int Amount { get; set; }
    public int MaxAmount => original.max;
    public bool Stackable => original.stackable;
    public bool CanDespawn => !original.important;
    public Sprite Sprite => original.sprite;
    public Mesh DroppedMesh => original.mesh;
    public Material DroppedMaterial => original.material;
    public Vector3 HeldRotationOffset => original.rotationOffset;
    public Vector3 HeldPositionOffset => original.positionOffset;
    public float HeldScale => original.scale;

    protected BaseNamespacedGeneratedItem(InventoryItem item, string nspace, string name)
    {
        original = item;
        Namespace = nspace;
        Name = name;
    }

    public abstract INamespacedItem Copy();
}

public class GeneratedArmorSet : IArmorSet
{
    public static Dictionary<string, string> sets = new()
    {
        ["ChunkiumArmor"] = "chunkium_armor",
        ["WolfArmor"] = "wolf_armor",
    };

    public string Namespace => "muck";
    public string Name { get; }
    public string Bonus { get; }

    public IArmorItem Helmet { get; internal set; }
    public IArmorItem Torso { get; internal set; }
    public IArmorItem Legs { get; internal set; }
    public IArmorItem Feet { get; internal set; }

    internal GeneratedArmorSet(string name, string bonus)
    {
        Name = name;
        Bonus = bonus;
    }
}
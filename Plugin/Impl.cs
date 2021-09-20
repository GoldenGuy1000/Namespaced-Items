using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable CS0618 // InventoryItem is obsolete

namespace NamespacedItems.Plugin
{
    static partial class Implementations
    {
        public static void Awake(ItemManager self)
        {
            ItemManager.Instance = self;
            self.list = new();
            self.allItems = new();
            self.allPowerups = new();
            self.stringToPowerupId = new();
            self.random = new();
            self.InitAllItems();
            self.InitAllPowerups();
            self.InitAllDropTables();
        }
        public static void InitAllItems(ItemManager self)
        {
            foreach (var item in self.allScriptableItems)
            {
                try
                {
                    var name = BaseNamespacedGeneratedItem.names[item.id];
                    var result = item.ToNamespacedItem("muck", name);
                    self.allItems[("muck", name)] = result;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            asm.Save(asm.GetName().Name + ".dll");
        }

        public static void DropItem(ItemManager self, int fromClient, INamespacedItem item, int amount, int objectID)
        {
            var droppedObject = ItemManager.Instantiate(self.dropItem);
            var newItem = item.Copy();
            newItem.Amount = amount;
            var droppedItem = droppedObject.GetComponent<Item>();
            droppedItem.item = newItem;
            droppedItem.UpdateMesh();
            droppedObject.AddComponent<BoxCollider>();
            Vector3 position = GameManager.players[fromClient].transform.position;
            Transform child = GameManager.players[fromClient].transform;
            if (fromClient == LocalClient.instance.myId)
            {
                child = child.transform.GetChild(0);
            }
            Vector3 normalized = (child.forward + Vector3.up * 0.35f).normalized;
            droppedObject.transform.position = position;
            droppedObject.GetComponent<Rigidbody>().AddForce(normalized * InventoryUI.throwForce);
            if (attatchDebug)
            {
                GameObject obj = Object.Instantiate(debug, droppedObject.transform);
                obj.GetComponent<DebugObject>().text = string.Concat(objectID);
                obj.transform.localPosition = Vector3.up * 1.25f;
            }
            droppedObject.GetComponent<Item>().objectID = objectID;
            list.Add(objectID, droppedObject);
        }
    }
}
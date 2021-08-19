using System;
using System.Collections.Generic;

public class InventoryItemWithGUID : InventoryItem
{
    public string plugin = "";

    public Dictionary<Type, IExtraData> data = new();
}
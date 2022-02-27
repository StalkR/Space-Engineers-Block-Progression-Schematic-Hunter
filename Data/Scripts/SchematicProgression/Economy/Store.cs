using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;

using SchematicProgression.Settings;
using SchematicProgression.Drawing;

using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using IMyStoreBlock = Sandbox.ModAPI.IMyStoreBlock;
using IMyTextSurfaceProvider = Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider;
using Sandbox.Game;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;

namespace SchematicProgression.Economy
{
  public class Store
  {
    public IMyStoreBlock Block;
    public Dictionary<long, ItemInfo> OrderDict; // uses the price as the key
    HashSet<long> _storePrices;
    Queue<MyPhysicalInventoryItem> _addQueue;
    List<MyStoreQueryItem> _queryList = new List<MyStoreQueryItem>();

    DrawSurface _drawSurface;
    MyInventory _storeInventory;
    Session _mod;
    int _ticks;
    float _minPrice, _maxPrice;

    public Store(IMyStoreBlock storeBlock, Session mod)
    {
      OrderDict = new Dictionary<long, ItemInfo>(20);
      Block = storeBlock;

      var inGame = storeBlock as Sandbox.ModAPI.Ingame.IMyStoreBlock;
      var provider = inGame as IMyTextSurfaceProvider;
      _addQueue = new Queue<MyPhysicalInventoryItem>(30);
      _drawSurface = new DrawSurface(provider.GetSurface(0), storeBlock);
      _storeInventory = Block.GetInventory() as MyInventory;
      _storePrices = new HashSet<long>();
      _minPrice = mod.RaritySettings.BasePrice_Minimum;
      _maxPrice = mod.RaritySettings.BasePrice_Maximum;
      _mod = mod;

      storeBlock.Flags |= VRage.ModAPI.EntityFlags.Sync;
      storeBlock.Synchronized = true;

      var invList = _storeInventory.GetItems();
      _queryList.Clear();
      storeBlock.GetPlayerStoreItems(_queryList);

      foreach (var item in _queryList)
        storeBlock.CancelStoreItem(item.Id);

      var completedHash = new HashSet<string>();
      for (int i = 0; i < invList.Count; i++)
      {
        MyDefinitionId typeDef;
        int price;

        var invItem = invList[i];
        var datapad = invItem.Content as MyObjectBuilder_Datapad;
        if (datapad == null || !MyDefinitionId.TryParse(datapad.Name, out typeDef))
          continue;

        if (completedHash.Contains(datapad.Name))
          continue;

        var lines = datapad.Data.Split('\n');
        if (lines.Length < 3)
          continue;

        var lineItems = lines[2].Split(':');
        if (lineItems.Length < 2 || !int.TryParse(lineItems[1], out price))
          continue;

        completedHash.Add(datapad.Name);

        if (price < 0)
        {
          BlockRarity br;
          if (!_mod.BlockTypeRarity.TryGetValue(typeDef, out br))
          {
            br = new BlockRarity(typeDef);
            _mod.BlockTypeRarity[typeDef] = br;
          }

          price = GetUniqueItemPrice(br);
        }

        int orderAmount = 1;
        for (int j = i + 1; j < invList.Count; j++)
        {
          var nextItem = invList[j];
          var nextDatpad = nextItem.Content as MyObjectBuilder_Datapad;
          if (nextDatpad == null)
            continue;

          if (nextDatpad.Name == datapad.Name)
            orderAmount++;
        }

        long storeId;
        var data = new MyStoreItemData(datapad.GetId(), orderAmount, price, OnPurchase, null);
        var result = Block.InsertOffer(data, out storeId);

        if (_mod.DebugMode)
          _mod.Logger.Log($"Attempting to add item to store: Item = {typeDef.ToString()}, Price = {price}, result = {result.ToString()}, StoreId = {storeId}");

        if (result != MyStoreInsertResults.Success)
          break;

        ItemInfo info;
        if (!OrderDict.TryGetValue(price, out info))
        {
          info = new ItemInfo(typeDef, storeId, price, orderAmount);
          OrderDict[price] = info;
        }
        else
          info.NumberAvailable += orderAmount;

        _storePrices.Add(price);
      }

      if (OrderDict.Count > 0)
      {
        storeBlock.CustomName = "StoreBlock [Schematic Hunter]";
        storeBlock.CustomNameChanged += StoreBlock_CustomNameChanged;
        storeBlock.OnClosing += StoreBlock_OnClosing;
        SetupDisplay();
      }
    }

    public Store(IMyStoreBlock storeBlock, int numOffers, Session mod)
    {
      OrderDict = new Dictionary<long, ItemInfo>(numOffers);
      Block = storeBlock;

      var inGame = storeBlock as Sandbox.ModAPI.Ingame.IMyStoreBlock;
      var provider = inGame as IMyTextSurfaceProvider;
      _drawSurface = new DrawSurface(provider.GetSurface(0), storeBlock);
      _storeInventory = Block.GetInventory() as MyInventory;
      _storePrices = new HashSet<long>();
      _addQueue = new Queue<MyPhysicalInventoryItem>(numOffers * 2);
      _minPrice = mod.RaritySettings.BasePrice_Minimum;
      _maxPrice = mod.RaritySettings.BasePrice_Maximum;
      _mod = mod;

      storeBlock.Flags |= VRage.ModAPI.EntityFlags.Sync;
      storeBlock.Synchronized = true;

      for (int i = 0; i < numOffers; i++)
      {
        if (!_storeInventory.CanItemsBeAdded(1, mod.MyItemType_Datapad))
          break;

        int num, wanted = MyUtils.GetRandomInt(1, 3);
        var schematicString = mod.GetRandomSchematic(out num, wanted);
        MyDefinitionId schematic;
        if (schematicString == null || !MyDefinitionId.TryParse(schematicString, out schematic))
          break;

        BlockRarity br;
        if (!mod.BlockTypeRarity.TryGetValue(schematic, out br))
        {
          br = new BlockRarity(schematic);
          mod.BlockTypeRarity[schematic] = br;
        }

        var price = GetUniqueItemPrice(br);
        var invItem = mod.BuildDatapad(schematic.ToString(), storePrice: price);

        long storeId;
        var result = AddOffer(invItem, price, num, out storeId);

        if (mod.DebugMode)
          mod.Logger.Log($"Attempting to add item to store: Item = {schematic}, Price = {price}, result = {result.ToString()}, StoreId = {storeId}");

        if (result != MyStoreInsertResults.Success)
          break;

        OrderDict[price] = new ItemInfo(schematic, storeId, price, num);
        for (int j = 0; j < num; j++)
          _addQueue.Enqueue(invItem);
      }

      if (OrderDict.Count > 0)
      {
        storeBlock.CustomName = "StoreBlock [Schematic Hunter]";
        storeBlock.CustomNameChanged += StoreBlock_CustomNameChanged;
        storeBlock.OnClosing += StoreBlock_OnClosing;
        SetupDisplay();
      }
    }

    public void Refill()
    {
      if (_mod.DebugMode)
        _mod.Logger.Log($"Running Store.Refill()");

      OrderDict.Clear();
      _queryList.Clear();

      var invList = _storeInventory.GetItems();
      Block.GetPlayerStoreItems(_queryList);

      foreach (var item in _queryList)
        Block.CancelStoreItem(item.Id);

      var numOffers = MyUtils.GetRandomInt(12, 15);
      numOffers -= _queryList.Count;
      bool done = false;

      if (_mod.DebugMode)
        _mod.Logger.Log($"Found {invList.Count} items in block inventory and {_queryList.Count} offers. Will add {numOffers} more");

      var completedHash = new HashSet<string>();
      for (int i = 0; i < invList.Count; i++)
      {
        MyDefinitionId typeDef;
        int price;

        var invItem = invList[i];
        var datapad = invItem.Content as MyObjectBuilder_Datapad;
        if (datapad == null || !MyDefinitionId.TryParse(datapad.Name, out typeDef))
          continue;

        if (completedHash.Contains(datapad.Name))
          continue;

        var lines = datapad.Data.Split('\n');
        if (lines.Length < 3)
          continue;

        var lineItems = lines[2].Split(':');
        if (lineItems.Length < 2 || !int.TryParse(lineItems[1], out price))
          continue;

        completedHash.Add(datapad.Name);

        if (price < 0)
        {
          BlockRarity br;
          if (!_mod.BlockTypeRarity.TryGetValue(typeDef, out br))
          {
            br = new BlockRarity(typeDef);
            _mod.BlockTypeRarity[typeDef] = br;
          }

          price = GetUniqueItemPrice(br);
        }

        int orderAmount = 1;
        for (int j = i + 1; j < invList.Count; j++)
        {
          var nextItem = invList[j];
          var nextDatpad = nextItem.Content as MyObjectBuilder_Datapad;
          if (nextDatpad == null)
            continue;

          if (nextDatpad.Name == datapad.Name)
            orderAmount++;
        }

        long storeId;
        var data = new MyStoreItemData(datapad.GetId(), orderAmount, price, OnPurchase, null);
        var result = Block.InsertOffer(data, out storeId);

        if (_mod.DebugMode)
          _mod.Logger.Log($"Attempting to add item to store: Item = {typeDef.ToString()}, Price = {price}, result = {result.ToString()}, StoreId = {storeId}");

        if (result != MyStoreInsertResults.Success)
        {
          done = true;
          break;
        }

        ItemInfo info;
        if (!OrderDict.TryGetValue(price, out info))
        {
          info = new ItemInfo(typeDef, storeId, price, orderAmount);
          OrderDict[price] = info;
        }
        else
          info.NumberAvailable += orderAmount;

        _storePrices.Add(price);
      }

      for (int i = 0; i < numOffers; i++)
      {
        if (done || !_storeInventory.CanItemsBeAdded(1, _mod.MyItemType_Datapad))
          break;

        int num, wanted = MyUtils.GetRandomInt(1, 3);
        var schematicString = _mod.GetRandomSchematic(out num, wanted);
        MyDefinitionId schematic;
        if (schematicString == null || !MyDefinitionId.TryParse(schematicString, out schematic))
          break;

        int price = -1;
        foreach (var item in OrderDict)
        {
          if (item.Value.ItemDefinition == schematic)
          {
            price = (int)item.Key;
            break;
          }
        }

        if (price < 0)
        {
          BlockRarity br;
          if (!_mod.BlockTypeRarity.TryGetValue(schematic, out br))
          {
            br = new BlockRarity(schematic);
            _mod.BlockTypeRarity[schematic] = br;
          }

          price = GetUniqueItemPrice(br);
        }

        var invItem = _mod.BuildDatapad(schematic.ToString(), storePrice: price);

        long storeId;
        var result = AddOffer(invItem, price, num, out storeId);

        if (_mod.DebugMode)
          _mod.Logger.Log($"Attempting to add item to store: Item = {schematic}, Price = {price}, result = {result.ToString()}, StoreId = {storeId}");

        if (result != MyStoreInsertResults.Success)
          break;

        ItemInfo info;
        if (!OrderDict.TryGetValue(price, out info))
        {
          info = new ItemInfo(schematic, storeId, price, num);
          OrderDict[price] = info;
        }
        else
          info.NumberAvailable += num;

        for (int j = 0; j < num; j++)
          _addQueue.Enqueue(invItem);
      }

      if (OrderDict.Count > 0)
        SetupDisplay();

      if (_addQueue.Count > 0)
        _mod.AddStoreToQueue(this);
    }

    public bool WorkQueue()
    {
      while (_addQueue.Count > 0)
      {
        var invItem = _addQueue.Dequeue();
        var added = _storeInventory.AddItems(1, invItem.Content);
        if (!added)
          _addQueue.Enqueue(invItem);

        break;
      }

      return _addQueue.Count > 0;
    }

    private void StoreBlock_OnClosing(VRage.ModAPI.IMyEntity obj)
    {
      try
      {
        if (Block == null)
          return;

        Block.OnClosing -= StoreBlock_OnClosing;
        Block.CustomNameChanged -= StoreBlock_CustomNameChanged;

        if (Block.CustomName != "StoreBlock [Schematic Hunter]")
          Block.CustomName = "StoreBlock [Schematic Hunter]";
      }
      catch { }
    }

    private void StoreBlock_CustomNameChanged(Sandbox.ModAPI.IMyTerminalBlock obj)
    {
      try
      {
        if (obj.CustomName == "StoreBlock [Schematic Hunter]")
          return;

        obj.CustomNameChanged -= StoreBlock_CustomNameChanged;

        obj.CustomName = "StoreBlock [Schematic Hunter]";

        obj.CustomNameChanged += StoreBlock_CustomNameChanged;
      }
      catch { }
    }

    int GetUniqueItemPrice(BlockRarity br)
    {
      var modifier = Math.Max(0.01f, br.PriceModifier);
      var min = _minPrice * modifier;
      var max = _maxPrice * modifier + 1;
      var price = (int)MyUtils.GetRandomFloat(min, max);

      while (!_storePrices.Add(price))
        price += MyUtils.GetRandomInt(100, 2501);

      return price;
    }

    MyStoreInsertResults AddOffer(MyPhysicalInventoryItem offerItem, int pricePer, int numToAdd, out long storeId)
    {
      MyStoreItemData itemData = new MyStoreItemData(offerItem.GetDefinitionId(), numToAdd, pricePer, OnPurchase, null);
      var result = Block.InsertOffer(itemData, out storeId);
      return result;
    }

    private void SetupDisplay()
    {
      _drawSurface.CreateSprites(OrderDict);
    }

    public void UpdateDisplay()
    {
      ++_ticks;
      var forceUpdate = _ticks % 2 == 0;

      _drawSurface.Draw(forceUpdate);
    }

    private void OnPurchase(int amtSold, int amtRemain, long cost, long storeOwner, long customer)
    {
      ItemInfo info;
      if (OrderDict.TryGetValue(cost, out info))
      {
        info.NumberAvailable -= amtSold;

        if (info.NumberAvailable <= 0)
        {
          OrderDict.Remove(cost);
          _storePrices.Remove(cost);
        }
      }

      SetupDisplay();

      if (_mod.DebugMode)
        _mod.Logger.Log($"Store.OnPurchase for {Block.EntityId}:\nAmtSold = {amtSold}, AmtRem = {amtRemain}, OwnerId = {storeOwner}, CustomerId = {customer}, Cost = {cost}\nInfo.StoreId = {info?.StoreItemId ?? -123}, Info.Name = {info?.ItemDefinition.ToString() ?? "NULL"}, info.Remaining = {info?.NumberAvailable ?? -1}");

      if (info == null)
        return;
      
      _mod.Players.Clear();
      MyAPIGateway.Players.GetPlayers(_mod.Players);

      IMyPlayer player = null;
      foreach (var p in _mod.Players)
      {
        if (p?.IdentityId == customer)
        {
          player = p;
          break;
        }
      }

      if (player?.Character == null)
      {
        if (_mod.DebugMode)
          _mod.Logger.Log($"Store.OnPurchase for {Block.EntityId}: Player.Character is null! CustomerId = {customer}", MessageType.WARNING);
  
        return;
      }

      var playerInventory = (MyInventory)player.Character.GetInventory();
      var invItems = playerInventory.GetItems();

      for (int i = invItems.Count - 1; i >= 0; i--)
      {
        var item = invItems[i];
        var datapad = item.Content as MyObjectBuilder_Datapad;
        if (datapad == null)
          continue;

        if (string.IsNullOrWhiteSpace(datapad.Name))
        {
          playerInventory.RemoveItemsAt(i);
          break;
        }
      }

      var itemToGive = _mod.BuildDatapad(info.ItemDefinition.ToString(), null, info.PricePerItem);
      if (!playerInventory.Add(itemToGive, 1))
        _mod.Logger.Log($"Unable to add datapad to player inventory! Definition = {info.Name}", MessageType.WARNING);
    }

    public void Close()
    {
      StoreBlock_OnClosing(Block);

      _storePrices?.Clear();
      _drawSurface?.Close();
      OrderDict?.Clear();
    }
  }
}

using ParallelTasks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using Sandbox.Game;
using VRage;
using VRage.Input;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Definitions.GUI;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Gui;
using Sandbox.Game.GUI;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using System.Collections.Concurrent;
using VRage.ObjectBuilders;
using VRage.Game.GUI.TextPanel;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.Game.Contracts;
using Sandbox.Game.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities.Blocks;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using SchematicProgression.Economy;
using SchematicProgression.Settings;
using SchematicProgression.Network;
using Sandbox.Common.ObjectBuilders.Definitions;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using IMyCargoContainer = Sandbox.ModAPI.IMyCargoContainer;
using VRage.Game.ModAPI.Ingame;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using VRage.Collections;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Graphics.GUI;
using IMyEntity = VRage.ModAPI.IMyEntity;
using Sandbox.Game.Entities.Cube.CubeBuilder;
using Sandbox.Game.Entities.Character;
using IMyStoreBlock = Sandbox.ModAPI.IMyStoreBlock;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Game.EntityComponents;

namespace SchematicProgression
{
  [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
  public class Session : MySessionComponentBase
  {
    HashSet<MyObjectBuilderType> _ignoreTypes = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer)
    {
      typeof(MyObjectBuilder_DebugSphere1),
      typeof(MyObjectBuilder_DebugSphere2),
      typeof(MyObjectBuilder_DebugSphere3),
    };

    HashSet<MyObjectBuilderType> _basicTypes = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer)
    {
      typeof(MyObjectBuilder_CubeBlock),
      typeof(MyObjectBuilder_Wheel),
      typeof(MyObjectBuilder_RealWheel),
      typeof(MyObjectBuilder_Reactor),
      typeof(MyObjectBuilder_Battery),
      typeof(MyObjectBuilder_BatteryBlock),
    };

    HashSet<MyObjectBuilderType> _typesThatCannotBeLocked = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer)
    {
      typeof(MyObjectBuilder_Assembler),
    };

    Dictionary<long, List<SerializableDefinitionId>> _gridsContainingSchematics = new Dictionary<long, List<SerializableDefinitionId>>();
    Dictionary<long, int> _playerInventoryCounts = new Dictionary<long, int>(MyAPIGateway.Session?.MaxPlayers ?? 16);
    Dictionary<uint, MyTuple<string, string>> _invDatapadDict = new Dictionary<uint, MyTuple<string, string>>();
    HashSet<MyDefinitionId> _alwaysLockedHash = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
    HashSet<MyEntity> _gpsRemovals = new HashSet<MyEntity>();
    HashSet<long> _ignoreGrids = new HashSet<long>();
    HashSet<long> _activePlayerIds = new HashSet<long>();
    HashSet<long> _playersInCockpit = new HashSet<long>();
    //HashSet<long> _playersControllingGrids = new HashSet<long>();
    HashSet<long> _storeBlockRemovals = new HashSet<long>();
    HashSet<long> _localStoreBlockIds = new HashSet<long>();
    List<MyDefinitionId> _spawnedItems = new List<MyDefinitionId>();
    List<MyDefinitionId> _schematics = new List<MyDefinitionId>();
    List<MyDefinitionId> _temp = new List<MyDefinitionId>();
    List<KeyValuePair<MyEntity, IMyGps>> _gpsUpdates = new List<KeyValuePair<MyEntity, IMyGps>>(10);
    List<string> _splitStrings = new List<string>(10);
    List<long> _gpsAddIDs = new List<long>(50);
    List<long> _localGpsGridIds = new List<long>(50);
    List<IMyCargoContainer> _gridCargos = new List<IMyCargoContainer>();
    List<MyEntity> _entities = new List<MyEntity>();
    List<MyPhysicalInventoryItem> _charItems = new List<MyPhysicalInventoryItem>();
    List<SerializableDefinitionId> _restrictedDefinitions = new List<SerializableDefinitionId>();
    Queue<Store> _newStores = new Queue<Store>(10);
    Queue<string> _schematicsToBuild = new Queue<string>();

    SerializableUniversalBlockSettings _universalSaveData;
    PlayerSettings _localPlayerSettings;
    DatapadPlacement _datapadPlacement;
    ResearchGroupSettings _blockGroupSettings;

    public Logger Logger;
    public Networking NetworkHandler;
    public ServerSettings ServerSaveData;
    public BlockRaritySettings RaritySettings;
    public UniversalBlockSettings UniversalBlockSettings;
    public List<IMyPlayer> Players = new List<IMyPlayer>(MyAPIGateway.Session?.MaxPlayers ?? 16);
    public HashSet<ulong> TempUnlockHash = new HashSet<ulong>();
    public HashSet<MyDefinitionId> LearnHash = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
    public HashSet<MyDefinitionId> AlwaysUnlockedHash = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
    public MyConcurrentDictionary<MyEntity, IMyGps> LocalGPSDictionary = new MyConcurrentDictionary<MyEntity, IMyGps>();
    public MyConcurrentDictionary<long, Store> StoreBlockDict = new MyConcurrentDictionary<long, Store>();
    public Dictionary<ulong, PlayerSettings> AllPlayerSettings = new Dictionary<ulong, PlayerSettings>(MyAPIGateway.Session?.MaxPlayers ?? 16);
    public Dictionary<SerializableDefinitionId, BlockRarity> BlockTypeRarity = new Dictionary<SerializableDefinitionId, BlockRarity>();
    public Dictionary<MyDefinitionId, ResearchGroup> BlockGroupDict = new Dictionary<MyDefinitionId, ResearchGroup>(25, MyDefinitionId.Comparer);
    public MyItemType MyItemType_Datapad = new MyItemType("MyObjectBuilder_Datapad", "Datapad");
    public bool IsServer, IsDedicatedServer, Registered, FactionSharingEnabled, UseDefaultSpawnSystem, DebugMode;

    public static Session Instance;

    Random rand = new Random();
    MyCommandLine CLI = new MyCommandLine();
    StringBuilder _splitSB = new StringBuilder(1024);
    IMyHudNotification _hudMsg;
    bool _needsUpdate, _gpsUpdatesAvailable, _toolbarChecked, _useSpawnTimer;
    int _tickCounter, _spawnTimer, _spawnProbabilityStation, _spawnProbabilityShip;
    string[] _dataLines = new string[3];
    string[] _lineItems = new string[2];

    protected override void UnloadData()
    {
      try
      {
        Registered = false;
        RemoveCubeBuilderEvents();
        MyAPIGateway.Utilities.MessageEntered -= HandleMessage;
        MyEntities.OnEntityAdd -= MyEntities_OnEntityAdd;
        MyEntities.OnEntityRemove -= MyEntities_OnEntityRemove;
        //MyVisualScriptLogicProvider.RemoteControlChanged -= OnRemoteControlChanged;
        MyVisualScriptLogicProvider.PlayerEnteredCockpit -= OnPlayerEnteredCockpit;
        MyVisualScriptLogicProvider.PlayerLeftCockpit -= OnPlayerLeftCockpit;
        MyVisualScriptLogicProvider.PlayerConnected -= OnPlayerConnected;
        MyVisualScriptLogicProvider.PlayerDisconnected -= OnPlayerDisconnected;
        MyVisualScriptLogicProvider.PlayerDied -= OnPlayerDisconnected;
        MyVisualScriptLogicProvider.PlayerSpawned -= OnPlayerConnected;
        MyCubeGrid.OnSplitGridCreated -= Grid_OnSplitGridCreated;

        var player = MyAPIGateway.Session?.Player;
        if (player?.Character != null)
          UnregisterPlayerInventory(player);

        if (StoreBlockDict != null)
        {
          foreach (var kvp in StoreBlockDict)
            kvp.Value?.Close();

          StoreBlockDict.Clear();
        }

        NetworkHandler?.Unregister();
        UniversalBlockSettings?.Close();
        ServerSaveData?.Close();
        RaritySettings?.Close();
        _blockGroupSettings?.Close();
        _localPlayerSettings?.Close();
        _universalSaveData?.Close();
        _datapadPlacement?.Close();
        _hudMsg?.Hide();

        AlwaysUnlockedHash?.Clear();
        LocalGPSDictionary?.Clear();
        AllPlayerSettings?.Clear();
        BlockTypeRarity?.Clear();
        LearnHash?.Clear();
        Players?.Clear();

        _typesThatCannotBeLocked?.Clear();
        _restrictedDefinitions?.Clear();
        _alwaysLockedHash?.Clear();
        _basicTypes?.Clear();
        _ignoreTypes?.Clear();
        _schematics?.Clear();
        _spawnedItems?.Clear();
        _schematicsToBuild?.Clear();
        _gridCargos?.Clear();
        _temp?.Clear();
        _playerInventoryCounts?.Clear();
        _gridsContainingSchematics?.Clear();
        _gpsUpdates?.Clear();
        _gpsRemovals?.Clear();
        _playersInCockpit?.Clear();
        //_playersControllingGrids?.Clear();
        _activePlayerIds?.Clear();
        _storeBlockRemovals?.Clear();
        _localStoreBlockIds?.Clear();
        _ignoreGrids?.Clear();
        _localGpsGridIds?.Clear();
        _gpsAddIDs?.Clear();
        _entities?.Clear();
        _charItems?.Clear();
        _invDatapadDict?.Clear();
        _splitSB?.Clear();
        _splitStrings?.Clear();

        Instance = null;
      }
      catch (Exception ex)
      {
        Logger?.Log($"Exception in UnloadData: {ex.Message}\n{ex.StackTrace}");
      }
      finally
      {
        Logger?.Close();
        base.UnloadData();
      }
    }

    void GetPlayers()
    {
      Players.Clear();
      MyAPIGateway.Players.GetPlayers(Players);
    }

    private void OnPlayerDisconnected(long playerId)
    {
      foreach (var player in Players)
      {
        if (player.IsBot || MyAPIGateway.Players.TryGetSteamId(player.IdentityId) == 0)
          continue;

        if (player.IdentityId == playerId)
        {
          UnregisterPlayerInventory(player);
          break;
        }
      }
    }

    private void OnPlayerConnected(long playerId)
    {
      _activePlayerIds.Add(playerId);
    }

    public override void SaveData()
    {
      if (Registered)
        SaveConfig();

      base.SaveData();
    }

    public void SaveConfig()
    {
      try
      {
        if (!Registered)
          return;

        if (!IsServer)
        {
          var packet = new SettingUpdatePacket(new SerializablePlayerSettings(_localPlayerSettings));
          NetworkHandler.SendToServer(packet);
          return;
        }

        if (RaritySettings == null)
          RaritySettings = new BlockRaritySettings { BlockRarityList = new List<BlockRarity>(BlockTypeRarity?.Count ?? 80) };

        if (_datapadPlacement == null)
          _datapadPlacement = new DatapadPlacement();

        RaritySettings.BlockRarityList.Clear();
        foreach (var kvp in BlockTypeRarity)
          RaritySettings.AddToList(kvp.Key, kvp.Value);

        _datapadPlacement.GridsGivenADatapad.Clear();
        foreach (var kvp in _gridsContainingSchematics)
        {
          var id = kvp.Key;
          var grid = MyEntities.GetEntityById(id, true);
          var gridPair = new DatapadGridPair(id, grid?.DisplayName ?? "Unknown", kvp.Value);
          _datapadPlacement.GridsGivenADatapad.Add(gridPair);

          if (!_datapadPlacement.GridHistory.Contains(id))
            _datapadPlacement.GridHistory.Add(id);
        }

        foreach (var pSetting in ServerSaveData.PlayerSettings)
        {
          PlayerSettings updatedSettings;
          if (AllPlayerSettings.TryGetValue(pSetting.SteamId, out updatedSettings))
            pSetting.Update(updatedSettings);
        }

        ServerSaveData.StoreBlockIds.Clear();
        foreach (var store in StoreBlockDict)
          ServerSaveData.StoreBlockIds.Add(store.Key);

        ServerSaveData.FactionSharing = FactionSharingEnabled;

        if (IsDedicatedServer)
          SaveConfigDS();
        else
          SaveConfigLocal();
      }
      catch (Exception ex)
      {
        Logger?.Log($"Error in BlockProgression.SaveConfig:\n{ex.Message}\n\n{ex.StackTrace}", MessageType.ERROR);
      }
    }

    void SaveConfigLocal(bool initial = false)
    {
      Config.WriteFileToWorldStorage("ServerSaveData.cfg", typeof(ServerSettings), ServerSaveData, Logger);
      Config.WriteFileToWorldStorage("BlockRaritySettings.cfg", typeof(BlockRaritySettings), RaritySettings, Logger);
      Config.WriteFileToWorldStorage("GridsContainingSchematics.cfg", typeof(DatapadPlacement), _datapadPlacement, Logger);

      if (initial)
      {
        Config.WriteFileToWorldStorage("UniversalBlockSettings.cfg", typeof(SerializableUniversalBlockSettings), _universalSaveData, Logger);
        Config.WriteFileToWorldStorage("ResearchGroupSettings.cfg", typeof(ResearchGroupSettings), _blockGroupSettings, Logger);
      }
    }

    StringBuilder _sessionName = new StringBuilder(128);
    void SaveConfigDS(bool initial = false)
    {
      _sessionName.Clear();
      var prefix = MyAPIGateway.Session?.Name ?? "";

      foreach (var ch in prefix)
      {
        if (char.IsLetterOrDigit(ch))
          _sessionName.Append(ch);
      }

      prefix = _sessionName.Length > 0 ? _sessionName.ToString() : "BlockProgression";

      Config.WriteFileToLocalStorage($"{prefix}_ServerSaveData.cfg", typeof(ServerSettings), ServerSaveData, Logger);
      Config.WriteFileToLocalStorage($"{prefix}_BlockRaritySettings.cfg", typeof(BlockRaritySettings), RaritySettings, Logger);
      Config.WriteFileToLocalStorage($"{prefix}_GridsContainingSchematics.cfg", typeof(DatapadPlacement), _datapadPlacement, Logger);

      if (initial)
      {
        Config.WriteFileToLocalStorage($"{prefix}_UniversalBlockSettings.cfg", typeof(SerializableUniversalBlockSettings), _universalSaveData, Logger);
        Config.WriteFileToLocalStorage($"{prefix}_ResearchGroupSettings.cfg", typeof(ResearchGroupSettings), _blockGroupSettings, Logger);
      }
    }

    void LoadConfigLocal()
    {
      ServerSaveData = Config.ReadFileFromWorldStorage<ServerSettings>("ServerSaveData.cfg", typeof(ServerSettings), Logger);
      RaritySettings = Config.ReadFileFromWorldStorage<BlockRaritySettings>("BlockRaritySettings.cfg", typeof(BlockRaritySettings), Logger);
      _blockGroupSettings = Config.ReadFileFromWorldStorage<ResearchGroupSettings>("ResearchGroupSettings.cfg", typeof(ResearchGroupSettings), Logger);
      _universalSaveData = Config.ReadFileFromWorldStorage<SerializableUniversalBlockSettings>("UniversalBlockSettings.cfg", typeof(SerializableUniversalBlockSettings), Logger);
      _datapadPlacement = Config.ReadFileFromWorldStorage<DatapadPlacement>("GridsContainingSchematics.cfg", typeof(DatapadPlacement), Logger);
    }

    void LoadConfigDS()
    {
      _sessionName.Clear();
      var prefix = MyAPIGateway.Session?.Name ?? "";

      foreach (var ch in prefix)
      {
        if (char.IsLetterOrDigit(ch))
          _sessionName.Append(ch);
      }

      prefix = _sessionName.Length > 0 ? _sessionName.ToString() : "BlockProgression";

      ServerSaveData = Config.ReadFileFromLocalStorage<ServerSettings>($"{prefix}_ServerSaveData.cfg", typeof(ServerSettings), Logger);
      RaritySettings = Config.ReadFileFromLocalStorage<BlockRaritySettings>($"{prefix}_BlockRaritySettings.cfg", typeof(BlockRaritySettings), Logger);
      _blockGroupSettings = Config.ReadFileFromLocalStorage<ResearchGroupSettings>($"{prefix}_ResearchGroupSettings.cfg", typeof(ResearchGroupSettings), Logger);
      _universalSaveData = Config.ReadFileFromLocalStorage<SerializableUniversalBlockSettings>($"{prefix}_UniversalBlockSettings.cfg", typeof(SerializableUniversalBlockSettings), Logger);
      _datapadPlacement = Config.ReadFileFromLocalStorage<DatapadPlacement>($"{prefix}_GridsContainingSchematics.cfg", typeof(DatapadPlacement), Logger);
    }

    public override void LoadData()
    {
      try
      {
        _restrictedDefinitions.Clear();

        foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
        {
          var cubeDef = def as MyCubeBlockDefinition;
          if (cubeDef == null)
            continue;

          if (!cubeDef.Public)
            _restrictedDefinitions.Add(cubeDef.Id);
        }
      }
      finally
      {
        base.LoadData();
      }
    }

    public override void BeforeStart()
    {
      try
      {
        Instance = this;
        IsServer = MyAPIGateway.Multiplayer.IsServer;
        IsDedicatedServer = MyAPIGateway.Utilities.IsDedicated;

        Logger = new Logger("BlockProgression.log", IsDedicatedServer);
        NetworkHandler = new Networking(22452, this);
        NetworkHandler.Register();

        MyAPIGateway.Utilities.MessageEntered += HandleMessage;

        try
        {
          if (MyAPIGateway.Session?.SessionSettings != null)
            MyAPIGateway.Session.SessionSettings.EnableResearch = false;
        }
        catch { }

        if (IsServer)
        {
          if (IsDedicatedServer)
            LoadConfigDS();
          else
            LoadConfigLocal();

          if (_blockGroupSettings == null)
          {
            _blockGroupSettings = new ResearchGroupSettings();
            var group = new ResearchGroup();
            group.BlockDefinitons.Add(new SerializableDefinitionId(null, ""));

            _blockGroupSettings.ResearchGroups.Add(group);
          }
          else if (_blockGroupSettings.ResearchGroups.Count > 0)
          {
            foreach (var groupList in _blockGroupSettings.ResearchGroups)
            {
              if (groupList?.BlockDefinitons == null)
                continue;

              for (int i = 0; i < groupList.BlockDefinitons.Count; i++)
              {
                var def = groupList.BlockDefinitons[i];
                if (def.TypeId.IsNull)
                  continue;

                ResearchGroup group;
                if (!BlockGroupDict.TryGetValue(def, out group))
                {
                  group = new ResearchGroup();
                  BlockGroupDict[def] = group;
                }

                for (int j = 0; j < groupList.BlockDefinitons.Count; j++)
                {
                  var toAdd = groupList.BlockDefinitons[j];
                  if (!group.BlockDefinitons.Contains(toAdd))
                    group.BlockDefinitons.Add(toAdd);
                }
              }
            }
          }
          else
          {
            var group = new ResearchGroup();
            group.BlockDefinitons.Add(new SerializableDefinitionId(null, ""));

            _blockGroupSettings.ResearchGroups.Add(group);
          }

          var allCubeBlockDefinitions = MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>();

          UniversalBlockSettings = new UniversalBlockSettings()
          {
            AlwaysUnlocked = new List<SerializableDefinitionId>(Math.Max(16, AlwaysUnlockedHash.Count)),
            AlwaysLocked = new List<SerializableDefinitionId>(Math.Max(16, _alwaysLockedHash.Count)),
            AllBlockTypes = new List<SerializableDefinitionId>(allCubeBlockDefinitions.Count)
          };

          if (ServerSaveData == null)
          {
            ServerSaveData = new ServerSettings()
            {
              PlayerSettings = new List<SerializablePlayerSettings>(MyAPIGateway.Session?.MaxPlayers ?? 16),
              StoreBlockIds = new List<long>(10),
              FactionSharing = false
            };
          }

          FactionSharingEnabled = ServerSaveData.FactionSharing;
          UseDefaultSpawnSystem = ServerSaveData.UseBuiltInSpawnSystem;
          _spawnProbabilityStation = (int)(MathHelper.Clamp(ServerSaveData.SpawnProbabilityPercent_Stations, 0, 100) * 0.1f);
          _spawnProbabilityShip = (int)(MathHelper.Clamp(ServerSaveData.SpawnProbabilityPercent_Ships, 0, 100) * 0.1f);

          if (_universalSaveData == null)
          {
            _universalSaveData = new SerializableUniversalBlockSettings()
            {
              AlwaysUnlocked = new List<SerializableDefinitionId>(Math.Max(16, AlwaysUnlockedHash.Count)),
              AlwaysLocked = new List<SerializableDefinitionId>(Math.Max(16, _alwaysLockedHash.Count)),
              AllBlockTypes = new List<SerializableDefinitionId>(allCubeBlockDefinitions.Count)
            };

            foreach (var cubeDef in allCubeBlockDefinitions)
            {
              var def = cubeDef.Id;
              if (_ignoreTypes.Contains(def.TypeId))
                continue;

              if (_basicTypes.Contains(def.TypeId) || _typesThatCannotBeLocked.Contains(def.TypeId))
              {
                if (!UniversalBlockSettings.AlwaysUnlocked.Contains(def))
                {
                  UniversalBlockSettings.AlwaysUnlocked.Add(def);
                  _universalSaveData.AlwaysUnlocked.Add(def);
                }
              }

              if (!UniversalBlockSettings.AllBlockTypes.Contains(def))
              {
                UniversalBlockSettings.AllBlockTypes.Add(def);
                _universalSaveData.AllBlockTypes.Add(def);
              }
            }
          }
          else
          {
            _universalSaveData.AllBlockTypes.Clear();
            UniversalBlockSettings.AllBlockTypes.Clear();

            foreach (var cubeDef in allCubeBlockDefinitions)
            {
              var def = cubeDef.Id;
              if (_ignoreTypes.Contains(def.TypeId))
                continue;

              if (!UniversalBlockSettings.AllBlockTypes.Contains(def))
              {
                UniversalBlockSettings.AllBlockTypes.Add(def);
                _universalSaveData.AllBlockTypes.Add(def);
              }
            }

            for (int i = _universalSaveData.AlwaysLocked.Count - 1; i >= 0; i--)
            {
              var def = _universalSaveData.AlwaysLocked[i];
              if (_typesThatCannotBeLocked.Contains(def.TypeId))
                _universalSaveData.AlwaysLocked.RemoveAtFast(i);
              else
                UniversalBlockSettings.AlwaysLocked.Add(def);
            }

            foreach (var def in _universalSaveData.AlwaysUnlocked)
              UniversalBlockSettings.AlwaysUnlocked.Add(def);

            foreach (var bType in _typesThatCannotBeLocked)
            {
              LearnHash.Clear();
              MyDefinitionManager.Static.TryGetDefinitionsByTypeId(bType, LearnHash);

              foreach (var def in LearnHash)
              {
                if (!UniversalBlockSettings.AlwaysUnlocked.Contains(def))
                  UniversalBlockSettings.AlwaysUnlocked.Add(def);
              }
            }
          }

          foreach (var def in UniversalBlockSettings.AlwaysUnlocked)
            AlwaysUnlockedHash.Add(def);

          foreach (var def in UniversalBlockSettings.AlwaysLocked)
          {
            if (!AlwaysUnlockedHash.Contains(def))
              _alwaysLockedHash.Add(def);
          }

          foreach (var item in ServerSaveData.PlayerSettings)
            AllPlayerSettings[item.SteamId] = new PlayerSettings(item, this);

          if (_datapadPlacement == null)
            _datapadPlacement = new DatapadPlacement();

          foreach (var item in _datapadPlacement.GridsGivenADatapad)
          {
            List<SerializableDefinitionId> bpList;
            if (!_gridsContainingSchematics.TryGetValue(item.GridId, out bpList))
            {
              bpList = item.Schematics;
              _gridsContainingSchematics[item.GridId] = bpList;
            }

            if (!_datapadPlacement.GridHistory.Contains(item.GridId))
              _datapadPlacement.GridHistory.Add(item.GridId);
          }

          if (RaritySettings == null)
          {
            RaritySettings = new BlockRaritySettings()
            {
              BlockRarityList = new List<BlockRarity>(UniversalBlockSettings.AllBlockTypes.Count)
            };
          }

          foreach (var setting in RaritySettings.BlockRarityList)
            BlockTypeRarity[setting.BlockDefinition] = setting;

          foreach (var bType in _universalSaveData.AllBlockTypes)
          {
            if (!BlockTypeRarity.ContainsKey(bType))
              BlockTypeRarity[bType] = new BlockRarity(bType);
          }

          RaritySettings.BlockRarityList.Clear();
          foreach (var item in BlockTypeRarity)
            RaritySettings.AddToList(item.Key, item.Value);

          if (_universalSaveData.AlwaysLocked.Count == 0)
            _universalSaveData.AlwaysLocked.Add(new SerializableDefinitionId());

          foreach (var item in UniversalBlockSettings.AllBlockTypes)
          {
            if (!AlwaysUnlockedHash.Contains(item) && !_alwaysLockedHash.Contains(item) && !_restrictedDefinitions.Contains(item) && !_schematics.Contains(item))
            {
              _schematics.Add(item);
            }
          }

          if (IsDedicatedServer)
            SaveConfigDS(true);
          else
            SaveConfigLocal(true);
          Registered = true;

          MyEntities.OnEntityAdd += MyEntities_OnEntityAdd;
          MyEntities.OnEntityRemove += MyEntities_OnEntityRemove;
          //MyVisualScriptLogicProvider.RemoteControlChanged += OnRemoteControlChanged;
          MyVisualScriptLogicProvider.PlayerEnteredCockpit += OnPlayerEnteredCockpit;
          MyVisualScriptLogicProvider.PlayerLeftCockpit += OnPlayerLeftCockpit;
          MyVisualScriptLogicProvider.PlayerConnected += OnPlayerConnected;
          MyVisualScriptLogicProvider.PlayerDisconnected += OnPlayerDisconnected;
          MyVisualScriptLogicProvider.PlayerDied += OnPlayerDisconnected;
          MyVisualScriptLogicProvider.PlayerSpawned += OnPlayerConnected;
          MyCubeGrid.OnSplitGridCreated += Grid_OnSplitGridCreated;

          var hash = MyEntities.GetEntities();
          foreach (var ent in hash)
            MyEntities_OnEntityAdd(ent);

          _useSpawnTimer = true;
        }

        if (!IsDedicatedServer)
        {
          Registered = true;
          var packet = new SettingRequestPacket();
          NetworkHandler.SendToServer(packet);

          SetupCubeBuilderEvents();

          if (IsServer)
          {
            var player = MyAPIGateway.Session?.Player;
            if (player?.Character != null)
              RegisterPlayerInventory(player);
            else
              OnPlayerConnected(player?.IdentityId ?? -1);
          }
        }
      }
      catch (Exception ex)
      {
        Logger?.Log($"Error in BuildProgression.BeforeStart\n{ex.Message}\n\n{ex.StackTrace}", MessageType.ERROR);
        Registered = false;
        UnloadData();
      }
    }

    bool _cubeBuilderEventsActive;
    bool _cubeActivatedInvoked, _cubeDeactivatedInvoked, _cubeVariantInvoked, _cubeSizeInvoked;

    void ResetCubeStuff()
    {
      _cubeActivatedInvoked = _cubeVariantInvoked = _cubeSizeInvoked = false;
    }

    void SetupCubeBuilderEvents()
    {
      if (_cubeBuilderEventsActive)
        return;

      MyCubeBuilder.Static.OnBlockSizeChanged += CubeBuilder_OnBlockSizeChanged;
      MyCubeBuilder.Static.OnBlockVariantChanged += CubeBuilder_OnBlockVariantChanged;
      MyCubeBuilder.Static.OnActivated += CubeBuilder_OnActivated;
      MyCubeBuilder.Static.OnDeactivated += CubeBuilder_OnDeactivated;
      //MyCubeBuilder.Static.OnBlockAdded += CubeBuilder_OnBlockAdded;

      _cubeBuilderEventsActive = true;
    }

    void RemoveCubeBuilderEvents()
    {
      if (MyCubeBuilder.Static == null)
        return;

      MyCubeBuilder.Static.OnBlockSizeChanged -= CubeBuilder_OnBlockSizeChanged;
      MyCubeBuilder.Static.OnBlockVariantChanged -= CubeBuilder_OnBlockVariantChanged;
      MyCubeBuilder.Static.OnActivated -= CubeBuilder_OnActivated;
      MyCubeBuilder.Static.OnDeactivated -= CubeBuilder_OnDeactivated;
      //MyCubeBuilder.Static.OnBlockAdded -= CubeBuilder_OnBlockAdded;

      _cubeBuilderEventsActive = false;
    }

    bool _activated, _variantChanged, _blockSizeChanged;
    int _currentBuildIndex = -1, _currentToolbarSlot = -1, _mouseDeltaSign;
    MyCubeBlockDefinition _currentBuildDef, _primaryGuiBuildDef;
    MyCubeSize? _currentCubeSize;

    int GetBuilderIndex(MyCubeBlockDefinition cubeDef)
    {
      var blocks = cubeDef?.BlockVariantsGroup?.Blocks;
      if (blocks?.Length > 0)
      {
        for (int i = 0; i < blocks.Length; i++)
        {
          if (cubeDef.Id == blocks[i]?.Id)
            return i;
        }
      }

      return -1;
    }

    private void CubeBuilder_OnBlockAdded(MyCubeBlockDefinition addedDef)
    {
      if (addedDef == null)
        return;

      if (!addedDef.Public)
      {
        int idx;
        MyCubeBlockDefinition other;
        if (!IsOtherSizeUnlocked(addedDef, out other, out idx))
        {
          CheckToolbarLocal();
          return;
        }

        _currentBuildDef = other;
        _currentCubeSize = other.CubeSize;
        _currentBuildIndex = idx;
        _primaryGuiBuildDef = other?.BlockVariantsGroup?.PrimaryGUIBlock ?? addedDef?.BlockVariantsGroup?.PrimaryGUIBlock;
      }
      else
      {
        _currentBuildDef = addedDef;
        _currentCubeSize = addedDef?.CubeSize;
        _primaryGuiBuildDef = addedDef?.BlockVariantsGroup?.PrimaryGUIBlock;
        _currentBuildIndex = GetBuilderIndex(addedDef);
      }

      try
      {
        if (MyAPIGateway.Session?.Player?.Character == null)
          return;

        _searching = true;

        for (int i = 0; i < 9; i++)
        {
          try
          {
            MyVisualScriptLogicProvider.SwitchToolbarToSlotLocal(i);
            var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
            if (def?.Id == addedDef.Id)
            {
              _currentToolbarSlot = i;
              break;
            }
          }
          catch (Exception ex)
          {
            Logger.Log($"CubeBuilder_OnBlockAdded: Attempted to check slot {i} of the toolbar but encountered an exception - clearing the toolbar slot.\nException info: {ex.Message}\n\n{ex.StackTrace}\n", MessageType.ERROR);
            MyVisualScriptLogicProvider.ClearToolbarSlotLocal(i);
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Log($"Error during CubeBuilder_OnBlockAdded: {ex.Message}\n{ex.StackTrace}\n\nClearing Toolbar..", MessageType.ERROR);
        MyVisualScriptLogicProvider.ClearAllToolbarSlots();
      }

      if (_primaryGuiBuildDef != null)
        MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _primaryGuiBuildDef;

      MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _currentBuildDef;
      _searching = false;
    }

    private void CubeBuilder_OnDeactivated()
    {
      if (_searching || _cubeDeactivatedInvoked)
        return;

      _cubeDeactivatedInvoked = true;

      _currentBuildIndex = -1;
      _mouseDeltaSign = 0;
      _currentToolbarSlot = -1;
      _currentCubeSize = null;
      _currentBuildDef = _primaryGuiBuildDef = null;
      _activated = _variantChanged = _blockSizeChanged = false;
    }

    private void CubeBuilder_OnActivated()
    {
      try
      {
        if (_searching || _cubeActivatedInvoked)
          return;

        _cubeActivatedInvoked = true;

        var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
        _activated = def == null;

        if (_activated)
          return;

        if (_currentToolbarSlot < 0)
        {
          CubeBuilder_OnBlockAdded(def);
          return;
        }

        if (def.Public)
          _currentBuildDef = def;
        else
        {
          int idx;
          MyCubeBlockDefinition other;
          if (IsOtherSizeUnlocked(def, out other, out idx))
          {
            _currentBuildDef = other;
            MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _currentBuildDef;
          }
          else
            MyCubeBuilder.Static.OnLostFocus();

          //else if (_currentBuildDef != null && def.Id.TypeId == _currentBuildDef.Id.TypeId && def.CubeSize != _currentBuildDef.CubeSize)
          //{
          //  MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _currentBuildDef;
          //}
          //else
          //{
          //  var primary = def.BlockVariantsGroup?.PrimaryGUIBlock;
          //  if (primary?.Public == true)
          //  {
          //    _currentBuildDef = primary;
          //    MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = primary;
          //  }
          //  else
          //    MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _currentBuildDef;
          //}
        }

        _currentBuildIndex = GetBuilderIndex(_currentBuildDef);
        _currentCubeSize = _currentBuildDef?.CubeSize;
        _primaryGuiBuildDef = _currentBuildDef?.BlockVariantsGroup?.PrimaryGUIBlock;
      }
      catch (Exception ex)
      {
        Logger.Log($"Exception during CubeBuilder_OnActivated: {ex.Message}\n{ex.StackTrace}", MessageType.ERROR);
      }
    }

    private void CubeBuilder_OnBlockVariantChanged()
    {
      try
      {
        if (_searching || _cubeVariantInvoked)
          return;

        _cubeVariantInvoked = true;

        var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
        _variantChanged = def == null;

        if (_variantChanged)
          return;

        if (def.Public && def.CubeSize == _currentCubeSize)
        {
          _currentBuildDef = def;
          _currentBuildIndex = GetBuilderIndex(_currentBuildDef);
          return;
        }

        var blocks = def.BlockVariantsGroup?.Blocks;
        if (blocks?.Length > 0)
        {
          int sign = _mouseDeltaSign;
          var startIdx = _currentBuildIndex + sign;
          for (int i = 0; i < blocks.Length; i++)
          {
            var next = (startIdx + sign * i) % blocks.Length;
            if (next < 0)
              next = blocks.Length + next;

            var blockDef = blocks[next];
            if (blockDef?.Id != _currentBuildDef.Id && blockDef.Public && blockDef.CubeSize == _currentCubeSize)
            {
              _currentBuildDef = blockDef;
              _currentBuildIndex = next;

              break;
            }
          }
        }

        MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _currentBuildDef;
      }
      catch (Exception ex)
      {
        Logger.Log($"Exception in CubeBuilder_OnBlockVariantChanged: {ex.Message}\n{ex.StackTrace}", MessageType.ERROR);
      }
    }

    private void CubeBuilder_OnBlockSizeChanged()
    {
      try
      {
        if (_searching || _cubeSizeInvoked)
          return;

        _cubeSizeInvoked = true;

        var newDef = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
        _blockSizeChanged = newDef == null || newDef.Id == _currentBuildDef?.Id;

        if (_blockSizeChanged)
          return;

        if (newDef.Public)
        {
          _currentBuildDef = newDef;
          _currentBuildIndex = GetBuilderIndex(_currentBuildDef);
          _currentCubeSize = _currentBuildDef.CubeSize;
        }
        else
        {
          //MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _primaryGuiBuildDef;
          MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition = _currentBuildDef;

          if (_currentCubeSize != _currentBuildDef?.CubeSize)
            _currentCubeSize = _currentBuildDef?.CubeSize;

          var newSize = _currentBuildDef.CubeSize == MyCubeSize.Large ? "small" : "large";
          ShowMessage($"You do not have the {newSize} version for {_currentBuildDef.DisplayNameText} unlocked!", timeToLive: 3000);
        }
      }
      catch (Exception ex)
      {
        Logger.Log($"Exception in CubeBuilder_OnBlockSizeChanged: {ex.Message}\n{ex.StackTrace}", MessageType.ERROR);
      }
    }

    bool IsOtherSizeUnlocked(MyCubeBlockDefinition cubeDef, out MyCubeBlockDefinition otherDef, out int index)
    {
      otherDef = null;
      index = -1;

      var blocks = cubeDef?.BlockVariantsGroup?.Blocks;
      if (blocks?.Length > 0)
      {
        for (int i = 0; i < blocks.Length; i++)
        {
          var def = blocks[i];
          if (def?.Public == true && def.Id.TypeId == cubeDef.Id.TypeId && def.CubeSize != cubeDef.CubeSize)
          {
            index = i;
            otherDef = def;
            return true;
          }
        }
      }

      if (_currentBuildDef != null)
      {
        var newSize = _currentBuildDef.CubeSize == MyCubeSize.Large ? "small" : "large";
        ShowMessage($"You do not have the {newSize} version for {_currentBuildDef.DisplayNameText} unlocked!", timeToLive: 3000);
      }
      return false;
    }

    List<MyKeys> _keys = new List<MyKeys>();
    public override void HandleInput()
    {
      try
      {
        var delta = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
        if (delta != 0)
          _mouseDeltaSign = -Math.Sign(delta);

        if (!MyAPIGateway.Input.IsAnyKeyPress())
          return;

        _keys.Clear();
        MyAPIGateway.Input.GetListOfPressedKeys(_keys);

        for (int i = _keys.Count - 1; i >= 0; i--)
        {
          var key = _keys[i];
          if (!MyAPIGateway.Input.IsNewKeyPressed(key))
            continue;

          if (key == MyKeys.OemTilde)
          {
            _currentToolbarSlot = -1;
            _mouseDeltaSign = 0;
            break;
          }

          int num;
          if (MyAPIGateway.Input.IsKeyDigit(key) && GetKeyDigitPressed(key, out num))
          {
            _currentToolbarSlot = num;
            break;
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Log($"Exception in HandleInput: {ex.Message}\n{ex.StackTrace}", MessageType.ERROR);
      }

      base.HandleInput();
    }

    bool GetKeyDigitPressed(MyKeys key, out int slot)
    {
      switch (key)
      {
        case MyKeys.D0:
          slot = 0;
          break;
        case MyKeys.D1:
          slot = 1;
          break;
        case MyKeys.D2:
          slot = 2;
          break;
        case MyKeys.D3:
          slot = 3;
          break;
        case MyKeys.D4:
          slot = 4;
          break;
        case MyKeys.D5:
          slot = 5;
          break;
        case MyKeys.D6:
          slot = 6;
          break;
        case MyKeys.D7:
          slot = 7;
          break;
        case MyKeys.D8:
          slot = 8;
          break;
        case MyKeys.D9:
          slot = 9;
          break;
        default:
          slot = -1;
          return false;
      }

      return true;
    }

    //private void OnRemoteControlChanged(bool GotControlled, long playerId, string entityName, long entityId, string gridName, long gridId)
    //{
    //  if (GotControlled)
    //    _playersControllingGrids.Add(playerId);
    //  else
    //    _playersControllingGrids.Remove(playerId);
    //}

    void SendGPSEntriesToPlayers()
    {
      if (!IsServer || MyAPIGateway.Players.Count == 0)
        return;

      _gpsAddIDs.Clear();
      _gpsRemovals.Clear();

      foreach (var kvp in LocalGPSDictionary)
      {
        if (kvp.Key == null || kvp.Key.MarkedForClose)
        {
          _gpsRemovals.Add(kvp.Key);
          continue;
        }

        _gpsAddIDs.Add(kvp.Key.EntityId);
      }

      foreach (var grid in _gpsRemovals)
      {
        if (grid == null)
          continue;

        IMyGps gps;
        if (LocalGPSDictionary.TryRemove(grid, out gps) && !IsDedicatedServer)
          MyAPIGateway.Session?.GPS?.RemoveLocalGps(gps);
      }

      if (_gpsAddIDs.Count > 0)
      {
        var packet = new GpsUpdatePacket(_gpsAddIDs);
        NetworkHandler.RelayToClients(packet);
      }
    }

    void UpdateGPSLocal(IMyPlayer player)
    {
      var character = player?.Character;
      var gpsList = MyAPIGateway.Session.GPS.GetGpsList(player.IdentityId);

      for (int i = 0; i < _localGpsGridIds.Count; i++)
      {
        var gridId = _localGpsGridIds[i];
        var grid = MyEntities.GetEntityById(gridId) as MyCubeGrid;
        if (grid == null)
          continue;

        if (!LocalGPSDictionary.ContainsKey(grid))
          AddGPSMarker(grid);
      }

      foreach (var kvp in LocalGPSDictionary)
      {
        var entity = kvp.Key;
        if (entity == null)
          continue;

        var gps = kvp.Value;
        var contains = gpsList.Contains(gps);

        if (character == null || character.IsDead)
        {
          if (contains)
            MyAPIGateway.Session.GPS.RemoveLocalGps(gps);

          continue;
        }

        var gridPosition = entity.PositionComp.WorldAABB.Center;
        var distanceSq = Vector3D.DistanceSquared(character.WorldAABB.Center, gridPosition);

        if (distanceSq > 1000000)
        {
          if (contains)
            MyAPIGateway.Session.GPS.RemoveLocalGps(gps);

          continue;
        }

        gps.Coords = gridPosition;
        gps.ShowOnHud = true;

        if (gps.Hash != kvp.Value.Hash)
          _gpsUpdates.Add(new KeyValuePair<MyEntity, IMyGps>(kvp.Key, gps));

        if (!contains)
          MyAPIGateway.Session.GPS.AddLocalGps(gps);
      }

      foreach (var kvp in _gpsUpdates)
      {
        IMyGps remGPS;
        LocalGPSDictionary.TryRemove(kvp.Key, out remGPS);
        LocalGPSDictionary.TryAdd(kvp.Key, kvp.Value);
      }

      _gpsUpdates.Clear();
    }

    void DoTick2(IMyPlayer player, bool charGood)
    {
      if (IsServer && _newStores.Count > 0)
        ConfigureStores();

      if (MyCubeBuilder.Static?.IsActivated == true)
      {
        if (_activated)
          CubeBuilder_OnActivated();
        else if (_variantChanged)
          CubeBuilder_OnBlockVariantChanged();
        else if (_blockSizeChanged)
          CubeBuilder_OnBlockSizeChanged();
        else
        {
          var builder = MyCubeBuilder.Static.CubeBuilderState?.CurrentBlockDefinition;
          if (builder != null && builder?.Id.TypeId == _currentBuildDef?.Id.TypeId && builder.CubeSize != _currentBuildDef?.CubeSize)
            CubeBuilder_OnBlockSizeChanged();
        }
      }

      if (!charGood || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.Inventory)
      {
        _invDatapadDict.Clear();
        return;
      }

      var inv = player.Character.GetInventory() as MyInventory;
      if (inv == null || inv.ItemCount == 0)
        return;

      _charItems.Clear();
      _charItems.AddList(inv.GetItems());

      foreach (var item in _charItems)
      {
        var thisDatapad = item.Content as MyObjectBuilder_Datapad;
        if (thisDatapad == null)
          continue;

        MyTuple<string, string> prevData;
        if (!_invDatapadDict.TryGetValue(item.ItemId, out prevData))
        {
          MyDefinitionId test;
          if (MyDefinitionId.TryParse(thisDatapad.Name, out test))
            _invDatapadDict[item.ItemId] = MyTuple.Create(thisDatapad.Name, thisDatapad.Data);

          continue;
        }

        if (thisDatapad.Name != prevData.Item1 || thisDatapad.Data != prevData.Item2)
        {
          thisDatapad.Name = prevData.Item1;
          thisDatapad.Data = prevData.Item2;

          var packet = new DataResetPacket(item.ItemId, prevData.Item1, prevData.Item2);
          NetworkHandler.SendToServer(packet);
        }
      }
    }

    public void ResetDatapad(ulong steamUserId, uint inventoryId, string name, string data)
    {
      IMyPlayer player = null;
      foreach (var p in Players)
      {
        if (p?.Character == null || p.SteamUserId != steamUserId)
          continue;

        player = p;
        break;
      }

      if (player == null)
        return;

      var inv = player.Character?.GetInventory() as MyInventory;
      var item = inv?.GetItemByID(inventoryId);
      var datapad = item?.Content as MyObjectBuilder_Datapad;

      if (datapad == null)
        return;

      datapad.Name = name;
      datapad.Data = data;
    }

    void DoTick10(IMyPlayer player, bool charGood)
    {
      if (IsServer && _playersInCockpit.Count > 0)
        CheckInventories();

      if (!charGood)
        return;

      UpdateGPSLocal(player);

      if (_playerInSeat)
      {
        var controller = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity as MyShipController;
        _playerInSeat = controller != null;

        var buildMode = controller?.BuildingMode ?? false;

        if (buildMode && _needsBuildModeCheck)
          UpdatePlayerBlocksForBuildMode(false);
        else if (!buildMode && !_needsBuildModeCheck)
          UpdatePlayerBlocksForBuildMode(true);
      }
      else if (!_toolbarChecked)
        CheckToolbarLocal();
    }

    void DoTick60()
    {
      GetPlayers();

      if (_activePlayerIds.Count > 0)
        RegisterPlayers();

      if (_gpsUpdatesAvailable)
        SendGPSEntriesToPlayers();

      foreach (var kvp in StoreBlockDict)
      {
        if (kvp.Value?.Block == null || kvp.Value.Block.MarkedForClose)
          _storeBlockRemovals.Add(kvp.Key);
        else
          kvp.Value.UpdateDisplay();
      }

      foreach (var id in _storeBlockRemovals)
      {
        Store store;
        if (StoreBlockDict.TryRemove(id, out store))
          store.Close();
      }

      _storeBlockRemovals.Clear();
    }

    void DoTick120(bool charGood)
    {
      if (_needsUpdate)
      {
        _needsUpdate = false;
        SaveConfig();
      }

      if (!charGood)
        return;

      foreach (var id in _localStoreBlockIds)
      {
        var store = MyEntities.GetEntityById(id) as Sandbox.ModAPI.Ingame.IMyStoreBlock;
        var provider = store as Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider;
        if (provider == null)
          continue;

        var screen = provider.GetSurface(0);
        if (screen != null)
        {
          screen.ContentType = ContentType.SCRIPT;
          screen.Script = null;
        }
      }
    }

    public override void UpdateAfterSimulation()
    {
      base.UpdateAfterSimulation();

      try
      {
        if (!Registered)
          return;

        if (_useSpawnTimer)
          ++_spawnTimer;

        ++_tickCounter;
        bool isTick2 = _tickCounter % 2 == 0;
        bool isTick10 = _tickCounter % 10 == 0;
        bool isTick60 = _tickCounter % 60 == 0;
        bool isTick120 = _tickCounter % 120 == 0;

        var player = MyAPIGateway.Session?.Player;
        var charGood = player?.Character != null;

        ResetCubeStuff();

        if (isTick2)
          DoTick2(player, charGood);

        if (isTick10)
          DoTick10(player, charGood);

        if (isTick60 && IsServer)
          DoTick60();

        if (isTick120)
          DoTick120(charGood);
      }
      catch (Exception ex)
      {
        Logger?.Log($"Error in BuildProgression.UpdateAfterSimulation\n{ex.Message}\n\n{ex.StackTrace}", MessageType.ERROR);
        Registered = false;
        UnloadData();
      }
    }

    void ConfigureStores()
    {
      while (_newStores.Count > 0)
      {
        var store = _newStores.Dequeue();
        if (store?.Block == null || store.Block.MarkedForClose)
          continue;

        if (store.WorkQueue())
          _newStores.Enqueue(store);

        break;
      }
    }

    public void AddStoreToQueue(Store store) => _newStores.Enqueue(store);

    private void RegisterPlayers()
    {
      if (!IsDedicatedServer && _activePlayerIds.Contains(-1))
      {
        var localPlayer = MyAPIGateway.Session?.Player;
        if (localPlayer?.Character != null)
          RegisterPlayerInventory(localPlayer);
      }
      
      foreach (var player in Players)
      {
        if (_activePlayerIds.Count == 0)
          return;

        if (player.IsBot || MyAPIGateway.Players.TryGetSteamId(player.IdentityId) == 0)
          continue;

        if (_activePlayerIds.Contains(player.IdentityId))
          RegisterPlayerInventory(player);
      }
    }

    void CheckInventories()
    {
      foreach (var p in Players)
      {
        if (p == null || p.IsBot || MyAPIGateway.Players.TryGetSteamId(p.IdentityId) == 0)
          continue;

        var character = p?.Character;
        if (character == null || !_playersInCockpit.Contains(p.IdentityId))
          continue;

        if (CheckPlayerInventory(p))
          _needsUpdate = true;
      }
    }

    bool CheckPlayerInventory(IMyPlayer player)
    {
      var character = player?.Character;
      var inventory = character?.GetInventory() as MyInventoryBase;
      if (inventory == null)
        return false;

      int prevCount, invCount = inventory.GetItemsCount();
      _playerInventoryCounts.TryGetValue(character.EntityId, out prevCount);
      _playerInventoryCounts[character.EntityId] = invCount;

      if (prevCount >= invCount) // nothing added
        return false;

      bool needsSave = false;
      PlayerSettings pSettings;
      if (!AllPlayerSettings.TryGetValue(player.SteamUserId, out pSettings))
      {
        pSettings = new PlayerSettings(player.SteamUserId, this);
        AddBasicPlayerSettings(pSettings);
        AllPlayerSettings[player.SteamUserId] = pSettings;
        needsSave = true;
      }

      var invItems = inventory.GetItems();

      for (int i = invItems.Count - 1; i >= 0; i--)
      {
        var item = invItems[i];
        var datapad = item.Content as MyObjectBuilder_Datapad;
        if (datapad == null)
          continue;

        MyDefinitionId typeDef;
        if (!MyDefinitionId.TryParse(datapad.Name, out typeDef) || MyDefinitionManager.Static.GetCubeBlockDefinition(typeDef) == null)
          continue;

        TryRemoveDatapadFromGrid(datapad);

        if (pSettings.UnlockType(typeDef, false))
        {
          if (FactionSharingEnabled)
          {
            bool localPlayerFound = false;
            var faction = MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(player.IdentityId);
            if (faction?.AcceptHumans == true)
            {
              foreach (var kvp in faction.Members)
              {
                var steamId = MyAPIGateway.Players.TryGetSteamId(kvp.Key);
                if (steamId == 0)
                  continue;

                if (steamId == player.SteamUserId)
                  localPlayerFound = true;

                UnlockBlockTypeForPlayer(steamId, typeDef);
              }
            }

            if (!localPlayerFound)
              UnlockBlockTypeForPlayer(player, typeDef);
          }
          else
            UnlockBlockTypeForPlayer(player, typeDef, true);

          BlockRarity br;
          if (BlockTypeRarity.TryGetValue(typeDef, out br) && br?.ConsumeWhenOpened == true)
          {
            ((MyInventory)inventory).RemoveItemsAt(i);
          }

          needsSave = true;
          break;
        }
      }

      return needsSave;
    }

    void UnregisterPlayerInventory(IMyPlayer player)
    {
      var inv = player?.Character?.GetInventory() as MyInventory;
      if (inv == null)
        return;

      _activePlayerIds.Remove(player.IdentityId);
      inv.InventoryContentChanged -= Inv_InventoryContentChanged;
    }

    void RegisterPlayerInventory(IMyPlayer player)
    {
      var inv = player?.Character?.GetInventory() as MyInventory;
      if (inv == null)
        return;

      if (player.IdentityId == MyAPIGateway.Session?.Player?.IdentityId)
        _activePlayerIds.Remove(-1);

      _activePlayerIds.Remove(player.IdentityId);
      inv.InventoryContentChanged -= Inv_InventoryContentChanged;
      inv.InventoryContentChanged += Inv_InventoryContentChanged;
      _playerInventoryCounts[inv.Entity.EntityId] = inv.ItemCount;
    }

    private void OnPlayerLeftCockpit(string entityName, long playerId, string gridName)
    {
      _playersInCockpit.Remove(playerId);
      //_playersControllingGrids.Remove(playerId);

      var steamId = MyAPIGateway.Players.TryGetSteamId(playerId);
      if (steamId != 0)
      {
        var packet = new BlockUnlockPacket(false);
        NetworkHandler.SendToPlayer(packet, steamId);
      }
    }

    private void OnPlayerEnteredCockpit(string entityName, long playerId, string gridName)
    {
      _playersInCockpit.Add(playerId);
      var steamId = MyAPIGateway.Players.TryGetSteamId(playerId);
      if (steamId != 0)
      {
        var packet = new BlockUnlockPacket(true);
        NetworkHandler.SendToPlayer(packet, steamId);
      }
    }

    bool _playerInSeat, _needsBuildModeCheck = true, _firstRun = true;

    void UpdatePlayerBlocksForBuildMode(bool enable)
    {
      //if (_needsBuildModeCheck)
      //{
      //  CheckToolbarLocal();
      //}
      //else
      //{
      //  foreach (var cubeDef in _allGameDefinitions)
      //    cubeDef.Public = enable || _localPlayerSettings.UnlockedBlocks.Contains(cubeDef.Id);
      //}

      foreach (var cubeDef in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
        cubeDef.Public = enable || _localPlayerSettings.UnlockedBlocks.Contains(cubeDef.Id);

      _needsBuildModeCheck = enable;
    }

    public void UpdatePlayerBlocksForCockpit(bool playerEntered = false)
    {
      var controller = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity as MyShipController;
      _playerInSeat = controller != null;

      if (playerEntered)
      {
        if (controller?.BuildingMode == true)
        {
          controller.BuildingMode = false;
          _needsBuildModeCheck = true;
        }

        foreach (var cubeDef in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
          cubeDef.Public = !_restrictedDefinitions.Contains(cubeDef.Id);
      }
      else
        _toolbarChecked = false;
    }

    private void Inv_InventoryContentChanged(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
    {
      try
      {
        var characterEnt = inventory?.Entity;
        var player = MyAPIGateway.Players.GetPlayerControllingEntity(characterEnt);

        if (!Registered || characterEnt == null || player == null)
          return;

        int prevCount, invCount = inventory.GetItemsCount();
        _playerInventoryCounts.TryGetValue(characterEnt.EntityId, out prevCount);
        _playerInventoryCounts[characterEnt.EntityId] = invCount;

        if (prevCount > invCount) // item removed
          return;

        var datapad = item.Content as MyObjectBuilder_Datapad;
        if (datapad == null)
          return;

        MyDefinitionId typeDef;
        if (MyDefinitionId.TryParse(datapad.Name, out typeDef))
        {
          TryRemoveDatapadFromGrid(datapad);

          if (IsServer)
          {
            PlayerSettings pSettings;
            if (AllPlayerSettings.TryGetValue(player.SteamUserId, out pSettings) && pSettings?.UnlockedBlocks.Contains(typeDef) == true)
              return;

            if (FactionSharingEnabled)
            {
              bool localPlayerFound = false;
              var faction = MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(player.IdentityId);
              if (faction?.AcceptHumans == true)
              {
                foreach (var kvp in faction.Members)
                {
                  var steamId = MyAPIGateway.Players.TryGetSteamId(kvp.Key);
                  if (steamId == 0)
                    continue;

                  if (steamId == player.SteamUserId)
                    localPlayerFound = true;

                  UnlockBlockTypeForPlayer(steamId, typeDef);
                }
              }

              if (!localPlayerFound)
                UnlockBlockTypeForPlayer(player, typeDef);
            }
            else
              UnlockBlockTypeForPlayer(player, typeDef);

            BlockRarity br;
            if (BlockTypeRarity.TryGetValue(typeDef, out br) && br?.ConsumeWhenOpened == true)
            {
              ((MyInventory)inventory).RemoveItemsInternal(item.ItemId, 1);
            }
          }

          _needsUpdate = true;
        }
      }
      catch (Exception ex)
      {
        Logger.Log($"Error in InventoryContentChanged:\n{ex.Message}\n\n{ex.StackTrace}", MessageType.ERROR);
      }
    }

    void UnlockBlockTypeForPlayer(IMyPlayer player, MyDefinitionId blockType, bool force = false)
    {
      UnlockBlockTypeForPlayer(player.SteamUserId, blockType, force);
    }

    void UnlockBlockTypeForPlayer(ulong steamUserId, MyDefinitionId blockType, bool force = false)
    {
      PlayerSettings pSettings;
      if (!AllPlayerSettings.TryGetValue(steamUserId, out pSettings))
      {
        pSettings = new PlayerSettings(steamUserId, this);
        AddBasicPlayerSettings(pSettings);
        AllPlayerSettings[steamUserId] = pSettings;
        _needsUpdate = true;
      }

      ResearchGroup group;
      if (BlockGroupDict.TryGetValue(blockType, out group))
      {
        foreach (var item in group.BlockDefinitons)
          pSettings.UnlockType(item, IsDedicatedServer);

        var packet = new BlockUnlockPacket(group.BlockDefinitons);
        NetworkHandler.SendToPlayer(packet, steamUserId);
      }
      else if (pSettings.UnlockType(blockType, IsDedicatedServer) || force)
      {
        var packet = new BlockUnlockPacket(blockType);
        NetworkHandler.SendToPlayer(packet, steamUserId);
      }
    }

    public void UnlockBlockTypeLocal(MyDefinitionId typeDef)
    {
      if (_localPlayerSettings == null)
        _localPlayerSettings = new PlayerSettings(MyAPIGateway.Multiplayer.MyId, this);

      _localPlayerSettings.UnlockType(typeDef, false);
    }

    public void AddBasicPlayerSettings(PlayerSettings pSettings)
    {
      foreach (var type in AlwaysUnlockedHash)
        pSettings.UnlockedBlocks.Add(type);
    }

    public void ReceiveSettings(SerializablePlayerSettings pSettings)
    {
      if (_localPlayerSettings == null)
        _localPlayerSettings = new PlayerSettings(pSettings, this);
      else
        _localPlayerSettings.Update(pSettings);
    }

    bool _searching;

    void CheckToolbarLocal()
    {
      if (_playerInSeat)
      {
        //_toolbarChecked = true;
        //_firstRun = false;
        return;
      }

      try
      {
        var character = MyAPIGateway.Session?.Player?.Character;
        if (character == null)
          return;

        _searching = true;
        foreach (var cubeDef in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
          cubeDef.Public = true;

        for (int i = 0; i < 9; i++)
        {
          MyVisualScriptLogicProvider.SetToolbarPageLocal(i);

          for (int j = 0; j < 9; j++)
          {
            try
            {
              MyVisualScriptLogicProvider.SwitchToolbarToSlotLocal(j);
              var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
              if (def != null && !_localPlayerSettings.UnlockedBlocks.Contains(def.Id))
                MyVisualScriptLogicProvider.ClearToolbarSlotLocal(j);
            }
            catch (Exception ex)
            {
              Logger.Log($"BeforeStart: Attempted to check page {i} slot {j} of the toolbar but encountered an exception - clearing the toolbar slot. Exception info:\n{ex.Message}\n\n{ex.StackTrace}", MessageType.ERROR);
              MyVisualScriptLogicProvider.ClearToolbarSlotLocal(j);
            }
          }
        }

        MyVisualScriptLogicProvider.SetToolbarPageLocal(0);
      }
      catch (Exception ex)
      {
        Logger.Log($"Error during CheckToolbarLocal: {ex.Message}\n{ex.StackTrace}\n\nClearing Toolbar..", MessageType.ERROR);
        MyVisualScriptLogicProvider.ClearAllToolbarSlots();
      }
      finally
      {
        _firstRun = false;
        _toolbarChecked = true;
        _searching = false;

        foreach (var cubeDef in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
          cubeDef.Public = !_restrictedDefinitions.Contains(cubeDef.Id) && _localPlayerSettings.UnlockedBlocks.Contains(cubeDef.Id);
      }
    }

    internal void AddStoreId(List<long> ids)
    {
      foreach (var id in ids)
        _localStoreBlockIds.Add(id);
    }

    private void MyEntities_OnEntityRemove(MyEntity obj)
    {
      if (!Registered || !IsServer)
        return;

      var grid = obj as MyCubeGrid;
      if (grid == null)
      {
        var floater = obj as MyFloatingObject;
        if (floater != null)
          TryRemoveDatapad(floater);

        return;
      }

      IMyGps gps;
      if (LocalGPSDictionary.TryRemove(grid, out gps) && !IsDedicatedServer)
        MyAPIGateway.Session?.GPS?.RemoveLocalGps(gps);

      List<SerializableDefinitionId> bpList;
      if (!_gridsContainingSchematics.TryGetValue(grid.EntityId, out bpList))
        return;

      foreach (var schematic in bpList)
      {
        BlockRarity br;
        if (!BlockTypeRarity.TryGetValue(schematic, out br))
          return;

        br.NumberInWorld += 1;
        if (!_schematics.Contains(schematic))
          _schematics.Add(schematic);
      }

      bpList.Clear();
      _gridsContainingSchematics.Remove(grid.EntityId);
    }

    void TryRemoveDatapadFromGrid(MyObjectBuilder_Datapad datapad)
    {
      SplitByChar(datapad.Data, '\n', ref _dataLines);
      long gridId;

      if (_dataLines.Length < 2)
        return;

      SplitByChar(_dataLines[1], ':', ref _lineItems);
      if (_lineItems.Length < 2 || !long.TryParse(_lineItems[1], out gridId))
        return;

      var grid = MyEntities.GetEntityById(gridId) as MyCubeGrid;
      if (grid == null)
        return;

      List<SerializableDefinitionId> bpList;
      _gridsContainingSchematics.TryGetValue(grid.EntityId, out bpList);

      if (bpList == null)
        return;

      MyDefinitionId datapadDef;
      if (!MyDefinitionId.TryParse(datapad.Name, out datapadDef))
        return;

      for (int i = bpList.Count - 1; i >= 0; i--)
      {
        var schematic = bpList[i];
        if (schematic == datapadDef)
        {
          bpList.RemoveAtFast(i);
          break;
        }
      }

      if (bpList.Count == 0)
      {
        _gridsContainingSchematics.Remove(grid.EntityId);
        RemoveGPSForGrid(grid);
      }
    }

    bool TryRemoveDatapad(MyFloatingObject floater)
    {
      var datapad = floater?.Item.Content as MyObjectBuilder_Datapad;
      if (datapad == null)
        return false;

      MyDefinitionId typeDef;
      if (!MyDefinitionId.TryParse(datapad.Name, out typeDef))
        return false;

      TryRemoveDatapadFromGrid(datapad);
      return true;
    }

    private void MyEntities_OnEntityAdd(MyEntity obj)
    {
      if (!Registered || !IsServer)
        return;

      var floater = obj as MyFloatingObject;
      if (floater != null)
      {
        if (TryRemoveDatapad(floater))
          AddGPSMarker(obj);

        return;
      }

      var grid = obj as MyCubeGrid;
      if (grid?.Physics == null || grid.IsPreview)
        return;

      Grid_OnClosing(grid);
      SetupGridEvents(grid);

      if (_ignoreGrids.Contains(grid.EntityId))
        return;

      foreach (var cube in grid.GetFatBlocks())
      {
        var store = cube as IMyStoreBlock;
        if (store == null || !ServerSaveData.StoreBlockIds.Contains(store.EntityId))
          continue;

        AddExistingStore(store);
      }

      var owners = grid.BigOwners.Count > 0 ? grid.BigOwners : grid.SmallOwners;
      bool friendly = false;
      foreach (var ownerId in owners)
      {
        if (ownerId == 0)
          continue;

        if (MyAPIGateway.Players.TryGetSteamId(ownerId) != 0)
        {
          friendly = true;
          break;
        }

        var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
        if (faction?.AcceptHumans == true)
        {
          friendly = true;
          break;
        }

        var rel = MyAPIGateway.Session?.Player?.GetRelationTo(ownerId);
        if (rel.HasValue && (rel == MyRelationsBetweenPlayerAndBlock.FactionShare || rel == MyRelationsBetweenPlayerAndBlock.Friends || rel == MyRelationsBetweenPlayerAndBlock.Owner))
        {
          friendly = true;
          break;
        }
      }

      if (friendly)
        return;

      if (_gridsContainingSchematics.ContainsKey(grid.EntityId))
      {
        AddGPSMarker(grid);
        return;
      }
      else if (_datapadPlacement.GridHistory.Contains(grid.EntityId))
        return;

      if (!UseDefaultSpawnSystem)
        return;

      if (grid.IsStatic)
      {
        if (_spawnProbabilityStation <= 0)
          return;
      }
      else if (_spawnProbabilityShip <= 0)
        return;

      bool allowed = true;
      var gridPosition = grid.PositionComp.WorldAABB.Center;

      foreach (var zone in MySessionComponentSafeZones.SafeZones)
      {
        if (zone == null || zone.MarkedForClose)
          continue;

        var pc = zone.PositionComp;
        if (zone.Shape == MySafeZoneShape.Box)
        {
          var box = new MyOrientedBoundingBoxD(pc.LocalAABB, pc.WorldMatrixRef);

          if (box.Contains(ref gridPosition))
          {
            allowed = false;
            break;
          }
        }
        else
        {
          var sphere = new BoundingSphereD(pc.WorldAABB.Center, zone.Radius);

          if (sphere.Contains(gridPosition) != ContainmentType.Disjoint)
          {
            allowed = false;
            break;
          }
        }
      }

      if (!allowed)
        return;

      if (_useSpawnTimer && _spawnTimer < 1800)
          return;

      var num = MyUtils.GetRandomInt(1, 11);

      if (DebugMode)
      {
        Logger.AddDebug($"Attempting to spawn schematics in grid {grid.DisplayName} (ID = {grid.EntityId}) | Station = {grid.IsStatic}\n");
        Logger.AddDebug($" -> Minimum roll for spawn = [Ship] {10 - _spawnProbabilityShip} [Station] {10 - _spawnProbabilityStation} | Roll = {num}\n");
      }

      if (grid.IsStatic)
      {
        if (num < 10 - _spawnProbabilityStation)
        {
          if (DebugMode)
            Logger.Log();
  
          return;
        }
      }
      else if (num < 10 - _spawnProbabilityShip)
      {
        if (DebugMode)
          Logger.Log();
  
        return;
      }

      if (SpawnItemInGrid(grid, _spawnedItems, spawnMultiple: ServerSaveData.AllowMultipleSpawnsPerGrid))
      {
        if (DebugMode)
          Logger.AddDebug($" -> Added {_spawnedItems.Count(x => !x.TypeId.IsNull)} schematics to grid!\n");
        
        List<SerializableDefinitionId> bpList;
        if (!_gridsContainingSchematics.TryGetValue(grid.EntityId, out bpList))
        {
          bpList = new List<SerializableDefinitionId>();
          _gridsContainingSchematics[grid.EntityId] = bpList;
        }

        if (!_datapadPlacement.GridHistory.Contains(grid.EntityId))
          _datapadPlacement.GridHistory.Add(grid.EntityId);

        foreach (var spawnedItem in _spawnedItems)
        {
          if (!spawnedItem.TypeId.IsNull)
            bpList.Add(spawnedItem);
        }

        _needsUpdate = true;
        _spawnTimer = 0;
      }
      else if (DebugMode)
        Logger.AddDebug($" -> Unable to add schematics to grid. Number created = {_spawnedItems.Count}\n");

      Logger.Log();
    }

    private void Grid_OnSplitGridCreated(MyCubeGrid grid)
    {
      _ignoreGrids.Add(grid.EntityId);
    }

    void SetupGridEvents(MyCubeGrid grid)
    {
      grid.OnGridSplit += Grid_OnGridSplit;
      grid.OnClosing += Grid_OnClosing;
    }

    void Grid_OnClosing(MyEntity obj)
    {
      var grid = obj as MyCubeGrid;
      if (grid == null)
        return;

      grid.OnGridSplit -= Grid_OnGridSplit;
      grid.OnClosing -= Grid_OnClosing;
    }

    void Grid_OnGridSplit(MyCubeGrid originalGrid, MyCubeGrid newGrid)
    {
      long gridId;
      if (GridContainsDatapad(originalGrid, out gridId))
        return;

      Grid_OnClosing(originalGrid);

      if (GridContainsDatapad(newGrid, out gridId, true))
      {
        Grid_OnClosing(newGrid);
        SetupGridEvents(newGrid);

        List<SerializableDefinitionId> bpList;
        if (_gridsContainingSchematics.TryGetValue(gridId, out bpList))
        {
          _gridsContainingSchematics.Remove(gridId);
          _gridsContainingSchematics[newGrid.EntityId] = bpList;
        }

        if (!_datapadPlacement.GridHistory.Contains(newGrid.EntityId))
          _datapadPlacement.GridHistory.Add(newGrid.EntityId);

        AddGPSMarker(newGrid);

        MyCubeGrid oldGrid = originalGrid;
        if (gridId != originalGrid.EntityId && !MyEntities.TryGetEntityById(gridId, out oldGrid))
          return;

        RemoveGPSForGrid(oldGrid);
      }
    }

    bool GridContainsDatapad(MyCubeGrid grid, out long entityId, bool remapId = false)
    {
      entityId = 0L;
      foreach (var block in grid.GetFatBlocks())
      {
        var cargo = block as MyCargoContainer;
        if (cargo == null)
          continue;

        foreach (var item in cargo.GetInventory().GetItems())
        {
          var datapad = item.Content as MyObjectBuilder_Datapad;
          if (datapad == null)
            continue;

          var name = datapad.Name;
          MyDefinitionId def;
          MyCubeBlockDefinition cubeDef;
          if (!MyDefinitionId.TryParse(name, out def) || !MyDefinitionManager.Static.TryGetCubeBlockDefinition(def, out cubeDef))
            continue;

          if (remapId)
          {
            SplitByChar(datapad.Data, '\n', ref _dataLines);
            if (_dataLines.Length > 1)
            {
              SplitByChar(_dataLines[1], ':', ref _lineItems);
              if (_lineItems.Length > 1)
                long.TryParse(_lineItems[1], out entityId);
            }

            datapad.Data = $"This datapad contains the schematic for building {cubeDef.DisplayNameText} ({cubeDef.CubeSize} Grid)\nGridId:{grid?.EntityId ?? -123L}\nStorePrice:-1";
          }

          return true;
        }
      }

      return false;
    }

    private void AddExistingStore(IMyStoreBlock store)
    {
      var storeBlock = new Store(store, this);
      if (storeBlock.OrderDict.Count > 0)
        StoreBlockDict.TryAdd(store.EntityId, storeBlock);
    }

    //public T CastProhibit<T>(T ptr, object val) => (T)val;

    private void HandleMessage(string messageText, ref bool sendToOthers)
    {
      if (!messageText.StartsWith("/progression", StringComparison.OrdinalIgnoreCase))
        return;

      sendToOthers = false;

      if (!Registered || !CLI.TryParse(messageText) || CLI.ArgumentCount < 2)
        return;

      var player = MyAPIGateway.Session?.Player;
      if (player == null)
        return;

      if (MyAPIGateway.Session.IsUserAdmin(player.SteamUserId) != true)
      {
        ShowMessage("You must have admin rights to use the Block Progression commands.", timeToLive: 5000);
        return;
      }

      var cmd = CLI.Argument(1);
      if (cmd.Equals("debug", StringComparison.OrdinalIgnoreCase))
      {
        bool b;
        if (CLI.ArgumentCount < 3 || !bool.TryParse(CLI.Argument(2), out b))
          b = !DebugMode;

        DebugMode = b;
        AdminFlags flags = AdminFlags.None;
        if (DebugMode)
          flags |= AdminFlags.DebugMode;

        ShowMessage($"Debug Mode switched = {b}");
        var packet = new AdminPacket(flags);
        NetworkHandler.SendToServer(packet);
      }
      else if (cmd.Equals("unregister", StringComparison.OrdinalIgnoreCase))
      {
        var flags = AdminFlags.Unregister;
        if (DebugMode)
          flags |= AdminFlags.DebugMode;

        var packet = new AdminPacket(flags);
        NetworkHandler.SendToServer(packet);
      }
      else if (cmd.Equals("unlock", StringComparison.OrdinalIgnoreCase))
      {
        if (CLI.ArgumentCount > 2)
        {
          string type = CLI.Argument(2).Trim();

          foreach (var cubeDef in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
          {
            if (!cubeDef.Public && !_restrictedDefinitions.Contains(cubeDef.Id) && cubeDef.Id.ToString().IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0 || cubeDef.DisplayNameText?.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0)
            {
              ShowMessage($"Unlocking {cubeDef.DisplayNameText} ({cubeDef.CubeSize} Grid)");
              cubeDef.Public = true;
              break;
            }
          }
        }
      }
      else if (cmd.Equals("unlockall", StringComparison.OrdinalIgnoreCase))
      {
        bool b = false;
        if (CLI.ArgumentCount > 2)
          bool.TryParse(CLI.Argument(2), out b);

        var flags = AdminFlags.UnlockAll;
        if (DebugMode)
          flags |= AdminFlags.DebugMode;
        if (b)
          flags |= AdminFlags.PermanentUnlock;

        var packet = new AdminPacket(flags);
        NetworkHandler.SendToServer(packet);
      }
      else if (cmd.Equals("create", StringComparison.OrdinalIgnoreCase))
      {
        string schematic = null;
        if (CLI.ArgumentCount > 2)
          schematic = FindSchematicWithName(CLI.Argument(2).Trim());

        var position = player.Character.WorldAABB.Center + player.Character.WorldMatrix.Forward * 3;

        var packet = new SpawnRequestPacket(position, schematic);
        NetworkHandler.SendToServer(packet);
      }
      else if (cmd.Equals("spawn", StringComparison.OrdinalIgnoreCase) && CLI.ArgumentCount > 2)
      {
        var gridName = CLI.Argument(2);
        string schematic = null;

        if (CLI.ArgumentCount > 3)
          schematic = FindSchematicWithName(CLI.Argument(3));

        var packet = new SpawnRequestPacket(gridName, schematic);
        NetworkHandler.SendToServer(packet);
      }
      else if (cmd.Equals("fillstore", StringComparison.OrdinalIgnoreCase))
      {
        var position = player.Character.WorldAABB.Center;
        var packet = new SpawnRequestPacket(position, fillReq: true);
        NetworkHandler.SendToServer(packet);
      }
      else if (cmd.Equals("factionshare", StringComparison.OrdinalIgnoreCase))
      {
        bool b = !FactionSharingEnabled;
        if (CLI.ArgumentCount > 2)
          bool.TryParse(CLI.Argument(2), out b);

        if (FactionSharingEnabled != b)
        {
          var flags = AdminFlags.FactionShare;
          if (DebugMode)
            flags |= AdminFlags.DebugMode;

          var packet = new AdminPacket(flags);
          NetworkHandler.SendToServer(packet);
        }
      }
      else if (cmd.Equals("blockcount", StringComparison.OrdinalIgnoreCase))
      {
        if (CLI.ArgumentCount < 2)
          return;

        MyCubeGrid grid = null;
        var gridName = CLI.Argument(2);
        foreach (var ent in MyEntities.GetEntities())
        {
          var cg = ent as MyCubeGrid;
          if (cg?.DisplayName.StartsWith(gridName, StringComparison.OrdinalIgnoreCase) == true)
          {
            grid = cg;
            break;
          }
        }

        ShowMessage($"{grid?.DisplayName ?? gridName} has {grid?.BlocksCount ?? -123} blocks", timeToLive: 5000);
      }
    }

    string FindSchematicWithName(string name)
    {
      foreach (var blockDef in MyDefinitionManager.Static.GetDefinitionsOfType<MyCubeBlockDefinition>())
      {
        var defString = blockDef.Id.ToString();
        if (defString.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || blockDef.DisplayNameText?.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
          return defString;
      }

      return null;
    }

    public void SpawnItem(Vector3D position, out string spawnedItem, string schematic = null)
    {
      spawnedItem = null;

      if (string.IsNullOrWhiteSpace(schematic))
      {
        int num;
        schematic = GetRandomSchematic(out num);
        if (string.IsNullOrWhiteSpace(schematic))
          return;
      }

      spawnedItem = schematic;
      var item = BuildDatapad(schematic, null);
      MyFloatingObjects.Spawn(item, position, Vector3D.Forward, Vector3D.Up);
    }

    public MyPhysicalInventoryItem BuildDatapad(string schematic, MyCubeGrid grid = null, long storePrice = -1)
    {
      if (DebugMode) 
        Logger.Log($"Attempting to create a datapad for {schematic}, Grid = {grid?.DisplayName ?? "NULL"}, Price = {storePrice}");
  
      var datapad = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Datapad>("Datapad");
      var def = MyDefinitionId.Parse(schematic);
      var cubeDef = MyDefinitionManager.Static.GetCubeBlockDefinition(def);
      datapad.Name = schematic;
      datapad.Data = $"This datapad contains the schematic for building {cubeDef.DisplayNameText} ({cubeDef.CubeSize} Grid)\nGridId:{grid?.EntityId ?? -123L}\nStorePrice:{storePrice}";

      var item = new MyPhysicalInventoryItem()
      {
        Amount = 1,
        Content = datapad
      };

      return item;
    }

    public bool SpawnItemInGrid(MyCubeGrid grid, List<MyDefinitionId> spawnedItems, string schematic = null, bool spawnMultiple = false)
    {
      spawnedItems.Clear();
      _schematicsToBuild.Clear();
      _gridCargos.Clear();

      MyDefinitionId newSpawn;
      if (string.IsNullOrEmpty(schematic))
      {
        var numToSpawn = spawnMultiple ? Math.Max(1, (int)((float)grid.BlocksCount / ServerSaveData.AddSpawnPerBlockCount)) : 1;

        int num;

        for (int i = 0; i < numToSpawn; i++)
        {
          schematic = GetRandomSchematic(out num);
          if (!string.IsNullOrEmpty(schematic) && MyDefinitionId.TryParse(schematic, out newSpawn))
            _schematicsToBuild.Enqueue(schematic);
        }

        if (_schematicsToBuild.Count == 0)
          return false;
      }
      else
      {
        if (!MyDefinitionId.TryParse(schematic, out newSpawn))
          return false;

        _schematicsToBuild.Enqueue(schematic);
      }

      foreach (var block in grid.GetFatBlocks())
      {
        var cargo = block as IMyCargoContainer;
        var inventory = cargo?.GetInventory();

        if (inventory == null || !inventory.CanItemsBeAdded(1, MyItemType_Datapad))
          continue;

        _gridCargos.Add(cargo);
      }

      if (_gridCargos.Count == 0)
        return false;

      var numPerCargo = 1;
      if (_gridCargos.Count < _schematicsToBuild.Count)
        numPerCargo = (int)Math.Ceiling(1.0 * _schematicsToBuild.Count / _gridCargos.Count);

      while(_schematicsToBuild.Count > 0)
      {
        if (_gridCargos.Count == 0)
          break;

        var next = rand.Next(0, _gridCargos.Count);
        var cargo = _gridCargos[next];
        _gridCargos.RemoveAtFast(next);

        var inv = cargo.GetInventory();
        if (inv == null || !inv.CanItemsBeAdded(numPerCargo, MyItemType_Datapad))
          continue;

        for (int i = 0; i < numPerCargo; i++)
        {
          if (_schematicsToBuild.Count == 0)
            break;

          schematic = _schematicsToBuild.Dequeue();
          if (!MyDefinitionId.TryParse(schematic, out newSpawn))
            continue;

          spawnedItems.Add(newSpawn);
          var item = BuildDatapad(schematic, grid);
          inv.AddItems(1, item.Content);
        }
      }

      if (spawnedItems.Count > 0)
      {
        AddGPSMarker(grid);
        return true;
      }

      return false;
    }

    public bool SpawnItemInGridWithName(string gridName, out MyDefinitionId spawnedItem, out long gridId, string schematic = null)
    {
      MyCubeGrid grid = null;
      spawnedItem = new MyDefinitionId();
      gridId = -1;

      if (string.IsNullOrEmpty(gridName))
        return false;

      foreach (var ent in MyEntities.GetEntities())
      {
        var cg = ent as MyCubeGrid;
        if (cg?.DisplayName == gridName)
        {
          grid = cg;
          break;
        }  
      }

      if (grid == null)
        return false;

      gridId = grid.EntityId;
      return SpawnItemInGrid(grid, _spawnedItems, schematic);
    }

    bool AddSchematicsToStore(IMyStoreBlock store, out Store newStore)
    {
      newStore = null;

      try
      {
        if (store == null)
          return false;

        Store storeBlock;
        if (StoreBlockDict.TryGetValue(store.EntityId, out storeBlock))
        {
          storeBlock.Refill();
          return true;
        }

        var num = rand.Next(12, 15);
        newStore = new Store(store, num, this);

        bool added = false;
        if (newStore.OrderDict.Count > 0)
          added = StoreBlockDict.TryAdd(store.EntityId, newStore);

        return added;
      }
      catch (Exception ex)
      {
        Logger.Log($"Error in AddSchematicsToStore: {ex.Message}\n{ex.StackTrace}", MessageType.ERROR);
        return false;
      }
    }

    void AddGPSMarker(MyEntity entity)
    {
      if (LocalGPSDictionary.ContainsKey(entity))
        return;

      var position = entity.PositionComp.WorldAABB.Center;
      var gps = MyAPIGateway.Session.GPS.Create("Faint Signal", "A schematic is nearby...", position, false);
      gps.GPSColor = Color.Goldenrod;

      if (LocalGPSDictionary.TryAdd(entity, gps) && IsServer)
        _gpsUpdatesAvailable = true;
    }

    void RemoveGPSForGrid(MyCubeGrid grid)
    {
      IMyGps gps;
      if (LocalGPSDictionary.TryRemove(grid, out gps) && !IsDedicatedServer)
        MyAPIGateway.Session?.GPS?.RemoveLocalGps(gps);

      _gpsUpdatesAvailable = true;
    }

    public string GetRandomSchematic(out int numAvailable, int numWanted = 1)
    {
      int tryCount = 0;

      while (true)
      {
        numAvailable = 0;
        if (_schematics.Count == 0)
          break;

        var random = rand.Next(0, _schematics.Count);
        var schematic = _schematics[random];

        BlockRarity br;
        if (!BlockTypeRarity.TryGetValue(schematic, out br) || br == null || br.NumberInWorld <= 0)
        {
          _schematics.RemoveAtFast(random);
          continue;
        }

        var chance = rand.Next(1, 11) * 0.1f;
        if (chance < 1 - br.SpawnChanceModifier && ++tryCount < 10)
          continue;

        numAvailable = Math.Min(numWanted, br.NumberInWorld);
        br.NumberInWorld -= numAvailable;
        return schematic.ToString();
      }

      return null;
    }

    public void ShowMessage(string text, string font = MyFontEnum.Red, int timeToLive = 2000)
    {
      if (_hudMsg == null)
        _hudMsg = MyAPIGateway.Utilities.CreateNotification(string.Empty);

      _hudMsg.Hide();
      _hudMsg.Font = font;
      _hudMsg.AliveTime = timeToLive;
      _hudMsg.Text = text;
      _hudMsg.Show();
    }

    internal void UpdateGPSCollection(List<long> toAdd)
    {
      if (toAdd == null)
        return;

      _localGpsGridIds.Clear();
      _localGpsGridIds.AddList(toAdd);

      var player = MyAPIGateway.Session?.Player;
      if (player == null)
        return;

      _gpsRemovals.Clear();
      foreach (var kvp in LocalGPSDictionary)
      {
        if (kvp.Key == null || kvp.Key.MarkedForClose)
        {
          _gpsRemovals.Add(kvp.Key);
          continue;
        }

        if (_localGpsGridIds.Contains(kvp.Key.EntityId))
          continue;

        MyAPIGateway.Session.GPS.RemoveLocalGps(kvp.Value);
        _gpsRemovals.Add(kvp.Key);
      }

      foreach (var grid in _gpsRemovals)
      {
        if (grid == null)
          continue;

        IMyGps _;
        LocalGPSDictionary.TryRemove(grid, out _);
      }

      for (int i = 0; i < _localGpsGridIds.Count; i++)
      {
        var entId = toAdd[i];
        var entity = MyEntities.GetEntityById(entId);
        if (entity == null)
          continue;

        AddGPSMarker(entity);
      }
    }

    internal void AddGridSchematicPair(long gridId, MyDefinitionId schematic)
    {
      List<SerializableDefinitionId> bpList;
      if (!_gridsContainingSchematics.TryGetValue(gridId, out bpList))
      {
        bpList = new List<SerializableDefinitionId>();
        _gridsContainingSchematics[gridId] = bpList;
      }

      if (!_datapadPlacement.GridHistory.Contains(gridId))
        _datapadPlacement.GridHistory.Add(gridId);

      bpList.Add(schematic);
      _needsUpdate = true;
    }

    internal MyTuple<string, long> CompleteFillRequest(Vector3D position)
    {
      _entities.Clear();
      var sphere = new BoundingSphereD(position, 10);
      MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, _entities);

      foreach (var ent in _entities)
      {
        var cube = ent as MyCubeBlock;
        var store = cube as IMyStoreBlock;
        if (store?.BlockDefinition.SubtypeName != "StoreBlock") //  || store.CustomName == "StoreBlock [Schematic Hunter]")
          continue;

        Store newStore;
        if (AddSchematicsToStore(store, out newStore))
        {
          _newStores.Enqueue(newStore);
          ServerSaveData.StoreBlockIds.Add(store.EntityId);
          _needsUpdate = true;
          return MyTuple.Create(store.CustomName, store.EntityId);
        }
      }

      return MyTuple.Create(string.Empty, 0L);
    }

    public void SplitByChar(string toSplit, char sep, ref string[] array, bool removeEmptyEntries = false)
    {
      _splitSB.Clear();
      _splitStrings.Clear();

      foreach (var ch in toSplit)
      {
        if (ch == sep)
        {
          if (!removeEmptyEntries || _splitSB.Length > 0)
            _splitStrings.Add(_splitSB.ToString());
  
          _splitSB.Clear();
        }
        else
          _splitSB.Append(ch);
      }

      if (!removeEmptyEntries || _splitSB.Length > 0)
        _splitStrings.Add(_splitSB.ToString());

      var num = _splitStrings.Count;
      if (array.Length != num)
        array = new string[num];

      for (int i = 0; i < num; i++)
        array[i] = _splitStrings[i];
    }
  }
}

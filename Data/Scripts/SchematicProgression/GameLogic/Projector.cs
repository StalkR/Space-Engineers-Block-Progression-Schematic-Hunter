using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;

using SchematicProgression.Network;
using SchematicProgression.Settings;

using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace SchematicProgression.GameLogic
{
  [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Projector), false)]
  public class Projector : MyGameLogicComponent
  {
    List<IMySlimBlock> _slimBlocks = new List<IMySlimBlock>();
    Sandbox.ModAPI.IMyProjector _projector;
    bool _playerOwned, _needsCheck = true;
    ulong _steamId;

    public override void Init(MyObjectBuilder_EntityBase objectBuilder)
    {
      _projector = (Sandbox.ModAPI.IMyProjector)Entity;
      NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
      base.Init(objectBuilder);
    }

    public override void Close()
    {
      _projector.EnabledChanged -= Block_EnabledChanged;
      _projector.OwnershipChanged -= Block_OwnershipChanged;
      _projector.PropertiesChanged -= Block_OnPropertiesChanged;
      _slimBlocks?.Clear();
      _slimBlocks = null;
      base.Close();
    }

    public override void UpdateOnceBeforeFrame()
    {
      _projector.EnabledChanged += Block_EnabledChanged;
      _projector.OwnershipChanged += Block_OwnershipChanged;
      _projector.PropertiesChanged += Block_OnPropertiesChanged;

      _playerOwned = IsPlayerOwned();
      CheckEnabled();

      base.UpdateOnceBeforeFrame();
    }

    private void Block_OnPropertiesChanged(IMyTerminalBlock obj)
    {
      if (_projector?.ProjectedGrid != null)
        _needsCheck = true;
    }

    public override void UpdateBeforeSimulation()
    {
      try
      {
        if (Session.Instance?.Registered != true || !Session.Instance.IsServer || !_playerOwned || !_projector.Enabled || !_needsCheck || Session.Instance.TempUnlockHash.Contains(_steamId))
          return;

        var grid = _projector.ProjectedGrid;
        var cubeGrid = grid as MyCubeGrid;
        if (grid == null)
        {
          _needsCheck = false;
          return;
        }

        PlayerSettings pSettings;
        if (!Session.Instance.AllPlayerSettings.TryGetValue(_steamId, out pSettings))
        {
          _projector.SetProjectedGrid(null);
          return;
        }

        grid.GetBlocks(_slimBlocks);
        foreach (var block in _slimBlocks)
        {
          var type = block.BlockDefinition.Id;
          if (pSettings.UnlockedBlocks.Contains(type))
            continue;

          var cubeDef = block.BlockDefinition as MyCubeBlockDefinition;
          var blockName = cubeDef?.DisplayNameText ?? type.ToString();
          var packet = new MessagePacket($"The blueprint for '{grid.DisplayName}' contains a block you haven't unlocked yet: {blockName}");
          Session.Instance.NetworkHandler.SendToPlayer(packet, _steamId);
          _projector.SetProjectedGrid(null);
          break;
        }

        _needsCheck = false;
      }
      catch (Exception ex)
      {
        Session.Instance?.Logger?.Log($"Error in Projector.UpdateBeforeSim: {ex.Message}\n{ex.StackTrace}", MessageType.ERROR);
      }

      base.UpdateBeforeSimulation();
    }

    private void Block_OwnershipChanged(IMyTerminalBlock obj)
    {
      _playerOwned = IsPlayerOwned();
      CheckEnabled();
    }

    private void Block_EnabledChanged(IMyTerminalBlock obj)
    {
      CheckEnabled();
    }

    bool IsPlayerOwned()
    {
      _steamId = MyAPIGateway.Players.TryGetSteamId(_projector.OwnerId);
      return _steamId != 0;
    }

    void CheckEnabled()
    {
      if (_playerOwned)
        return;

      _projector.EnabledChanged -= Block_EnabledChanged;
      _projector.SetProjectedGrid(null);
      _projector.Enabled = false;
      _projector.EnabledChanged += Block_EnabledChanged;
    }
  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

using Sandbox.ModAPI;

using SchematicProgression.Settings;

using VRage.Game.ModAPI;

namespace SchematicProgression.Network
{
  [ProtoContract]
  public class SettingRequestPacket : PacketBase
  {
    public override bool Received(Networking netHandler)
    {
      if (!netHandler.SessionComp.IsServer)
        return false;

      var playerId = MyAPIGateway.Players.TryGetIdentityId(SenderId);
      var faction = MyAPIGateway.Session?.Factions.TryGetPlayerFaction(playerId);
      netHandler.SessionComp.Logger.Log($"Server received Request for Settings. SenderId = {SenderId}, Faction = {faction?.Tag ?? "NULL"}");

      PlayerSettings pSettings;
      netHandler.SessionComp.AllPlayerSettings.TryGetValue(SenderId, out pSettings);

      if (pSettings == null)
      {
        pSettings = new PlayerSettings(SenderId, netHandler.SessionComp);
        netHandler.SessionComp.AllPlayerSettings[SenderId] = pSettings;
      }

      netHandler.SessionComp.AddBasicPlayerSettings(pSettings);
      SerializablePlayerSettings serialSettings = new SerializablePlayerSettings(pSettings);

      for (int i = netHandler.SessionComp.ServerSaveData.PlayerSettings.Count - 1; i >= 0; i--)
      {
        var test = netHandler.SessionComp.ServerSaveData.PlayerSettings[i];
        if (test.SteamId == SenderId)
        {
          netHandler.SessionComp.ServerSaveData.PlayerSettings.RemoveAtFast(i);
          break;
        }
      }

      netHandler.SessionComp.ServerSaveData.PlayerSettings.Add(serialSettings);
      netHandler.SessionComp.SaveConfig();

      var packet = new SettingProvidePacket(serialSettings, netHandler.SessionComp.ServerSaveData.StoreBlockIds);
      netHandler.SendToPlayer(packet, serialSettings.SteamId);

      return false;
    }
  }
}

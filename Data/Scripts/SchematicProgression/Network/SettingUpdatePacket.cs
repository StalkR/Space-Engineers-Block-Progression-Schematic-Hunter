using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;
using SchematicProgression.Settings;

namespace SchematicProgression.Network
{
  [ProtoContract]
  public class SettingUpdatePacket : PacketBase
  {
    [ProtoMember(1)]
    public SerializablePlayerSettings PlayerSettings;

    public SettingUpdatePacket() { }

    public SettingUpdatePacket(SerializablePlayerSettings pSettings)
    {
      PlayerSettings = pSettings;
    }

    public override bool Received(Networking netHandler)
    {
      if (!netHandler.SessionComp.IsServer)
        return false;

      bool found = false;
      foreach (var pSetting in netHandler.SessionComp.ServerSaveData.PlayerSettings)
      {
        if (pSetting.SteamId == SenderId)
        {
          found = true;
          pSetting.Update(PlayerSettings);
          break;
        }
      }

      if (!found)
        netHandler.SessionComp.ServerSaveData.PlayerSettings.Add(PlayerSettings);

      netHandler.SessionComp.SaveConfig();
      return false;
    }
  }
}

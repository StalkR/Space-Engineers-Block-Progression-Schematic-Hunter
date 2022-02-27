using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

using SchematicProgression.Settings;

using VRage.Game.ModAPI;

namespace SchematicProgression.Network
{
  [ProtoContract]
  public class SettingProvidePacket : PacketBase
  {
    [ProtoMember(1)]
    public SerializablePlayerSettings PlayerSettings { get; set; }

    [ProtoMember(2)]
    public List<long> StoreBlockIds { get; set; }

    public SettingProvidePacket() { }

    public SettingProvidePacket(SerializablePlayerSettings pSettings, List<long> storeIds)
    {
      PlayerSettings = pSettings;
      StoreBlockIds = storeIds;
    }

    public override bool Received(Networking netHandler)
    {
      if (netHandler.SessionComp.IsDedicatedServer)
        return false;

      netHandler.SessionComp.ReceiveSettings(PlayerSettings);

      if (StoreBlockIds?.Count > 0)
        netHandler.SessionComp.AddStoreId(StoreBlockIds);
  
      return false;
    }
  }
}

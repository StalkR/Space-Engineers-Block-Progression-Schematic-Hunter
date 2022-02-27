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
  public class MessagePacket : PacketBase
  {
    [ProtoMember(1)]
    public string Message;

    [ProtoMember(2)]
    public List<long> EntityIds;

    public MessagePacket() { }

    public MessagePacket(string msg, List<long> ids = null)
    {
      Message = msg;
      EntityIds = ids;
    }

    public override bool Received(Networking netHandler)
    {
      if (netHandler.SessionComp.IsDedicatedServer)
        return false;

      if (!string.IsNullOrEmpty(Message))
        netHandler.SessionComp.ShowMessage(Message, timeToLive: 5000);

      if (EntityIds?.Count > 0)
        netHandler.SessionComp.AddStoreId(EntityIds);

      return false;
    }
  }
}

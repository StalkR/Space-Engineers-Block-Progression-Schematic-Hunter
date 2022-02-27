using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

namespace SchematicProgression.Network
{
  [ProtoContract]
  public class DataResetPacket : PacketBase
  {
    [ProtoMember(1)]
    public uint InventoryItemId;

    [ProtoMember(2)]
    public string Name;

    [ProtoMember(3)]
    public string Data;

    public DataResetPacket() { }

    public DataResetPacket(uint id, string name, string data)
    {
      InventoryItemId = id;
      Name = name;
      Data = data;
    }

    public override bool Received(Networking netHandler)
    {
      netHandler.SessionComp.ResetDatapad(SenderId, InventoryItemId, Name, Data);
      return false;
    }
  }
}

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
  public class GpsUpdatePacket : PacketBase
  {
    [ProtoMember(1)]
    public List<long> GridIDs;

    public GpsUpdatePacket() { }

    public GpsUpdatePacket(List<long> adds)
    {
      GridIDs = adds;
    }

    public override bool Received(Networking netHandler)
    {
      netHandler.SessionComp.UpdateGPSCollection(GridIDs);
      return false;
    }
  }
}

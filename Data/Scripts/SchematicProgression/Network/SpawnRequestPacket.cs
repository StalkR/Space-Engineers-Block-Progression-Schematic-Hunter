using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

using Sandbox.Game.Entities;
using Sandbox.ModAPI;

using VRage;
using VRage.Game;

using VRageMath;

namespace SchematicProgression.Network
{
  [ProtoContract]
  public class SpawnRequestPacket : PacketBase
  {
    [ProtoMember(1)]
    public SerializableVector3D SpawnPosition;

    [ProtoMember(2)]
    public bool IsFillRequest;

    [ProtoMember(3)]
    public string GridName;

    [ProtoMember(4)]
    public string Schematic;

    public SpawnRequestPacket() { }

    public SpawnRequestPacket(Vector3D position, string schematic = null, bool fillReq = false)
    {
      SpawnPosition = position;
      Schematic = schematic;
      IsFillRequest = fillReq;
    }

    public SpawnRequestPacket(string gridName, string schematic)
    {
      GridName = gridName;
      Schematic = schematic;
    }

    public override bool Received(Networking netHandler)
    {
      if (!SpawnPosition.IsZero)
      {
        if (IsFillRequest)
        {
          var tuple = netHandler.SessionComp.CompleteFillRequest(SpawnPosition);
          if (!string.IsNullOrWhiteSpace(tuple.Item1))
          {
            var list = new List<long>() { tuple.Item2 };
            var packet = new MessagePacket($"Found store block '{tuple.Item1}' and attempted to add schematics", list);
            netHandler.SendToPlayer(packet, SenderId);
          }
        }
        else
        {
          string item;
          netHandler.SessionComp.SpawnItem(SpawnPosition, out item, Schematic);

          var packet = new MessagePacket($"Attempted to create a datapad. Success = {!string.IsNullOrWhiteSpace(item)}.");
          netHandler.SendToPlayer(packet, SenderId);
        }
      }
      else if (!string.IsNullOrWhiteSpace(GridName))
      {
        MyDefinitionId item;
        long id;

        if (netHandler.SessionComp.SpawnItemInGridWithName(GridName, out item, out id, Schematic))
          netHandler.SessionComp.AddGridSchematicPair(id, item);

        var packet = new MessagePacket($"Attempted to spawn a datapad for grid {GridName}. Success = {!item.TypeId.IsNull}\nDatapad contains schematic for {item.ToString()}");
        netHandler.SendToPlayer(packet, SenderId);
      }

      return false;
    }
  }
}

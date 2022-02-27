using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

using SchematicProgression.Settings;

using VRage.ObjectBuilders;

namespace SchematicProgression.Network
{
  [ProtoContract]
  public class BlockUnlockPacket : PacketBase
  {
    [ProtoMember(1)]
    public SerializableDefinitionId BlockDefinition;

    [ProtoMember(2)]
    public List<SerializableDefinitionId> BlockDefsMulti;

    [ProtoMember(3)]
    public bool IsCockpitUpdate;

    [ProtoMember(4)]
    public bool EnteredCockpit;

    public BlockUnlockPacket() { }

    public BlockUnlockPacket(bool enteredCockpit)
    {
      IsCockpitUpdate = true;
      EnteredCockpit = enteredCockpit;
    }

    public BlockUnlockPacket(SerializableDefinitionId definition)
    {
      BlockDefinition = definition;
    }

    public BlockUnlockPacket(List<SerializableDefinitionId> definitions)
    {
      BlockDefsMulti = new List<SerializableDefinitionId>();

      foreach (var item in definitions)
        BlockDefsMulti.Add(item);
    }

    public override bool Received(Networking netHandler)
    {
      if (netHandler.SessionComp.IsDedicatedServer)
        return false;

      if (IsCockpitUpdate)
      {
        netHandler.SessionComp.UpdatePlayerBlocksForCockpit(EnteredCockpit);
        return false;
      }

      if (BlockDefinition.TypeId.IsNull && (BlockDefsMulti == null || BlockDefsMulti.Count == 0))
      {
        netHandler.SessionComp.Logger.Log("Received unlock packet with null block type", MessageType.WARNING);
        return false;
      }

      if (!BlockDefinition.TypeId.IsNull)
        netHandler.SessionComp.UnlockBlockTypeLocal(BlockDefinition);

      if (BlockDefsMulti != null)
      {
        foreach (var type in BlockDefsMulti)
          netHandler.SessionComp.UnlockBlockTypeLocal(type);
      }

      return false;
    }
  }
}

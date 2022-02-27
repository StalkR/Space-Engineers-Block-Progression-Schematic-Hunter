using ProtoBuf;
using Sandbox.ModAPI;

namespace SchematicProgression.Network
{
  [ProtoInclude(1000, typeof(SettingRequestPacket))]
  [ProtoInclude(2000, typeof(SettingProvidePacket))]
  [ProtoInclude(3000, typeof(SettingUpdatePacket))]
  [ProtoInclude(4000, typeof(BlockUnlockPacket))]
  [ProtoInclude(5000, typeof(MessagePacket))]
  [ProtoInclude(6000, typeof(GpsUpdatePacket))]
  [ProtoInclude(7000, typeof(AdminPacket))]
  [ProtoInclude(8000, typeof(SpawnRequestPacket))]
  [ProtoInclude(9000, typeof(DataResetPacket))]
  [ProtoContract]
  public abstract class PacketBase
  {
    [ProtoMember(1)]
    public readonly ulong SenderId;

    public PacketBase()
    {
      SenderId = MyAPIGateway.Multiplayer.MyId;
    }

    public abstract bool Received(Networking netHandler);
  }
}

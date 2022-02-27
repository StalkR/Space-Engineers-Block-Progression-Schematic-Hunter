using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

using SchematicProgression.Settings;

namespace SchematicProgression.Network
{
  [Flags]
  public enum AdminFlags
  {
    None = 0,
    DebugMode = 1,
    UnlockAll = 2,
    PermanentUnlock = 4,
    Unregister = 8,
    FactionShare = 16
  }


  [ProtoContract]
  public class AdminPacket : PacketBase
  {
    [ProtoMember(1)]
    public bool DebugMode { get; set; }
    
    [ProtoMember(2)]
    public bool UnlockAll { get; set; }

    [ProtoMember(3)]
    public bool PermanentUnlock { get; set; }

    [ProtoMember(4)]
    public bool UnRegister { get; set; }

    [ProtoMember(5)]
    public bool FactionShare { get; set; }

    public AdminPacket() { }

    public AdminPacket(AdminFlags flags)
    {
      DebugMode = (AdminFlags.DebugMode & flags) > 0;
      UnlockAll = (AdminFlags.UnlockAll & flags) > 0;
      UnRegister = (AdminFlags.Unregister & flags) > 0;
      PermanentUnlock = (AdminFlags.PermanentUnlock & flags) > 0;
      FactionShare = (AdminFlags.FactionShare & flags) > 0;
    }

    public override bool Received(Networking netHandler)
    {
      var prevMode = netHandler.SessionComp.DebugMode;
      if (prevMode != DebugMode)
      {
        netHandler.SessionComp.DebugMode = DebugMode;
        var packet = new MessagePacket($"Debug Mode set to {DebugMode}.");
        netHandler.SendToPlayer(packet, SenderId);
      }

      prevMode = netHandler.SessionComp.Registered;
      var newMode = !UnRegister;

      if (prevMode != newMode)
      {
        netHandler.SessionComp.Registered = newMode;
        var packet = new MessagePacket($"Registered set to {newMode}.");
        netHandler.SendToPlayer(packet, SenderId);
      }

      prevMode = netHandler.SessionComp.FactionSharingEnabled;
      if (prevMode != FactionShare)
      {
        netHandler.SessionComp.FactionSharingEnabled = FactionShare;
        var packet = new MessagePacket($"Faction Sharing set to {FactionShare}.");
        netHandler.SendToPlayer(packet, SenderId);
      }

      PlayerSettings pSettings = null;
      if (UnlockAll && netHandler?.SessionComp?.AllPlayerSettings?.TryGetValue(SenderId, out pSettings) == true)
      {
        if (PermanentUnlock)
        {
          foreach (var item in netHandler.SessionComp.UniversalBlockSettings.AllBlockTypes)
            pSettings.UnlockType(item, true);
        }
        else
          netHandler?.SessionComp?.TempUnlockHash.Add(SenderId);

        var packet = new BlockUnlockPacket(netHandler.SessionComp.UniversalBlockSettings.AllBlockTypes);
        netHandler.SendToPlayer(packet, SenderId);

        var message = new MessagePacket("You have unlocked all block types!");
        netHandler.SendToPlayer(message, SenderId);
      }

      return false;
    }
  }
}

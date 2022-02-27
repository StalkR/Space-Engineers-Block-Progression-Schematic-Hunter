using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VRage.Game;
using VRage.ObjectBuilders;
using ProtoBuf;

namespace SchematicProgression.Settings
{
  [ProtoContract]
  public class SerializablePlayerSettings
  {
    [ProtoMember(1)]
    public ulong SteamId { get; set; }

    [ProtoMember(2)]
    public List<SerializableDefinitionId> BlockTypesUnlocked { get; set; }

    public SerializablePlayerSettings() { }

    public SerializablePlayerSettings(PlayerSettings pSettings)
    {
      SteamId = pSettings.SteamId;

      if (BlockTypesUnlocked == null)
        BlockTypesUnlocked = new List<SerializableDefinitionId>(pSettings.UnlockedBlocks.Count);

      foreach (var item in pSettings.UnlockedBlocks)
        BlockTypesUnlocked.Add(item);
    }

    public void UnlockBlockType(MyDefinitionId blockDef)
    {
      if (BlockTypesUnlocked == null)
        BlockTypesUnlocked = new List<SerializableDefinitionId>(80);

      if (BlockTypesUnlocked.Contains(blockDef))
        return;

      BlockTypesUnlocked.Add(blockDef);
    }

    public void Update(PlayerSettings pSettings)
    {
      if (BlockTypesUnlocked == null)
        BlockTypesUnlocked = new List<SerializableDefinitionId>(pSettings.UnlockedBlocks.Count);
      else
        BlockTypesUnlocked.Clear();

      foreach (var item in pSettings.UnlockedBlocks)
        BlockTypesUnlocked.Add(item);
    }

    public void Update(SerializablePlayerSettings pSettings)
    {
      BlockTypesUnlocked?.Clear();
      BlockTypesUnlocked = pSettings.BlockTypesUnlocked;
    }

    public void Close()
    {
      BlockTypesUnlocked?.Clear();
      BlockTypesUnlocked = null;
    }
  }
}

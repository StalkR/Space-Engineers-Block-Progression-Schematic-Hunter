using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Definitions;

using VRage.Game;
using VRage.ObjectBuilders;

namespace SchematicProgression.Settings
{
  public class PlayerSettings
  {
    public ulong SteamId;
    public HashSet<MyDefinitionId> UnlockedBlocks = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
    StringBuilder _sb = new StringBuilder(512);
    Session _mod;

    public PlayerSettings(ulong id, Session mod)
    {
      SteamId = id;
      _mod = mod;
    }

    public PlayerSettings(SerializablePlayerSettings pSettings, Session mod)
    {
      SteamId = pSettings.SteamId;
      _mod = mod;

      foreach (var blockDef in pSettings.BlockTypesUnlocked)
          UnlockType(blockDef, true);
    }

    public bool UnlockType(MyDefinitionId blockDef, bool isLoading)
    {
      if (!UnlockedBlocks.Add(blockDef))
        return false;

      var cubeDef = MyDefinitionManager.Static.GetCubeBlockDefinition(blockDef);
      if (cubeDef == null)
      {
        UnlockedBlocks.Remove(blockDef);
        return false;
      }

      cubeDef.Public = true;

      if (!isLoading && !_mod.AlwaysUnlockedHash.Contains(blockDef))
        _mod?.ShowMessage($"You have learned how to build blocks of type: {cubeDef.DisplayNameText} ({cubeDef.CubeSize} Grid)", MyFontEnum.Blue, 5000);

      return true;
    }

    public void Update(SerializablePlayerSettings pSettings)
    {
      if (UnlockedBlocks == null)
        UnlockedBlocks = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
      else
        UnlockedBlocks.Clear();

      foreach (var blockDef in pSettings.BlockTypesUnlocked)
          UnlockType(blockDef, true);
    }

    public void Close()
    {
      UnlockedBlocks?.Clear();
      UnlockedBlocks = null;
    }
  }
}

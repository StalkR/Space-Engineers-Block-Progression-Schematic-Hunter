using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchematicProgression.Settings
{
  public class ServerSettings
  {
    public bool FactionSharing { get; set; }

    public bool UseBuiltInSpawnSystem { get; set; } = true;

    public int SpawnProbabilityPercent_Stations { get; set; } = 100;

    public int SpawnProbabilityPercent_Ships { get; set; } = 60;

    public bool AllowMultipleSpawnsPerGrid { get; set; } = true;

    public int AddSpawnPerBlockCount { get; set; } = 1000;

    public List<SerializablePlayerSettings> PlayerSettings { get; set; }

    public List<long> StoreBlockIds { get; set; }

    public void Close()
    {
      StoreBlockIds?.Clear();
      StoreBlockIds = null;

      if (PlayerSettings != null)
      {
        foreach (var ps in PlayerSettings)
          ps?.Close();

        PlayerSettings?.Clear();
        PlayerSettings = null;
      }
    }
  }
}

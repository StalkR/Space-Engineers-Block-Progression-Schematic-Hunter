using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VRage.ObjectBuilders;

namespace SchematicProgression.Settings
{
  public class UniversalBlockSettings
  {
    public List<SerializableDefinitionId> AlwaysUnlocked { get; set; }
    public List<SerializableDefinitionId> AlwaysLocked { get; set; }
    public List<SerializableDefinitionId> AllBlockTypes { get; set; }

    public void Close()
    {
      AlwaysLocked?.Clear();
      AlwaysUnlocked?.Clear();
      AllBlockTypes?.Clear();

      AlwaysLocked = null;
      AlwaysUnlocked = null;
      AllBlockTypes = null;
    }
  }
}

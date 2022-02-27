using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using ProtoBuf;


using VRage.ObjectBuilders;

namespace SchematicProgression.Settings
{
  public class ResearchGroup
  {
    public List<SerializableDefinitionId> BlockDefinitons { get; set; } = new List<SerializableDefinitionId>();
  }

  public class ResearchGroupSettings
  {
    public string Note;

    [XmlElement("ResearchGroup", typeof(ResearchGroup))]
    public List<ResearchGroup> ResearchGroups { get; set; } = new List<ResearchGroup>();

    public ResearchGroupSettings()
    {
      Note = "\n\tEach of the following ResearchGroup sections denotes a group of blocks that are unlocked together.\n\tIf the same block is found in multiple groups, unlocking that block will unlock all blocks from all containing groups.\n\tIf a block is not listed in any group, it will be unlocked by itself.\n\tAdd all grouped definitions to the BlockDefinitions section of the research group\n\t";
    }

    public void Close()
    {
      if (ResearchGroups == null)
        return;

      for (int i = ResearchGroups.Count - 1; i >= 0; i--)
        ResearchGroups[i]?.BlockDefinitons?.Clear();

      ResearchGroups?.Clear();
      ResearchGroups = null;
    }
  }
}

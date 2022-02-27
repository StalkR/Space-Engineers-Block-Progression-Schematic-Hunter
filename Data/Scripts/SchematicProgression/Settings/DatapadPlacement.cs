using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VRage.ObjectBuilders;

namespace SchematicProgression.Settings
{
  public class DatapadGridPair
  {
    public long GridId;
    public string GridName;
    public List<SerializableDefinitionId> Schematics;

    public DatapadGridPair() { }

    public DatapadGridPair(long id, string gridName, List<SerializableDefinitionId> schematicList)
    {
      GridId = id;
      GridName = gridName;
      Schematics = schematicList;
    }

    public void Close()
    {
      Schematics?.Clear();
      Schematics = null;
    }
  }

  public class DatapadPlacement
  {
    public string Note { get; set; }

    public List<DatapadGridPair> GridsGivenADatapad { get; set; }

    public List<long> GridHistory { get; set; }

    public DatapadPlacement()
    {
      Note = "DO NOT ALTER THIS DOCUMENT!";
      GridsGivenADatapad = new List<DatapadGridPair>(20);
      GridHistory = new List<long>(100);
    }

    public void Close()
    {
      GridHistory?.Clear();

      if (GridsGivenADatapad != null)
      {
        foreach (var item in GridsGivenADatapad)
          item?.Close();

        GridsGivenADatapad?.Clear();
      }

      GridHistory = null;
      GridsGivenADatapad = null;
    }
  }
}

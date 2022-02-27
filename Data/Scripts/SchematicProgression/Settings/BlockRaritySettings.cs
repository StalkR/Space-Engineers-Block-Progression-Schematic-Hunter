using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VRage.ObjectBuilders;

namespace SchematicProgression.Settings
{
  public class BlockRarity
  {
    public SerializableDefinitionId BlockDefinition;
    public int NumberInWorld;
    public float PriceModifier;
    public float SpawnChanceModifier;
    public bool ConsumeWhenOpened;

    public BlockRarity() { }

    public BlockRarity(SerializableDefinitionId definition, int num = 10, float priceModifier = 1, float spawnModifier = 1, bool consume = false)
    {
      BlockDefinition = definition;
      NumberInWorld = num;
      PriceModifier = priceModifier;
      SpawnChanceModifier = spawnModifier;
      ConsumeWhenOpened = consume;
    }
  }

  public class BlockRaritySettings
  {
    public float BasePrice_Minimum { get; set; } = 50000f;
    public float BasePrice_Maximum { get; set; } = 500000f;
    public List<BlockRarity> BlockRarityList { get; set; }

    public void AddToList(SerializableDefinitionId type, int num)
    {
      if (BlockRarityList == null)
        BlockRarityList = new List<BlockRarity>(80);

      foreach (var item in BlockRarityList)
      {
        if (item.BlockDefinition.TypeId == type.TypeId && item.BlockDefinition.SubtypeId == type.SubtypeId)
          return;
      }

      var br = new BlockRarity(type, num);
      BlockRarityList.Add(br);
    }

    public void AddToList(SerializableDefinitionId type, BlockRarity br)
    {
      if (BlockRarityList == null)
        BlockRarityList = new List<BlockRarity>(80);

      foreach (var item in BlockRarityList)
      {
        if (item.BlockDefinition.TypeId == type.TypeId && item.BlockDefinition.SubtypeId == type.SubtypeId)
          return;
      }

      BlockRarityList.Add(br);
    }

    public void Close()
    {
      BlockRarityList?.Clear();
      BlockRarityList = null;
    }
  }
}

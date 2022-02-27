using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Definitions;

using VRage.Game;
using VRage.ObjectBuilders;

namespace SchematicProgression.Economy
{
  public class ItemInfo
  {
    public SerializableDefinitionId ItemDefinition;
    public long StoreItemId, PricePerItem;
    public int NumberAvailable;
    public string Name;
    public MyCubeSize Size;

    public ItemInfo(MyDefinitionId def, long id, long price, int num)
    {
      ItemDefinition = def;
      StoreItemId = id;
      PricePerItem = price;
      NumberAvailable = num;

      MyDefinitionBase defBase;
      if (MyDefinitionManager.Static.TryGetDefinition(def, out defBase))
      {
        var cubeDef = defBase as MyCubeBlockDefinition;
        Name = cubeDef.DisplayNameText;
        Size = cubeDef.CubeSize;
      }
      else
      {
        var str = def.ToString();
        var split = str.Split('/');
        Name = (split.Length > 1) ? split[1] : split[0];
        Size = (str.Contains("SmallBlock")) ? MyCubeSize.Small : MyCubeSize.Large;
      }

    }
  }
}

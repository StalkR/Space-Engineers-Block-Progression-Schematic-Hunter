using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Definitions;
using Sandbox.ModAPI;

using SchematicProgression.Economy;

using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.ObjectBuilders;

using VRageMath;

using IMyTextSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;

namespace SchematicProgression.Drawing
{
  public class DrawSurface
  {
    public readonly IMyTerminalBlock Block;
    public readonly IMyTextSurface Surface;
    public Vector2 TextSurface, TextStart;
    public Vector2 ScreenCenter, StringPixels;
    public int ScrollIndex = 0;
    public float FontScale = 0.7f;

    StringBuilder _sb = new StringBuilder(128);
    List<MySprite> _sprites = new List<MySprite>();
    Color _white, _black;

    public DrawSurface(IMyTextSurface surface, IMyTerminalBlock block)
    {
      Block = block;
      Surface = surface;

      ScreenCenter = surface.TextureSize * 0.5f;
      TextSurface = surface.SurfaceSize;
      TextStart = ScreenCenter - (TextSurface * 0.5f);
      _white = Color.White;
      _black = Color.Black;
    }

    public void CreateSprites(Dictionary<long, ItemInfo> itemPriceDict)
    {
      _sprites.Clear();
      CalculateScreenPixels();
      var yPosition = TextStart.Y;

      var position = new Vector2(TextStart.X + 10, yPosition);
      var header = DrawUtils.CreateText("Schematics For Sale", DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.LEFT);
      _sprites.Add(header);

      position = new Vector2(TextStart.X + TextSurface.X * 0.6f, yPosition);
      header = DrawUtils.CreateText("Size", DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.RIGHT);
      _sprites.Add(header);

      position = new Vector2(TextStart.X + TextSurface.X * 0.7f, yPosition);
      header = DrawUtils.CreateText("Qty", DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.RIGHT);
      _sprites.Add(header);

      position = new Vector2(TextStart.X + TextSurface.X - 10, yPosition);
      header = DrawUtils.CreateText("Price Per Unit", DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.RIGHT);
      _sprites.Add(header);
      
      yPosition += StringPixels.Y + 2;
      var from = new Vector2(TextStart.X + 10, yPosition);
      var to = new Vector2(TextStart.X + TextSurface.X - 10, yPosition);
      var line = DrawUtils.DrawLine(from, to, 2f, _white);
      _sprites.Add(line);

      yPosition += 2;

      foreach (var pricePair in itemPriceDict.Values)
        yPosition = CreateLineItem(pricePair, yPosition);
    }

    public void Draw(bool forceUpdate)
    {
      Surface.ContentType = ContentType.SCRIPT;
      Surface.Script = null;

      CalculateScreenPixels();

      using (var frame = Surface.DrawFrame())
      {
        if (forceUpdate)
        {
          var extra = DrawUtils.CreateSprite(DrawUtils.SQUARE, ref ScreenCenter, ref TextSurface, ref _white);
          frame.Add(extra);
        }

        var background = DrawUtils.CreateSprite(DrawUtils.SQUARE, ref ScreenCenter, ref TextSurface, ref _black);
        frame.Add(background);
        frame.AddRange(_sprites);
      }
    }

    void CalculateScreenPixels()
    {
      if (!Vector2.IsZero(ref StringPixels))
        return;

      _sb.Clear().Append("M");
      StringPixels = Surface.MeasureStringInPixels(_sb, DrawUtils.FONT, FontScale);
    }

    float CreateLineItem(ItemInfo info, float lastDrawnPosition)
    {
      var strPix = StringPixels;
      var start = TextStart.X;
      var pixels = TextSurface;
      var yPos = lastDrawnPosition;

      _sb.Clear().Append(info.Name);
      var length = Surface.MeasureStringInPixels(_sb, DrawUtils.FONT, FontScale);
      var maxLength = pixels.X * 0.5f;
      while (length.X > maxLength)
      {
        _sb.Length--;
        length = Surface.MeasureStringInPixels(_sb, DrawUtils.FONT, FontScale);
      }

      var name = _sb.ToString();
      var position = new Vector2(start + 10, yPos);
      var sprite = DrawUtils.CreateText(name, DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.LEFT);
      _sprites.Add(sprite);

      position = new Vector2(start + pixels.X * 0.6f, yPos);
      var size = info.Size == MyCubeSize.Small ? "SM" : "LG";
      sprite = DrawUtils.CreateText(size, DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.RIGHT);
      _sprites.Add(sprite);

      position = new Vector2(start + pixels.X * 0.7f, yPos);
      sprite = DrawUtils.CreateText(info.NumberAvailable.ToString(), DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.RIGHT);
      _sprites.Add(sprite);

      position = new Vector2(start + pixels.X - 10, yPos);
      var price = $"{info.PricePerItem:#,0} sc";
      sprite = DrawUtils.CreateText(price, DrawUtils.FONT, ref FontScale, ref position, ref _white, TextAlignment.RIGHT);
      _sprites.Add(sprite);

      return lastDrawnPosition + strPix.Y;
    }

    public void Close()
    {
      _sprites?.Clear();
      _sb?.Clear();
    }
  }
}

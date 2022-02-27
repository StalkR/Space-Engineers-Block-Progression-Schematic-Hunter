using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SchematicProgression.Economy;

using VRage.Game.GUI.TextPanel;

using VRageMath;

namespace SchematicProgression.Drawing
{
  public static class DrawUtils
  {
    public const string FONT = "Debug";
    public const string SQUARE = "SquareSimple";
    public const string SQUARE_HOLLOW = "SquareHollow";
    public const string TRIANGLE = "Triangle";
    public const string CIRCLE = "Circle";
    public const string CIRCLE_HOLLOW = "CircleHollow";
    public const string BRACKET_L = "DecorativeBracketLeft";
    public const string BRACKET_R = "DecorativeBracketRight";

    public static MySprite DrawLine(Vector2 lineFrom, Vector2 lineTo, float width, Color color, float rotation = 0)
    {
      Vector2 position = 0.5f * (lineFrom + lineTo);
      Vector2 diff = lineTo - lineFrom;
      float length = diff.Length();
      Vector2 size = new Vector2(length, width);

      return CreateSprite(SQUARE, ref position, ref size, ref color, rotation);
    }

    public static MySprite CreateSprite(string data, ref Vector2 position, ref Vector2 size, ref Color color, float rotationAngle = 0f)
    {
      return new MySprite(SpriteType.TEXTURE, data, position, size, color, rotation: rotationAngle);
    }

    public static MySprite CreateText(string text, string font, ref float scale, ref Vector2 position, ref Color color, TextAlignment alignment = TextAlignment.CENTER)
    {
      return new MySprite(SpriteType.TEXT, text, position, null, color, font, alignment, scale);
    }
  }
}

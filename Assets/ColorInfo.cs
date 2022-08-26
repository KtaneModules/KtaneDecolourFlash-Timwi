using DecolourFlash;
using UnityEngine;

struct ColorInfo
{
    public Color Color;
    public string ColorName;
    public CFColour Word;

    public ColorInfo(Color color, string colorName, CFColour word)
    {
        Color = color;
        ColorName = colorName;
        Word = word;
    }

    public override string ToString()
    {
        return string.Format("word {0}, color {1}", Word.ToString().ToLowerInvariant(), ColorName);
    }
}
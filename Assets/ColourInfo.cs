using DecolourFlash;
using UnityEngine;

struct ColourInfo
{
    public Color Colour { get; private set; }
    public CFColour ColourIx { get; private set; }
    public CFColour Word { get; private set; }

    public int Index { get { return (int) Word * 6 + (int) ColourIx; } }

    public ColourInfo(Color colour, CFColour colourIx, CFColour word)
    {
        Colour = colour;
        ColourIx = colourIx;
        Word = word;
    }

    public override string ToString()
    {
        return string.Format("word {0}, colour {1}", Word, ColourIx);
    }
}
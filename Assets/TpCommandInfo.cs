namespace DecolourFlash
{
    internal struct TpCommandInfo
    {
        public bool YesButton { get; private set; }
        public CFColour? Word { get; private set; }
        public CFColour? Colour { get; private set; }

        public TpCommandInfo(bool yes, CFColour? word, CFColour? colour)
        {
            YesButton = yes;
            Word = word;
            Colour = colour;
        }
    }
}
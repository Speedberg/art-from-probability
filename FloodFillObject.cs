namespace MarkovImage;

readonly struct FloodFillObject
{
    public readonly Point Point;
    public readonly System.Drawing.Color PreviousColour;

    public FloodFillObject(Point point, System.Drawing.Color colour)
    {
        Point = point;
        PreviousColour = colour;
    }
}
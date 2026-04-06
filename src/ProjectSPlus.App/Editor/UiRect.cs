namespace ProjectSPlus.App.Editor;

public readonly record struct UiRect(float X, float Y, float Width, float Height)
{
    public bool Contains(float x, float y)
    {
        return x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }
}

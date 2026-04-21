namespace ProjectSPlus.App.Editor;

public static class PixelStudioMirrorAxisMath
{
    public static float GetDefaultAxis(int dimension)
    {
        return dimension <= 0
            ? 0f
            : (dimension - 1) * 0.5f;
    }

    public static float NormalizeAxis(float axis, int dimension)
    {
        float snapped = float.IsFinite(axis)
            ? MathF.Round(axis * 2f) * 0.5f
            : GetDefaultAxis(dimension);
        float max = Math.Max(dimension - 1, 0);
        return Math.Clamp(snapped, 0f, max);
    }

    public static float GetAxisFromMouse(float mouseCoordinate, float viewportStart, int cellSize, int dimension)
    {
        float rawAxis = ((mouseCoordinate - viewportStart) / Math.Max(cellSize, 1)) - 0.5f;
        return NormalizeAxis(rawAxis, dimension);
    }

    public static int MirrorCoordinate(int coordinate, float axis)
    {
        return (int)MathF.Round((2f * axis) - coordinate);
    }

    public static float GetGuidePosition(float viewportStart, int cellSize, float axis)
    {
        return viewportStart + ((axis + 0.5f) * Math.Max(cellSize, 1)) - 0.5f;
    }

    public static UiRect GetVisibleCanvasRect(UiRect viewportRect, UiRect clipRect)
    {
        float left = Math.Max(viewportRect.X, clipRect.X);
        float top = Math.Max(viewportRect.Y, clipRect.Y);
        float right = Math.Min(viewportRect.X + viewportRect.Width, clipRect.X + clipRect.Width);
        float bottom = Math.Min(viewportRect.Y + viewportRect.Height, clipRect.Y + clipRect.Height);
        return new UiRect(left, top, Math.Max(right - left, 0f), Math.Max(bottom - top, 0f));
    }

    public static bool Intersects(UiRect a, UiRect b)
    {
        return GetOverlapArea(a, b) > 0f;
    }

    public static UiRect GetVerticalHandleRect(UiRect visibleCanvasRect, UiRect? obstructionRect, float guideX)
    {
        const float handleWidth = 16f;
        const float handleHeight = 18f;
        const float inset = 6f;
        float maxX = visibleCanvasRect.X + Math.Max(visibleCanvasRect.Width - handleWidth, 0f);
        float x = Math.Clamp(guideX - (handleWidth * 0.5f), visibleCanvasRect.X, maxX);
        float maxY = visibleCanvasRect.Y + Math.Max(visibleCanvasRect.Height - handleHeight, 0f);
        UiRect topRect = new(
            x,
            Math.Clamp(visibleCanvasRect.Y + inset, visibleCanvasRect.Y, maxY),
            handleWidth,
            handleHeight);
        UiRect bottomRect = new(
            x,
            Math.Clamp((visibleCanvasRect.Y + visibleCanvasRect.Height) - handleHeight - inset, visibleCanvasRect.Y, maxY),
            handleWidth,
            handleHeight);
        return ChoosePreferredHandleRect(topRect, bottomRect, obstructionRect);
    }

    public static UiRect GetHorizontalHandleRect(UiRect visibleCanvasRect, UiRect? obstructionRect, float guideY)
    {
        const float handleWidth = 18f;
        const float handleHeight = 16f;
        const float inset = 6f;
        float maxX = visibleCanvasRect.X + Math.Max(visibleCanvasRect.Width - handleWidth, 0f);
        float maxY = visibleCanvasRect.Y + Math.Max(visibleCanvasRect.Height - handleHeight, 0f);
        float y = Math.Clamp(guideY - (handleHeight * 0.5f), visibleCanvasRect.Y, maxY);
        UiRect leftRect = new(
            Math.Clamp(visibleCanvasRect.X + inset, visibleCanvasRect.X, maxX),
            y,
            handleWidth,
            handleHeight);
        UiRect rightRect = new(
            Math.Clamp((visibleCanvasRect.X + visibleCanvasRect.Width) - handleWidth - inset, visibleCanvasRect.X, maxX),
            y,
            handleWidth,
            handleHeight);
        return ChoosePreferredHandleRect(leftRect, rightRect, obstructionRect);
    }

    private static UiRect ChoosePreferredHandleRect(UiRect primaryRect, UiRect secondaryRect, UiRect? obstructionRect)
    {
        if (obstructionRect is null)
        {
            return primaryRect;
        }

        float primaryOverlap = GetOverlapArea(primaryRect, obstructionRect.Value);
        float secondaryOverlap = GetOverlapArea(secondaryRect, obstructionRect.Value);
        return secondaryOverlap < primaryOverlap
            ? secondaryRect
            : primaryRect;
    }

    private static float GetOverlapArea(UiRect a, UiRect b)
    {
        float left = Math.Max(a.X, b.X);
        float top = Math.Max(a.Y, b.Y);
        float right = Math.Min(a.X + a.Width, b.X + b.Width);
        float bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= left || bottom <= top)
        {
            return 0f;
        }

        return (right - left) * (bottom - top);
    }
}

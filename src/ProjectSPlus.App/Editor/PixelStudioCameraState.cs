namespace ProjectSPlus.App.Editor;

public readonly record struct PixelStudioCameraState(
    int Zoom,
    float PanX,
    float PanY,
    UiRect ViewportRect)
{
    public const int MinZoom = 2;
    public const int MaxZoom = 96;
}

public static class PixelStudioCameraMath
{
    public static int ClampZoom(int desiredZoom)
    {
        return Math.Clamp(desiredZoom, PixelStudioCameraState.MinZoom, PixelStudioCameraState.MaxZoom);
    }

    public static int ComputeFitZoom(UiRect clipRect, int canvasWidth, int canvasHeight)
    {
        int safeCanvasWidth = Math.Max(canvasWidth, 1);
        int safeCanvasHeight = Math.Max(canvasHeight, 1);
        float safeClipWidth = Math.Max(SanitizeFinite(clipRect.Width), 0f);
        float safeClipHeight = Math.Max(SanitizeFinite(clipRect.Height), 0f);
        if (safeClipWidth <= 0f || safeClipHeight <= 0f)
        {
            return PixelStudioCameraState.MinZoom;
        }

        float fitZoom = MathF.Min(safeClipWidth / safeCanvasWidth, safeClipHeight / safeCanvasHeight);
        if (!float.IsFinite(fitZoom))
        {
            return PixelStudioCameraState.MinZoom;
        }

        return ClampZoom(Math.Max((int)MathF.Floor(fitZoom), PixelStudioCameraState.MinZoom));
    }

    public static PixelStudioCameraState Compute(UiRect clipRect, int canvasWidth, int canvasHeight, int desiredZoom, float panX, float panY)
    {
        int safeCanvasWidth = Math.Max(canvasWidth, 1);
        int safeCanvasHeight = Math.Max(canvasHeight, 1);
        int zoom = ClampZoom(desiredZoom);

        float safeClipX = SanitizeFinite(clipRect.X);
        float safeClipY = SanitizeFinite(clipRect.Y);
        float safeClipWidth = Math.Max(SanitizeFinite(clipRect.Width), 0f);
        float safeClipHeight = Math.Max(SanitizeFinite(clipRect.Height), 0f);
        float viewportWidth = safeCanvasWidth * zoom;
        float viewportHeight = safeCanvasHeight * zoom;

        float safePanX = SanitizeFinite(panX);
        float safePanY = SanitizeFinite(panY);
        float maxPanX = Math.Max((viewportWidth + safeClipWidth) * 0.5f, safeClipWidth + 24f);
        float maxPanY = Math.Max((viewportHeight + safeClipHeight) * 0.5f, safeClipHeight + 24f);
        safePanX = Math.Clamp(safePanX, -maxPanX, maxPanX);
        safePanY = Math.Clamp(safePanY, -maxPanY, maxPanY);

        float viewportX = safeClipX + ((safeClipWidth - viewportWidth) * 0.5f) + safePanX;
        float viewportY = safeClipY + ((safeClipHeight - viewportHeight) * 0.5f) + safePanY;
        if (!float.IsFinite(viewportX))
        {
            viewportX = safeClipX;
            safePanX = 0f;
        }

        if (!float.IsFinite(viewportY))
        {
            viewportY = safeClipY;
            safePanY = 0f;
        }

        UiRect viewportRect = new(
            MathF.Round(viewportX),
            MathF.Round(viewportY),
            MathF.Round(Math.Max(viewportWidth, 0f)),
            MathF.Round(Math.Max(viewportHeight, 0f)));

        return new PixelStudioCameraState(
            zoom,
            safePanX,
            safePanY,
            viewportRect);
    }

    private static float SanitizeFinite(float value)
    {
        return float.IsFinite(value) ? value : 0f;
    }
}

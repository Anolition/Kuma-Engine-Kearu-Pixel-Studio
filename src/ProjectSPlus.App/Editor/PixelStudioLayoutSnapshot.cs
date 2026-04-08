namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioLayoutSnapshot
{
    public required UiRect HeaderRect { get; init; }

    public required UiRect CommandBarRect { get; init; }

    public required UiRect ToolbarRect { get; init; }

    public required UiRect CanvasPanelRect { get; init; }

    public required UiRect CanvasClipRect { get; init; }

    public required UiRect CanvasViewportRect { get; init; }

    public required UiRect LeftSplitterRect { get; init; }

    public required UiRect RightSplitterRect { get; init; }

    public required UiRect LeftCollapseHandleRect { get; init; }

    public required UiRect RightCollapseHandleRect { get; init; }

    public required UiRect PalettePanelRect { get; init; }

    public required UiRect ToolSettingsPanelRect { get; init; }

    public required UiRect LayersPanelRect { get; init; }

    public required UiRect TimelinePanelRect { get; init; }

    public required UiRect ActiveColorRect { get; init; }

    public required UiRect PlaybackPreviewRect { get; init; }

    public UiRect? PaletteSwatchViewportRect { get; init; }

    public UiRect? PaletteSwatchScrollTrackRect { get; init; }

    public UiRect? PaletteSwatchScrollThumbRect { get; init; }

    public UiRect? SavedPaletteViewportRect { get; init; }

    public UiRect? SavedPaletteScrollTrackRect { get; init; }

    public UiRect? SavedPaletteScrollThumbRect { get; init; }

    public UiRect? LayerListViewportRect { get; init; }

    public UiRect? LayerScrollTrackRect { get; init; }

    public UiRect? LayerScrollThumbRect { get; init; }

    public UiRect? FrameListViewportRect { get; init; }

    public UiRect? FrameScrollTrackRect { get; init; }

    public UiRect? FrameScrollThumbRect { get; init; }

    public UiRect? PaletteLibraryRect { get; init; }

    public UiRect? PaletteRenameFieldRect { get; init; }

    public UiRect? LayerRenameFieldRect { get; init; }

    public UiRect? FrameRenameFieldRect { get; init; }

    public UiRect? PalettePromptRect { get; init; }

    public UiRect? ContextMenuRect { get; init; }

    public UiRect? BrushSizeSliderRect { get; init; }

    public UiRect? BrushSizeFillRect { get; init; }

    public UiRect? BrushSizeKnobRect { get; init; }

    public UiRect? BrushPreviewRect { get; init; }

    public required int CameraZoom { get; init; }

    public required float CameraPanX { get; init; }

    public required float CameraPanY { get; init; }

    public required int CanvasCellSize { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioToolKind>> ToolButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> DocumentButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> CanvasButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> PaletteButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> ToolSettingsButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> PaletteLibraryButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> PalettePromptButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioContextMenuAction>> ContextMenuButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> LayerButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> TimelineButtons { get; init; }

    public required IReadOnlyList<IndexedRect> PaletteSwatches { get; init; }

    public required IReadOnlyList<IndexedRect> SavedPaletteRows { get; init; }

    public required IReadOnlyList<IndexedRect> LayerRows { get; init; }

    public required IReadOnlyList<IndexedRect> LayerVisibilityButtons { get; init; }

    public required IReadOnlyList<IndexedRect> FrameRows { get; init; }

    public required IReadOnlyList<IndexedRect> CanvasCells { get; init; }

    public PixelStudioLayoutSnapshot WithCanvasCamera(PixelStudioCameraState camera)
    {
        return new PixelStudioLayoutSnapshot
        {
            HeaderRect = HeaderRect,
            CommandBarRect = CommandBarRect,
            ToolbarRect = ToolbarRect,
            CanvasPanelRect = CanvasPanelRect,
            CanvasClipRect = CanvasClipRect,
            CanvasViewportRect = camera.ViewportRect,
            LeftSplitterRect = LeftSplitterRect,
            RightSplitterRect = RightSplitterRect,
            LeftCollapseHandleRect = LeftCollapseHandleRect,
            RightCollapseHandleRect = RightCollapseHandleRect,
            PalettePanelRect = PalettePanelRect,
            ToolSettingsPanelRect = ToolSettingsPanelRect,
            LayersPanelRect = LayersPanelRect,
            TimelinePanelRect = TimelinePanelRect,
            ActiveColorRect = ActiveColorRect,
            PlaybackPreviewRect = PlaybackPreviewRect,
            PaletteSwatchViewportRect = PaletteSwatchViewportRect,
            PaletteSwatchScrollTrackRect = PaletteSwatchScrollTrackRect,
            PaletteSwatchScrollThumbRect = PaletteSwatchScrollThumbRect,
            SavedPaletteViewportRect = SavedPaletteViewportRect,
            SavedPaletteScrollTrackRect = SavedPaletteScrollTrackRect,
            SavedPaletteScrollThumbRect = SavedPaletteScrollThumbRect,
            LayerListViewportRect = LayerListViewportRect,
            LayerScrollTrackRect = LayerScrollTrackRect,
            LayerScrollThumbRect = LayerScrollThumbRect,
            FrameListViewportRect = FrameListViewportRect,
            FrameScrollTrackRect = FrameScrollTrackRect,
            FrameScrollThumbRect = FrameScrollThumbRect,
            PaletteLibraryRect = PaletteLibraryRect,
            PaletteRenameFieldRect = PaletteRenameFieldRect,
            LayerRenameFieldRect = LayerRenameFieldRect,
            FrameRenameFieldRect = FrameRenameFieldRect,
            PalettePromptRect = PalettePromptRect,
            ContextMenuRect = ContextMenuRect,
            BrushSizeSliderRect = BrushSizeSliderRect,
            BrushSizeFillRect = BrushSizeFillRect,
            BrushSizeKnobRect = BrushSizeKnobRect,
            BrushPreviewRect = BrushPreviewRect,
            CameraZoom = camera.Zoom,
            CameraPanX = camera.PanX,
            CameraPanY = camera.PanY,
            CanvasCellSize = camera.Zoom,
            ToolButtons = ToolButtons,
            DocumentButtons = DocumentButtons,
            CanvasButtons = CanvasButtons,
            PaletteButtons = PaletteButtons,
            ToolSettingsButtons = ToolSettingsButtons,
            PaletteLibraryButtons = PaletteLibraryButtons,
            PalettePromptButtons = PalettePromptButtons,
            ContextMenuButtons = ContextMenuButtons,
            LayerButtons = LayerButtons,
            TimelineButtons = TimelineButtons,
            PaletteSwatches = PaletteSwatches,
            SavedPaletteRows = SavedPaletteRows,
            LayerRows = LayerRows,
            LayerVisibilityButtons = LayerVisibilityButtons,
            FrameRows = FrameRows,
            CanvasCells = CanvasCells
        };
    }

    public PixelStudioLayoutSnapshot WithToolSettingsPanel(UiRect panelRect)
    {
        float deltaX = panelRect.X - ToolSettingsPanelRect.X;
        float deltaY = panelRect.Y - ToolSettingsPanelRect.Y;

        return new PixelStudioLayoutSnapshot
        {
            HeaderRect = HeaderRect,
            CommandBarRect = CommandBarRect,
            ToolbarRect = ToolbarRect,
            CanvasPanelRect = CanvasPanelRect,
            CanvasClipRect = CanvasClipRect,
            CanvasViewportRect = CanvasViewportRect,
            LeftSplitterRect = LeftSplitterRect,
            RightSplitterRect = RightSplitterRect,
            LeftCollapseHandleRect = LeftCollapseHandleRect,
            RightCollapseHandleRect = RightCollapseHandleRect,
            PalettePanelRect = PalettePanelRect,
            ToolSettingsPanelRect = panelRect,
            LayersPanelRect = LayersPanelRect,
            TimelinePanelRect = TimelinePanelRect,
            ActiveColorRect = ActiveColorRect,
            PlaybackPreviewRect = PlaybackPreviewRect,
            PaletteSwatchViewportRect = PaletteSwatchViewportRect,
            PaletteSwatchScrollTrackRect = PaletteSwatchScrollTrackRect,
            PaletteSwatchScrollThumbRect = PaletteSwatchScrollThumbRect,
            SavedPaletteViewportRect = SavedPaletteViewportRect,
            SavedPaletteScrollTrackRect = SavedPaletteScrollTrackRect,
            SavedPaletteScrollThumbRect = SavedPaletteScrollThumbRect,
            LayerListViewportRect = LayerListViewportRect,
            LayerScrollTrackRect = LayerScrollTrackRect,
            LayerScrollThumbRect = LayerScrollThumbRect,
            FrameListViewportRect = FrameListViewportRect,
            FrameScrollTrackRect = FrameScrollTrackRect,
            FrameScrollThumbRect = FrameScrollThumbRect,
            PaletteLibraryRect = PaletteLibraryRect,
            PaletteRenameFieldRect = PaletteRenameFieldRect,
            LayerRenameFieldRect = LayerRenameFieldRect,
            FrameRenameFieldRect = FrameRenameFieldRect,
            PalettePromptRect = PalettePromptRect,
            ContextMenuRect = ContextMenuRect,
            BrushSizeSliderRect = OffsetRect(BrushSizeSliderRect, deltaX, deltaY),
            BrushSizeFillRect = OffsetRect(BrushSizeFillRect, deltaX, deltaY),
            BrushSizeKnobRect = OffsetRect(BrushSizeKnobRect, deltaX, deltaY),
            BrushPreviewRect = OffsetRect(BrushPreviewRect, deltaX, deltaY),
            CameraZoom = CameraZoom,
            CameraPanX = CameraPanX,
            CameraPanY = CameraPanY,
            CanvasCellSize = CanvasCellSize,
            ToolButtons = ToolButtons,
            DocumentButtons = DocumentButtons,
            CanvasButtons = CanvasButtons,
            PaletteButtons = PaletteButtons,
            ToolSettingsButtons = ToolSettingsButtons,
            PaletteLibraryButtons = PaletteLibraryButtons,
            PalettePromptButtons = PalettePromptButtons,
            ContextMenuButtons = ContextMenuButtons,
            LayerButtons = LayerButtons,
            TimelineButtons = TimelineButtons,
            PaletteSwatches = PaletteSwatches,
            SavedPaletteRows = SavedPaletteRows,
            LayerRows = LayerRows,
            LayerVisibilityButtons = LayerVisibilityButtons,
            FrameRows = FrameRows,
            CanvasCells = CanvasCells
        };
    }

    private static UiRect? OffsetRect(UiRect? rect, float deltaX, float deltaY)
    {
        if (rect is null)
        {
            return null;
        }

        return new UiRect(
            rect.Value.X + deltaX,
            rect.Value.Y + deltaY,
            rect.Value.Width,
            rect.Value.Height);
    }
}

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioLayoutSnapshot
{
    public required UiRect HeaderRect { get; init; }

    public required UiRect CommandBarRect { get; init; }

    public required UiRect ToolbarRect { get; init; }

    public required UiRect CanvasPanelRect { get; init; }

    public required UiRect CanvasViewportRect { get; init; }

    public required UiRect LeftSplitterRect { get; init; }

    public required UiRect RightSplitterRect { get; init; }

    public required UiRect LeftCollapseHandleRect { get; init; }

    public required UiRect RightCollapseHandleRect { get; init; }

    public required UiRect PalettePanelRect { get; init; }

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

    public UiRect? PalettePromptRect { get; init; }

    public required int CanvasCellSize { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioToolKind>> ToolButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> DocumentButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> CanvasButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> PaletteButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> PaletteLibraryButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> PalettePromptButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> LayerButtons { get; init; }

    public required IReadOnlyList<ActionRect<PixelStudioAction>> TimelineButtons { get; init; }

    public required IReadOnlyList<IndexedRect> PaletteSwatches { get; init; }

    public required IReadOnlyList<IndexedRect> SavedPaletteRows { get; init; }

    public required IReadOnlyList<IndexedRect> LayerRows { get; init; }

    public required IReadOnlyList<IndexedRect> LayerVisibilityButtons { get; init; }

    public required IReadOnlyList<IndexedRect> FrameRows { get; init; }

    public required IReadOnlyList<IndexedRect> CanvasCells { get; init; }
}

using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioViewState
{
    public string DocumentName { get; set; } = string.Empty;

    public int CanvasWidth { get; set; }

    public int CanvasHeight { get; set; }

    public int Zoom { get; set; }

    public int BrushSize { get; set; }

    public float CanvasPanX { get; set; }

    public float CanvasPanY { get; set; }

    public bool ShowGrid { get; set; }

    public int FramesPerSecond { get; set; }

    public bool IsPlaying { get; set; }

    public bool CanUndo { get; set; }

    public bool CanRedo { get; set; }

    public PixelStudioToolKind ActiveTool { get; set; } = PixelStudioToolKind.Pencil;

    public int ActivePaletteIndex { get; set; }

    public string ActiveColorHex { get; set; } = "#FFFFFF";

    public ThemeColor ActiveColor { get; set; } = new(1.0f, 1.0f, 1.0f);

    public string ActivePaletteName { get; set; } = "Current Palette";

    public bool PaletteLibraryVisible { get; set; }

    public bool PalettePromptVisible { get; set; }

    public bool PaletteRenameActive { get; set; }

    public bool LayerRenameActive { get; set; }

    public bool FrameRenameActive { get; set; }

    public bool HasSelection { get; set; }

    public int SelectionX { get; set; }

    public int SelectionY { get; set; }

    public int SelectionWidth { get; set; }

    public int SelectionHeight { get; set; }

    public bool CanvasResizeDialogVisible { get; set; }

    public string CanvasResizeWidthBuffer { get; set; } = string.Empty;

    public string CanvasResizeHeightBuffer { get; set; } = string.Empty;

    public bool CanvasResizeWidthFieldActive { get; set; }

    public bool CanvasResizeHeightFieldActive { get; set; }

    public bool CanvasResizeWouldCrop { get; set; }

    public string CanvasResizeWarningText { get; set; } = string.Empty;

    public PixelStudioResizeAnchor CanvasResizeAnchor { get; set; } = PixelStudioResizeAnchor.TopLeft;

    public bool PromptForPaletteGenerationAfterImport { get; set; } = true;

    public float ToolsPanelPreferredWidth { get; set; } = 164;

    public float SidebarPreferredWidth { get; set; } = 360;

    public bool ToolsPanelCollapsed { get; set; }

    public bool SidebarCollapsed { get; set; }

    public bool TimelineVisible { get; set; }

    public float ToolSettingsPanelOffsetX { get; set; }

    public float ToolSettingsPanelOffsetY { get; set; }

    public int PaletteSwatchScrollRow { get; set; }

    public int SavedPaletteScrollRow { get; set; }

    public int LayerScrollRow { get; set; }

    public int FrameScrollRow { get; set; }

    public string PaletteRenameBuffer { get; set; } = string.Empty;

    public string LayerRenameBuffer { get; set; } = string.Empty;

    public string FrameRenameBuffer { get; set; } = string.Empty;

    public bool ContextMenuVisible { get; set; }

    public float ContextMenuX { get; set; }

    public float ContextMenuY { get; set; }

    public IReadOnlyList<PixelStudioContextMenuItemView> ContextMenuItems { get; set; } = [];

    public IReadOnlyList<ThemeColor> Palette { get; set; } = [];

    public IReadOnlyList<ThemeColor?> CompositePixels { get; set; } = [];

    public IReadOnlyList<ThemeColor?> PreviewPixels { get; set; } = [];

    public int CompositePixelsRevision { get; set; }

    public int PreviewPixelsRevision { get; set; }

    public IReadOnlyList<PixelStudioSavedPaletteView> SavedPalettes { get; set; } = [];

    public IReadOnlyList<PixelStudioLayerView> Layers { get; set; } = [];

    public IReadOnlyList<PixelStudioFrameView> Frames { get; set; } = [];
}

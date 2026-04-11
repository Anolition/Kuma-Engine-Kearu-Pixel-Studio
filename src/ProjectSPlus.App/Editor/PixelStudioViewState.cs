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

    public PixelStudioMirrorMode MirrorMode { get; set; }

    public int FramesPerSecond { get; set; }

    public bool ShowOnionSkin { get; set; }

    public float OnionOpacity { get; set; } = 0.42f;

    public bool IsPlaying { get; set; }

    public bool CanUndo { get; set; }

    public bool CanRedo { get; set; }

    public PixelStudioToolKind ActiveTool { get; set; } = PixelStudioToolKind.Pencil;

    public PixelStudioShapeRenderMode RectangleRenderMode { get; set; } = PixelStudioShapeRenderMode.Outline;

    public PixelStudioShapeRenderMode EllipseRenderMode { get; set; } = PixelStudioShapeRenderMode.Outline;

    public PixelStudioShapePreset ShapePreset { get; set; } = PixelStudioShapePreset.Star;

    public PixelStudioShapeRenderMode ShapeRenderMode { get; set; } = PixelStudioShapeRenderMode.Outline;

    public int ActivePaletteIndex { get; set; }

    public string ActiveColorHex { get; set; } = "#FFFFFF";

    public ThemeColor ActiveColor { get; set; } = new(1.0f, 1.0f, 1.0f);

    public ThemeColor SecondaryColor { get; set; } = new(0.0f, 0.0f, 0.0f);

    public float ActiveColorAlpha { get; set; } = 1.0f;

    public float ActiveLayerOpacity { get; set; } = 1.0f;

    public bool ActiveLayerAlphaLocked { get; set; }

    public bool LayerOpacityControlsVisible { get; set; }

    public string ActivePaletteName { get; set; } = "Current Palette";

    public PixelStudioColorPickerMode ColorPickerMode { get; set; } = PixelStudioColorPickerMode.RgbField;

    public bool PaletteLibraryVisible { get; set; }

    public bool PalettePromptVisible { get; set; }

    public bool PaletteRenameActive { get; set; }

    public bool PaletteRenameSelected { get; set; }

    public bool LayerRenameActive { get; set; }

    public bool LayerRenameSelected { get; set; }

    public bool FrameRenameActive { get; set; }

    public bool FrameRenameSelected { get; set; }

    public bool HoverTooltipVisible { get; set; }

    public string HoverTooltipTitle { get; set; } = string.Empty;

    public string HoverTooltipBody { get; set; } = string.Empty;

    public float HoverTooltipX { get; set; }

    public float HoverTooltipY { get; set; }

    public bool HasSelection { get; set; }

    public PixelStudioSelectionMode SelectionMode { get; set; } = PixelStudioSelectionMode.Box;

    public bool SelectionTransformModeActive { get; set; }

    public int SelectionX { get; set; }

    public int SelectionY { get; set; }

    public int SelectionWidth { get; set; }

    public int SelectionHeight { get; set; }

    public bool SelectionUsesMask { get; set; }

    public IReadOnlySet<int> SelectionMaskIndices { get; set; } = new HashSet<int>();

    public bool SelectionTransformPreviewVisible { get; set; }

    public int SelectionTransformPreviewX { get; set; }

    public int SelectionTransformPreviewY { get; set; }

    public int SelectionTransformPreviewWidth { get; set; }

    public int SelectionTransformPreviewHeight { get; set; }

    public float SelectionTransformPreviewRotationDegrees { get; set; }

    public bool HasClipboardSelection { get; set; }

    public int ClipboardWidth { get; set; }

    public int ClipboardHeight { get; set; }

    public bool CanvasResizeDialogVisible { get; set; }

    public string CanvasResizeWidthBuffer { get; set; } = string.Empty;

    public string CanvasResizeHeightBuffer { get; set; } = string.Empty;

    public bool CanvasResizeWidthFieldActive { get; set; }

    public bool CanvasResizeWidthSelected { get; set; }

    public bool CanvasResizeHeightFieldActive { get; set; }

    public bool CanvasResizeHeightSelected { get; set; }

    public bool CanvasResizeWouldCrop { get; set; }

    public string CanvasResizeWarningText { get; set; } = string.Empty;

    public bool WarningDialogVisible { get; set; }

    public string WarningDialogTitle { get; set; } = string.Empty;

    public string WarningDialogMessage { get; set; } = string.Empty;

    public string WarningDialogConfirmLabel { get; set; } = "Continue";

    public string WarningDialogCancelLabel { get; set; } = "Cancel";

    public bool WarningToastVisible { get; set; }

    public string WarningToastText { get; set; } = string.Empty;

    public PixelStudioResizeAnchor CanvasResizeAnchor { get; set; } = PixelStudioResizeAnchor.TopLeft;

    public bool PromptForPaletteGenerationAfterImport { get; set; } = true;

    public float ToolsPanelPreferredWidth { get; set; } = 40;

    public float SidebarPreferredWidth { get; set; } = 360;

    public bool ToolsPanelCollapsed { get; set; }

    public bool SidebarCollapsed { get; set; }

    public bool TimelineVisible { get; set; }

    public float ToolSettingsPanelOffsetX { get; set; }

    public float ToolSettingsPanelOffsetY { get; set; }

    public bool NavigatorVisible { get; set; }

    public float NavigatorPanelOffsetX { get; set; }

    public float NavigatorPanelOffsetY { get; set; }

    public float NavigatorPanelWidth { get; set; }

    public float NavigatorPanelHeight { get; set; }

    public bool AnimationPreviewVisible { get; set; }

    public float AnimationPreviewPanelOffsetX { get; set; }

    public float AnimationPreviewPanelOffsetY { get; set; }

    public float AnimationPreviewPanelWidth { get; set; }

    public float AnimationPreviewPanelHeight { get; set; }

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

    public IReadOnlyList<ThemeColor> RecentColors { get; set; } = [];

    public IReadOnlyList<ThemeColor?> CompositePixels { get; set; } = [];

    public IReadOnlyList<ThemeColor?> OnionPreviousPixels { get; set; } = [];

    public IReadOnlyList<ThemeColor?> OnionNextPixels { get; set; } = [];

    public IReadOnlyList<ThemeColor?> PreviewPixels { get; set; } = [];

    public int CompositePixelsRevision { get; set; }

    public int OnionPreviousPixelsRevision { get; set; }

    public int OnionNextPixelsRevision { get; set; }

    public int PreviewPixelsRevision { get; set; }

    public IReadOnlyList<PixelStudioSavedPaletteView> SavedPalettes { get; set; } = [];

    public IReadOnlyList<PixelStudioLayerView> Layers { get; set; } = [];

    public IReadOnlyList<PixelStudioFrameView> Frames { get; set; } = [];
}

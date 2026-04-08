using AppTextRenderer = ProjectSPlus.App.Rendering.TextRenderer;
using AppImageRenderer = ProjectSPlus.App.Rendering.ImageRenderer;
using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Shell;
using ProjectSPlus.Editor.Themes;
using SixLabors.Fonts;
using Silk.NET.OpenGL;

namespace ProjectSPlus.App.Editor;

public sealed class EditorShellRenderer : IDisposable
{
    private const float UiTextLineHeight = 20f;
    private const float UiHeaderHeight = 32f;
    private const float UiButtonHeight = 34f;
    private const float UiCompactButtonHeight = 30f;
    private const float PixelPanelHeaderHeight = UiHeaderHeight;
    private const float PixelPanelPadding = 10f;
    private const float PixelPanelGap = 10f;
    private const float CollapsedPanelThreshold = 40f;
    private const float TextTexturePadding = 6f;
    private static readonly bool ShowTextDebugBounds = false;

    private enum UiTextAlignment
    {
        Left,
        Center
    }

    private readonly record struct TextMeasurement(float Width, float Ascent, float Descent, float TextHeight);

    private readonly GL _gl;
    private readonly EditorShell _shell;
    private readonly AppTextRenderer _textRenderer;
    private readonly AppImageRenderer _imageRenderer;

    private EditorTheme _theme;
    private EditorTypography _typography;
    private EditorUiState _uiState;
    private EditorLayoutSnapshot? _layoutSnapshot;
    private FontFamily _resolvedFontFamily = default;
    private bool _hasResolvedFontFamily;
    private int _width;
    private int _height;
    private readonly string _brandingImagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Branding", "KumaEngineLogo.png");

    public EditorShellRenderer(GL gl, EditorShell shell, EditorTheme theme, EditorTypography typography, EditorUiState uiState)
    {
        _gl = gl;
        _shell = shell;
        _theme = theme;
        _typography = typography;
        _uiState = uiState;
        _textRenderer = new AppTextRenderer(gl);
        _imageRenderer = new AppImageRenderer(gl);
    }

    public void UpdateTheme(EditorTheme theme)
    {
        _theme = theme;
    }

    public void UpdateTypography(EditorTypography typography)
    {
        bool changed = !string.Equals(_typography.PreferredFontFamily, typography.PreferredFontFamily, StringComparison.OrdinalIgnoreCase)
            || _typography.FontSizePreset != typography.FontSizePreset
            || !_typography.MenuText.Equals(typography.MenuText)
            || !_typography.BodyText.Equals(typography.BodyText)
            || !_typography.StatusText.Equals(typography.StatusText)
            || !_typography.PanelTitleText.Equals(typography.PanelTitleText);

        _typography = typography;
        if (changed)
        {
            _hasResolvedFontFamily = false;
        }
    }

    public void UpdateUiState(EditorUiState uiState)
    {
        _uiState = uiState;
    }

    public void UpdateLayoutSnapshot(EditorLayoutSnapshot? layoutSnapshot)
    {
        _layoutSnapshot = layoutSnapshot;
    }

    public void InvalidateTextCache()
    {
        _hasResolvedFontFamily = false;
        _textRenderer.ClearCache();
    }

    public void Resize(int width, int height)
    {
        _width = Math.Max(width, 1);
        _height = Math.Max(height, 1);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
        _textRenderer.Resize(_width, _height);
        _imageRenderer.Resize(_width, _height);
    }

    public void Render()
    {
        if (_layoutSnapshot is null)
        {
            return;
        }

        _gl.Disable(EnableCap.ScissorTest);
        Clear(_theme.Background);
        _gl.Enable(EnableCap.ScissorTest);

        DrawShellRegions();
        DrawMenuButtons();
        DrawMenuLogo();
        DrawTabs();
        DrawPageSurface();
        DrawShellText();
        DrawMenuDropdown();
        DrawMenuDropdownText();
    }

    public void Dispose()
    {
        _imageRenderer.Dispose();
        _textRenderer.Dispose();
        GC.SuppressFinalize(this);
    }

    private void DrawShellRegions()
    {
        DrawUiRect(_layoutSnapshot!.MenuBarRect, ResolveTopBarColor());
        DrawUiRect(_layoutSnapshot.TabStripRect, _theme.TabStrip);
        DrawPixelPanel(_layoutSnapshot.LeftPanelRect, _theme.SidePanel);
        DrawPixelPanel(_layoutSnapshot.RightPanelRect, _theme.SidePanel);
        DrawUiRect(_layoutSnapshot.WorkspaceRect, _theme.Workspace);
        DrawUiRect(_layoutSnapshot.StatusBarRect, _theme.StatusBar);

        DrawUiRect(new UiRect(0, _layoutSnapshot.MenuBarRect.Height - 1, _width, 1), _theme.Divider);
        DrawUiRect(new UiRect(0, _layoutSnapshot.StatusBarRect.Y - 1, _width, 1), _theme.Divider);
        DrawUiRect(new UiRect(_layoutSnapshot.LeftPanelRect.Width, _layoutSnapshot.LeftPanelRect.Y, 1, _layoutSnapshot.LeftPanelRect.Height), _theme.Divider);
        DrawUiRect(new UiRect(_layoutSnapshot.RightPanelRect.X - 1, _layoutSnapshot.RightPanelRect.Y, 1, _layoutSnapshot.RightPanelRect.Height), _theme.Divider);
        DrawUiRect(_layoutSnapshot.LeftSplitterRect, Blend(_theme.Divider, _theme.Background, 0.18f));
        DrawUiRect(_layoutSnapshot.RightSplitterRect, Blend(_theme.Divider, _theme.Background, 0.18f));
        DrawUiRect(_layoutSnapshot.LeftCollapseHandleRect, Blend(_theme.TabInactive, _theme.Accent, 0.22f));
        DrawUiRect(_layoutSnapshot.RightCollapseHandleRect, Blend(_theme.TabInactive, _theme.Accent, 0.22f));

        EditorPageKind currentPage = _uiState.Tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;
        if (currentPage != EditorPageKind.PixelStudio)
        {
            foreach (IndexedRect row in _layoutSnapshot.LeftPanelRecentProjectRows)
            {
                DrawUiRect(row.Rect, _theme.TabInactive);
            }

            DrawScrollRegion(_layoutSnapshot.LeftPanelRecentScrollTrackRect, _layoutSnapshot.LeftPanelRecentScrollThumbRect);
        }
    }

    private void DrawMenuButtons()
    {
        ThemeColor topBarColor = ResolveTopBarColor();
        ThemeColor inactiveColor = Blend(topBarColor, _theme.TabInactive, 0.22f);
        ThemeColor activeColor = Blend(topBarColor, _theme.Accent, 0.38f);

        foreach (NamedRect button in _layoutSnapshot!.MenuButtons)
        {
            ThemeColor color = string.Equals(_uiState.OpenMenuName, button.Id, StringComparison.Ordinal)
                ? activeColor
                : inactiveColor;

            DrawUiRect(button.Rect, color);
        }
    }

    private void DrawTabs()
    {
        foreach (NamedRect tab in _layoutSnapshot!.TabButtons)
        {
            ThemeColor color = string.Equals(_uiState.SelectedTabId, tab.Id, StringComparison.Ordinal)
                ? _theme.TabActive
                : _theme.TabInactive;

            DrawUiRect(tab.Rect, color);
        }

        foreach (NamedRect close in _layoutSnapshot.TabCloseButtons)
        {
            DrawUiRect(close.Rect, _theme.Divider);
        }
    }

    private void DrawPageSurface()
    {
        EditorPageKind currentPage = _uiState.Tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;
        switch (currentPage)
        {
            case EditorPageKind.Home:
                DrawHomePage();
                break;
            case EditorPageKind.PixelStudio:
                DrawPixelStudioPage();
                break;
            case EditorPageKind.Projects:
                DrawProjectsPage();
                break;
            case EditorPageKind.Preferences:
                DrawPreferencesPage();
                break;
            case EditorPageKind.Layout:
                DrawLayoutPage();
                break;
            case EditorPageKind.Scratch:
                DrawScratchPage();
                break;
        }
    }

    private void DrawHomePage()
    {
        if (_layoutSnapshot!.HomeHeroPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.HomeHeroPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.12f));
        }

        if (_layoutSnapshot.HomeActionsPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.HomeActionsPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.18f));
        }

        if (_layoutSnapshot.HomeRecentPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.HomeRecentPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.12f));
        }

        foreach (ActionRect<EditorHomeAction> card in _layoutSnapshot!.HomeCards)
        {
            DrawUiRect(card.Rect, _theme.TabInactive);
        }

        foreach (IndexedRect row in _layoutSnapshot.RecentProjectRows)
        {
            DrawUiRect(row.Rect, _theme.TabInactive);
        }

        DrawScrollRegion(_layoutSnapshot.HomeRecentScrollTrackRect, _layoutSnapshot.HomeRecentScrollThumbRect);
    }

    private void DrawPixelStudioPage()
    {
        if (_layoutSnapshot!.PixelStudio is null)
        {
            return;
        }

        PixelStudioLayoutSnapshot layout = _layoutSnapshot.PixelStudio;
        ThemeColor canvasPanelColor = new(0.14f, 0.14f, 0.15f);
        ThemeColor canvasSurfaceColor = new(0.18f, 0.18f, 0.19f);
        ThemeColor canvasCheckerLight = new(0.24f, 0.24f, 0.25f);
        ThemeColor canvasCheckerDark = new(0.19f, 0.19f, 0.20f);
        bool toolsCollapsed = IsCollapsedPanel(layout.ToolbarRect);
        bool sidebarCollapsed = IsCollapsedPanel(layout.PalettePanelRect);
        bool toolSettingsVisible = !IsCollapsedPanel(layout.ToolSettingsPanelRect);
        DrawUiRect(layout.HeaderRect, _theme.TabInactive);
        DrawUiRect(layout.CommandBarRect, _theme.SidePanel);
        DrawPixelPanel(layout.ToolbarRect, _theme.SidePanel);
        DrawPixelPanel(layout.CanvasPanelRect, canvasPanelColor);
        DrawPixelPanel(layout.PalettePanelRect, _theme.SidePanel);
        DrawPixelPanel(layout.LayersPanelRect, _theme.SidePanel);
        if (layout.TimelinePanelRect.Height > 0)
        {
            DrawPixelPanel(layout.TimelinePanelRect, _theme.SidePanel);
        }
        DrawUiRect(GetPixelPanelBodyRect(layout.CanvasPanelRect), canvasSurfaceColor);
        DrawUiRect(layout.LeftSplitterRect, Blend(_theme.Divider, _theme.Background, 0.18f));
        DrawUiRect(layout.RightSplitterRect, Blend(_theme.Divider, _theme.Background, 0.18f));
        DrawUiRect(layout.LeftCollapseHandleRect, Blend(_theme.TabInactive, _theme.Accent, 0.22f));
        DrawUiRect(layout.RightCollapseHandleRect, Blend(_theme.TabInactive, _theme.Accent, 0.22f));

        if (!sidebarCollapsed && layout.PaletteButtons.Count > 0)
        {
            UiRect paletteBodyRect = GetPixelPanelBodyRect(layout.PalettePanelRect);
            ThemeColor paletteSectionColor = Blend(_theme.SidePanel, _theme.Workspace, 0.26f);
            ActionRect<PixelStudioAction> addSwatchButton = layout.PaletteButtons.First(button => button.Action == PixelStudioAction.AddPaletteSwatch);
            float swatchSectionY = layout.ActiveColorRect.Y + layout.ActiveColorRect.Height + 28;
            UiRect activeSectionRect = new(paletteBodyRect.X, paletteBodyRect.Y, paletteBodyRect.Width, Math.Max((layout.ActiveColorRect.Y + layout.ActiveColorRect.Height) - paletteBodyRect.Y + 8, 112));
            UiRect swatchSectionRect = new(paletteBodyRect.X, swatchSectionY, paletteBodyRect.Width, Math.Max(addSwatchButton.Rect.Y - swatchSectionY - 14, 52));
            UiRect actionSectionRect = new(paletteBodyRect.X, addSwatchButton.Rect.Y - 24, paletteBodyRect.Width, addSwatchButton.Rect.Height + 32);
            DrawUiRect(activeSectionRect, paletteSectionColor);
            DrawUiRect(swatchSectionRect, paletteSectionColor);
            DrawUiRect(actionSectionRect, paletteSectionColor);
            DrawUiRect(layout.ActiveColorRect, _theme.Divider);
            DrawUiRect(new UiRect(layout.ActiveColorRect.X + 4, layout.ActiveColorRect.Y + 4, layout.ActiveColorRect.Width - 8, layout.ActiveColorRect.Height - 8), _uiState.PixelStudio.ActiveColor);
        }
        DrawUiRect(layout.PlaybackPreviewRect, _theme.TabInactive);

        foreach (ActionRect<PixelStudioToolKind> toolButton in layout.ToolButtons)
        {
            ThemeColor color = toolButton.Action == _uiState.PixelStudio.ActiveTool
                ? toolButton.Action == PixelStudioToolKind.Select
                    ? Blend(_theme.Accent, _theme.TabActive, 0.72f)
                    : _theme.TabActive
                : _theme.TabInactive;
            DrawUiRect(toolButton.Rect, color);
        }

        foreach (ActionRect<PixelStudioAction> button in layout.DocumentButtons)
        {
            DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
        }

        foreach (ActionRect<PixelStudioAction> button in layout.CanvasButtons)
        {
            DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
        }

        foreach (ActionRect<PixelStudioAction> button in layout.PaletteButtons)
        {
            DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
        }

        if (!sidebarCollapsed && layout.PaletteLibraryRect is not null)
        {
            DrawUiRect(layout.PaletteLibraryRect.Value, Blend(_theme.SidePanel, _theme.Workspace, 0.38f));
            foreach (ActionRect<PixelStudioAction> button in layout.PaletteLibraryButtons)
            {
                DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
            }

            if (layout.PaletteRenameFieldRect is not null)
            {
                DrawUiRect(layout.PaletteRenameFieldRect.Value, _theme.TabInactive);
            }

            if (layout.ContextMenuRect is not null)
            {
                DrawUiRect(layout.ContextMenuRect.Value, Blend(_theme.MenuBar, _theme.Workspace, 0.18f));
            }

            foreach (IndexedRect row in layout.SavedPaletteRows)
            {
                bool isDefaultPaletteRow = row.Index < 0;
                bool isSelectedSavedPalette = row.Index >= 0
                    && row.Index < _uiState.PixelStudio.SavedPalettes.Count
                    && _uiState.PixelStudio.SavedPalettes[row.Index].IsSelected;
                ThemeColor rowColor = isDefaultPaletteRow
                    ? Blend(_theme.TabInactive, _theme.Accent, 0.10f)
                    : isSelectedSavedPalette
                        ? _theme.TabActive
                        : _theme.TabInactive;
                DrawUiRect(row.Rect, rowColor);
            }
        }

        if (!sidebarCollapsed && layout.PalettePromptRect is not null)
        {
            DrawUiRect(layout.PalettePromptRect.Value, _theme.Workspace);
            foreach (ActionRect<PixelStudioAction> button in layout.PalettePromptButtons)
            {
                DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
            }
        }

        foreach (ActionRect<PixelStudioAction> button in layout.LayerButtons)
        {
            DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
        }

        if (layout.TimelinePanelRect.Height > 0)
        {
            foreach (ActionRect<PixelStudioAction> button in layout.TimelineButtons)
            {
                DrawUiRect(button.Rect, ResolvePixelActionColor(button.Action));
            }
        }

        for (int index = 0; index < layout.PaletteSwatches.Count && index < _uiState.PixelStudio.Palette.Count; index++)
        {
            int paletteIndex = layout.PaletteSwatches[index].Index;
            if (paletteIndex < 0 || paletteIndex >= _uiState.PixelStudio.Palette.Count)
            {
                continue;
            }

            UiRect rect = layout.PaletteSwatches[index].Rect;
            ThemeColor border = paletteIndex == _uiState.PixelStudio.ActivePaletteIndex ? _theme.Accent : _theme.Divider;
            DrawUiRect(rect, border);
            DrawUiRect(new UiRect(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6), _uiState.PixelStudio.Palette[paletteIndex]);
        }

        ThemeColor checkerLight = canvasCheckerLight;
        ThemeColor checkerDark = canvasCheckerDark;
        DrawUiRectClipped(layout.CanvasViewportRect, canvasSurfaceColor, layout.CanvasClipRect);

        int canvasWidth = Math.Max(_uiState.PixelStudio.CanvasWidth, 1);
        int canvasHeight = Math.Max(_uiState.PixelStudio.CanvasHeight, 1);
        int cellSize = Math.Max(layout.CanvasCellSize, 1);
        for (int y = 0; y < canvasHeight; y++)
        {
            for (int x = 0; x < canvasWidth; x++)
            {
                int index = (y * canvasWidth) + x;
                if (index >= _uiState.PixelStudio.CompositePixels.Count)
                {
                    break;
                }

                UiRect rect = new(
                    layout.CanvasViewportRect.X + (x * cellSize),
                    layout.CanvasViewportRect.Y + (y * cellSize),
                    cellSize,
                    cellSize);
                ThemeColor cellColor = _uiState.PixelStudio.CompositePixels[index]
                    ?? (((x + y) % 2 == 0) ? checkerLight : checkerDark);
                DrawUiRectClipped(rect, cellColor, layout.CanvasClipRect);

                if (_uiState.PixelStudio.ShowGrid && rect.Width >= 10 && rect.Height >= 10)
                {
                    DrawUiRectClipped(new UiRect(rect.X + rect.Width - 1, rect.Y, 1, rect.Height), _theme.Divider, layout.CanvasClipRect);
                    DrawUiRectClipped(new UiRect(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), _theme.Divider, layout.CanvasClipRect);
                }
            }
        }

        if (_uiState.PixelStudio.HasSelection)
        {
            UiRect selectionRect = new(
                layout.CanvasViewportRect.X + (_uiState.PixelStudio.SelectionX * cellSize),
                layout.CanvasViewportRect.Y + (_uiState.PixelStudio.SelectionY * cellSize),
                Math.Max(_uiState.PixelStudio.SelectionWidth * cellSize, 1),
                Math.Max(_uiState.PixelStudio.SelectionHeight * cellSize, 1));
            DrawUiRectClipped(new UiRect(selectionRect.X, selectionRect.Y, selectionRect.Width, 2), _theme.Accent, layout.CanvasClipRect);
            DrawUiRectClipped(new UiRect(selectionRect.X, selectionRect.Y + selectionRect.Height - 2, selectionRect.Width, 2), _theme.Accent, layout.CanvasClipRect);
            DrawUiRectClipped(new UiRect(selectionRect.X, selectionRect.Y, 2, selectionRect.Height), _theme.Accent, layout.CanvasClipRect);
            DrawUiRectClipped(new UiRect(selectionRect.X + selectionRect.Width - 2, selectionRect.Y, 2, selectionRect.Height), _theme.Accent, layout.CanvasClipRect);
        }

        if (toolSettingsVisible)
        {
            ThemeColor floatingPanelColor = new(0.06f, 0.06f, 0.07f, 0.94f);
            ThemeColor floatingPanelInsetColor = new(0.10f, 0.10f, 0.11f, 0.98f);
            DrawRoundedUiRect(layout.ToolSettingsPanelRect, floatingPanelColor, 14f);
            DrawRoundedUiRect(new UiRect(layout.ToolSettingsPanelRect.X + 1, layout.ToolSettingsPanelRect.Y + 1, Math.Max(layout.ToolSettingsPanelRect.Width - 2, 0), Math.Max(layout.ToolSettingsPanelRect.Height - 2, 0)), floatingPanelInsetColor, 13f);
            if (layout.BrushSizeSliderRect is not null)
            {
                UiRect sliderRect = layout.BrushSizeSliderRect.Value;
                UiRect sizeBadgeRect = new(
                    sliderRect.X - 6,
                    sliderRect.Y - 28,
                    sliderRect.Width + 12,
                    sliderRect.Width + 12);
                DrawRoundedUiRect(sizeBadgeRect, new ThemeColor(0.11f, 0.11f, 0.12f, 1.0f), sizeBadgeRect.Width * 0.5f);
                DrawRoundedUiRect(sliderRect, new ThemeColor(0.11f, 0.11f, 0.12f, 1.0f), sliderRect.Width * 0.5f);
                float brushRatio = Math.Clamp((_uiState.PixelStudio.BrushSize - 1) / 15f, 0f, 1f);
                float fillHeight = Math.Max(sliderRect.Height * brushRatio, 0f);
                if (fillHeight > 0.5f)
                {
                    UiRect liveFillRect = new(
                        sliderRect.X + 1,
                        sliderRect.Y + sliderRect.Height - fillHeight,
                        Math.Max(sliderRect.Width - 2, 1),
                        fillHeight);
                    DrawRoundedUiRect(liveFillRect, Blend(_theme.Accent, _theme.TabActive, 0.58f), Math.Max((sliderRect.Width - 2) * 0.5f, 1f));
                }
                float knobHeight = 12f;
                float knobTravel = Math.Max(sliderRect.Height - knobHeight, 0f);
                float knobY = sliderRect.Y + ((1f - brushRatio) * knobTravel);
                UiRect liveKnobRect = new(sliderRect.X - 3, knobY, sliderRect.Width + 6, knobHeight);
                DrawRoundedUiRect(liveKnobRect, Blend(_theme.Accent, _theme.TabActive, 0.50f), knobHeight * 0.5f);
            }

            if (layout.BrushPreviewRect is not null)
            {
                DrawRoundedUiRect(layout.BrushPreviewRect.Value, new ThemeColor(0.11f, 0.11f, 0.12f, 1.0f), layout.BrushPreviewRect.Value.Width * 0.5f);
                float dotSize = Math.Clamp(_uiState.PixelStudio.BrushSize * 2f, 6f, Math.Max(layout.BrushPreviewRect.Value.Width - 10, 6f));
                float dotX = layout.BrushPreviewRect.Value.X + ((layout.BrushPreviewRect.Value.Width - dotSize) * 0.5f);
                float dotY = layout.BrushPreviewRect.Value.Y + ((layout.BrushPreviewRect.Value.Height - dotSize) * 0.5f);
                DrawRoundedUiRect(new UiRect(dotX, dotY, dotSize, dotSize), _theme.Accent, Math.Max(dotSize * 0.22f, 2f));
            }
        }

        foreach (IndexedRect row in layout.LayerRows)
        {
            ThemeColor color = row.Index < _uiState.PixelStudio.Layers.Count && _uiState.PixelStudio.Layers[row.Index].IsActive
                ? _theme.TabActive
                : _theme.TabInactive;
            DrawUiRect(row.Rect, color);
        }

        foreach (IndexedRect button in layout.LayerVisibilityButtons)
        {
            bool visible = button.Index < _uiState.PixelStudio.Layers.Count && _uiState.PixelStudio.Layers[button.Index].IsVisible;
            DrawUiRect(button.Rect, visible ? _theme.TabActive : _theme.TabInactive);
        }

        if (layout.LayerRenameFieldRect is not null)
        {
            DrawUiRect(layout.LayerRenameFieldRect.Value, _theme.TabInactive);
        }

        if (layout.FrameRenameFieldRect is not null)
        {
            DrawUiRect(layout.FrameRenameFieldRect.Value, _theme.TabInactive);
        }

        foreach (IndexedRect frame in layout.FrameRows)
        {
            ThemeColor color = frame.Index < _uiState.PixelStudio.Frames.Count && _uiState.PixelStudio.Frames[frame.Index].IsPreviewing
                ? _theme.TabActive
                : _theme.TabInactive;
            DrawUiRect(frame.Rect, color);
        }

        DrawScrollRegion(layout.PaletteSwatchScrollTrackRect, layout.PaletteSwatchScrollThumbRect);
        DrawScrollRegion(layout.SavedPaletteScrollTrackRect, layout.SavedPaletteScrollThumbRect);
        DrawScrollRegion(layout.LayerScrollTrackRect, layout.LayerScrollThumbRect);
        DrawScrollRegion(layout.FrameScrollTrackRect, layout.FrameScrollThumbRect);

        if (layout.TimelinePanelRect.Height > 0)
        {
            DrawPixelPreview(layout.PlaybackPreviewRect, _uiState.PixelStudio.PreviewPixels, _uiState.PixelStudio.CanvasWidth, _uiState.PixelStudio.CanvasHeight);
        }
    }

    private void DrawPixelPanel(UiRect panelRect, ThemeColor bodyColor)
    {
        DrawUiRect(panelRect, bodyColor);
        UiRect headerRect = GetPixelPanelHeaderRect(panelRect);
        DrawUiRect(headerRect, Blend(bodyColor, _theme.MenuBar, 0.22f));
        DrawUiRect(new UiRect(panelRect.X, headerRect.Y + headerRect.Height - 1, panelRect.Width, 1), _theme.Divider);
    }

    private void DrawScrollRegion(UiRect? trackRect, UiRect? thumbRect)
    {
        if (trackRect is null || thumbRect is null)
        {
            return;
        }

        DrawUiRect(trackRect.Value, Blend(_theme.TabInactive, _theme.Background, 0.26f));
        DrawUiRect(thumbRect.Value, Blend(_theme.Accent, _theme.TabActive, 0.42f));
    }

    private void DrawProjectsPage()
    {
        if (_layoutSnapshot!.ProjectsFormPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.ProjectsFormPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.14f));
        }

        if (_layoutSnapshot.ProjectsRecentPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.ProjectsRecentPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.10f));
        }

        foreach (ActionRect<ProjectFormAction> action in _layoutSnapshot!.ProjectFormActions)
        {
            ThemeColor color = action.Action switch
            {
                ProjectFormAction.ActivateProjectName when _uiState.ProjectForm.ActiveField == EditorTextField.ProjectName => _theme.TabActive,
                ProjectFormAction.ActivateProjectLibraryPath when _uiState.ProjectForm.ActiveField == EditorTextField.ProjectLibraryPath => _theme.TabActive,
                _ => _theme.TabInactive
            };

            DrawUiRect(action.Rect, color);
        }

        foreach (IndexedRect row in _layoutSnapshot!.ProjectRows)
        {
            DrawUiRect(row.Rect, _theme.TabInactive);
        }

        DrawScrollRegion(_layoutSnapshot.ProjectsRecentScrollTrackRect, _layoutSnapshot.ProjectsRecentScrollThumbRect);

        if (_layoutSnapshot.FolderPickerRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.FolderPickerRect.Value, _theme.SidePanel);
            foreach (ActionRect<EditorFolderPickerAction> action in _layoutSnapshot.FolderPickerActions)
            {
                DrawUiRect(action.Rect, _theme.TabInactive);
            }

            foreach (IndexedRect row in _layoutSnapshot.FolderPickerRows)
            {
                DrawUiRect(row.Rect, _theme.TabInactive);
            }

            DrawScrollRegion(_layoutSnapshot.FolderPickerScrollTrackRect, _layoutSnapshot.FolderPickerScrollThumbRect);
        }
    }

    private void DrawPreferencesPage()
    {
        if (_layoutSnapshot!.PreferencesGeneralPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.PreferencesGeneralPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.14f));
        }

        if (_layoutSnapshot.PreferencesShortcutPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.PreferencesShortcutPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.10f));
        }

        foreach (ActionRect<EditorPreferenceAction> action in _layoutSnapshot!.PreferenceActions)
        {
            DrawUiRect(action.Rect, _theme.TabInactive);
        }

        foreach (IndexedRect row in _layoutSnapshot!.PreferenceRows)
        {
            ThemeColor color = row.Index == _uiState.SelectedShortcutIndex ? _theme.TabActive : _theme.TabInactive;
            DrawUiRect(row.Rect, color);
        }

        DrawScrollRegion(_layoutSnapshot.PreferenceScrollTrackRect, _layoutSnapshot.PreferenceScrollThumbRect);
    }

    private void DrawLayoutPage()
    {
        if (_layoutSnapshot!.LayoutInfoPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.LayoutInfoPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.12f));
        }
    }

    private void DrawScratchPage()
    {
        if (_layoutSnapshot!.ScratchInfoPanelRect is not null)
        {
            DrawPixelPanel(_layoutSnapshot.ScratchInfoPanelRect.Value, Blend(_theme.TabInactive, _theme.Workspace, 0.12f));
        }
    }

    private void DrawMenuLogo()
    {
        if (_layoutSnapshot is null)
        {
            return;
        }

        UiRect logoRect = _layoutSnapshot.MenuLogoRect;
        if (File.Exists(_brandingImagePath))
        {
            _imageRenderer.DrawImage(_brandingImagePath, logoRect.X + 2, logoRect.Y + 1, 110, Math.Max(logoRect.Height - 2, 36));
            return;
        }

        DrawUiRect(new UiRect(logoRect.X + 8, logoRect.Y + 8, 24, 24), _theme.Accent);
        DrawUiRect(new UiRect(logoRect.X + 15, logoRect.Y + 15, 10, 10), _theme.Background);
        DrawUiRect(new UiRect(logoRect.X + 20, logoRect.Y + 20, 15, 15), _theme.Accent);
    }

    private void DrawMenuDropdown()
    {
        if (_layoutSnapshot!.MenuDropdownRect is null)
        {
            return;
        }

        DrawUiRect(_layoutSnapshot.MenuDropdownRect.Value, _theme.SidePanel);
        foreach (ActionRect<EditorMenuAction> entry in _layoutSnapshot.MenuEntries)
        {
            DrawUiRect(entry.Rect, _theme.TabInactive);
        }
    }

    private void DrawShellText()
    {
        Font menuFont = ResolveFont(_typography.MenuText.Size);
        Font bodyFont = ResolveFont(_typography.BodyText.Size);
        Font titleFont = ResolveFont(_typography.PanelTitleText.Size);
        Font statusFont = ResolveFont(_typography.StatusText.Size);

        SixLabors.ImageSharp.Color bodyText = ToImageSharpColor(_typography.BodyText.Color);
        SixLabors.ImageSharp.Color statusText = ToImageSharpColor(_typography.StatusText.Color);
        SixLabors.ImageSharp.Color topBarText = ToImageSharpColor(ResolveTopBarTextColor());
        SixLabors.ImageSharp.Color brandingText = topBarText;

        foreach (NamedRect button in _layoutSnapshot!.MenuButtons)
        {
            DrawCenteredTextInRect(button.Id, menuFont, topBarText, button.Rect, 8, 4);
        }

        Font brandingTitleFont = ResolveFont(_typography.PanelTitleText.Size + 2f);
        Font brandingBodyFont = ResolveFont(_typography.BodyText.Size + 1f);
        float logoMidY = _layoutSnapshot.MenuLogoRect.Y + (_layoutSnapshot.MenuLogoRect.Height * 0.5f);
        DrawTextClipped(new UiRect(_layoutSnapshot.MenuLogoRect.X, _layoutSnapshot.MenuLogoRect.Y, _layoutSnapshot.MenuLogoRect.Width, Math.Max(logoMidY - _layoutSnapshot.MenuLogoRect.Y + 1, 0)), "Kuma", brandingTitleFont, brandingText, 88, 3);
        DrawTextClipped(new UiRect(_layoutSnapshot.MenuLogoRect.X, logoMidY - 1, _layoutSnapshot.MenuLogoRect.Width, Math.Max((_layoutSnapshot.MenuLogoRect.Y + _layoutSnapshot.MenuLogoRect.Height) - logoMidY + 1, 0)), "Engine", brandingBodyFont, brandingText, 88, 2);

        foreach (NamedRect tab in _layoutSnapshot.TabButtons)
        {
            string title = _uiState.Tabs.FirstOrDefault(item => item.Id == tab.Id)?.Title ?? tab.Id;
            DrawCenteredTextClippedInRect(title, bodyFont, bodyText, tab.Rect, 5, 4);
        }

        foreach (NamedRect close in _layoutSnapshot.TabCloseButtons)
        {
            DrawCenteredTextInRect("x", bodyFont, bodyText, close.Rect, 2, 0);
        }

        DrawSidePanelText(titleFont, bodyFont, statusFont, bodyText);
        DrawWorkspaceText(titleFont, bodyFont, statusFont, bodyText, statusText);
        DrawTextInRect(_uiState.StatusText, statusFont, statusText, _layoutSnapshot.StatusBarRect, 18, 6);
    }

    private void DrawMenuDropdownText()
    {
        if (_layoutSnapshot?.MenuDropdownRect is null)
        {
            return;
        }

        Font bodyFont = ResolveFont(_typography.BodyText.Size);
        SixLabors.ImageSharp.Color bodyText = ToImageSharpColor(_typography.BodyText.Color);

        for (int index = 0; index < _layoutSnapshot.MenuEntries.Count; index++)
        {
            string label = GetMenuLabel(_layoutSnapshot.MenuEntries[index].Action);
            UiRect rect = _layoutSnapshot.MenuEntries[index].Rect;
            DrawTextInRect(label, bodyFont, bodyText, rect, 10, 5);
        }
    }

    private void DrawSidePanelText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText)
    {
        DrawCenteredTextInRect(IsCollapsedPanel(_layoutSnapshot!.LeftPanelRect) ? ">" : "<", bodyFont, bodyText, _layoutSnapshot.LeftCollapseHandleRect, 2, 2);
        DrawCenteredTextInRect(IsCollapsedPanel(_layoutSnapshot.RightPanelRect) ? "<" : ">", bodyFont, bodyText, _layoutSnapshot.RightCollapseHandleRect, 2, 2);

        EditorPageKind currentPage = _uiState.Tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;
        if (currentPage == EditorPageKind.PixelStudio)
        {
            DrawPixelStudioPanelText(titleFont, bodyFont, statusFont, bodyText);
            return;
        }

        UiRect leftPanel = _layoutSnapshot!.LeftPanelRect;
        if (IsCollapsedPanel(leftPanel))
        {
            return;
        }
        else
        {
            UiRect leftHeaderTitleRect = GetHeaderTitleRect(_layoutSnapshot.LeftPanelHeaderRect, _layoutSnapshot.LeftCollapseHandleRect);
            DrawTextInRect("Projects", titleFont, bodyText, leftHeaderTitleRect, 0, 7);
            UiRect leftBody = _layoutSnapshot.LeftPanelBodyRect;
            DrawTextInRect("Project Library", statusFont, bodyText, new UiRect(leftBody.X, leftBody.Y + 6, leftBody.Width, 18), 0, 0);
            DrawTextClippedInRect(_uiState.ProjectLibraryPath, bodyFont, bodyText, new UiRect(leftBody.X, leftBody.Y + 28, leftBody.Width, 20), 0, 0);
            if (leftBody.Width >= 180)
            {
                DrawTextInRect("Recent Projects", statusFont, bodyText, new UiRect(leftBody.X, leftBody.Y + 84, leftBody.Width, 18), 0, 0);
            }

            if (_layoutSnapshot.LeftPanelRecentProjectRows.Count == 0)
            {
                DrawTextInRect("No recent projects yet.", statusFont, bodyText, new UiRect(leftBody.X, leftBody.Y + 116, leftBody.Width, 18), 0, 0);
            }
            else
            {
                foreach (IndexedRect row in _layoutSnapshot.LeftPanelRecentProjectRows)
                {
                    if (row.Index < 0 || row.Index >= _uiState.RecentProjects.Count)
                    {
                        continue;
                    }

                    RecentProjectEntry project = _uiState.RecentProjects[row.Index];
                    DrawTextInRect(project.Name, bodyFont, bodyText, row.Rect, 10, 7);
                    DrawTextClippedInRect(project.Path, statusFont, bodyText, new UiRect(row.Rect.X + 10, row.Rect.Y + 24, Math.Max(row.Rect.Width - 20, 0), 16), 0, 0);
                }
            }

            if (File.Exists(_brandingImagePath))
            {
                UiRect workspaceLogoRect = GetWorkspaceLogoRect(leftPanel);
                _imageRenderer.DrawImage(_brandingImagePath, workspaceLogoRect.X, workspaceLogoRect.Y, workspaceLogoRect.Width, workspaceLogoRect.Height);
            }
        }

        UiRect rightPanel = _layoutSnapshot.RightPanelRect;
        if (IsCollapsedPanel(rightPanel))
        {
            return;
        }

        UiRect rightHeaderTitleRect = GetLeadingHeaderTitleRect(_layoutSnapshot.RightPanelHeaderRect, _layoutSnapshot.RightCollapseHandleRect);
        DrawTextInRect("Inspector", titleFont, bodyText, rightHeaderTitleRect, 0, 7);
        UiRect rightBody = _layoutSnapshot.RightPanelBodyRect;
        DrawTextInRect("Selected View", statusFont, bodyText, new UiRect(rightBody.X, rightBody.Y, rightBody.Width, 18), 0, 0);
        string currentTab = _uiState.Tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Title ?? "Home";
        DrawTextInRect(currentTab, bodyFont, bodyText, new UiRect(rightBody.X, rightBody.Y + 22, rightBody.Width, 20), 0, 0);
        if (rightPanel.Width >= 180)
        {
            DrawTextInRect("Workspace details will appear here", statusFont, bodyText, new UiRect(rightBody.X, rightBody.Y + 66, rightBody.Width, 18), 0, 0);
            DrawTextInRect("as scene tools, assets, and editors", statusFont, bodyText, new UiRect(rightBody.X, rightBody.Y + 84, rightBody.Width, 18), 0, 0);
            DrawTextInRect("gain richer selections.", statusFont, bodyText, new UiRect(rightBody.X, rightBody.Y + 102, rightBody.Width, 18), 0, 0);
        }
    }

    private void DrawWorkspaceText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText, SixLabors.ImageSharp.Color statusText)
    {
        EditorPageKind currentPage = _uiState.Tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;

        switch (currentPage)
        {
            case EditorPageKind.Home:
                DrawHomeText(titleFont, bodyFont, statusFont, bodyText);
                break;
            case EditorPageKind.PixelStudio:
                DrawPixelStudioText(titleFont, bodyFont, statusFont, bodyText, statusText);
                break;
            case EditorPageKind.Projects:
                DrawProjectsText(titleFont, bodyFont, statusFont, bodyText);
                break;
            case EditorPageKind.Preferences:
                DrawPreferencesText(titleFont, bodyFont, statusFont, bodyText);
                break;
            case EditorPageKind.Layout:
                if (_layoutSnapshot!.LayoutInfoPanelRect is not null)
                {
                    DrawTextInRect("Workspace Layout", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.LayoutInfoPanelRect.Value), 12, 7);
                    UiRect layoutBody = GetPixelPanelBodyRect(_layoutSnapshot.LayoutInfoPanelRect.Value);
                    DrawTextInRect("This page is the shared shell testbed for docking, resizing, collapse behavior, and saved editor layouts.", bodyFont, bodyText, new UiRect(layoutBody.X, layoutBody.Y + 8, layoutBody.Width, 22), 0, 0);
                    DrawTextInRect("It now follows the same panel, clipping, and adaptive sizing system as the rest of the app.", bodyFont, bodyText, new UiRect(layoutBody.X, layoutBody.Y + 38, layoutBody.Width, 22), 0, 0);
                    DrawTextInRect("Next here: drag resizing persistence refinement, panel reset actions, and future docking presets.", statusFont, statusText, new UiRect(layoutBody.X, layoutBody.Y + 76, layoutBody.Width, 20), 0, 0);
                }
                break;
            case EditorPageKind.Scratch:
                if (_layoutSnapshot!.ScratchInfoPanelRect is not null)
                {
                    DrawTextInRect("Scratch Tab", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.ScratchInfoPanelRect.Value), 12, 7);
                    UiRect scratchBody = GetPixelPanelBodyRect(_layoutSnapshot.ScratchInfoPanelRect.Value);
                    DrawTextInRect("A lightweight extra document tab for testing future editor workflows and layout ideas.", bodyFont, bodyText, new UiRect(scratchBody.X, scratchBody.Y + 8, scratchBody.Width, 22), 0, 0);
                    DrawTextInRect("It benefits from the same shared panel and text rules as the main pages now, so it can grow safely later.", bodyFont, bodyText, new UiRect(scratchBody.X, scratchBody.Y + 38, scratchBody.Width, 22), 0, 0);
                    DrawTextInRect("Scratch tabs can still be closed from the small x on the tab strip.", statusFont, statusText, new UiRect(scratchBody.X, scratchBody.Y + 76, scratchBody.Width, 20), 0, 0);
                }
                break;
        }
    }

    private void DrawHomeText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText)
    {
        if (_layoutSnapshot!.HomeHeroPanelRect is not null)
        {
            DrawTextInRect("Home", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.HomeHeroPanelRect.Value), 12, 7);
            UiRect heroBody = GetPixelPanelBodyRect(_layoutSnapshot.HomeHeroPanelRect.Value);
            DrawTextInRect($"Start a project, jump into {EditorBranding.PixelToolName}, or tune the editor before the larger engine tools arrive.", bodyFont, bodyText, new UiRect(heroBody.X, heroBody.Y + 8, heroBody.Width, 22), 0, 0);
            DrawTextInRect("Everything on this page now flows through the shared panel and scrolling system used across the app.", statusFont, bodyText, new UiRect(heroBody.X, heroBody.Y + 40, heroBody.Width, 20), 0, 0);
        }

        if (_layoutSnapshot.HomeActionsPanelRect is not null)
        {
            DrawTextInRect("Quick Actions", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.HomeActionsPanelRect.Value), 12, 7);
        }

        IReadOnlyList<(string Title, string Desc)> cardText =
        [
            ("New Project Slot", "Create a basic project folder in your library."),
            (EditorBranding.PixelToolName, "Open the sprite and animation workspace."),
            ("Projects", "View recent projects and your library path."),
            ("Preferences", "Edit theme, text, and shortcuts.")
        ];

        for (int index = 0; index < _layoutSnapshot.HomeCards.Count && index < cardText.Count; index++)
        {
            UiRect rect = _layoutSnapshot.HomeCards[index].Rect;
            DrawTextInRect(cardText[index].Title, titleFont, bodyText, rect, 14, 14);
            DrawTextInRect(cardText[index].Desc, statusFont, bodyText, rect, 14, 48);
            DrawTextInRect("Click to open", statusFont, bodyText, rect, 14, 76);
        }

        if (_layoutSnapshot.HomeRecentPanelRect is not null)
        {
            DrawTextInRect("Recent Projects", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.HomeRecentPanelRect.Value), 12, 7);
        }

        if (_uiState.RecentProjects.Count == 0)
        {
            if (_layoutSnapshot.HomeRecentPanelRect is not null)
            {
                UiRect recentBody = GetPixelPanelBodyRect(_layoutSnapshot.HomeRecentPanelRect.Value);
                DrawTextInRect("No projects yet. Use New Project Slot to create your first folder.", bodyFont, bodyText, new UiRect(recentBody.X, recentBody.Y + 8, recentBody.Width, 22), 0, 0);
            }

            return;
        }

        for (int index = 0; index < _layoutSnapshot.RecentProjectRows.Count && index < _uiState.RecentProjects.Count; index++)
        {
            IndexedRect row = _layoutSnapshot.RecentProjectRows[index];
            if (row.Index < 0 || row.Index >= _uiState.RecentProjects.Count)
            {
                continue;
            }

            RecentProjectEntry project = _uiState.RecentProjects[row.Index];
            UiRect rect = _layoutSnapshot.RecentProjectRows[index].Rect;
            DrawTextInRect(project.Name, bodyFont, bodyText, rect, 12, 10);
            DrawTextInRect(TrimPath(project.Path, 54), statusFont, bodyText, rect, 12, 24);
        }
    }

    private void DrawProjectsText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText)
    {
        if (_layoutSnapshot!.ProjectsFormPanelRect is not null)
        {
            DrawTextInRect("Project Setup", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.ProjectsFormPanelRect.Value), 12, 9);
            UiRect formBody = GetPixelPanelBodyRect(_layoutSnapshot.ProjectsFormPanelRect.Value);
            DrawTextInRect("Name a project, choose where it should live, and create the first folder structure from here.", statusFont, bodyText, new UiRect(formBody.X, formBody.Y + 8, formBody.Width, 20), 0, 0);
        }

        ActionRect<ProjectFormAction> nameField = _layoutSnapshot.ProjectFormActions.First(action => action.Action == ProjectFormAction.ActivateProjectName);
        ActionRect<ProjectFormAction> pathField = _layoutSnapshot.ProjectFormActions.First(action => action.Action == ProjectFormAction.ActivateProjectLibraryPath);
        ActionRect<ProjectFormAction> createButton = _layoutSnapshot.ProjectFormActions.First(action => action.Action == ProjectFormAction.CreateProject);
        ActionRect<ProjectFormAction> documentsButton = _layoutSnapshot.ProjectFormActions.First(action => action.Action == ProjectFormAction.UseDocumentsFolder);
        ActionRect<ProjectFormAction> desktopButton = _layoutSnapshot.ProjectFormActions.First(action => action.Action == ProjectFormAction.UseDesktopFolder);
        ActionRect<ProjectFormAction> browseButton = _layoutSnapshot.ProjectFormActions.First(action => action.Action == ProjectFormAction.OpenFolderPicker);

        DrawTextInRect("Project Name", statusFont, bodyText, new UiRect(nameField.Rect.X, nameField.Rect.Y - 16, nameField.Rect.Width, 18), 0, 0);
        DrawTextInRect(
            string.IsNullOrWhiteSpace(_uiState.ProjectForm.ProjectName) ? "MyGame" : _uiState.ProjectForm.ProjectName,
            bodyFont,
            bodyText,
            nameField.Rect,
            10,
            11);

        DrawTextInRect("Project Library", statusFont, bodyText, new UiRect(pathField.Rect.X, pathField.Rect.Y - 16, pathField.Rect.Width, 18), 0, 0);
        DrawTextInRect(
            TrimPath(string.IsNullOrWhiteSpace(_uiState.ProjectForm.ProjectLibraryPath) ? _uiState.ProjectLibraryPath : _uiState.ProjectForm.ProjectLibraryPath, 72),
            bodyFont,
            bodyText,
            pathField.Rect,
            10,
            11);

        DrawCenteredTextInRect("Create Project", bodyFont, bodyText, createButton.Rect, 12, 8);
        DrawCenteredTextInRect("Use Documents", bodyFont, bodyText, documentsButton.Rect, 12, 8);
        DrawCenteredTextInRect("Use Desktop", bodyFont, bodyText, desktopButton.Rect, 12, 8);
        DrawCenteredTextInRect("Browse Folder", bodyFont, bodyText, browseButton.Rect, 12, 8);

        if (_layoutSnapshot.ProjectsRecentPanelRect is not null)
        {
            DrawTextInRect("Recent Projects", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.ProjectsRecentPanelRect.Value), 12, 7);
        }

        if (_uiState.RecentProjects.Count == 0)
        {
            if (_layoutSnapshot.ProjectsRecentPanelRect is not null)
            {
                UiRect recentBody = GetPixelPanelBodyRect(_layoutSnapshot.ProjectsRecentPanelRect.Value);
                DrawTextInRect("No recent projects yet. Use the form above to create the first one.", bodyFont, bodyText, new UiRect(recentBody.X, recentBody.Y + 8, recentBody.Width, 22), 0, 0);
            }
        }
        else
        {
            for (int index = 0; index < _layoutSnapshot.ProjectRows.Count && index < _uiState.RecentProjects.Count; index++)
            {
                IndexedRect row = _layoutSnapshot.ProjectRows[index];
                if (row.Index < 0 || row.Index >= _uiState.RecentProjects.Count)
                {
                    continue;
                }

                RecentProjectEntry project = _uiState.RecentProjects[row.Index];
                UiRect rect = _layoutSnapshot.ProjectRows[index].Rect;
                DrawTextInRect(project.Name, bodyFont, bodyText, rect, 12, 10);
                DrawTextInRect(TrimPath(project.Path, 58), statusFont, bodyText, rect, 12, 24);
            }
        }

        if (_layoutSnapshot.FolderPickerRect is not null)
        {
            UiRect picker = _layoutSnapshot.FolderPickerRect.Value;
            DrawTextInRect("Folder Picker", titleFont, bodyText, GetPixelPanelHeaderRect(picker), 12, 7);
            UiRect pickerBody = GetPixelPanelBodyRect(picker);
            DrawTextInRect("Choose a folder for the project library.", statusFont, bodyText, new UiRect(pickerBody.X, pickerBody.Y + 40, pickerBody.Width, 18), 0, 0);
            DrawTextInRect(TrimPath(_uiState.ProjectForm.FolderPickerPath, 40), statusFont, bodyText, new UiRect(pickerBody.X, pickerBody.Y + 56, pickerBody.Width, 18), 0, 0);

            ActionRect<EditorFolderPickerAction> upButton = _layoutSnapshot.FolderPickerActions.First(action => action.Action == EditorFolderPickerAction.NavigateUp);
            ActionRect<EditorFolderPickerAction> selectButton = _layoutSnapshot.FolderPickerActions.First(action => action.Action == EditorFolderPickerAction.SelectCurrent);
            DrawCenteredTextInRect("Up", bodyFont, bodyText, upButton.Rect, 8, 6);
            DrawCenteredTextInRect("Use This Folder", statusFont, bodyText, selectButton.Rect, 8, 6);

            for (int index = 0; index < _layoutSnapshot.FolderPickerRows.Count && index < _uiState.ProjectForm.FolderPickerEntries.Count; index++)
            {
                IndexedRect row = _layoutSnapshot.FolderPickerRows[index];
                if (row.Index < 0 || row.Index >= _uiState.ProjectForm.FolderPickerEntries.Count)
                {
                    continue;
                }

                string folder = _uiState.ProjectForm.FolderPickerEntries[row.Index];
                UiRect rect = _layoutSnapshot.FolderPickerRows[index].Rect;
                DrawTextInRect(Path.GetFileName(folder), bodyFont, bodyText, rect, 8, 5);
            }
        }
    }

    private void DrawPreferencesText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText)
    {
        if (_layoutSnapshot!.PreferencesGeneralPanelRect is not null)
        {
            DrawTextInRect("Appearance", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.PreferencesGeneralPanelRect.Value), 12, 7);
            UiRect generalBody = GetPixelPanelBodyRect(_layoutSnapshot.PreferencesGeneralPanelRect.Value);
            DrawTextInRect("Theme, typography, and general editor polish live here.", statusFont, bodyText, new UiRect(generalBody.X, generalBody.Y, generalBody.Width, 18), 0, 0);
            DrawTextInRect("These settings persist when you close and reopen the editor.", statusFont, bodyText, new UiRect(generalBody.X, generalBody.Y + 18, generalBody.Width, 18), 0, 0);
        }

        ActionRect<EditorPreferenceAction> themeButton = _layoutSnapshot.PreferenceActions.First(action => action.Action == EditorPreferenceAction.ToggleTheme);
        ActionRect<EditorPreferenceAction> sizeButton = _layoutSnapshot.PreferenceActions.First(action => action.Action == EditorPreferenceAction.CycleFontSize);
        ActionRect<EditorPreferenceAction> fontButton = _layoutSnapshot.PreferenceActions.First(action => action.Action == EditorPreferenceAction.CycleFontFamily);
        DrawCenteredTextInRect($"Theme: {_uiState.ThemeLabel}", bodyFont, bodyText, themeButton.Rect, 12, 8);
        DrawCenteredTextInRect($"Text: {_uiState.FontSizeLabel}", bodyFont, bodyText, sizeButton.Rect, 12, 8);
        DrawCenteredTextInRect($"Font: {TrimPath(_uiState.FontFamily, 16)}", bodyFont, bodyText, fontButton.Rect, 12, 8);
        if (_layoutSnapshot.PreferencesGeneralPanelRect is not null)
        {
            UiRect generalBody = GetPixelPanelBodyRect(_layoutSnapshot.PreferencesGeneralPanelRect.Value);
            DrawTextInRect($"Aa Bb Cc 123 - {EditorBranding.EngineName} Sample", bodyFont, bodyText, new UiRect(generalBody.X, generalBody.Y + 92, generalBody.Width, 20), 0, 0);
        }

        if (_layoutSnapshot.PreferencesShortcutPanelRect is not null)
        {
            DrawTextInRect("Shortcuts", titleFont, bodyText, GetPixelPanelHeaderRect(_layoutSnapshot.PreferencesShortcutPanelRect.Value), 12, 7);
            UiRect shortcutBody = GetPixelPanelBodyRect(_layoutSnapshot.PreferencesShortcutPanelRect.Value);
            DrawTextInRect("Click a row to select it. Click again or press Enter to rebind.", statusFont, bodyText, new UiRect(shortcutBody.X, shortcutBody.Y, shortcutBody.Width, 18), 0, 0);
        }

        for (int index = 0; index < _layoutSnapshot.PreferenceRows.Count && index < _uiState.Shortcuts.Count; index++)
        {
            IndexedRect row = _layoutSnapshot.PreferenceRows[index];
            if (row.Index < 0 || row.Index >= _uiState.Shortcuts.Count)
            {
                continue;
            }

            EditorShortcutBinding binding = _uiState.Shortcuts[row.Index];
            UiRect rect = row.Rect;
            string keyLabel = row.Index == _uiState.SelectedShortcutIndex && _uiState.AwaitingShortcutKey
                ? "Press key..."
                : binding.Key.ToString();

            DrawTextInRect($"{binding.Label} [{keyLabel}]", bodyFont, bodyText, rect, 12, 10);
            DrawTextInRect(binding.Description, statusFont, bodyText, rect, 12, 24);
        }
    }

    private void DrawPixelStudioText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText, SixLabors.ImageSharp.Color statusText)
    {
        if (_layoutSnapshot!.PixelStudio is null)
        {
            return;
        }

        PixelStudioLayoutSnapshot layout = _layoutSnapshot.PixelStudio;
        PixelStudioViewState pixelStudio = _uiState.PixelStudio;
        UiRect toolsHeaderRect = GetPixelPanelHeaderRect(layout.ToolbarRect);
        UiRect canvasHeaderRect = GetPixelPanelHeaderRect(layout.CanvasPanelRect);
        UiRect canvasBodyRect = GetPixelPanelBodyRect(layout.CanvasPanelRect);
        UiRect toolSettingsBodyRect = layout.ToolSettingsPanelRect;
        UiRect paletteHeaderRect = GetPixelPanelHeaderRect(layout.PalettePanelRect);
        UiRect paletteBodyRect = GetPixelPanelBodyRect(layout.PalettePanelRect);
        UiRect layersHeaderRect = GetPixelPanelHeaderRect(layout.LayersPanelRect);
        UiRect layersBodyRect = GetPixelPanelBodyRect(layout.LayersPanelRect);
        UiRect framesHeaderRect = GetPixelPanelHeaderRect(layout.TimelinePanelRect);
        UiRect canvasHeaderControlsRect = GetUnionRect(layout.CanvasButtons.Select(button => button.Rect));
        UiRect canvasTitleRect = GetHeaderTitleRect(canvasHeaderRect, canvasHeaderControlsRect);
        UiRect framesAccessoryRect = new(framesHeaderRect.X + Math.Max(framesHeaderRect.Width - 84, 0), framesHeaderRect.Y, Math.Min(72, Math.Max(framesHeaderRect.Width - 24, 0)), framesHeaderRect.Height);
        UiRect framesTitleRect = GetHeaderTitleRect(framesHeaderRect, framesAccessoryRect);
        bool toolsCollapsed = IsCollapsedPanel(layout.ToolbarRect);
        bool sidebarCollapsed = IsCollapsedPanel(layout.PalettePanelRect);
        bool toolSettingsVisible = !IsCollapsedPanel(layout.ToolSettingsPanelRect);
        bool timelineVisible = layout.TimelinePanelRect.Height > 0;

        DrawTextInRect(EditorBranding.PixelToolName, titleFont, bodyText, new UiRect(layout.HeaderRect.X + 14, layout.HeaderRect.Y, 164, layout.HeaderRect.Height), 0, 6);
        DrawTextInRect(
            $"{pixelStudio.DocumentName} - {pixelStudio.CanvasWidth}x{pixelStudio.CanvasHeight} - {pixelStudio.Frames.Count} frame(s)",
            statusFont,
            statusText,
            new UiRect(layout.HeaderRect.X + 186, layout.HeaderRect.Y, Math.Max(layout.HeaderRect.Width - 200, 0), layout.HeaderRect.Height),
            0,
            11);

        foreach (ActionRect<PixelStudioAction> button in layout.DocumentButtons)
        {
            DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 8, 6);
        }

        UiRect toolsTitleRect = GetHeaderTitleRect(toolsHeaderRect, layout.LeftCollapseHandleRect);
        DrawTextInRect(toolsCollapsed ? "T" : "Tools", titleFont, bodyText, toolsTitleRect, 0, 7);
        DrawCenteredTextInRect(toolsCollapsed ? ">" : "<", bodyFont, bodyText, layout.LeftCollapseHandleRect, 2, 2);
        DrawCenteredTextInRect(sidebarCollapsed ? "<" : ">", bodyFont, bodyText, layout.RightCollapseHandleRect, 2, 2);
        foreach (ActionRect<PixelStudioToolKind> toolButton in layout.ToolButtons)
        {
            DrawCenteredTextClippedInRect(GetPixelToolLabel(toolButton.Action), bodyFont, bodyText, toolButton.Rect, 8, 8);
        }

        DrawTextInRect("Canvas", titleFont, bodyText, canvasTitleRect, 0, 7);
        foreach (ActionRect<PixelStudioAction> button in layout.CanvasButtons)
        {
            DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 6, 6);
        }
        if (canvasBodyRect.Width >= 260)
        {
            DrawTextInRect("Ctrl+Z undo, Ctrl+Y redo, Delete clears selection.", statusFont, statusText, new UiRect(canvasBodyRect.X, canvasBodyRect.Y, canvasBodyRect.Width, 18), 0, 0);
        }

        if (toolSettingsVisible)
        {
            if (layout.BrushSizeSliderRect is not null)
            {
                UiRect sliderRect = layout.BrushSizeSliderRect.Value;
                UiRect sizeBadgeRect = new(
                    sliderRect.X - 6,
                    sliderRect.Y - 28,
                    sliderRect.Width + 12,
                    sliderRect.Width + 12);
                DrawCenteredTextClippedInRect(pixelStudio.BrushSize.ToString(), statusFont, statusText, sizeBadgeRect, 0, 0);
            }
        }

        UiRect paletteTitleRect = GetLeadingHeaderTitleRect(paletteHeaderRect, layout.RightCollapseHandleRect);
        DrawTextInRect(sidebarCollapsed ? "C" : "Colors", titleFont, bodyText, paletteTitleRect, 0, 7);
        if (!sidebarCollapsed && layout.PaletteButtons.Count > 0)
        {
            float infoX = layout.ActiveColorRect.X + layout.ActiveColorRect.Width + 14;
            float infoWidth = (paletteBodyRect.X + paletteBodyRect.Width) - infoX;
            UiRect activeInfoRect = new(infoX, layout.ActiveColorRect.Y, infoWidth, layout.ActiveColorRect.Height);
            bool showPaletteLabels = paletteBodyRect.Width >= 220;

            if (showPaletteLabels)
            {
                DrawTextInRect("Active Color", statusFont, statusText, new UiRect(paletteBodyRect.X, paletteBodyRect.Y, paletteBodyRect.Width, 18), 0, 0);
            }
            DrawTextInRect(pixelStudio.ActivePaletteName, statusFont, statusText, new UiRect(activeInfoRect.X, activeInfoRect.Y, activeInfoRect.Width, 18), 0, 0);
            DrawTextInRect(pixelStudio.ActiveColorHex, bodyFont, bodyText, new UiRect(activeInfoRect.X, activeInfoRect.Y + 24, activeInfoRect.Width, 20), 0, 0);
            if (showPaletteLabels)
            {
                DrawTextInRect("Colors", statusFont, statusText, new UiRect(paletteBodyRect.X, layout.ActiveColorRect.Y + layout.ActiveColorRect.Height + 10, paletteBodyRect.Width, 18), 0, 0);
            }
            foreach (ActionRect<PixelStudioAction> button in layout.PaletteButtons)
            {
                DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 6, 6);
            }
        }

        if (!sidebarCollapsed && layout.PaletteLibraryRect is not null)
        {
            if (layout.PaletteLibraryRect.Value.Width >= 160)
            {
                DrawTextInRect("Saved Palettes", statusFont, statusText, new UiRect(layout.PaletteLibraryRect.Value.X + 10, layout.PaletteLibraryRect.Value.Y + 8, layout.PaletteLibraryRect.Value.Width - 20, 18), 0, 0);
            }
            foreach (ActionRect<PixelStudioAction> button in layout.PaletteLibraryButtons)
            {
                DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 6, 6);
            }

            if (layout.PaletteRenameFieldRect is not null)
            {
                string renameText = string.IsNullOrWhiteSpace(pixelStudio.PaletteRenameBuffer) ? "Type a new palette name..." : pixelStudio.PaletteRenameBuffer;
                DrawTextInRect(renameText, bodyFont, bodyText, layout.PaletteRenameFieldRect.Value, 8, 7);
            }

            for (int visibleIndex = 0; visibleIndex < layout.SavedPaletteRows.Count; visibleIndex++)
            {
                IndexedRect row = layout.SavedPaletteRows[visibleIndex];
                UiRect rowRect = row.Rect;
                if (row.Index < 0)
                {
                    DrawTextInRect("Default Palette", bodyFont, bodyText, rowRect, 8, 6);
                    DrawTextInRect("Built-in", statusFont, statusText, new UiRect(rowRect.X + Math.Max(rowRect.Width - 58, 0), rowRect.Y, 50, rowRect.Height), 0, 6);
                    continue;
                }

                PixelStudioSavedPaletteView palette = pixelStudio.SavedPalettes[row.Index];
                DrawTextInRect(palette.Name, bodyFont, bodyText, rowRect, 8, 6);

                float previewX = rowRect.X + rowRect.Width - 58;
                for (int colorIndex = 0; colorIndex < Math.Min(palette.PreviewColors.Count, 4); colorIndex++)
                {
                    DrawUiRect(new UiRect(previewX + (colorIndex * 12), rowRect.Y + 7, 10, 14), palette.PreviewColors[colorIndex]);
                }
            }
        }

        if (layout.ContextMenuRect is not null)
        {
            foreach (ActionRect<PixelStudioContextMenuAction> button in layout.ContextMenuButtons)
            {
                bool isDestructive = button.Action is PixelStudioContextMenuAction.DeleteLayer or PixelStudioContextMenuAction.DeletePalette or PixelStudioContextMenuAction.DeleteFrame;
                DrawUiRect(button.Rect, isDestructive ? Blend(_theme.TabInactive, _theme.Accent, 0.14f) : _theme.TabInactive);
                DrawTextInRect(GetContextMenuLabel(button.Action), bodyFont, bodyText, button.Rect, 10, 6);
            }
        }

        if (!sidebarCollapsed && layout.PalettePromptRect is not null)
        {
            DrawTextInRect("Generate palette from image?", statusFont, statusText, new UiRect(layout.PalettePromptRect.Value.X + 10, layout.PalettePromptRect.Value.Y + 10, layout.PalettePromptRect.Value.Width - 20, 18), 0, 0);
            foreach (ActionRect<PixelStudioAction> button in layout.PalettePromptButtons)
            {
                DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 6, 6);
            }
        }

        DrawTextInRect(sidebarCollapsed ? "L" : "Layers", titleFont, bodyText, new UiRect(layersHeaderRect.X + 22, layersHeaderRect.Y, Math.Max(layersHeaderRect.Width - 30, 0), layersHeaderRect.Height), 0, 7);
        foreach (ActionRect<PixelStudioAction> button in layout.LayerButtons)
        {
            DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 6, 6);
        }
        if (layout.LayerRenameFieldRect is not null)
        {
            string renameText = string.IsNullOrWhiteSpace(pixelStudio.LayerRenameBuffer) ? "Type a new layer name..." : pixelStudio.LayerRenameBuffer;
            DrawTextInRect(renameText, bodyFont, bodyText, layout.LayerRenameFieldRect.Value, 8, 7);
        }
        for (int index = 0; index < layout.LayerRows.Count && index < pixelStudio.Layers.Count; index++)
        {
            int layerIndex = layout.LayerRows[index].Index;
            if (layerIndex < 0 || layerIndex >= pixelStudio.Layers.Count || index >= layout.LayerVisibilityButtons.Count)
            {
                continue;
            }

            PixelStudioLayerView layer = pixelStudio.Layers[layerIndex];
            DrawCenteredTextInRect(layer.IsVisible ? "On" : "Off", statusFont, bodyText, layout.LayerVisibilityButtons[index].Rect, 6, 6);
            DrawTextInRect($"{layer.Name}{(layer.IsLocked ? " [Locked]" : string.Empty)}", bodyFont, bodyText, layout.LayerRows[index].Rect, 10, 7);
        }

        if (timelineVisible)
        {
            DrawTextInRect("Frames", titleFont, bodyText, framesTitleRect, 0, 7);
            DrawTextInRect($"{pixelStudio.FramesPerSecond} FPS", statusFont, statusText, framesAccessoryRect, 0, 8);
            foreach (ActionRect<PixelStudioAction> button in layout.TimelineButtons)
            {
                DrawCenteredTextClippedInRect(GetPixelStudioActionLabel(button.Action), bodyFont, bodyText, button.Rect, 6, 6);
            }
            if (layout.FrameRenameFieldRect is not null)
            {
                string renameText = string.IsNullOrWhiteSpace(pixelStudio.FrameRenameBuffer) ? "Type a new frame name..." : pixelStudio.FrameRenameBuffer;
                DrawTextInRect(renameText, bodyFont, bodyText, layout.FrameRenameFieldRect.Value, 8, 7);
            }
            for (int index = 0; index < layout.FrameRows.Count && index < pixelStudio.Frames.Count; index++)
            {
                int frameIndex = layout.FrameRows[index].Index;
                if (frameIndex < 0 || frameIndex >= pixelStudio.Frames.Count)
                {
                    continue;
                }

                string suffix = pixelStudio.Frames[frameIndex].IsPreviewing ? " *" : string.Empty;
                DrawCenteredTextClippedInRect($"{pixelStudio.Frames[frameIndex].Name}{suffix}", bodyFont, bodyText, layout.FrameRows[index].Rect, 10, 8);
            }
        }
    }

    private static bool IsCollapsedPanel(UiRect rect)
    {
        return rect.Width <= CollapsedPanelThreshold;
    }

    private static UiRect GetPixelPanelHeaderRect(UiRect panelRect)
    {
        if (panelRect.Width <= CollapsedPanelThreshold)
        {
            return SnapRect(panelRect);
        }

        float headerHeight = Math.Min(PixelPanelHeaderHeight, panelRect.Height);
        return SnapRect(new UiRect(panelRect.X, panelRect.Y, panelRect.Width, headerHeight));
    }

    private static UiRect GetPixelPanelBodyRect(UiRect panelRect)
    {
        if (panelRect.Width <= CollapsedPanelThreshold)
        {
            return new UiRect(panelRect.X, panelRect.Y + panelRect.Height, 0, 0);
        }

        float headerHeight = Math.Min(PixelPanelHeaderHeight, panelRect.Height);
        return SnapRect(new UiRect(
            panelRect.X + PixelPanelPadding,
            panelRect.Y + headerHeight + PixelPanelPadding,
            Math.Max(panelRect.Width - (PixelPanelPadding * 2), 0),
            Math.Max(panelRect.Height - headerHeight - (PixelPanelPadding * 2), 0)));
    }

    private static UiRect GetWorkspaceLogoRect(UiRect panelRect)
    {
        const float horizontalInset = 4f;
        const float bottomInset = 12f;
        const float targetHeight = 188f;

        float logoWidth = Math.Max(panelRect.Width - (horizontalInset * 2), 0);
        float logoHeight = Math.Min(targetHeight, Math.Max(panelRect.Height - bottomInset, 0));
        float logoY = panelRect.Y + Math.Max(panelRect.Height - logoHeight - bottomInset, 196f);
        return new UiRect(
            panelRect.X + horizontalInset,
            logoY,
            logoWidth,
            logoHeight);
    }

    private static UiRect GetHeaderTitleRect(UiRect headerRect, UiRect trailingRect, float leftPadding = 12, float rightPadding = 12, float spacing = 10)
    {
        float left = headerRect.X + leftPadding;
        float right = Math.Max(trailingRect.X - spacing, left);
        float maxRight = headerRect.X + headerRect.Width - rightPadding;
        right = Math.Min(right, maxRight);
        return SnapRect(new UiRect(left, headerRect.Y, Math.Max(right - left, 0), headerRect.Height));
    }

    private static UiRect GetLeadingHeaderTitleRect(UiRect headerRect, UiRect leadingRect, float leftPadding = 12, float rightPadding = 12, float spacing = 10)
    {
        float left = Math.Max(leadingRect.X + leadingRect.Width + spacing, headerRect.X + leftPadding);
        float right = headerRect.X + headerRect.Width - rightPadding;
        return SnapRect(new UiRect(left, headerRect.Y, Math.Max(right - left, 0), headerRect.Height));
    }

    private ThemeColor ResolveTopBarColor()
    {
        return string.Equals(_theme.Name, EditorThemeCatalog.LightThemeName, StringComparison.OrdinalIgnoreCase)
            ? new ThemeColor(0.98f, 0.98f, 0.98f, 1.0f)
            : new ThemeColor(0.03f, 0.03f, 0.03f, 1.0f);
    }

    private ThemeColor ResolveTopBarTextColor()
    {
        return string.Equals(_theme.Name, EditorThemeCatalog.LightThemeName, StringComparison.OrdinalIgnoreCase)
            ? new ThemeColor(0.06f, 0.06f, 0.06f, 1.0f)
            : new ThemeColor(0.98f, 0.98f, 0.98f, 1.0f);
    }

    private static UiRect GetUnionRect(IEnumerable<UiRect> rects)
    {
        List<UiRect> items = rects.Where(rect => rect.Width > 0 && rect.Height > 0).ToList();
        if (items.Count == 0)
        {
            return default;
        }

        float left = items.Min(rect => rect.X);
        float top = items.Min(rect => rect.Y);
        float right = items.Max(rect => rect.X + rect.Width);
        float bottom = items.Max(rect => rect.Y + rect.Height);
        return SnapRect(new UiRect(left, top, right - left, bottom - top));
    }

    private TextMeasurement MeasureText(string text, Font font)
    {
        TextOptions options = new(font);
        FontRectangle bounds = TextMeasurer.MeasureBounds(text, options);
        FontRectangle size = TextMeasurer.MeasureSize(text, options);
        float ascender = MathF.Ceiling(MeasureFontAscender(font));
        float descender = MathF.Ceiling(MeasureFontDescender(font));
        float textHeight = MathF.Ceiling(ascender + descender);
        float width = MathF.Ceiling(Math.Max(bounds.Width, size.Width));
        return new TextMeasurement(width, ascender, descender, textHeight);
    }

    private void DrawTextClipped(UiRect rect, string text, Font font, SixLabors.ImageSharp.Color color, float paddingX = 0, float paddingY = 0, UiTextAlignment alignment = UiTextAlignment.Left)
    {
        DrawTextInternal(rect, text, font, color, paddingX, paddingY, alignment, false);
    }

    private void DrawTextEllipsis(UiRect rect, string text, Font font, SixLabors.ImageSharp.Color color, float paddingX = 0, float paddingY = 0, UiTextAlignment alignment = UiTextAlignment.Left)
    {
        DrawTextInternal(rect, text, font, color, paddingX, paddingY, alignment, true);
    }

    private void DrawTextInternal(UiRect rect, string text, Font font, SixLabors.ImageSharp.Color color, float paddingX, float paddingY, UiTextAlignment alignment, bool ellipsis)
    {
        if (string.IsNullOrWhiteSpace(text) || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        _ = paddingY;
        float availableWidth = Math.Max(rect.Width - (paddingX * 2), 8);
        string fitted = ellipsis ? FitTextToWidth(text, font, availableWidth) : text;
        if (string.IsNullOrWhiteSpace(fitted))
        {
            return;
        }

        TextMeasurement measurement = MeasureText(fitted, font);
        float textHeight = MathF.Max(measurement.Ascent + measurement.Descent, measurement.TextHeight);
        UiRect contentRect = alignment == UiTextAlignment.Center
            ? new UiRect(
                rect.X + paddingX,
                rect.Y + paddingY,
                Math.Max(rect.Width - (paddingX * 2), 0),
                Math.Max(rect.Height - (paddingY * 2), 0))
            : new UiRect(
                rect.X + paddingX,
                rect.Y + paddingY,
                Math.Max(rect.Width - (paddingX * 2), 0),
                Math.Max(rect.Height - paddingY, 0));
        float contentX = alignment == UiTextAlignment.Center
            ? contentRect.X + Math.Max((contentRect.Width - measurement.Width) * 0.5f, 0)
            : contentRect.X;
        float textTop = alignment == UiTextAlignment.Center
            ? contentRect.Y + Math.Max((contentRect.Height - textHeight) * 0.5f, 0)
            : contentRect.Y;
        float drawX = MathF.Round(contentX - TextTexturePadding);
        float drawY = MathF.Round(textTop - TextTexturePadding);

        if (ShowTextDebugBounds)
        {
            DrawDebugOutline(rect, new ThemeColor(0.15f, 0.75f, 1.0f, 1.0f));
            DrawDebugOutline(SnapRect(new UiRect(contentX, textTop, measurement.Width, textHeight)), new ThemeColor(1.0f, 0.38f, 0.2f, 1.0f));
        }

        _textRenderer.DrawTextClipped(fitted, font, color, drawX, drawY, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private void DrawTextInRect(string text, Font font, SixLabors.ImageSharp.Color color, UiRect rect, float paddingX, float paddingY)
    {
        DrawTextEllipsis(rect, text, font, color, paddingX, paddingY, UiTextAlignment.Left);
    }

    private void DrawTextClippedInRect(string text, Font font, SixLabors.ImageSharp.Color color, UiRect rect, float paddingX, float paddingY)
    {
        DrawTextClipped(rect, text, font, color, paddingX, paddingY, UiTextAlignment.Left);
    }

    private void DrawCenteredTextInRect(string text, Font font, SixLabors.ImageSharp.Color color, UiRect rect, float minPaddingX, float minPaddingY)
    {
        DrawTextEllipsis(rect, text, font, color, minPaddingX, minPaddingY, UiTextAlignment.Center);
    }

    private void DrawCenteredTextClippedInRect(string text, Font font, SixLabors.ImageSharp.Color color, UiRect rect, float minPaddingX, float minPaddingY)
    {
        DrawTextClipped(rect, text, font, color, minPaddingX, minPaddingY, UiTextAlignment.Center);
    }

    private static string FitTextToWidth(string text, Font font, float maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
        {
            return string.Empty;
        }

        if (MeasureTextWidth(text, font) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        string working = text.Trim();
        while (working.Length > 1 && MeasureTextWidth(working + ellipsis, font) > maxWidth)
        {
            working = working[..^1];
        }

        return working.Length == text.Length ? text : working + ellipsis;
    }

    private static float MeasureTextWidth(string text, Font font)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        TextOptions options = new(font);
        FontRectangle bounds = TextMeasurer.MeasureBounds(text, options);
        FontRectangle size = TextMeasurer.MeasureSize(text, options);
        return MathF.Ceiling(Math.Max(bounds.Width, size.Width));
    }

    private static FontRectangle MeasureTextBounds(string text, Font font)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        return TextMeasurer.MeasureBounds(text, new TextOptions(font));
    }

    private void DrawUiRect(UiRect rect, ThemeColor color)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        DrawRect(
            (int)MathF.Round(rect.X),
            _height - (int)MathF.Round(rect.Y + rect.Height),
            (int)MathF.Round(rect.Width),
            (int)MathF.Round(rect.Height),
            color);
    }

    private void DrawUiRectClipped(UiRect rect, ThemeColor color, UiRect clipRect)
    {
        UiRect? clipped = IntersectRect(rect, clipRect);
        if (clipped is null)
        {
            return;
        }

        DrawUiRect(clipped.Value, color);
    }

    private void DrawRoundedUiRect(UiRect rect, ThemeColor color, float radius)
    {
        rect = SnapRect(rect);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        float resolvedRadius = MathF.Min(MathF.Max(radius, 0f), MathF.Min(rect.Width, rect.Height) * 0.5f);
        if (resolvedRadius < 1.5f)
        {
            DrawUiRect(rect, color);
            return;
        }

        int steps = Math.Max((int)MathF.Ceiling(resolvedRadius), 1);
        float middleHeight = Math.Max(rect.Height - (steps * 2), 0f);
        if (middleHeight > 0)
        {
            DrawUiRect(new UiRect(rect.X, rect.Y + steps, rect.Width, middleHeight), color);
        }

        for (int index = 0; index < steps; index++)
        {
            float sample = index + 0.5f;
            float distanceFromCenter = resolvedRadius - sample;
            float inset = resolvedRadius - MathF.Sqrt(MathF.Max((resolvedRadius * resolvedRadius) - (distanceFromCenter * distanceFromCenter), 0f));
            float rowWidth = Math.Max(rect.Width - (inset * 2f), 0f);
            if (rowWidth <= 0f)
            {
                continue;
            }

            DrawUiRect(new UiRect(rect.X + inset, rect.Y + index, rowWidth, 1), color);
            DrawUiRect(new UiRect(rect.X + inset, rect.Y + rect.Height - index - 1, rowWidth, 1), color);
        }
    }

    private void DrawDebugOutline(UiRect rect, ThemeColor color)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        DrawUiRect(new UiRect(rect.X, rect.Y, rect.Width, 1), color);
        DrawUiRect(new UiRect(rect.X, rect.Y + Math.Max(rect.Height - 1, 0), rect.Width, 1), color);
        DrawUiRect(new UiRect(rect.X, rect.Y, 1, rect.Height), color);
        DrawUiRect(new UiRect(rect.X + Math.Max(rect.Width - 1, 0), rect.Y, 1, rect.Height), color);
    }

    private static float MeasureFontAscender(Font font)
    {
        HorizontalMetrics metrics = font.FontMetrics.HorizontalMetrics;
        return MathF.Max(metrics.Ascender * MeasureFontMetricScale(font), font.Size * 0.72f);
    }

    private static float MeasureFontDescender(Font font)
    {
        HorizontalMetrics metrics = font.FontMetrics.HorizontalMetrics;
        return MathF.Max(MathF.Abs(metrics.Descender * MeasureFontMetricScale(font)), font.Size * 0.24f);
    }

    private static float MeasureFontMetricScale(Font font)
    {
        float unitsPerEm = MathF.Max(font.FontMetrics.UnitsPerEm, 1f);
        return font.Size / unitsPerEm;
    }

    private static UiRect SnapRect(UiRect rect)
    {
        return new UiRect(
            MathF.Round(rect.X),
            MathF.Round(rect.Y),
            MathF.Round(Math.Max(rect.Width, 0)),
            MathF.Round(Math.Max(rect.Height, 0)));
    }

    private static UiRect? IntersectRect(UiRect a, UiRect b)
    {
        float left = MathF.Max(a.X, b.X);
        float top = MathF.Max(a.Y, b.Y);
        float right = MathF.Min(a.X + a.Width, b.X + b.Width);
        float bottom = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= left || bottom <= top)
        {
            return null;
        }

        return SnapRect(new UiRect(left, top, right - left, bottom - top));
    }

    private void DrawRect(int x, int y, int width, int height, ThemeColor color)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _gl.Enable(EnableCap.ScissorTest);
        _gl.Scissor(x, y, (uint)width, (uint)height);
        _gl.ClearColor(color.R, color.G, color.B, color.A);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    private void Clear(ThemeColor color)
    {
        _gl.ClearColor(color.R, color.G, color.B, color.A);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    private Font ResolveFont(float size)
    {
        FontFamily family = ResolveFontFamily();
        return family.CreateFont(size, FontStyle.Regular);
    }

    private FontFamily ResolveFontFamily()
    {
        if (_hasResolvedFontFamily)
        {
            return _resolvedFontFamily;
        }

        if (TryResolveFontFamily(_typography.PreferredFontFamily, out FontFamily resolved))
        {
            _resolvedFontFamily = resolved;
            _hasResolvedFontFamily = true;
            return resolved;
        }

        _resolvedFontFamily = SystemFonts.Collection.Families.First();
        _hasResolvedFontFamily = true;
        return _resolvedFontFamily;
    }

    private static bool TryResolveFontFamily(string preferredFamily, out FontFamily family)
    {
        if (SystemFonts.TryGet(preferredFamily, out family))
        {
            return true;
        }

        foreach (string candidate in EditorTypographyCatalog.ReadableFontCandidates)
        {
            if (SystemFonts.TryGet(candidate, out family))
            {
                return true;
            }
        }

        family = default!;
        return false;
    }

    private static string GetMenuLabel(EditorMenuAction action)
    {
        return action switch
        {
            EditorMenuAction.OpenHome => "Home",
            EditorMenuAction.OpenPixelStudio => EditorBranding.PixelToolName,
            EditorMenuAction.OpenProjects => "Projects",
            EditorMenuAction.OpenLayout => "Layout",
            EditorMenuAction.OpenPreferences => "Preferences",
            EditorMenuAction.CreateProjectSlot => "Create Project Slot",
            EditorMenuAction.OpenProjectLibrary => "Project Library",
            EditorMenuAction.NewScratchTab => "New Scratch Tab",
            EditorMenuAction.ToggleTheme => "Toggle Theme",
            EditorMenuAction.CycleFontSize => "Cycle Font Size",
            EditorMenuAction.CycleFontFamily => "Cycle Font Family",
            _ => action.ToString()
        };
    }

    private void DrawPixelStudioPanelText(Font titleFont, Font bodyFont, Font statusFont, SixLabors.ImageSharp.Color bodyText)
    {
        UiRect leftPanel = _layoutSnapshot!.LeftPanelRect;
        if (IsCollapsedPanel(leftPanel))
        {
            return;
        }
        else
        {
            UiRect leftContentRect = new(leftPanel.X + 16, leftPanel.Y + 10, Math.Max(leftPanel.Width - 32, 0), Math.Max(leftPanel.Height - 20, 0));
            UiRect toolValueRect = new(leftContentRect.X, leftPanel.Y + 54, leftContentRect.Width, 26);
            UiRect documentValueRect = new(leftContentRect.X, leftPanel.Y + 108, leftContentRect.Width, 26);
            UiRect libraryValueRect = new(leftContentRect.X, leftPanel.Y + 180, leftContentRect.Width, 30);
            ThemeColor workspaceInfoBoxColor = Blend(_theme.TabInactive, _theme.Workspace, 0.24f);

            DrawTextInRect("Workspace", titleFont, bodyText, leftPanel, 18, 6);
            DrawTextInRect("Current Tool", statusFont, bodyText, leftPanel, 18, 34);
            DrawUiRect(toolValueRect, workspaceInfoBoxColor);
            DrawCenteredTextClippedInRect(GetPixelToolLabel(_uiState.PixelStudio.ActiveTool), bodyFont, bodyText, toolValueRect, 6, 0);
            DrawTextInRect("Document", statusFont, bodyText, leftPanel, 18, 86);
            DrawUiRect(documentValueRect, workspaceInfoBoxColor);
            DrawCenteredTextClippedInRect(_uiState.PixelStudio.DocumentName, bodyFont, bodyText, documentValueRect, 6, 0);
            DrawTextInRect("Project Library", statusFont, bodyText, leftPanel, 18, 144);
            DrawUiRect(libraryValueRect, workspaceInfoBoxColor);
            DrawCenteredTextClippedInRect(_uiState.ProjectLibraryPath, bodyFont, bodyText, libraryValueRect, 6, 0);

            if (File.Exists(_brandingImagePath))
            {
                UiRect workspaceLogoRect = GetWorkspaceLogoRect(leftPanel);
                _imageRenderer.DrawImage(_brandingImagePath, workspaceLogoRect.X, workspaceLogoRect.Y, workspaceLogoRect.Width, workspaceLogoRect.Height);
            }
        }

        UiRect rightPanel = _layoutSnapshot.RightPanelRect;
        if (IsCollapsedPanel(rightPanel))
        {
            return;
        }

        DrawTextInRect("Pixel Inspector", titleFont, bodyText, rightPanel, 18, 18);
        DrawTextInRect("Selected Color", statusFont, bodyText, rightPanel, 18, 46);
        DrawTextInRect(_uiState.PixelStudio.ActiveColorHex, bodyFont, bodyText, rightPanel, 18, 66);
        DrawTextInRect("Zoom", statusFont, bodyText, rightPanel, 18, 100);
        DrawTextInRect($"{_uiState.PixelStudio.Zoom}x", bodyFont, bodyText, rightPanel, 18, 120);
        if (rightPanel.Width >= 180)
        {
            DrawTextInRect("Frame", statusFont, bodyText, rightPanel, 18, 154);
            DrawTextInRect(_uiState.PixelStudio.Frames.FirstOrDefault(frame => frame.IsActive)?.Name ?? "Frame 1", bodyFont, bodyText, rightPanel, 18, 174);
            DrawTextInRect("Layer", statusFont, bodyText, rightPanel, 18, 208);
            DrawTextInRect(_uiState.PixelStudio.Layers.FirstOrDefault(layer => layer.IsActive)?.Name ?? "Layer 1", bodyFont, bodyText, rightPanel, 18, 228);
            DrawTextInRect("Grid", statusFont, bodyText, rightPanel, 18, 262);
            DrawTextInRect(_uiState.PixelStudio.ShowGrid ? "Visible" : "Hidden", bodyFont, bodyText, rightPanel, 18, 282);
        }
    }

    private static string GetPixelToolLabel(PixelStudioToolKind tool)
    {
        return tool switch
        {
            PixelStudioToolKind.Select => "Select",
            PixelStudioToolKind.Pencil => "Pencil",
            PixelStudioToolKind.Eraser => "Eraser",
            PixelStudioToolKind.Line => "Line",
            PixelStudioToolKind.Fill => "Fill",
            PixelStudioToolKind.Picker => "Picker",
            _ => tool.ToString()
        };
    }

    private static string GetPixelStudioActionLabel(PixelStudioAction action)
    {
        return action switch
        {
            PixelStudioAction.NewBlankDocument => "New Sprite",
            PixelStudioAction.SaveProjectDocument => "Save",
            PixelStudioAction.LoadProjectDocument => "Open",
            PixelStudioAction.LoadDemoDocument => "Demo",
            PixelStudioAction.ImportImage => "Import",
            PixelStudioAction.ResizeCanvas16 => "16px",
            PixelStudioAction.ResizeCanvas32 => "32px",
            PixelStudioAction.ResizeCanvas64 => "64px",
            PixelStudioAction.ResizeCanvas128 => "128px",
            PixelStudioAction.ZoomOut => "-",
            PixelStudioAction.ZoomIn => "+",
            PixelStudioAction.ToggleGrid => "Grid",
            PixelStudioAction.FitCanvas => "Fit",
            PixelStudioAction.ResetView => "Reset",
            PixelStudioAction.ExportPng => "Export",
            PixelStudioAction.DockToolSettingsLeft => "Dock Left",
            PixelStudioAction.DockToolSettingsRight => "Dock Right",
            PixelStudioAction.DecreaseBrushSize => "Brush -",
            PixelStudioAction.IncreaseBrushSize => "Brush +",
            PixelStudioAction.ToggleTimelinePanel => "Frames",
            PixelStudioAction.TogglePaletteLibrary => "Library",
            PixelStudioAction.AddPaletteSwatch => "Add",
            PixelStudioAction.SaveCurrentPalette => "Save",
            PixelStudioAction.GeneratePaletteFromImage => "Generate",
            PixelStudioAction.RenameSelectedPalette => "Rename",
            PixelStudioAction.DeleteSelectedPalette => "Delete",
            PixelStudioAction.PalettePromptGenerate => "Yes",
            PixelStudioAction.PalettePromptDismiss => "No",
            PixelStudioAction.PalettePromptDismissForever => "Don't Ask",
            PixelStudioAction.DecreaseRed => "R-",
            PixelStudioAction.IncreaseRed => "R+",
            PixelStudioAction.DecreaseGreen => "G-",
            PixelStudioAction.IncreaseGreen => "G+",
            PixelStudioAction.DecreaseBlue => "B-",
            PixelStudioAction.IncreaseBlue => "B+",
            PixelStudioAction.AddLayer => "Layer +",
            PixelStudioAction.DeleteLayer => "Layer -",
            PixelStudioAction.AddFrame => "Frame +",
            PixelStudioAction.DeleteFrame => "Frame -",
            PixelStudioAction.TogglePlayback => "Play",
            PixelStudioAction.DecreaseFrameRate => "FPS -",
            PixelStudioAction.IncreaseFrameRate => "FPS +",
            _ => action.ToString()
        };
    }

    private static string GetContextMenuLabel(PixelStudioContextMenuAction action)
    {
        return action switch
        {
            PixelStudioContextMenuAction.DisableSelection => "Disable Selection",
            PixelStudioContextMenuAction.RenamePalette => "Rename Palette",
            PixelStudioContextMenuAction.DeletePalette => "Delete Palette",
            PixelStudioContextMenuAction.RenameLayer => "Rename Layer",
            PixelStudioContextMenuAction.DuplicateLayer => "Duplicate Layer",
            PixelStudioContextMenuAction.MoveLayerUp => "Move Layer Up",
            PixelStudioContextMenuAction.MoveLayerDown => "Move Layer Down",
            PixelStudioContextMenuAction.ToggleLayerLock => "Toggle Layer Lock",
            PixelStudioContextMenuAction.DeleteLayer => "Delete Layer",
            PixelStudioContextMenuAction.RenameFrame => "Rename Frame",
            PixelStudioContextMenuAction.DuplicateFrame => "Duplicate Frame",
            PixelStudioContextMenuAction.MoveFrameLeft => "Move Frame Left",
            PixelStudioContextMenuAction.MoveFrameRight => "Move Frame Right",
            PixelStudioContextMenuAction.DeleteFrame => "Delete Frame",
            _ => action.ToString()
        };
    }

    private ThemeColor ResolvePixelActionColor(PixelStudioAction action)
    {
        return action switch
        {
            PixelStudioAction.ToggleGrid when _uiState.PixelStudio.ShowGrid => _theme.TabActive,
            PixelStudioAction.TogglePlayback when _uiState.PixelStudio.IsPlaying => _theme.TabActive,
            PixelStudioAction.DeleteLayer when _uiState.PixelStudio.Layers.Count <= 1 => _theme.Divider,
            PixelStudioAction.DeleteFrame when _uiState.PixelStudio.Frames.Count <= 1 => _theme.Divider,
            PixelStudioAction.ResizeCanvas16 when _uiState.PixelStudio.CanvasWidth == 16 && _uiState.PixelStudio.CanvasHeight == 16 => _theme.TabActive,
            PixelStudioAction.ResizeCanvas32 when _uiState.PixelStudio.CanvasWidth == 32 && _uiState.PixelStudio.CanvasHeight == 32 => _theme.TabActive,
            PixelStudioAction.ResizeCanvas64 when _uiState.PixelStudio.CanvasWidth == 64 && _uiState.PixelStudio.CanvasHeight == 64 => _theme.TabActive,
            PixelStudioAction.ResizeCanvas128 when _uiState.PixelStudio.CanvasWidth == 128 && _uiState.PixelStudio.CanvasHeight == 128 => _theme.TabActive,
            PixelStudioAction.ToggleTimelinePanel when _uiState.PixelStudio.TimelineVisible => _theme.TabActive,
            PixelStudioAction.TogglePaletteLibrary when _uiState.PixelStudio.PaletteLibraryVisible => _theme.TabActive,
            _ => _theme.TabInactive
        };
    }

    private void DrawPixelPreview(UiRect rect, IReadOnlyList<ThemeColor?> pixels, int canvasWidth, int canvasHeight)
    {
        if (pixels.Count == 0 || canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        ThemeColor checkerLight = Blend(_theme.Workspace, new ThemeColor(0.90f, 0.92f, 0.95f), 0.18f);
        ThemeColor checkerDark = Blend(_theme.Workspace, new ThemeColor(0.08f, 0.09f, 0.12f), 0.22f);
        UiRect paddedRect = new(rect.X + 6, rect.Y + 6, Math.Max(rect.Width - 12, 0), Math.Max(rect.Height - 12, 0));
        float scale = MathF.Min(
            Math.Max(paddedRect.Width, 1f) / Math.Max(canvasWidth, 1),
            Math.Max(paddedRect.Height, 1f) / Math.Max(canvasHeight, 1));
        float cellSize = Math.Max(scale, 0.25f);
        float viewportWidth = cellSize * canvasWidth;
        float viewportHeight = cellSize * canvasHeight;
        float startX = paddedRect.X + Math.Max((paddedRect.Width - viewportWidth) * 0.5f, 0);
        float startY = paddedRect.Y + Math.Max((paddedRect.Height - viewportHeight) * 0.5f, 0);

        for (int index = 0; index < pixels.Count; index++)
        {
            int x = index % canvasWidth;
            int y = index / canvasWidth;
            ThemeColor color = pixels[index] ?? (((x + y) % 2 == 0) ? checkerLight : checkerDark);
            DrawUiRectClipped(new UiRect(startX + (x * cellSize), startY + (y * cellSize), cellSize, cellSize), color, paddedRect);
        }
    }

    private static string TrimPath(string path, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length <= maxLength)
        {
            return path;
        }

        return "..." + path[^Math.Max(maxLength - 3, 1)..];
    }

    private static SixLabors.ImageSharp.Color ToImageSharpColor(ThemeColor color)
    {
        byte r = (byte)Math.Clamp((int)(color.R * 255), 0, 255);
        byte g = (byte)Math.Clamp((int)(color.G * 255), 0, 255);
        byte b = (byte)Math.Clamp((int)(color.B * 255), 0, 255);
        byte a = (byte)Math.Clamp((int)(color.A * 255), 0, 255);
        return SixLabors.ImageSharp.Color.FromRgba(r, g, b, a);
    }

    private static ThemeColor Blend(ThemeColor baseColor, ThemeColor targetColor, float amount)
    {
        return new ThemeColor(
            baseColor.R + ((targetColor.R - baseColor.R) * amount),
            baseColor.G + ((targetColor.G - baseColor.G) * amount),
            baseColor.B + ((targetColor.B - baseColor.B) * amount),
            1.0f);
    }
}

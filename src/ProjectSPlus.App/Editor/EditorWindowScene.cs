using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Shell;
using ProjectSPlus.Editor.Themes;
using ProjectSPlus.Runtime.Application;
using SixLabors.Fonts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ProjectSPlus.App.Editor;

public sealed partial class EditorWindowScene : IWindowScene
{
    private enum ShellDragMode
    {
        None,
        ResizeLeftPanel,
        ResizeRightPanel
    }

    private readonly EditorShell _shell;
    private readonly List<EditorShortcutBinding> _shortcuts;
    private readonly List<EditorWorkspaceTab> _tabs;
    private readonly List<RecentProjectEntry> _recentProjects;
    private readonly PixelStudioState _pixelStudio;
    private readonly List<SavedPixelPalette> _savedPixelPalettes;
    private readonly EditorUiState _uiState = new();

    private string _preferredFontFamily;
    private string _projectLibraryPath;
    private string? _lastProjectPath;
    private string? _activePixelPaletteId;
    private string? _selectedPixelPaletteId;
    private string? _lastImportedImagePath;
    private float _leftPanelWidth = 280;
    private float _rightPanelWidth = 320;
    private float _previousLeftPanelWidth = 280;
    private float _previousRightPanelWidth = 320;
    private float _pixelToolsPanelWidth = 164;
    private float _pixelSidebarWidth = 360;
    private float _previousPixelToolsPanelWidth = 164;
    private float _previousPixelSidebarWidth = 360;
    private int _leftPanelRecentScrollRow;
    private int _homeRecentScrollRow;
    private int _projectRecentScrollRow;
    private int _preferenceScrollRow;
    private int _folderPickerScrollRow;
    private int _paletteSwatchScrollRow;
    private int _savedPaletteScrollRow;
    private int _layerScrollRow;
    private int _frameScrollRow;
    private FontSizePreset _fontSizePreset;
    private EditorTheme _theme;
    private EditorTypography _typography;
    private EditorShellRenderer? _renderer;
    private EditorLayoutSnapshot? _layoutSnapshot;
    private IWindow? _window;
    private int _width;
    private int _height;
    private int _scratchCounter;
    private bool _paletteLibraryVisible;
    private bool _palettePromptVisible;
    private bool _paletteRenameActive;
    private bool _layerRenameActive;
    private bool _pixelContextMenuVisible;
    private bool _promptForPaletteGenerationAfterImport;
    private bool _leftPanelCollapsed;
    private bool _rightPanelCollapsed;
    private bool _pixelToolsCollapsed;
    private bool _pixelSidebarCollapsed;
    private bool _pixelTimelineVisible;
    private ShellDragMode _shellDragMode;
    private PixelStudioDragMode _pixelDragMode;
    private string _paletteRenameBuffer = string.Empty;
    private string _layerRenameBuffer = string.Empty;
    private float _pixelContextMenuX;
    private float _pixelContextMenuY;
    private float _pixelToolSettingsPanelOffsetX = float.NaN;
    private float _pixelToolSettingsPanelOffsetY = float.NaN;

    public EditorWindowScene(EditorShell shell, string initialThemeName)
        : this(shell, initialThemeName, "Segoe UI", FontSizePreset.Medium, new ShortcutBindings(), string.Empty, [], null, new EditorLayoutSettings(), [], null, true)
    {
    }

    public EditorWindowScene(
        EditorShell shell,
        string initialThemeName,
        string preferredFontFamily,
        FontSizePreset fontSizePreset,
        ShortcutBindings shortcuts,
        string projectLibraryPath,
        IReadOnlyList<RecentProjectEntry> recentProjects,
        string? lastProjectPath,
        EditorLayoutSettings layoutSettings,
        IReadOnlyList<SavedPixelPalette> savedPixelPalettes,
        string? activePixelPaletteId,
        bool promptForPaletteGenerationAfterImport)
    {
        _shell = shell;
        _preferredFontFamily = ResolveAvailableFontFamily(preferredFontFamily);
        _fontSizePreset = fontSizePreset;
        _projectLibraryPath = string.IsNullOrWhiteSpace(projectLibraryPath)
            ? GetDefaultProjectLibraryPath()
            : projectLibraryPath;
        _lastProjectPath = lastProjectPath;
        _theme = EditorThemeCatalog.GetByName(initialThemeName);
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, fontSizePreset);
        _shortcuts = CreateShortcuts(shortcuts);
        _recentProjects = recentProjects
            .Where(project => !string.IsNullOrWhiteSpace(project.Path))
            .Select(project => new RecentProjectEntry { Name = project.Name, Path = project.Path })
            .ToList();
        EditorLayoutSettings normalizedLayout = layoutSettings.Normalize();
        _leftPanelWidth = normalizedLayout.LeftPanelWidth;
        _rightPanelWidth = normalizedLayout.RightPanelWidth;
        _previousLeftPanelWidth = normalizedLayout.LeftPanelWidth;
        _previousRightPanelWidth = normalizedLayout.RightPanelWidth;
        _leftPanelCollapsed = normalizedLayout.LeftPanelCollapsed;
        _rightPanelCollapsed = normalizedLayout.RightPanelCollapsed;
        _pixelToolsPanelWidth = normalizedLayout.PixelToolsPanelWidth;
        _pixelSidebarWidth = normalizedLayout.PixelSidebarWidth;
        _previousPixelToolsPanelWidth = normalizedLayout.PixelToolsPanelWidth;
        _previousPixelSidebarWidth = normalizedLayout.PixelSidebarWidth;
        _pixelToolsCollapsed = normalizedLayout.PixelToolsPanelCollapsed;
        _pixelSidebarCollapsed = normalizedLayout.PixelSidebarCollapsed;
        _pixelTimelineVisible = normalizedLayout.PixelTimelineVisible;
        _pixelToolSettingsPanelOffsetX = normalizedLayout.PixelToolSettingsOffsetX ?? float.NaN;
        _pixelToolSettingsPanelOffsetY = normalizedLayout.PixelToolSettingsOffsetY ?? float.NaN;
        _savedPixelPalettes = savedPixelPalettes
            .Where(palette => !string.IsNullOrWhiteSpace(palette.Id))
            .Select(CloneSavedPixelPalette)
            .ToList();
        _activePixelPaletteId = activePixelPaletteId;
        _selectedPixelPaletteId = activePixelPaletteId;
        _promptForPaletteGenerationAfterImport = promptForPaletteGenerationAfterImport;
        _tabs = CreateDefaultTabs();
        _pixelStudio = CreateDefaultPixelStudio();
        ApplyInitialSavedPaletteIfAvailable();
        if (!string.IsNullOrWhiteSpace(_lastProjectPath))
        {
            string? lastDocumentPath = FindPreferredPixelStudioDocument(_lastProjectPath);
            if (!string.IsNullOrWhiteSpace(lastDocumentPath))
            {
                TryLoadPixelStudioDocument(lastDocumentPath, out _);
            }
        }
        _uiState.ProjectForm = new EditorProjectFormState
        {
            ProjectLibraryPath = _projectLibraryPath,
            FolderPickerPath = _projectLibraryPath,
            FolderPickerEntries = GetDirectoryEntries(_projectLibraryPath)
        };
        _uiState.SelectedTabId = _tabs[0].Id;
        SyncUiState();
    }

    public void Initialize(IWindow window, GL gl, Vector2D<int> framebufferSize)
    {
        _window = window;
        _renderer = new EditorShellRenderer(gl, _shell, _theme, _typography, _uiState);
        Resize(framebufferSize.X, framebufferSize.Y);
        UpdateWindowTitle();
    }

    public void Resize(int width, int height)
    {
        _width = Math.Max(width, 1);
        _height = Math.Max(height, 1);
        _layoutSnapshot = EditorLayoutEngine.Create(_width, _height, _shell.Layout, _uiState);
        _renderer?.Resize(_width, _height);
        _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
    }

    public void Render()
    {
        if (_layoutSnapshot is null)
        {
            _layoutSnapshot = EditorLayoutEngine.Create(_width, _height, _shell.Layout, _uiState);
        }

        UpdatePixelStudioPlayback();

        _renderer?.UpdateTheme(_theme);
        _renderer?.UpdateTypography(_typography);
        _renderer?.UpdateUiState(_uiState);
        _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
        _renderer?.Render();
    }

    public void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        if (CurrentPage == EditorPageKind.PixelStudio && HandlePixelStudioTextKeyDown(key))
        {
            return;
        }

        if (_uiState.AwaitingShortcutKey)
        {
            ApplyShortcutRebind(key);
            return;
        }

        if (_uiState.ProjectForm.ActiveField != EditorTextField.None)
        {
            if (key == Key.Backspace)
            {
                RemoveLastTextCharacter();
                return;
            }

            if (key == Key.Enter && CurrentPage == EditorPageKind.Projects)
            {
                CreateProjectFromForm();
                return;
            }

            if (key == Key.Escape)
            {
                _uiState.ProjectForm.ActiveField = EditorTextField.None;
                SyncUiState("Stopped editing project field.");
                return;
            }
        }

        if (CurrentPage == EditorPageKind.Preferences)
        {
            if (key == Key.Up)
            {
                _uiState.SelectedShortcutIndex = Math.Max(_uiState.SelectedShortcutIndex - 1, 0);
                SyncUiState();
                return;
            }

            if (key == Key.Down)
            {
                _uiState.SelectedShortcutIndex = Math.Min(_uiState.SelectedShortcutIndex + 1, _shortcuts.Count - 1);
                SyncUiState();
                return;
            }

            if (key == Key.Enter)
            {
                _uiState.AwaitingShortcutKey = true;
                SyncUiState($"Rebinding {_shortcuts[_uiState.SelectedShortcutIndex].Label}. Press a new key.");
                return;
            }
        }

        if (CurrentPage == EditorPageKind.PixelStudio && HandlePixelStudioKeyDown(keyboard, key))
        {
            return;
        }

        if (MatchesShortcut(ShortcutAction.ToggleTheme, key))
        {
            ToggleTheme();
            return;
        }

        if (MatchesShortcut(ShortcutAction.CycleFontSize, key))
        {
            CycleFontSize();
            return;
        }

        if (MatchesShortcut(ShortcutAction.CycleFontFamily, key))
        {
            CycleFontFamily();
            return;
        }

        if (MatchesShortcut(ShortcutAction.TogglePreferences, key))
        {
            OpenPage(EditorPageKind.Preferences);
        }
    }

    public void OnKeyChar(IKeyboard keyboard, char character)
    {
        if (CurrentPage == EditorPageKind.PixelStudio && HandlePixelStudioTextInput(character))
        {
            return;
        }

        if (_uiState.ProjectForm.ActiveField == EditorTextField.None)
        {
            return;
        }

        if (char.IsControl(character))
        {
            return;
        }

        switch (_uiState.ProjectForm.ActiveField)
        {
            case EditorTextField.ProjectName:
                _uiState.ProjectForm.ProjectName += character;
                SyncUiState("Editing project name.");
                break;
            case EditorTextField.ProjectLibraryPath:
                _uiState.ProjectForm.ProjectLibraryPath += character;
                _uiState.ProjectForm.FolderPickerPath = _uiState.ProjectForm.ProjectLibraryPath;
                _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(_uiState.ProjectForm.ProjectLibraryPath);
                SyncUiState("Editing project library path.");
                break;
        }
    }

    public void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (_layoutSnapshot is null)
        {
            return;
        }

        float mouseX = mouse.Position.X;
        float mouseY = mouse.Position.Y;

        if (button != MouseButton.Left)
        {
            if (CurrentPage == EditorPageKind.PixelStudio && _layoutSnapshot.PixelStudio is not null)
            {
                HandlePixelStudioMouse(_layoutSnapshot.PixelStudio, mouseX, mouseY, button);
            }

            return;
        }

        if (HandleShellChromeMouseDown(mouseX, mouseY))
        {
            return;
        }

        ActionRect<EditorMenuAction>? menuEntry = _layoutSnapshot.MenuEntries.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (menuEntry is not null)
        {
            ExecuteMenuAction(menuEntry.Action);
            return;
        }

        NamedRect? menuButton = _layoutSnapshot.MenuButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (menuButton is not null)
        {
            _uiState.OpenMenuName = string.Equals(_uiState.OpenMenuName, menuButton.Id, StringComparison.Ordinal)
                ? null
                : menuButton.Id;
            SyncUiState();
            return;
        }

        NamedRect? tabButton = _layoutSnapshot.TabButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (tabButton is not null)
        {
            SelectTab(tabButton.Id);
            return;
        }

        NamedRect? tabClose = _layoutSnapshot.TabCloseButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (tabClose is not null)
        {
            CloseTab(tabClose.Id);
            return;
        }

        IndexedRect? shellRecentRow = _layoutSnapshot.LeftPanelRecentProjectRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (shellRecentRow is not null)
        {
            OpenRecentProject(shellRecentRow.Index);
            return;
        }

        switch (CurrentPage)
        {
            case EditorPageKind.Home:
                ActionRect<EditorHomeAction>? card = _layoutSnapshot.HomeCards.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                if (card is not null)
                {
                    ExecuteHomeAction(card.Action);
                    return;
                }

                IndexedRect? recentRow = _layoutSnapshot.RecentProjectRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                if (recentRow is not null)
                {
                    OpenRecentProject(recentRow.Index);
                    return;
                }

                break;
            case EditorPageKind.PixelStudio:
                if (_layoutSnapshot.PixelStudio is not null && HandlePixelStudioMouse(_layoutSnapshot.PixelStudio, mouseX, mouseY, button))
                {
                    return;
                }

                break;
            case EditorPageKind.Projects:
                ActionRect<ProjectFormAction>? projectAction = _layoutSnapshot.ProjectFormActions.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                if (projectAction is not null)
                {
                    ExecuteProjectFormAction(projectAction.Action);
                    return;
                }

                if (_layoutSnapshot.FolderPickerRect is not null)
                {
                    ActionRect<EditorFolderPickerAction>? pickerAction = _layoutSnapshot.FolderPickerActions.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                    if (pickerAction is not null)
                    {
                        ExecuteFolderPickerAction(pickerAction.Action);
                        return;
                    }

                    IndexedRect? pickerRow = _layoutSnapshot.FolderPickerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                    if (pickerRow is not null)
                    {
                        SelectFolderPickerEntry(pickerRow.Index);
                        return;
                    }
                }

                IndexedRect? projectRow = _layoutSnapshot.ProjectRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                if (projectRow is not null)
                {
                    OpenRecentProject(projectRow.Index);
                    return;
                }

                break;
            case EditorPageKind.Preferences:
                ActionRect<EditorPreferenceAction>? preferenceAction = _layoutSnapshot.PreferenceActions.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                if (preferenceAction is not null)
                {
                    ExecutePreferenceAction(preferenceAction.Action);
                    return;
                }

                IndexedRect? prefRow = _layoutSnapshot.PreferenceRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
                if (prefRow is not null)
                {
                    if (_uiState.SelectedShortcutIndex == prefRow.Index)
                    {
                        _uiState.AwaitingShortcutKey = true;
                        SyncUiState($"Rebinding {_shortcuts[prefRow.Index].Label}. Press a new key.");
                    }
                    else
                    {
                        _uiState.SelectedShortcutIndex = prefRow.Index;
                        SyncUiState();
                    }

                    return;
                }

                break;
        }

        if (!string.IsNullOrWhiteSpace(_uiState.OpenMenuName))
        {
            _uiState.OpenMenuName = null;
            SyncUiState();
        }
    }

    public void OnMouseUp(IMouse mouse, MouseButton button)
    {
        _shellDragMode = ShellDragMode.None;
        if (_layoutSnapshot?.PixelStudio is not null)
        {
            HandlePixelStudioMouseUp(button);
        }
    }

    public void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        if (_layoutSnapshot is null)
        {
            return;
        }

        if (HandleShellLayoutDrag(_layoutSnapshot, position.X))
        {
            return;
        }

        if (_layoutSnapshot.PixelStudio is null)
        {
            return;
        }

        if (HandlePixelStudioLayoutDrag(_layoutSnapshot.PixelStudio, position.X, position.Y))
        {
            return;
        }

        HandlePixelStudioMouseMove(_layoutSnapshot.PixelStudio, position.X, position.Y);
    }

    public void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        if (_layoutSnapshot is null)
        {
            return;
        }

        if (HandleShellScroll(_layoutSnapshot, mouse.Position.X, mouse.Position.Y, scrollWheel))
        {
            return;
        }

        if (_layoutSnapshot.PixelStudio is null || CurrentPage != EditorPageKind.PixelStudio)
        {
            return;
        }

        HandlePixelStudioScroll(_layoutSnapshot.PixelStudio, mouse.Position.X, mouse.Position.Y, scrollWheel);
    }

    private bool HandleShellChromeMouseDown(float mouseX, float mouseY)
    {
        if (_layoutSnapshot is null)
        {
            return false;
        }

        if (_layoutSnapshot.LeftCollapseHandleRect.Contains(mouseX, mouseY))
        {
            ToggleLeftPanelCollapse();
            return true;
        }

        if (_layoutSnapshot.RightCollapseHandleRect.Contains(mouseX, mouseY))
        {
            ToggleRightPanelCollapse();
            return true;
        }

        if (_layoutSnapshot.LeftSplitterRect.Contains(mouseX, mouseY))
        {
            _shellDragMode = ShellDragMode.ResizeLeftPanel;
            return true;
        }

        if (_layoutSnapshot.RightSplitterRect.Contains(mouseX, mouseY))
        {
            _shellDragMode = ShellDragMode.ResizeRightPanel;
            return true;
        }

        return false;
    }

    private bool HandleShellLayoutDrag(EditorLayoutSnapshot layout, float mouseX)
    {
        if (_shellDragMode == ShellDragMode.None)
        {
            return false;
        }

        switch (_shellDragMode)
        {
            case ShellDragMode.ResizeLeftPanel:
            {
                _leftPanelCollapsed = false;
                float maxWidth = Math.Max(layout.RightPanelRect.X - 280f, 220f);
                _leftPanelWidth = Math.Clamp(mouseX, 220f, maxWidth);
                _previousLeftPanelWidth = _leftPanelWidth;
                SyncUiState("Resizing navigation panel.");
                return true;
            }
            case ShellDragMode.ResizeRightPanel:
            {
                _rightPanelCollapsed = false;
                float maxWidth = Math.Max((_width - layout.LeftPanelRect.Width) - 280f, 240f);
                _rightPanelWidth = Math.Clamp(_width - mouseX, 240f, maxWidth);
                _previousRightPanelWidth = _rightPanelWidth;
                SyncUiState("Resizing inspector panel.");
                return true;
            }
            default:
                return false;
        }
    }

    private bool HandleShellScroll(EditorLayoutSnapshot layout, float mouseX, float mouseY, ScrollWheel scrollWheel)
    {
        int direction = scrollWheel.Y < 0 ? 1 : scrollWheel.Y > 0 ? -1 : 0;
        if (direction == 0)
        {
            return false;
        }

        if (layout.LeftPanelRecentViewportRect is not null && layout.LeftPanelRecentViewportRect.Value.Contains(mouseX, mouseY))
        {
            _leftPanelRecentScrollRow = Math.Max(_leftPanelRecentScrollRow + direction, 0);
            SyncUiState("Scrolling recent projects.");
            return true;
        }

        switch (CurrentPage)
        {
            case EditorPageKind.Home when layout.HomeRecentViewportRect is not null && layout.HomeRecentViewportRect.Value.Contains(mouseX, mouseY):
                _homeRecentScrollRow = Math.Max(_homeRecentScrollRow + direction, 0);
                SyncUiState("Scrolling home projects.");
                return true;
            case EditorPageKind.Projects when layout.FolderPickerViewportRect is not null && layout.FolderPickerViewportRect.Value.Contains(mouseX, mouseY):
                _folderPickerScrollRow = Math.Max(_folderPickerScrollRow + direction, 0);
                SyncUiState("Scrolling folders.");
                return true;
            case EditorPageKind.Projects when layout.ProjectsRecentViewportRect is not null && layout.ProjectsRecentViewportRect.Value.Contains(mouseX, mouseY):
                _projectRecentScrollRow = Math.Max(_projectRecentScrollRow + direction, 0);
                SyncUiState("Scrolling project list.");
                return true;
            case EditorPageKind.Preferences when layout.PreferenceViewportRect is not null && layout.PreferenceViewportRect.Value.Contains(mouseX, mouseY):
                _preferenceScrollRow = Math.Max(_preferenceScrollRow + direction, 0);
                SyncUiState("Scrolling shortcuts.");
                return true;
            default:
                return false;
        }
    }

    private void ToggleLeftPanelCollapse()
    {
        _leftPanelCollapsed = !_leftPanelCollapsed;
        if (_leftPanelCollapsed)
        {
            _previousLeftPanelWidth = Math.Max(_leftPanelWidth, 280f);
            SyncUiState("Collapsed navigation panel.");
            return;
        }

        _leftPanelWidth = Math.Max(_previousLeftPanelWidth, 280f);
        SyncUiState("Expanded navigation panel.");
    }

    private void ToggleRightPanelCollapse()
    {
        _rightPanelCollapsed = !_rightPanelCollapsed;
        if (_rightPanelCollapsed)
        {
            _previousRightPanelWidth = Math.Max(_rightPanelWidth, 320f);
            SyncUiState("Collapsed inspector panel.");
            return;
        }

        _rightPanelWidth = Math.Max(_previousRightPanelWidth, 320f);
        SyncUiState("Expanded inspector panel.");
    }

    public AppSettings CaptureSettings(AppSettings currentSettings, IWindow window)
    {
        if (!string.IsNullOrWhiteSpace(_currentPixelDocumentPath))
        {
            SavePixelStudioDocument();
        }

        WindowSettings previousWindow = currentSettings.Window.Normalize();
        int safeWidth = window.Size.X >= WindowSettings.MinimumWidth ? window.Size.X : previousWindow.Width;
        int safeHeight = window.Size.Y >= WindowSettings.MinimumHeight ? window.Size.Y : previousWindow.Height;

        WindowSettings windowSettings = new()
        {
            Title = EditorBranding.EngineName,
            Width = safeWidth,
            Height = safeHeight,
            StartMaximized = window.WindowState == WindowState.Maximized
        };

        EditorSettings editorSettings = new()
        {
            ThemeName = _theme.Name,
            PreferredFontFamily = _preferredFontFamily,
            FontSizePreset = _fontSizePreset,
            Shortcuts = BuildShortcutBindings(),
            ProjectLibraryPath = _projectLibraryPath,
            RecentProjects = _recentProjects.Take(10).ToList(),
            LastProjectPath = _lastProjectPath ?? currentSettings.Editor.LastProjectPath,
            Layout = new EditorLayoutSettings
            {
                LeftPanelWidth = _leftPanelCollapsed ? Math.Max(_previousLeftPanelWidth, _leftPanelWidth) : _leftPanelWidth,
                RightPanelWidth = _rightPanelCollapsed ? Math.Max(_previousRightPanelWidth, _rightPanelWidth) : _rightPanelWidth,
                LeftPanelCollapsed = _leftPanelCollapsed,
                RightPanelCollapsed = _rightPanelCollapsed,
                PixelToolsPanelWidth = _pixelToolsCollapsed ? Math.Max(_previousPixelToolsPanelWidth, _pixelToolsPanelWidth) : _pixelToolsPanelWidth,
                PixelSidebarWidth = _pixelSidebarCollapsed ? Math.Max(_previousPixelSidebarWidth, _pixelSidebarWidth) : _pixelSidebarWidth,
                PixelToolsPanelCollapsed = _pixelToolsCollapsed,
                PixelSidebarCollapsed = _pixelSidebarCollapsed,
                PixelTimelineVisible = _pixelTimelineVisible,
                PixelToolSettingsOffsetX = float.IsFinite(_pixelToolSettingsPanelOffsetX) ? _pixelToolSettingsPanelOffsetX : null,
                PixelToolSettingsOffsetY = float.IsFinite(_pixelToolSettingsPanelOffsetY) ? _pixelToolSettingsPanelOffsetY : null
            },
            PixelPalettes = _savedPixelPalettes.Select(CloneSavedPixelPalette).ToList(),
            ActivePixelPaletteId = _activePixelPaletteId,
            PromptForPaletteGenerationAfterImport = _promptForPaletteGenerationAfterImport
        };

        return new AppSettings
        {
            Window = windowSettings,
            Editor = editorSettings
        };
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private EditorPageKind CurrentPage =>
        _tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;

    private string CurrentThemeLabel =>
        string.Equals(_theme.Name, EditorThemeCatalog.DarkThemeName, StringComparison.OrdinalIgnoreCase)
            ? "Dark"
            : string.Equals(_theme.Name, EditorThemeCatalog.LightThemeName, StringComparison.OrdinalIgnoreCase)
                ? "Light"
                : string.Equals(_theme.Name, EditorThemeCatalog.KearuThemeName, StringComparison.OrdinalIgnoreCase)
                    ? "Kearu"
                    : "Kuma";

    private void SyncUiState(string? overrideStatus = null)
    {
        _uiState.ThemeLabel = CurrentThemeLabel;
        _uiState.FontFamily = _preferredFontFamily;
        _uiState.FontSizeLabel = _fontSizePreset.ToString();
        _uiState.ProjectLibraryPath = _projectLibraryPath;
        _uiState.LeftPanelPreferredWidth = _leftPanelWidth;
        _uiState.RightPanelPreferredWidth = _rightPanelWidth;
        _uiState.LeftPanelCollapsed = _leftPanelCollapsed;
        _uiState.RightPanelCollapsed = _rightPanelCollapsed;
        _uiState.LeftPanelRecentScrollRow = _leftPanelRecentScrollRow;
        _uiState.HomeRecentScrollRow = _homeRecentScrollRow;
        _uiState.ProjectRecentScrollRow = _projectRecentScrollRow;
        _uiState.PreferenceScrollRow = _preferenceScrollRow;
        _uiState.FolderPickerScrollRow = _folderPickerScrollRow;
        _uiState.MenuItems = _shell.MenuItems;
        _uiState.Tabs = _tabs.ToList();
        _uiState.RecentProjects = _recentProjects.ToList();
        _uiState.Shortcuts = _shortcuts;
        _uiState.PixelStudio = BuildPixelStudioViewState();
        _uiState.PreferencesVisible = CurrentPage == EditorPageKind.Preferences;
        if (string.IsNullOrWhiteSpace(_uiState.ProjectForm.ProjectLibraryPath))
        {
            _uiState.ProjectForm.ProjectLibraryPath = _projectLibraryPath;
        }
        if (string.IsNullOrWhiteSpace(_uiState.ProjectForm.FolderPickerPath))
        {
            _uiState.ProjectForm.FolderPickerPath = _projectLibraryPath;
        }
        _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(_uiState.ProjectForm.FolderPickerPath);

        string shortcutSummary = CurrentPage == EditorPageKind.PixelStudio
            ? $"{EditorBranding.PixelToolName}: Ctrl+Z Undo | Ctrl+Y Redo | Wheel zooms | Middle drag pans | Right-click layers and palettes for options"
            : $"Theme: {GetKeyLabel(ShortcutAction.ToggleTheme)} | Size: {GetKeyLabel(ShortcutAction.CycleFontSize)} | Font: {GetKeyLabel(ShortcutAction.CycleFontFamily)} | Prefs: {GetKeyLabel(ShortcutAction.TogglePreferences)}";
        _uiState.StatusText = overrideStatus ?? shortcutSummary;
        _shell.SetStatus(_uiState.StatusText);

        if (_width > 0 && _height > 0)
        {
            _layoutSnapshot = EditorLayoutEngine.Create(_width, _height, _shell.Layout, _uiState);
            _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
        }
    }

    private void UpdateWindowTitle()
    {
        if (_window is null)
        {
            return;
        }

        string tabTitle = _tabs.FirstOrDefault(tab => tab.Id == _uiState.SelectedTabId)?.Title ?? "Home";
        _window.Title = $"{EditorBranding.EngineName} [{CurrentThemeLabel}] - {tabTitle}";
    }

    private void ApplyShortcutRebind(Key key)
    {
        if (key == Key.Escape)
        {
            _uiState.AwaitingShortcutKey = false;
            SyncUiState("Shortcut rebind cancelled.");
            return;
        }

        _shortcuts[_uiState.SelectedShortcutIndex].Key = key;
        _uiState.AwaitingShortcutKey = false;
        SyncUiState($"Updated {_shortcuts[_uiState.SelectedShortcutIndex].Label} to {key}.");
        _renderer?.InvalidateTextCache();
    }

    private void ToggleTheme()
    {
        _theme = EditorThemeCatalog.Toggle(_theme);
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        SyncUiState();
        UpdateWindowTitle();
        _renderer?.InvalidateTextCache();
    }

    private void CycleFontSize()
    {
        _fontSizePreset = EditorTypographyCatalog.NextSize(_fontSizePreset);
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        SyncUiState();
        _renderer?.InvalidateTextCache();
    }

    private void CycleFontFamily()
    {
        IReadOnlyList<string> availableFonts = GetAvailableFontFamilies();
        int index = availableFonts
            .Select((font, idx) => new { font, idx })
            .FirstOrDefault(entry => string.Equals(entry.font, _preferredFontFamily, StringComparison.OrdinalIgnoreCase))
            ?.idx ?? -1;

        int nextIndex = (index + 1 + availableFonts.Count) % availableFonts.Count;
        _preferredFontFamily = availableFonts[nextIndex];
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        SyncUiState();
        _renderer?.InvalidateTextCache();
    }

    private void ExecuteMenuAction(EditorMenuAction action)
    {
        _uiState.OpenMenuName = null;

        switch (action)
        {
            case EditorMenuAction.OpenHome:
                OpenPage(EditorPageKind.Home);
                break;
            case EditorMenuAction.OpenPixelStudio:
                OpenPage(EditorPageKind.PixelStudio);
                break;
            case EditorMenuAction.OpenProjects:
                OpenPage(EditorPageKind.Projects);
                break;
            case EditorMenuAction.OpenLayout:
                OpenPage(EditorPageKind.Layout);
                break;
            case EditorMenuAction.OpenPreferences:
                OpenPage(EditorPageKind.Preferences);
                break;
            case EditorMenuAction.CreateProjectSlot:
                CreateProjectSlot();
                break;
            case EditorMenuAction.OpenProjectLibrary:
                OpenPage(EditorPageKind.Projects, $"Project Library: {_projectLibraryPath}");
                break;
            case EditorMenuAction.NewScratchTab:
                CreateScratchTab();
                break;
            case EditorMenuAction.ToggleTheme:
                ToggleTheme();
                break;
            case EditorMenuAction.CycleFontSize:
                CycleFontSize();
                break;
            case EditorMenuAction.CycleFontFamily:
                CycleFontFamily();
                break;
        }
    }

    private void ExecuteHomeAction(EditorHomeAction action)
    {
        switch (action)
        {
            case EditorHomeAction.CreateProjectSlot:
                CreateProjectSlot();
                break;
            case EditorHomeAction.OpenPixelStudio:
                OpenPage(EditorPageKind.PixelStudio);
                break;
            case EditorHomeAction.OpenProjects:
                OpenPage(EditorPageKind.Projects);
                break;
            case EditorHomeAction.OpenPreferences:
                OpenPage(EditorPageKind.Preferences);
                break;
            case EditorHomeAction.NewScratchTab:
                CreateScratchTab();
                break;
        }
    }

    private void CreateProjectSlot()
    {
        Directory.CreateDirectory(_projectLibraryPath);

        int projectNumber = _recentProjects.Count + 1;
        string projectName = $"Project-{projectNumber:00}";
        string projectPath = Path.Combine(_projectLibraryPath, projectName);
        while (Directory.Exists(projectPath))
        {
            projectNumber++;
            projectName = $"Project-{projectNumber:00}";
            projectPath = Path.Combine(_projectLibraryPath, projectName);
        }

        Directory.CreateDirectory(projectPath);

        RecentProjectEntry entry = new()
        {
            Name = projectName,
            Path = projectPath
        };

        AddRecentProject(entry);
        _lastProjectPath = entry.Path;
        ReplacePixelStudioDocument(CreateBlankPixelStudio(32, 32));
        _pixelStudio.DocumentName = entry.Name;
        _currentPixelDocumentPath = null;
        OpenPage(EditorPageKind.Projects, $"Created {projectName}");
    }

    private void OpenRecentProject(int index)
    {
        if (index < 0 || index >= _recentProjects.Count)
        {
            return;
        }

        RecentProjectEntry project = _recentProjects[index];
        AddRecentProject(project);
        _lastProjectPath = project.Path;
        _currentPixelDocumentPath = FindPreferredPixelStudioDocument(project.Path);
        string status = !string.IsNullOrWhiteSpace(_currentPixelDocumentPath) && TryLoadPixelStudioDocument(_currentPixelDocumentPath, out string loadStatus)
            ? loadStatus
            : $"Selected {project.Name}";
        OpenPage(EditorPageKind.Projects, status);
    }

    private void AddRecentProject(RecentProjectEntry project)
    {
        _recentProjects.RemoveAll(existing => string.Equals(existing.Path, project.Path, StringComparison.OrdinalIgnoreCase));
        _recentProjects.Insert(0, project);

        if (_recentProjects.Count > 10)
        {
            _recentProjects.RemoveRange(10, _recentProjects.Count - 10);
        }
    }

    private void CreateProjectFromForm()
    {
        string projectName = SanitizeProjectName(_uiState.ProjectForm.ProjectName);
        string libraryPath = NormalizeDirectoryPath(_uiState.ProjectForm.ProjectLibraryPath);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            SyncUiState("Project name cannot be empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            SyncUiState("Project library path cannot be empty.");
            return;
        }

        Directory.CreateDirectory(libraryPath);
        string projectPath = Path.Combine(libraryPath, projectName);
        int suffix = 1;
        while (Directory.Exists(projectPath))
        {
            suffix++;
            projectPath = Path.Combine(libraryPath, $"{projectName}-{suffix:00}");
        }

        Directory.CreateDirectory(projectPath);
        _projectLibraryPath = libraryPath;

        RecentProjectEntry entry = new()
        {
            Name = Path.GetFileName(projectPath),
            Path = projectPath
        };

        AddRecentProject(entry);
        _lastProjectPath = entry.Path;
        ReplacePixelStudioDocument(CreateBlankPixelStudio(32, 32));
        _pixelStudio.DocumentName = entry.Name;
        _currentPixelDocumentPath = null;
        _uiState.ProjectForm.ProjectName = $"{projectName}{suffix switch { > 1 => suffix.ToString(), _ => string.Empty }}";
        _uiState.ProjectForm.ActiveField = EditorTextField.None;
        _uiState.ProjectForm.FolderPickerVisible = false;
        OpenPage(EditorPageKind.Projects, $"Created {entry.Name}");
    }

    private void OpenPage(EditorPageKind page, string? status = null)
    {
        EditorWorkspaceTab? existing = _tabs.FirstOrDefault(tab => tab.Page == page && page != EditorPageKind.Scratch);
        if (existing is not null)
        {
            SelectTab(existing.Id, status);
            return;
        }

        if (page == EditorPageKind.Scratch)
        {
            CreateScratchTab(status);
            return;
        }

        EditorWorkspaceTab tab = CreateTab(page, GetDefaultTabTitle(page), Guid.NewGuid().ToString("N"));
        _tabs.Add(tab);
        SelectTab(tab.Id, status);
    }

    private void CreateScratchTab(string? status = null)
    {
        _scratchCounter++;
        EditorWorkspaceTab scratchTab = CreateTab(EditorPageKind.Scratch, $"Scratch {_scratchCounter}", $"scratch-{_scratchCounter}");
        _tabs.Add(scratchTab);
        SelectTab(scratchTab.Id, status ?? $"Opened {scratchTab.Title}");
    }

    private void SelectTab(string tabId, string? status = null)
    {
        _uiState.SelectedTabId = tabId;
        _uiState.OpenMenuName = null;
        _uiState.AwaitingShortcutKey = false;
        SyncUiState(status);
        UpdateWindowTitle();
        _renderer?.InvalidateTextCache();
    }

    private void CloseTab(string tabId)
    {
        EditorWorkspaceTab? tab = _tabs.FirstOrDefault(item => item.Id == tabId);
        if (tab is null || tab.Page != EditorPageKind.Scratch)
        {
            return;
        }

        bool wasSelected = string.Equals(_uiState.SelectedTabId, tabId, StringComparison.Ordinal);
        _tabs.Remove(tab);

        if (wasSelected)
        {
            _uiState.SelectedTabId = _tabs.First().Id;
        }

        SyncUiState($"Closed {tab.Title}");
        UpdateWindowTitle();
        _renderer?.InvalidateTextCache();
    }

    private bool MatchesShortcut(ShortcutAction action, Key key)
    {
        return _shortcuts.First(binding => binding.Action == action).Key == key;
    }

    private string GetKeyLabel(ShortcutAction action)
    {
        return _shortcuts.First(binding => binding.Action == action).Key.ToString();
    }

    private ShortcutBindings BuildShortcutBindings()
    {
        return new ShortcutBindings
        {
            ToggleTheme = GetKeyLabel(ShortcutAction.ToggleTheme),
            CycleFontSize = GetKeyLabel(ShortcutAction.CycleFontSize),
            CycleFontFamily = GetKeyLabel(ShortcutAction.CycleFontFamily),
            TogglePreferences = GetKeyLabel(ShortcutAction.TogglePreferences)
        };
    }

    private static List<EditorShortcutBinding> CreateShortcuts(ShortcutBindings shortcuts)
    {
        return
        [
                new EditorShortcutBinding
                {
                    Action = ShortcutAction.ToggleTheme,
                    Label = "Toggle Theme",
                    Description = "Cycle between Dark, Light, Kuma, and Kearu editor themes.",
                    Key = ParseKey(shortcuts.ToggleTheme, Key.F6)
                },
            new EditorShortcutBinding
            {
                Action = ShortcutAction.CycleFontSize,
                Label = "Cycle Text Size",
                Description = "Rotate text sizing between Small, Medium, and Large.",
                Key = ParseKey(shortcuts.CycleFontSize, Key.F7)
            },
            new EditorShortcutBinding
            {
                Action = ShortcutAction.CycleFontFamily,
                Label = "Cycle Font Family",
                Description = "Rotate through the editor's readable font list.",
                Key = ParseKey(shortcuts.CycleFontFamily, Key.F8)
            },
            new EditorShortcutBinding
            {
                Action = ShortcutAction.TogglePreferences,
                Label = "Toggle Preferences",
                Description = "Show or hide the preferences view.",
                Key = ParseKey(shortcuts.TogglePreferences, Key.F9)
            }
        ];
    }

    private static Key ParseKey(string value, Key fallback)
    {
        return Enum.TryParse(value, true, out Key parsedKey)
            ? parsedKey
            : fallback;
    }

    private static string GetDefaultProjectLibraryPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), EditorBranding.DefaultProjectLibraryName);
    }

    private static string ResolveAvailableFontFamily(string? preferredFontFamily)
    {
        IReadOnlyList<string> availableFonts = GetAvailableFontFamilies();
        if (!string.IsNullOrWhiteSpace(preferredFontFamily)
            && availableFonts.Any(font => string.Equals(font, preferredFontFamily, StringComparison.OrdinalIgnoreCase)))
        {
            return availableFonts.First(font => string.Equals(font, preferredFontFamily, StringComparison.OrdinalIgnoreCase));
        }

        return availableFonts[0];
    }

    private static IReadOnlyList<string> GetAvailableFontFamilies()
    {
        List<string> available = EditorTypographyCatalog.ReadableFontCandidates
            .Where(font => SystemFonts.TryGet(font, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (available.Count > 0)
        {
            return available;
        }

        FontFamily fallbackFamily = SystemFonts.Collection.Families.First();
        return [fallbackFamily.Name];
    }

    private void ExecuteProjectFormAction(ProjectFormAction action)
    {
        switch (action)
        {
            case ProjectFormAction.ActivateProjectName:
                _uiState.ProjectForm.ActiveField = EditorTextField.ProjectName;
                SyncUiState("Editing project name.");
                break;
            case ProjectFormAction.ActivateProjectLibraryPath:
                _uiState.ProjectForm.ActiveField = EditorTextField.ProjectLibraryPath;
                SyncUiState("Editing project library path.");
                break;
            case ProjectFormAction.CreateProject:
                CreateProjectFromForm();
                break;
            case ProjectFormAction.UseDocumentsFolder:
                SetProjectLibraryPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                break;
            case ProjectFormAction.UseDesktopFolder:
                SetProjectLibraryPath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                break;
            case ProjectFormAction.OpenFolderPicker:
                _uiState.ProjectForm.FolderPickerVisible = !_uiState.ProjectForm.FolderPickerVisible;
                _uiState.ProjectForm.FolderPickerPath = NormalizeDirectoryPath(_uiState.ProjectForm.ProjectLibraryPath);
                _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(_uiState.ProjectForm.FolderPickerPath);
                SyncUiState(_uiState.ProjectForm.FolderPickerVisible ? "Opened folder picker." : "Closed folder picker.");
                break;
        }
    }

    private void ExecuteFolderPickerAction(EditorFolderPickerAction action)
    {
        switch (action)
        {
            case EditorFolderPickerAction.NavigateUp:
                string current = NormalizeDirectoryPath(_uiState.ProjectForm.FolderPickerPath);
                DirectoryInfo? parent = Directory.GetParent(current);
                if (parent is not null)
                {
                    _uiState.ProjectForm.FolderPickerPath = parent.FullName;
                    _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(parent.FullName);
                    SyncUiState($"Browsing {parent.FullName}");
                }
                break;
            case EditorFolderPickerAction.SelectCurrent:
                SetProjectLibraryPath(_uiState.ProjectForm.FolderPickerPath);
                _uiState.ProjectForm.FolderPickerVisible = false;
                SyncUiState("Selected folder for project library.");
                break;
        }
    }

    private void ExecutePreferenceAction(EditorPreferenceAction action)
    {
        switch (action)
        {
            case EditorPreferenceAction.ToggleTheme:
                ToggleTheme();
                break;
            case EditorPreferenceAction.CycleFontSize:
                CycleFontSize();
                break;
            case EditorPreferenceAction.CycleFontFamily:
                CycleFontFamily();
                break;
        }
    }

    private void SelectFolderPickerEntry(int index)
    {
        if (index < 0 || index >= _uiState.ProjectForm.FolderPickerEntries.Count)
        {
            return;
        }

        string selected = _uiState.ProjectForm.FolderPickerEntries[index];
        _uiState.ProjectForm.FolderPickerPath = selected;
        _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(selected);
        SyncUiState($"Browsing {selected}");
    }

    private void SetProjectLibraryPath(string path)
    {
        string normalized = NormalizeDirectoryPath(path);
        _projectLibraryPath = normalized;
        _uiState.ProjectForm.ProjectLibraryPath = normalized;
        _uiState.ProjectForm.FolderPickerPath = normalized;
        _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(normalized);
        _uiState.ProjectForm.ActiveField = EditorTextField.None;
        SyncUiState($"Project library set to {normalized}");
    }

    private void RemoveLastTextCharacter()
    {
        switch (_uiState.ProjectForm.ActiveField)
        {
            case EditorTextField.ProjectName:
                if (_uiState.ProjectForm.ProjectName.Length > 0)
                {
                    _uiState.ProjectForm.ProjectName = _uiState.ProjectForm.ProjectName[..^1];
                    SyncUiState("Editing project name.");
                }
                break;
            case EditorTextField.ProjectLibraryPath:
                if (_uiState.ProjectForm.ProjectLibraryPath.Length > 0)
                {
                    _uiState.ProjectForm.ProjectLibraryPath = _uiState.ProjectForm.ProjectLibraryPath[..^1];
                    _uiState.ProjectForm.FolderPickerPath = NormalizeDirectoryPath(_uiState.ProjectForm.ProjectLibraryPath);
                    _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(_uiState.ProjectForm.FolderPickerPath);
                    SyncUiState("Editing project library path.");
                }
                break;
        }
    }

    private static string SanitizeProjectName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "MyGame" : sanitized;
    }

    private static string NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return GetDefaultProjectLibraryPath();
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return GetDefaultProjectLibraryPath();
        }
    }

    private static IReadOnlyList<string> GetDirectoryEntries(string path)
    {
        try
        {
            string normalized = NormalizeDirectoryPath(path);
            if (!Directory.Exists(normalized))
            {
                Directory.CreateDirectory(normalized);
            }

            return Directory.GetDirectories(normalized)
                .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<EditorWorkspaceTab> CreateDefaultTabs()
    {
        return
        [
            CreateTab(EditorPageKind.Home, "Home", "home"),
            CreateTab(EditorPageKind.PixelStudio, EditorBranding.PixelToolName, "pixel-studio"),
            CreateTab(EditorPageKind.Projects, "Projects", "projects"),
            CreateTab(EditorPageKind.Layout, "Layout", "layout"),
            CreateTab(EditorPageKind.Preferences, "Preferences", "preferences")
        ];
    }

    private static EditorWorkspaceTab CreateTab(EditorPageKind page, string title, string id)
    {
        return new EditorWorkspaceTab
        {
            Id = id,
            Title = title,
            Page = page
        };
    }

    private static string GetDefaultTabTitle(EditorPageKind page)
    {
        return page switch
        {
            EditorPageKind.Home => "Home",
            EditorPageKind.PixelStudio => EditorBranding.PixelToolName,
            EditorPageKind.Projects => "Projects",
            EditorPageKind.Layout => "Layout",
            EditorPageKind.Preferences => "Preferences",
            EditorPageKind.Scratch => "Scratch",
            _ => "Page"
        };
    }

    // Pixel Studio logic lives in EditorWindowScene.PixelStudio.cs.
}

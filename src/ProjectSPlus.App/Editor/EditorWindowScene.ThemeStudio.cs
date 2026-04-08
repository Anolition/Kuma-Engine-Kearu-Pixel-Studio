using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Themes;
using Silk.NET.Input;

namespace ProjectSPlus.App.Editor;

public sealed partial class EditorWindowScene
{
    private enum ThemeStudioAdjustMode
    {
        None,
        WheelHue,
        WheelField
    }

    private readonly Dictionary<EditorThemeColorRole, ThemeColor> _themeStudioColors = [];
    private EditorTheme? _themeStudioOriginalTheme;
    private string? _themeStudioEditingThemeId;
    private string _themeStudioNameBuffer = string.Empty;
    private EditorThemeColorRole _themeStudioSelectedRole = EditorThemeColorRole.Accent;
    private bool _themeStudioVisible;
    private bool _themeStudioNameActive;
    private ThemeStudioAdjustMode _themeStudioAdjustMode;

    private EditorThemeStudioViewState BuildThemeStudioViewState()
    {
        return new EditorThemeStudioViewState
        {
            Visible = _themeStudioVisible,
            ThemeName = _themeStudioNameBuffer,
            ThemeNameActive = _themeStudioNameActive,
            ThemeNameSelected = _themeStudioNameActive && IsTextSelected(EditableTextTarget.ThemeStudioName),
            CanDelete = !string.IsNullOrWhiteSpace(_themeStudioEditingThemeId),
            SaveLabel = string.IsNullOrWhiteSpace(_themeStudioEditingThemeId) ? "Save Theme" : "Update Theme",
            SelectedRole = _themeStudioSelectedRole,
            SelectedRoleLabel = GetThemeRoleLabel(_themeStudioSelectedRole),
            SelectedColor = GetThemeStudioRoleColor(_themeStudioSelectedRole),
            Roles = Enum.GetValues<EditorThemeColorRole>()
                .Select(role => new EditorThemeRoleView
                {
                    Role = role,
                    Label = GetThemeRoleLabel(role),
                    Color = GetThemeStudioRoleColor(role),
                    IsSelected = role == _themeStudioSelectedRole
                })
                .ToList()
        };
    }

    private void OpenThemeStudio()
    {
        _themeStudioOriginalTheme = CloneEditorTheme(_theme);
        _themeStudioEditingThemeId = _customThemes.Any(theme => string.Equals(theme.Id, _theme.Name, StringComparison.Ordinal))
            ? _theme.Name
            : null;
        _themeStudioNameBuffer = !string.IsNullOrWhiteSpace(_themeStudioEditingThemeId)
            ? _theme.DisplayName
            : BuildNextCustomThemeName();
        _themeStudioSelectedRole = EditorThemeColorRole.Accent;
        _themeStudioNameActive = false;
        _themeStudioAdjustMode = ThemeStudioAdjustMode.None;
        _themeStudioVisible = true;
        ClearSelectedText(EditableTextTarget.ThemeStudioName);
        LoadThemeStudioColors(_theme);
        SyncUiState("Opened theme studio.");
        _renderer?.InvalidateTextCache();
    }

    private bool HandleThemeStudioMouseDown(EditorLayoutSnapshot layout, float mouseX, float mouseY, MouseButton button)
    {
        if (!_themeStudioVisible || CurrentPage != EditorPageKind.Preferences)
        {
            return false;
        }

        if (button != MouseButton.Left)
        {
            return true;
        }

        if (layout.ThemeStudioDialogRect is null)
        {
            return true;
        }

        if (!layout.ThemeStudioDialogRect.Value.Contains(mouseX, mouseY))
        {
            CloseThemeStudio(restoreOriginalTheme: true, "Closed theme studio.");
            return true;
        }

        if (layout.ThemeStudioNameFieldRect is not null && layout.ThemeStudioNameFieldRect.Value.Contains(mouseX, mouseY))
        {
            _themeStudioNameActive = true;
            SelectAllText(EditableTextTarget.ThemeStudioName);
            SyncUiState("Editing custom theme name.");
            return true;
        }

        _themeStudioNameActive = false;
        ClearSelectedText(EditableTextTarget.ThemeStudioName);

        ActionRect<EditorThemeStudioAction>? buttonHit = layout.ThemeStudioButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (buttonHit is not null)
        {
            ExecuteThemeStudioAction(buttonHit.Action);
            return true;
        }

        ActionRect<EditorThemeColorRole>? roleButton = layout.ThemeStudioRoleButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (roleButton is not null)
        {
            _themeStudioSelectedRole = roleButton.Action;
            SyncUiState($"Editing {GetThemeRoleLabel(roleButton.Action)} color.");
            return true;
        }

        if (layout.ThemeStudioWheelRect is not null
            && layout.ThemeStudioWheelFieldRect is not null
            && layout.ThemeStudioWheelRect.Value.Contains(mouseX, mouseY)
            && TryBeginThemeStudioWheelAdjustment(layout.ThemeStudioWheelRect.Value, layout.ThemeStudioWheelFieldRect.Value, mouseX, mouseY))
        {
            return true;
        }

        return true;
    }

    private void HandleThemeStudioMouseMove(EditorLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (!_themeStudioVisible || CurrentPage != EditorPageKind.Preferences)
        {
            return;
        }

        if (_themeStudioAdjustMode == ThemeStudioAdjustMode.None)
        {
            return;
        }

        if (layout.ThemeStudioWheelRect is null || layout.ThemeStudioWheelFieldRect is null)
        {
            return;
        }

        switch (_themeStudioAdjustMode)
        {
            case ThemeStudioAdjustMode.WheelHue:
                UpdateThemeStudioColorFromHue(layout.ThemeStudioWheelRect.Value, mouseX, mouseY);
                break;
            case ThemeStudioAdjustMode.WheelField:
                UpdateThemeStudioColorFromField(layout.ThemeStudioWheelFieldRect.Value, mouseX, mouseY);
                break;
        }
    }

    private void HandleThemeStudioMouseUp(MouseButton button)
    {
        if (!_themeStudioVisible || button != MouseButton.Left)
        {
            return;
        }

        _themeStudioAdjustMode = ThemeStudioAdjustMode.None;
    }

    private bool HandleThemeStudioKeyDown(Key key)
    {
        if (!_themeStudioVisible)
        {
            return false;
        }

        switch (key)
        {
            case Key.Backspace when _themeStudioNameActive:
                HandleTextBackspace(EditableTextTarget.ThemeStudioName);
                return true;
            case Key.Enter:
                SaveThemeStudio();
                return true;
            case Key.Escape:
                CloseThemeStudio(restoreOriginalTheme: true, "Cancelled theme studio changes.");
                return true;
            case Key.Tab:
                _themeStudioNameActive = !_themeStudioNameActive;
                if (_themeStudioNameActive)
                {
                    SelectAllText(EditableTextTarget.ThemeStudioName);
                }
                else
                {
                    ClearSelectedText(EditableTextTarget.ThemeStudioName);
                }
                SyncUiState(_themeStudioNameActive ? "Editing custom theme name." : "Theme studio ready.");
                return true;
            default:
                return true;
        }
    }

    private bool HandleThemeStudioTextInput(char character)
    {
        if (!_themeStudioVisible || !_themeStudioNameActive || char.IsControl(character))
        {
            return false;
        }

        if (_themeStudioNameBuffer.Length >= 32)
        {
            return true;
        }

        if (ConsumeSelectedText(EditableTextTarget.ThemeStudioName))
        {
            _themeStudioNameBuffer = string.Empty;
        }

        _themeStudioNameBuffer += character;
        ApplyThemeStudioPreview("Editing custom theme name.");
        return true;
    }

    private void ExecuteThemeStudioAction(EditorThemeStudioAction action)
    {
        switch (action)
        {
            case EditorThemeStudioAction.ActivateThemeNameField:
                _themeStudioNameActive = true;
                SelectAllText(EditableTextTarget.ThemeStudioName);
                SyncUiState("Editing custom theme name.");
                break;
            case EditorThemeStudioAction.SaveTheme:
                SaveThemeStudio();
                break;
            case EditorThemeStudioAction.CancelThemeStudio:
                CloseThemeStudio(restoreOriginalTheme: true, "Cancelled theme studio changes.");
                break;
            case EditorThemeStudioAction.DeleteTheme:
                DeleteThemeStudio();
                break;
        }
    }

    private void SaveThemeStudio()
    {
        string requestedName = SanitizeThemeStudioName(_themeStudioNameBuffer);
        string resolvedName = EnsureUniqueCustomThemeName(requestedName, _themeStudioEditingThemeId);
        string themeId = _themeStudioEditingThemeId ?? $"ProjectSPlus.Custom.{Guid.NewGuid():N}";
        SavedEditorTheme savedTheme = BuildSavedEditorTheme(themeId, resolvedName);
        int existingIndex = _customThemes.FindIndex(theme => string.Equals(theme.Id, themeId, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            _customThemes[existingIndex] = savedTheme;
        }
        else
        {
            _customThemes.Add(savedTheme);
        }

        _themeStudioEditingThemeId = themeId;
        _themeStudioNameBuffer = resolvedName;
        _theme = EditorThemeCatalog.GetByName(themeId, _customThemes);
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        _themeStudioOriginalTheme = CloneEditorTheme(_theme);
        _themeStudioVisible = false;
        _themeStudioNameActive = false;
        _themeStudioAdjustMode = ThemeStudioAdjustMode.None;
        ClearSelectedText(EditableTextTarget.ThemeStudioName);
        SyncUiState($"Saved custom theme {resolvedName}.");
        UpdateWindowTitle();
        _renderer?.InvalidateTextCache();
    }

    private void DeleteThemeStudio()
    {
        if (string.IsNullOrWhiteSpace(_themeStudioEditingThemeId))
        {
            return;
        }

        SavedEditorTheme? existingTheme = _customThemes.FirstOrDefault(theme => string.Equals(theme.Id, _themeStudioEditingThemeId, StringComparison.Ordinal));
        _customThemes.RemoveAll(theme => string.Equals(theme.Id, _themeStudioEditingThemeId, StringComparison.Ordinal));
        _themeStudioEditingThemeId = null;
        _themeStudioVisible = false;
        _themeStudioNameActive = false;
        _themeStudioAdjustMode = ThemeStudioAdjustMode.None;
        ClearSelectedText(EditableTextTarget.ThemeStudioName);
        _theme = EditorThemeCatalog.GetByName(EditorThemeCatalog.KumaThemeName, _customThemes);
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        SyncUiState(existingTheme is null
            ? "Deleted custom theme."
            : $"Deleted custom theme {existingTheme.Name}.");
        UpdateWindowTitle();
        _renderer?.InvalidateTextCache();
    }

    private void CloseThemeStudio(bool restoreOriginalTheme, string status)
    {
        if (restoreOriginalTheme && _themeStudioOriginalTheme is not null)
        {
            _theme = CloneEditorTheme(_themeStudioOriginalTheme);
            _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        }

        _themeStudioVisible = false;
        _themeStudioNameActive = false;
        _themeStudioAdjustMode = ThemeStudioAdjustMode.None;
        ClearSelectedText(EditableTextTarget.ThemeStudioName);
        SyncUiState(status);
        UpdateWindowTitle();
        _renderer?.InvalidateTextCache();
    }

    private bool TryBeginThemeStudioWheelAdjustment(UiRect wheelRect, UiRect fieldRect, float mouseX, float mouseY)
    {
        if (fieldRect.Contains(mouseX, mouseY))
        {
            _themeStudioAdjustMode = ThemeStudioAdjustMode.WheelField;
            UpdateThemeStudioColorFromField(fieldRect, mouseX, mouseY);
            return true;
        }

        float centerX = wheelRect.X + (wheelRect.Width * 0.5f);
        float centerY = wheelRect.Y + (wheelRect.Height * 0.5f);
        float outerRadius = MathF.Min(wheelRect.Width, wheelRect.Height) * 0.5f;
        float innerFieldRadius = MathF.Sqrt((fieldRect.Width * 0.5f * fieldRect.Width * 0.5f) + (fieldRect.Height * 0.5f * fieldRect.Height * 0.5f));
        float deltaX = mouseX - centerX;
        float deltaY = mouseY - centerY;
        float distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance < innerFieldRadius || distance > outerRadius)
        {
            return false;
        }

        _themeStudioAdjustMode = ThemeStudioAdjustMode.WheelHue;
        UpdateThemeStudioColorFromHue(wheelRect, mouseX, mouseY);
        return true;
    }

    private void UpdateThemeStudioColorFromHue(UiRect wheelRect, float mouseX, float mouseY)
    {
        float centerX = wheelRect.X + (wheelRect.Width * 0.5f);
        float centerY = wheelRect.Y + (wheelRect.Height * 0.5f);
        float hue = MathF.Atan2(mouseY - centerY, mouseX - centerX) * 180f / MathF.PI;
        if (hue < 0f)
        {
            hue += 360f;
        }

        (float _, float saturation, float value) = ToHsv(GetThemeStudioRoleColor(_themeStudioSelectedRole));
        SetThemeStudioRoleColor(_themeStudioSelectedRole, FromHsv(hue, saturation, value));
        ApplyThemeStudioPreview("Updated custom theme color.");
    }

    private void UpdateThemeStudioColorFromField(UiRect fieldRect, float mouseX, float mouseY)
    {
        float relativeX = Math.Clamp((mouseX - fieldRect.X) / Math.Max(fieldRect.Width, 1f), 0f, 1f);
        float relativeY = Math.Clamp((mouseY - fieldRect.Y) / Math.Max(fieldRect.Height, 1f), 0f, 1f);
        (float hue, _, _) = ToHsv(GetThemeStudioRoleColor(_themeStudioSelectedRole));
        SetThemeStudioRoleColor(_themeStudioSelectedRole, FromHsv(hue, relativeX, 1f - relativeY));
        ApplyThemeStudioPreview("Updated custom theme color.");
    }

    private void ApplyThemeStudioPreview(string? status = null)
    {
        if (!_themeStudioVisible)
        {
            return;
        }

        _theme = BuildThemeStudioPreviewTheme(_themeStudioEditingThemeId ?? "ProjectSPlus.Custom.Preview");
        _typography = EditorTypographyCatalog.Create(_theme, _preferredFontFamily, _fontSizePreset);
        SyncUiState(status);
        UpdateWindowTitle();
    }

    private void LoadThemeStudioColors(EditorTheme theme)
    {
        _themeStudioColors[EditorThemeColorRole.Background] = theme.Background;
        _themeStudioColors[EditorThemeColorRole.MenuBar] = theme.MenuBar;
        _themeStudioColors[EditorThemeColorRole.SidePanel] = theme.SidePanel;
        _themeStudioColors[EditorThemeColorRole.Workspace] = theme.Workspace;
        _themeStudioColors[EditorThemeColorRole.TabStrip] = theme.TabStrip;
        _themeStudioColors[EditorThemeColorRole.TabActive] = theme.TabActive;
        _themeStudioColors[EditorThemeColorRole.TabInactive] = theme.TabInactive;
        _themeStudioColors[EditorThemeColorRole.StatusBar] = theme.StatusBar;
        _themeStudioColors[EditorThemeColorRole.Divider] = theme.Divider;
        _themeStudioColors[EditorThemeColorRole.Accent] = theme.Accent;
    }

    private ThemeColor GetThemeStudioRoleColor(EditorThemeColorRole role)
    {
        return _themeStudioColors.TryGetValue(role, out ThemeColor color)
            ? color
            : role switch
            {
                EditorThemeColorRole.Background => _theme.Background,
                EditorThemeColorRole.MenuBar => _theme.MenuBar,
                EditorThemeColorRole.SidePanel => _theme.SidePanel,
                EditorThemeColorRole.Workspace => _theme.Workspace,
                EditorThemeColorRole.TabStrip => _theme.TabStrip,
                EditorThemeColorRole.TabActive => _theme.TabActive,
                EditorThemeColorRole.TabInactive => _theme.TabInactive,
                EditorThemeColorRole.StatusBar => _theme.StatusBar,
                EditorThemeColorRole.Divider => _theme.Divider,
                _ => _theme.Accent
            };
    }

    private void SetThemeStudioRoleColor(EditorThemeColorRole role, ThemeColor color)
    {
        _themeStudioColors[role] = new ThemeColor(color.R, color.G, color.B, 1f);
    }

    private EditorTheme BuildThemeStudioPreviewTheme(string themeId)
    {
        string displayName = string.IsNullOrWhiteSpace(_themeStudioNameBuffer)
            ? "Custom Theme"
            : _themeStudioNameBuffer.Trim();
        return new EditorTheme
        {
            Name = themeId,
            DisplayName = displayName,
            Background = GetThemeStudioRoleColor(EditorThemeColorRole.Background),
            MenuBar = GetThemeStudioRoleColor(EditorThemeColorRole.MenuBar),
            SidePanel = GetThemeStudioRoleColor(EditorThemeColorRole.SidePanel),
            Workspace = GetThemeStudioRoleColor(EditorThemeColorRole.Workspace),
            TabStrip = GetThemeStudioRoleColor(EditorThemeColorRole.TabStrip),
            TabActive = GetThemeStudioRoleColor(EditorThemeColorRole.TabActive),
            TabInactive = GetThemeStudioRoleColor(EditorThemeColorRole.TabInactive),
            StatusBar = GetThemeStudioRoleColor(EditorThemeColorRole.StatusBar),
            Divider = GetThemeStudioRoleColor(EditorThemeColorRole.Divider),
            Accent = GetThemeStudioRoleColor(EditorThemeColorRole.Accent)
        };
    }

    private SavedEditorTheme BuildSavedEditorTheme(string themeId, string name)
    {
        return new SavedEditorTheme
        {
            Id = themeId,
            Name = name,
            Background = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.Background)),
            MenuBar = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.MenuBar)),
            SidePanel = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.SidePanel)),
            Workspace = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.Workspace)),
            TabStrip = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.TabStrip)),
            TabActive = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.TabActive)),
            TabInactive = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.TabInactive)),
            StatusBar = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.StatusBar)),
            Divider = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.Divider)),
            Accent = ToPaletteColorSetting(GetThemeStudioRoleColor(EditorThemeColorRole.Accent))
        };
    }

    private string BuildNextCustomThemeName()
    {
        HashSet<string> existingNames = _customThemes
            .Select(theme => theme.Name)
            .Concat(["Dark", "Light", "Kuma", "Kearu"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int themeNumber = 1;
        string candidate = $"Custom Theme {themeNumber}";
        while (existingNames.Contains(candidate))
        {
            themeNumber++;
            candidate = $"Custom Theme {themeNumber}";
        }

        return candidate;
    }

    private string EnsureUniqueCustomThemeName(string requestedName, string? existingThemeId)
    {
        HashSet<string> existingNames = _customThemes
            .Where(theme => !string.Equals(theme.Id, existingThemeId, StringComparison.Ordinal))
            .Select(theme => theme.Name)
            .Concat(["Dark", "Light", "Kuma", "Kearu"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(requestedName))
        {
            return requestedName;
        }

        string baseName = requestedName;
        int copyNumber = 2;
        string candidate = $"{baseName} {copyNumber}";
        while (existingNames.Contains(candidate))
        {
            copyNumber++;
            candidate = $"{baseName} {copyNumber}";
        }

        return candidate;
    }

    private static string SanitizeThemeStudioName(string value)
    {
        string sanitized = new string(value
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Custom Theme" : sanitized;
    }

    private static string GetThemeRoleLabel(EditorThemeColorRole role)
    {
        return role switch
        {
            EditorThemeColorRole.Background => "Background",
            EditorThemeColorRole.MenuBar => "Top Bar",
            EditorThemeColorRole.SidePanel => "Panels",
            EditorThemeColorRole.Workspace => "Workspace",
            EditorThemeColorRole.TabStrip => "Tab Strip",
            EditorThemeColorRole.TabActive => "Tab Active",
            EditorThemeColorRole.TabInactive => "Tab Idle",
            EditorThemeColorRole.StatusBar => "Status Bar",
            EditorThemeColorRole.Divider => "Dividers",
            _ => "Accent"
        };
    }

    private static EditorTheme CloneEditorTheme(EditorTheme theme)
    {
        return new EditorTheme
        {
            Name = theme.Name,
            DisplayName = theme.DisplayName,
            Background = theme.Background,
            MenuBar = theme.MenuBar,
            SidePanel = theme.SidePanel,
            Workspace = theme.Workspace,
            TabStrip = theme.TabStrip,
            TabActive = theme.TabActive,
            TabInactive = theme.TabInactive,
            StatusBar = theme.StatusBar,
            Divider = theme.Divider,
            Accent = theme.Accent
        };
    }

    private static SavedEditorTheme CloneSavedEditorTheme(SavedEditorTheme theme)
    {
        return new SavedEditorTheme
        {
            Id = theme.Id,
            Name = theme.Name,
            Background = ClonePaletteColorSetting(theme.Background),
            MenuBar = ClonePaletteColorSetting(theme.MenuBar),
            SidePanel = ClonePaletteColorSetting(theme.SidePanel),
            Workspace = ClonePaletteColorSetting(theme.Workspace),
            TabStrip = ClonePaletteColorSetting(theme.TabStrip),
            TabActive = ClonePaletteColorSetting(theme.TabActive),
            TabInactive = ClonePaletteColorSetting(theme.TabInactive),
            StatusBar = ClonePaletteColorSetting(theme.StatusBar),
            Divider = ClonePaletteColorSetting(theme.Divider),
            Accent = ClonePaletteColorSetting(theme.Accent)
        };
    }

    private static PaletteColorSetting ClonePaletteColorSetting(PaletteColorSetting color)
    {
        return new PaletteColorSetting
        {
            R = color.R,
            G = color.G,
            B = color.B,
            A = color.A
        };
    }
}

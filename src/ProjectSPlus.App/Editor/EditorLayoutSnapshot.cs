namespace ProjectSPlus.App.Editor;

public sealed class EditorLayoutSnapshot
{
    public required UiRect LeftPanelRect { get; init; }

    public required UiRect LeftPanelHeaderRect { get; init; }

    public required UiRect LeftPanelBodyRect { get; init; }

    public required UiRect RightPanelRect { get; init; }

    public required UiRect RightPanelHeaderRect { get; init; }

    public required UiRect RightPanelBodyRect { get; init; }

    public required UiRect WorkspaceRect { get; init; }

    public required UiRect StatusBarRect { get; init; }

    public required UiRect MenuBarRect { get; init; }

    public required UiRect MenuLogoRect { get; init; }

    public required UiRect TabStripRect { get; init; }

    public required UiRect LeftSplitterRect { get; init; }

    public required UiRect RightSplitterRect { get; init; }

    public required UiRect LeftCollapseHandleRect { get; init; }

    public required UiRect RightCollapseHandleRect { get; init; }

    public required IReadOnlyList<NamedRect> MenuButtons { get; init; }

    public required IReadOnlyList<NamedRect> TabButtons { get; init; }

    public UiRect? HomeHeroPanelRect { get; init; }

    public UiRect? HomeActionsPanelRect { get; init; }

    public UiRect? HomeRecentPanelRect { get; init; }

    public required IReadOnlyList<ActionRect<EditorHomeAction>> HomeCards { get; init; }

    public required IReadOnlyList<IndexedRect> RecentProjectRows { get; init; }

    public UiRect? HomeRecentViewportRect { get; init; }

    public UiRect? HomeRecentScrollTrackRect { get; init; }

    public UiRect? HomeRecentScrollThumbRect { get; init; }

    public UiRect? ProjectsFormPanelRect { get; init; }

    public UiRect? ProjectsRecentPanelRect { get; init; }

    public required IReadOnlyList<IndexedRect> ProjectRows { get; init; }

    public UiRect? ProjectsRecentViewportRect { get; init; }

    public UiRect? ProjectsRecentScrollTrackRect { get; init; }

    public UiRect? ProjectsRecentScrollThumbRect { get; init; }

    public UiRect? PreferencesGeneralPanelRect { get; init; }

    public UiRect? PreferencesShortcutPanelRect { get; init; }

    public required IReadOnlyList<IndexedRect> PreferenceRows { get; init; }

    public UiRect? PreferenceViewportRect { get; init; }

    public UiRect? PreferenceScrollTrackRect { get; init; }

    public UiRect? PreferenceScrollThumbRect { get; init; }

    public UiRect? ThemeStudioDialogRect { get; init; }

    public UiRect? ThemeStudioNameFieldRect { get; init; }

    public UiRect? ThemeStudioWheelRect { get; init; }

    public UiRect? ThemeStudioWheelFieldRect { get; init; }

    public UiRect? ThemeStudioPreviewRect { get; init; }

    public UiRect? LayoutInfoPanelRect { get; init; }

    public UiRect? ScratchInfoPanelRect { get; init; }

    public required IReadOnlyList<IndexedRect> LeftPanelRecentProjectRows { get; init; }

    public UiRect? LeftPanelRecentViewportRect { get; init; }

    public UiRect? LeftPanelRecentScrollTrackRect { get; init; }

    public UiRect? LeftPanelRecentScrollThumbRect { get; init; }

    public required IReadOnlyList<ActionRect<EditorMenuAction>> MenuEntries { get; init; }

    public UiRect? MenuDropdownRect { get; init; }

    public required IReadOnlyList<NamedRect> TabCloseButtons { get; init; }

    public required IReadOnlyList<ActionRect<ProjectFormAction>> ProjectFormActions { get; init; }

    public required IReadOnlyList<ActionRect<EditorPreferenceAction>> PreferenceActions { get; init; }

    public required IReadOnlyList<ActionRect<EditorThemeStudioAction>> ThemeStudioButtons { get; init; }

    public required IReadOnlyList<ActionRect<EditorThemeColorRole>> ThemeStudioRoleButtons { get; init; }

    public required IReadOnlyList<ActionRect<EditorFolderPickerAction>> FolderPickerActions { get; init; }

    public required IReadOnlyList<IndexedRect> FolderPickerRows { get; init; }

    public UiRect? FolderPickerRect { get; init; }

    public UiRect? FolderPickerHeaderRect { get; init; }

    public UiRect? FolderPickerBodyRect { get; init; }

    public UiRect? FolderPickerViewportRect { get; init; }

    public UiRect? FolderPickerScrollTrackRect { get; init; }

    public UiRect? FolderPickerScrollThumbRect { get; init; }

    public PixelStudioLayoutSnapshot? PixelStudio { get; init; }
}

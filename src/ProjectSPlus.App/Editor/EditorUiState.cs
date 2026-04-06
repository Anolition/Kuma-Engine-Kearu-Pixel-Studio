using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.App.Editor;

public sealed class EditorUiState
{
    public string? OpenMenuName { get; set; }

    public bool PreferencesVisible { get; set; }

    public bool AwaitingShortcutKey { get; set; }

    public int SelectedShortcutIndex { get; set; }

    public string? SelectedTabId { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public string ThemeLabel { get; set; } = string.Empty;

    public string FontFamily { get; set; } = string.Empty;

    public string FontSizeLabel { get; set; } = string.Empty;

    public string ProjectLibraryPath { get; set; } = string.Empty;

    public float LeftPanelPreferredWidth { get; set; } = 280f;

    public float RightPanelPreferredWidth { get; set; } = 320f;

    public bool LeftPanelCollapsed { get; set; }

    public bool RightPanelCollapsed { get; set; }

    public int LeftPanelRecentScrollRow { get; set; }

    public int HomeRecentScrollRow { get; set; }

    public int ProjectRecentScrollRow { get; set; }

    public int PreferenceScrollRow { get; set; }

    public int FolderPickerScrollRow { get; set; }

    public IReadOnlyList<string> MenuItems { get; set; } = [];

    public IReadOnlyList<EditorWorkspaceTab> Tabs { get; set; } = [];

    public IReadOnlyList<RecentProjectEntry> RecentProjects { get; set; } = [];

    public IReadOnlyList<EditorShortcutBinding> Shortcuts { get; set; } = [];

    public EditorProjectFormState ProjectForm { get; set; } = new();

    public PixelStudioViewState PixelStudio { get; set; } = new();
}

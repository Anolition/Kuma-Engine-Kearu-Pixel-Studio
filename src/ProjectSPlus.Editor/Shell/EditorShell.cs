namespace ProjectSPlus.Editor.Shell;

public sealed class EditorShell
{
    public string StatusText { get; private set; }

    public ShellLayout Layout { get; }

    public IReadOnlyList<ShellRegion> Regions { get; }

    public IReadOnlyList<string> MenuItems { get; }

    public IReadOnlyList<string> WorkspaceTabs { get; }

    public EditorShell(
        ShellLayout layout,
        IReadOnlyList<ShellRegion> regions,
        IReadOnlyList<string> menuItems,
        IReadOnlyList<string> workspaceTabs,
        string statusText)
    {
        Layout = layout;
        Regions = regions;
        MenuItems = menuItems;
        WorkspaceTabs = workspaceTabs;
        StatusText = statusText;
    }

    public void SetStatus(string statusText)
    {
        StatusText = statusText;
    }

    public static EditorShell CreateDefault()
    {
        ShellLayout layout = new();

        return new EditorShell(
            layout,
        [
            new ShellRegion(ShellRegionKind.MenuBar, "Menu Bar", "Top-level app commands and future project actions."),
            new ShellRegion(ShellRegionKind.LeftPanel, "Project", "Project explorer and asset browser foundation."),
            new ShellRegion(ShellRegionKind.WorkspaceTabs, "Workspace Tabs", "Central document tabs for editor tools."),
            new ShellRegion(ShellRegionKind.Workspace, "Workspace", "Primary tool surface for scenes, graphs, and art tools."),
            new ShellRegion(ShellRegionKind.RightPanel, "Inspector", "Inspector and property editing foundation."),
            new ShellRegion(ShellRegionKind.StatusBar, "Status Bar", "Status, hints, and background task messages.")
        ],
        [
            "File",
            "Edit",
            "View",
            "Project",
            "Tools",
            "Help"
        ],
        [
            "Welcome",
            "Layout",
            "Preferences"
        ],
        "Preferences: F9");
    }
}

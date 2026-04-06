namespace ProjectSPlus.App.Editor;

public sealed class EditorProjectFormState
{
    public string ProjectName { get; set; } = "MyGame";

    public string ProjectLibraryPath { get; set; } = string.Empty;

    public EditorTextField ActiveField { get; set; } = EditorTextField.None;

    public bool FolderPickerVisible { get; set; }

    public string FolderPickerPath { get; set; } = string.Empty;

    public IReadOnlyList<string> FolderPickerEntries { get; set; } = [];
}

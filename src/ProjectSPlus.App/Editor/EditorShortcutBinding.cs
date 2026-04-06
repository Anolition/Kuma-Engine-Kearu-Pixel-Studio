using ProjectSPlus.Core.Configuration;
using Silk.NET.Input;

namespace ProjectSPlus.App.Editor;

public sealed class EditorShortcutBinding
{
    public required ShortcutAction Action { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    public required Key Key { get; set; }
}

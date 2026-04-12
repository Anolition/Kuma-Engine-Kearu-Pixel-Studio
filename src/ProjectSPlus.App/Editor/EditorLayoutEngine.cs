using ProjectSPlus.Editor.Shell;
using ProjectSPlus.Core.Configuration;
using SixLabors.Fonts;

namespace ProjectSPlus.App.Editor;

public static partial class EditorLayoutEngine
{
    private static readonly float UiBodyTextHeight = MeasureTextHeight(16f);
    private static readonly float UiStatusTextHeight = MeasureTextHeight(15f);
    private static readonly float UiTitleTextHeight = MeasureTextHeight(17f);
    private static readonly float UiHeaderHeight = EnsureTextContainerHeight(UiTitleTextHeight, 14f, 32f);
    private static readonly float UiButtonHeight = EnsureTextContainerHeight(UiBodyTextHeight, 14f, 34f);
    private static readonly float UiCompactButtonHeight = EnsureTextContainerHeight(UiStatusTextHeight, 12f, 30f);
    private static readonly float PixelPanelHeaderHeight = UiHeaderHeight;
    private const float PixelPanelPadding = 10f;
    private const float PixelPanelGap = 10f;
    private const float CollapsedPanelWidth = 34f;
    private const float CollapsedPanelThreshold = 40f;

    public static EditorLayoutSnapshot Create(int width, int height, ShellLayout shellLayout, EditorUiState uiState)
    {
        return CreateUnified(width, height, shellLayout, uiState);
#if false
        int menuHeight = shellLayout.MenuBarHeight;
        int tabHeight = shellLayout.TabStripHeight;
        int statusHeight = shellLayout.StatusBarHeight;
        EditorPageKind currentPage = uiState.Tabs.FirstOrDefault(tab => tab.Id == uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;
        float preferredLeftWidth = Math.Clamp(shellLayout.LeftPanelWidth, 220, Math.Max(220, width / 3));
        float preferredRightWidth = Math.Clamp(shellLayout.RightPanelWidth, 260, Math.Max(260, width / 3));
        IReadOnlyList<AdaptivePanelAllocation> rootAllocations = AllocateAdaptiveWidths(
            width,
            [
                new AdaptivePanelSpec
                {
                    Id = "Shell.Left",
                    MinWidth = currentPage == EditorPageKind.PixelStudio ? 160 : 200,
                    PreferredWidth = preferredLeftWidth,
                    FlexibleWidth = true,
                    Priority = 0,
                    AllowCollapse = true,
                    CollapsedWidth = CollapsedPanelWidth
                },
                new AdaptivePanelSpec
                {
                    Id = "Shell.Workspace",
                    MinWidth = 220,
                    PreferredWidth = Math.Max(width - preferredLeftWidth - preferredRightWidth, 420),
                    FlexibleWidth = true,
                    Priority = 4
                },
                new AdaptivePanelSpec
                {
                    Id = "Shell.Right",
                    MinWidth = currentPage == EditorPageKind.PixelStudio ? 180 : 240,
                    PreferredWidth = preferredRightWidth,
                    FlexibleWidth = true,
                    Priority = 1,
                    AllowCollapse = true,
                    CollapsedWidth = CollapsedPanelWidth
                }
            ]);
        int leftWidth = Math.Max((int)MathF.Round(rootAllocations.First(panel => panel.Id == "Shell.Left").Width), 0);
        int rightWidth = Math.Max((int)MathF.Round(rootAllocations.First(panel => panel.Id == "Shell.Right").Width), 0);
        int contentHeight = height - menuHeight - statusHeight;
        int workspaceWidth = Math.Max(width - leftWidth - rightWidth, 160);
        int workspaceX = leftWidth;
        int workspaceHeight = Math.Max(contentHeight - tabHeight, 120);
        const float pagePadding = 22;
        const float pageHeaderHeight = 108;

        UiPanel menuPanel = new()
        {
            Id = "TopBar.MenuStrip",
            Bounds = new UiRect(14, 8, Math.Max(width - 180, 120), Math.Max(menuHeight - 16, 24)),
            Padding = 0,
            Spacing = 8
        };
        IReadOnlyList<UiLayoutPlacement<string>> menuButtonPlacements = LayoutHorizontal(
            menuPanel,
            uiState.MenuItems
                .Select(menu => new UiLayoutItem<string>
                {
                    Id = $"TopBar.Menu.{menu}",
                    Label = menu,
                    Value = menu,
                    MinWidth = 82,
                    MaxWidth = 160,
                    Height = Math.Max(menuHeight - 16, 24),
                    HorizontalPadding = 36
                })
                .ToList(),
            wrap: false);
        List<NamedRect> menuButtons = menuButtonPlacements
            .Select(button => new NamedRect
            {
                Id = button.Value,
                Rect = button.Rect
            })
            .ToList();

        float menuButtonsRight = menuButtons.Count > 0
            ? menuButtons.Max(button => button.Rect.X + button.Rect.Width)
            : 14;

        float logoWidth = 152;
        float logoX = Math.Max(menuButtonsRight + 10, width - logoWidth - 14);
        UiRect menuLogoRect = new(logoX, 6, logoWidth, Math.Max(menuHeight - 12, 28));

        UiPanel tabPanel = new()
        {
            Id = "TopBar.TabStrip",
            Bounds = new UiRect(workspaceX + 10, menuHeight + 6, Math.Max(workspaceWidth - 20, 120), Math.Max(tabHeight - 12, 22)),
            Padding = 0,
            Spacing = 8
        };
        IReadOnlyList<UiLayoutPlacement<EditorWorkspaceTab>> tabPlacements = LayoutHorizontal(
            tabPanel,
            uiState.Tabs
                .Select(tab => new UiLayoutItem<EditorWorkspaceTab>
                {
                    Id = $"Tab.{tab.Id}",
                    Label = tab.Title,
                    Value = tab,
                    MinWidth = tab.Page == EditorPageKind.Scratch ? 88 : 70,
                    MaxWidth = tab.Page == EditorPageKind.Scratch ? 220 : 240,
                    Height = Math.Max(tabHeight - 12, 22),
                    HorizontalPadding = tab.Page == EditorPageKind.Scratch ? 48 : 36,
                    Priority = string.Equals(tab.Id, uiState.SelectedTabId, StringComparison.Ordinal) ? 3 : 1
                })
                .ToList(),
            wrap: false);
        List<NamedRect> tabButtons = [];
        List<NamedRect> tabCloseButtons = [];
        foreach (UiLayoutPlacement<EditorWorkspaceTab> tabPlacement in tabPlacements)
        {
            tabButtons.Add(new NamedRect
            {
                Id = tabPlacement.Value.Id,
                Rect = tabPlacement.Rect
            });
            if (tabPlacement.Value.Page == EditorPageKind.Scratch)
            {
                if (tabPlacement.Rect.Width >= 104)
                {
                    tabCloseButtons.Add(new NamedRect
                    {
                        Id = tabPlacement.Value.Id,
                        Rect = ClampToBounds(
                            new UiRect(tabPlacement.Rect.X + tabPlacement.Rect.Width - 22, tabPlacement.Rect.Y + 5, 14, 14),
                            tabPlacement.Rect)
                    });
                }
            }
        }

        float cardGap = 16;
        float cardWidth = Math.Max(200, Math.Min(250, (workspaceWidth - (pagePadding * 2) - (cardGap * 3)) / 4));
        float cardHeight = 124;
        float cardY = menuHeight + tabHeight + pageHeaderHeight;
        float cardStartX = workspaceX + pagePadding;

        List<ActionRect<EditorHomeAction>> homeCards =
        [
            new ActionRect<EditorHomeAction>
            {
                Action = EditorHomeAction.CreateProjectSlot,
                Rect = new UiRect(cardStartX, cardY, cardWidth, cardHeight)
            },
            new ActionRect<EditorHomeAction>
            {
                Action = EditorHomeAction.OpenPixelStudio,
                Rect = new UiRect(cardStartX + cardWidth + cardGap, cardY, cardWidth, cardHeight)
            },
            new ActionRect<EditorHomeAction>
            {
                Action = EditorHomeAction.OpenProjects,
                Rect = new UiRect(cardStartX + ((cardWidth + cardGap) * 2), cardY, cardWidth, cardHeight)
            },
            new ActionRect<EditorHomeAction>
            {
                Action = EditorHomeAction.OpenPreferences,
                Rect = new UiRect(cardStartX + ((cardWidth + cardGap) * 3), cardY, cardWidth, cardHeight)
            }
        ];

        List<IndexedRect> recentRows = [];
        float recentY = cardY + cardHeight + 74;
        for (int index = 0; index < uiState.RecentProjects.Count; index++)
        {
            recentRows.Add(new IndexedRect
            {
                Index = index,
                Rect = new UiRect(workspaceX + pagePadding, recentY + (index * 56), Math.Min(workspaceWidth - (pagePadding * 2), 720), 46)
            });
        }

        List<IndexedRect> projectRows = [];
        float projectY = menuHeight + tabHeight + 392;
        for (int index = 0; index < uiState.RecentProjects.Count; index++)
        {
            projectRows.Add(new IndexedRect
            {
                Index = index,
                Rect = new UiRect(workspaceX + pagePadding, projectY + (index * 56), Math.Min(workspaceWidth - (pagePadding * 2), 720), 46)
            });
        }

        List<IndexedRect> preferenceRows = [];
        float prefY = menuHeight + tabHeight + 300;
        for (int index = 0; index < uiState.Shortcuts.Count; index++)
        {
            preferenceRows.Add(new IndexedRect
            {
                Index = index,
                Rect = new UiRect(workspaceX + pagePadding, prefY + (index * 60), Math.Min(workspaceWidth - (pagePadding * 2), 680), 46)
            });
        }

        IReadOnlyList<ActionRect<EditorMenuAction>> menuEntries = [];
        UiRect? menuDropdownRect = null;
        if (!string.IsNullOrWhiteSpace(uiState.OpenMenuName))
        {
            IReadOnlyList<EditorMenuEntry> entries = GetMenuEntries(uiState.OpenMenuName);
            NamedRect? button = menuButtons.FirstOrDefault(entry => entry.Id == uiState.OpenMenuName);
            if (button is not null)
            {
                float dropdownX = button.Rect.X;
                float dropdownY = button.Rect.Y + button.Rect.Height + 6;
                float dropdownWidth = Math.Max(220, entries.Max(entry => EstimateButtonWidth(entry.Label, 0, 36)));
                float dropdownHeight = entries.Count * 40 + 12;
                menuDropdownRect = new UiRect(dropdownX, dropdownY, dropdownWidth, dropdownHeight);

                List<ActionRect<EditorMenuAction>> entryRects = [];
                for (int index = 0; index < entries.Count; index++)
                {
                    entryRects.Add(new ActionRect<EditorMenuAction>
                    {
                        Action = entries[index].Action,
                        Rect = new UiRect(dropdownX + 8, dropdownY + 6 + (index * 40), dropdownWidth - 16, 34)
                    });
                }

                menuEntries = entryRects;
            }
        }

        IReadOnlyList<ActionRect<EditorPreferenceAction>> preferenceActions =
        [
            new ActionRect<EditorPreferenceAction>
            {
                Action = EditorPreferenceAction.ToggleTheme,
                Rect = new UiRect(workspaceX + pagePadding, menuHeight + tabHeight + 132, 200, 48)
            },
            new ActionRect<EditorPreferenceAction>
            {
                Action = EditorPreferenceAction.CycleFontSize,
                Rect = new UiRect(workspaceX + pagePadding + 216, menuHeight + tabHeight + 132, 200, 48)
            },
            new ActionRect<EditorPreferenceAction>
            {
                Action = EditorPreferenceAction.CycleFontFamily,
                Rect = new UiRect(workspaceX + pagePadding + 432, menuHeight + tabHeight + 132, 260, 48)
            }
        ];

        List<ActionRect<ProjectFormAction>> projectFormActions =
        [
            new ActionRect<ProjectFormAction>
            {
                Action = ProjectFormAction.ActivateProjectName,
                Rect = new UiRect(workspaceX + pagePadding, menuHeight + tabHeight + 140, 380, 48)
            },
            new ActionRect<ProjectFormAction>
            {
                Action = ProjectFormAction.ActivateProjectLibraryPath,
                Rect = new UiRect(workspaceX + pagePadding, menuHeight + tabHeight + 228, 580, 48)
            },
            new ActionRect<ProjectFormAction>
            {
                Action = ProjectFormAction.CreateProject,
                Rect = new UiRect(workspaceX + pagePadding, menuHeight + tabHeight + 308, 204, 52)
            },
            new ActionRect<ProjectFormAction>
            {
                Action = ProjectFormAction.UseDocumentsFolder,
                Rect = new UiRect(workspaceX + pagePadding + 218, menuHeight + tabHeight + 308, 180, 52)
            },
            new ActionRect<ProjectFormAction>
            {
                Action = ProjectFormAction.UseDesktopFolder,
                Rect = new UiRect(workspaceX + pagePadding + 414, menuHeight + tabHeight + 308, 172, 52)
            },
            new ActionRect<ProjectFormAction>
            {
                Action = ProjectFormAction.OpenFolderPicker,
                Rect = new UiRect(workspaceX + pagePadding + 604, menuHeight + tabHeight + 228, 180, 48)
            }
        ];

        UiRect? folderPickerRect = null;
        IReadOnlyList<ActionRect<EditorFolderPickerAction>> folderPickerActions = [];
        IReadOnlyList<IndexedRect> folderPickerRows = [];
        if (uiState.ProjectForm.FolderPickerVisible)
        {
            float pickerX = workspaceX + Math.Max(workspaceWidth - 440, 24);
            float pickerY = menuHeight + tabHeight + 120;
            float pickerWidth = Math.Min(416, workspaceWidth - 32);
            float pickerHeight = 360;
            folderPickerRect = new UiRect(pickerX, pickerY, pickerWidth, pickerHeight);

            folderPickerActions =
            [
                new ActionRect<EditorFolderPickerAction>
                {
                    Action = EditorFolderPickerAction.NavigateUp,
                    Rect = new UiRect(pickerX + 12, pickerY + 74, 80, 32)
                },
                new ActionRect<EditorFolderPickerAction>
                {
                    Action = EditorFolderPickerAction.SelectCurrent,
                    Rect = new UiRect(pickerX + pickerWidth - 152, pickerY + 74, 136, 32)
                }
            ];

            List<IndexedRect> pickerRows = [];
            float rowY = pickerY + 122;
            for (int index = 0; index < uiState.ProjectForm.FolderPickerEntries.Count; index++)
            {
                pickerRows.Add(new IndexedRect
                {
                    Index = index,
                    Rect = new UiRect(pickerX + 12, rowY + (index * 36), pickerWidth - 24, 30)
                });
            }

            folderPickerRows = pickerRows;
        }

        UiRect workspaceRect = new(workspaceX, menuHeight + tabHeight, workspaceWidth, workspaceHeight);
        PixelStudioLayoutSnapshot? pixelStudioLayout = uiState.PixelStudio.CanvasWidth > 0 && uiState.PixelStudio.CanvasHeight > 0
            ? CreatePixelStudioLayout(workspaceRect, uiState.PixelStudio)
            : null;

        return new EditorLayoutSnapshot
        {
            LeftPanelRect = new UiRect(0, menuHeight, leftWidth, contentHeight),
            RightPanelRect = new UiRect(width - rightWidth, menuHeight, rightWidth, contentHeight),
            WorkspaceRect = workspaceRect,
            StatusBarRect = new UiRect(0, height - statusHeight, width, statusHeight),
            MenuBarRect = new UiRect(0, 0, width, menuHeight),
            MenuLogoRect = menuLogoRect,
            TabStripRect = new UiRect(workspaceX, menuHeight, workspaceWidth, tabHeight),
            MenuButtons = menuButtons,
            TabButtons = tabButtons,
            HomeCards = homeCards,
            RecentProjectRows = recentRows,
            ProjectRows = projectRows,
            PreferenceRows = preferenceRows,
            MenuEntries = menuEntries,
            MenuDropdownRect = menuDropdownRect,
            TabCloseButtons = tabCloseButtons,
            ProjectFormActions = projectFormActions,
            PreferenceActions = preferenceActions,
            FolderPickerActions = folderPickerActions,
            FolderPickerRows = folderPickerRows,
            FolderPickerRect = folderPickerRect,
            PixelStudio = pixelStudioLayout
        };
#endif
    }

    private static IReadOnlyList<EditorMenuEntry> GetMenuEntries(string menuName)
    {
        return menuName switch
        {
            "File" =>
            [
                new EditorMenuEntry { Label = "Home", Action = EditorMenuAction.OpenHome },
                new EditorMenuEntry { Label = EditorBranding.PixelToolName, Action = EditorMenuAction.OpenPixelStudio },
                new EditorMenuEntry { Label = "Create Project Slot", Action = EditorMenuAction.CreateProjectSlot },
                new EditorMenuEntry { Label = "Projects", Action = EditorMenuAction.OpenProjects },
                new EditorMenuEntry { Label = "New Scratch Tab", Action = EditorMenuAction.NewScratchTab }
            ],
            "Edit" =>
            [
                new EditorMenuEntry { Label = "Preferences", Action = EditorMenuAction.OpenPreferences },
                new EditorMenuEntry { Label = "Toggle Theme", Action = EditorMenuAction.ToggleTheme },
                new EditorMenuEntry { Label = "Cycle Font Size", Action = EditorMenuAction.CycleFontSize },
                new EditorMenuEntry { Label = "Cycle Font Family", Action = EditorMenuAction.CycleFontFamily },
                new EditorMenuEntry { Label = "Cycle Color Picker", Action = EditorMenuAction.CycleColorPickerMode }
            ],
            "View" =>
            [
                new EditorMenuEntry { Label = "Home", Action = EditorMenuAction.OpenHome },
                new EditorMenuEntry { Label = EditorBranding.PixelToolName, Action = EditorMenuAction.OpenPixelStudio },
                new EditorMenuEntry { Label = "Layout", Action = EditorMenuAction.OpenLayout },
                new EditorMenuEntry { Label = "Preferences", Action = EditorMenuAction.OpenPreferences },
                new EditorMenuEntry { Label = "Projects", Action = EditorMenuAction.OpenProjects }
            ],
            "Project" =>
            [
                new EditorMenuEntry { Label = "Create Project Slot", Action = EditorMenuAction.CreateProjectSlot },
                new EditorMenuEntry { Label = "Open Project Library", Action = EditorMenuAction.OpenProjectLibrary },
                new EditorMenuEntry { Label = "Projects", Action = EditorMenuAction.OpenProjects }
            ],
            "Tools" =>
            [
                new EditorMenuEntry { Label = EditorBranding.PixelToolName, Action = EditorMenuAction.OpenPixelStudio },
                new EditorMenuEntry { Label = "Toggle Theme", Action = EditorMenuAction.ToggleTheme },
                new EditorMenuEntry { Label = "Cycle Font Size", Action = EditorMenuAction.CycleFontSize },
                new EditorMenuEntry { Label = "Cycle Font Family", Action = EditorMenuAction.CycleFontFamily },
                new EditorMenuEntry { Label = "Cycle Color Picker", Action = EditorMenuAction.CycleColorPickerMode },
                new EditorMenuEntry { Label = "Preferences", Action = EditorMenuAction.OpenPreferences }
            ],
            "Help" =>
            [
                new EditorMenuEntry { Label = "Home", Action = EditorMenuAction.OpenHome },
                new EditorMenuEntry { Label = "Projects", Action = EditorMenuAction.OpenProjects },
                new EditorMenuEntry { Label = "Test Warning Sound", Action = EditorMenuAction.TestWarningSound },
                new EditorMenuEntry { Label = "Test Crash Sound", Action = EditorMenuAction.TestCrashSound },
                new EditorMenuEntry { Label = "Trigger Crash Reporter", Action = EditorMenuAction.TriggerCrashReporterTest }
            ],
            _ => []
        };
    }

    private sealed class UiPanel
    {
        public required string Id { get; init; }

        public required UiRect Bounds { get; init; }

        public string? ParentId { get; init; }

        public DockSide DockSide { get; init; } = DockSide.Center;

        public bool Visible { get; init; } = true;

        public float Padding { get; init; } = 0;

        public float Spacing { get; init; } = 0;

        public UiRect ContentRect => Inset(Bounds, Padding);
    }

    private sealed class UiLayoutItem<T>
    {
        public required string Id { get; init; }

        public required string Label { get; init; }

        public required T Value { get; init; }

        public string? ParentId { get; init; }

        public bool Visible { get; init; } = true;

        public float MinWidth { get; init; }

        public float MaxWidth { get; init; } = float.PositiveInfinity;

        public float Height { get; init; }

        public float HorizontalPadding { get; init; } = 20;

        public int Priority { get; init; } = 0;
    }

    private sealed class UiLayoutPlacement<T>
    {
        public required string Id { get; init; }

        public required string? ParentId { get; init; }

        public required bool Visible { get; init; }

        public required UiRect Rect { get; init; }

        public required T Value { get; init; }
    }

    private sealed class AdaptivePanelSpec
    {
        public required string Id { get; init; }

        public DockSide DockSide { get; init; } = DockSide.Center;

        public float MinWidth { get; init; }

        public float PreferredWidth { get; init; }

        public bool FlexibleWidth { get; init; } = true;

        public int Priority { get; init; }

        public bool AllowCollapse { get; init; }

        public float CollapsedWidth { get; init; } = CollapsedPanelWidth;
    }

    private sealed class AdaptivePanelAllocation
    {
        public required string Id { get; init; }

        public required float Width { get; init; }

        public required bool IsCollapsed { get; init; }
    }

    private sealed class ScrollRegionLayout
    {
        public required UiRect ContentRect { get; init; }

        public UiRect? TrackRect { get; init; }

        public UiRect? ThumbRect { get; init; }

        public required int StartRow { get; init; }

        public required int VisibleRows { get; init; }
    }

    private enum DockSide
    {
        Left,
        Right,
        Top,
        Bottom,
        Center
    }

    private static float EstimateButtonWidth(string label, float minimumWidth, float horizontalPadding, float maxWidth = float.PositiveInfinity, float fontSize = 14f)
    {
        float measuredWidth = MeasureTextWidth(label, fontSize) + horizontalPadding;
        return Math.Clamp(measuredWidth, minimumWidth, maxWidth);
    }

    private static float EnsureTextContainerHeight(float textHeight, float verticalPadding, float minimumHeight = 0f)
    {
        return MathF.Ceiling(Math.Max(textHeight + verticalPadding, minimumHeight));
    }

    private static IReadOnlyList<AdaptivePanelAllocation> AllocateAdaptiveWidths(float totalWidth, IReadOnlyList<AdaptivePanelSpec> specs)
    {
        if (specs.Count == 0)
        {
            return [];
        }

        float[] widths = specs.Select(spec => Math.Max(spec.PreferredWidth, 0)).ToArray();
        bool[] collapsed = specs.Select(_ => false).ToArray();
        float overflow = Math.Max(widths.Sum() - totalWidth, 0);

        if (overflow > 0)
        {
            foreach (int index in specs
                         .Select((spec, index) => new { spec, index })
                         .OrderBy(entry => entry.spec.Priority)
                         .ThenBy(entry => entry.index)
                         .Select(entry => entry.index))
            {
                if (!specs[index].FlexibleWidth)
                {
                    continue;
                }

                float minimumWidth = Math.Max(specs[index].MinWidth, 0);
                float shrinkableWidth = Math.Max(widths[index] - minimumWidth, 0);
                if (shrinkableWidth <= 0)
                {
                    continue;
                }

                float reduction = Math.Min(shrinkableWidth, overflow);
                widths[index] -= reduction;
                overflow -= reduction;
                if (overflow <= 0)
                {
                    break;
                }
            }
        }

        if (overflow > 0)
        {
            foreach (int index in specs
                         .Select((spec, index) => new { spec, index })
                         .Where(entry => entry.spec.AllowCollapse)
                         .OrderBy(entry => entry.spec.Priority)
                         .ThenBy(entry => entry.index)
                         .Select(entry => entry.index))
            {
                float collapseTarget = Math.Max(specs[index].CollapsedWidth, 0);
                float reducibleWidth = Math.Max(widths[index] - collapseTarget, 0);
                if (reducibleWidth <= 0)
                {
                    continue;
                }

                float reduction = Math.Min(reducibleWidth, overflow);
                widths[index] -= reduction;
                overflow -= reduction;
                collapsed[index] = widths[index] <= Math.Max(collapseTarget, CollapsedPanelThreshold);
                if (overflow <= 0)
                {
                    break;
                }
            }
        }

        if (overflow < 0.5f)
        {
            overflow = 0;
        }

        if (overflow > 0)
        {
            foreach (int index in specs
                         .Select((spec, index) => new { spec, index })
                         .OrderByDescending(entry => entry.spec.Priority)
                         .ThenBy(entry => entry.index)
                         .Select(entry => entry.index))
            {
                float emergencyMinimum = specs[index].AllowCollapse ? Math.Max(specs[index].CollapsedWidth, 24) : 120;
                float reducibleWidth = Math.Max(widths[index] - emergencyMinimum, 0);
                if (reducibleWidth <= 0)
                {
                    continue;
                }

                float reduction = Math.Min(reducibleWidth, overflow);
                widths[index] -= reduction;
                overflow -= reduction;
                collapsed[index] = specs[index].AllowCollapse && widths[index] <= Math.Max(specs[index].CollapsedWidth, CollapsedPanelThreshold);
                if (overflow <= 0)
                {
                    break;
                }
            }
        }

        if (widths.Sum() < totalWidth)
        {
            float remaining = totalWidth - widths.Sum();
            foreach (int index in specs
                         .Select((spec, index) => new { spec, index })
                         .Where(entry => entry.spec.FlexibleWidth)
                         .OrderByDescending(entry => entry.spec.Priority)
                         .ThenBy(entry => entry.index)
                         .Select(entry => entry.index))
            {
                widths[index] += remaining;
                break;
            }
        }

        return specs
            .Select((spec, index) => new AdaptivePanelAllocation
            {
                Id = spec.Id,
                Width = Math.Max(widths[index], 0),
                IsCollapsed = collapsed[index] || widths[index] <= Math.Max(spec.CollapsedWidth, CollapsedPanelThreshold)
            })
            .ToList();
    }

    private static IReadOnlyList<UiLayoutPlacement<T>> LayoutHorizontal<T>(UiPanel panel, IReadOnlyList<UiLayoutItem<T>> items, bool wrap)
    {
        List<UiLayoutItem<T>> visibleItems = items.Where(entry => entry.Visible).ToList();
        if (visibleItems.Count == 0)
        {
            return [];
        }

        UiRect content = panel.ContentRect;
        if (!wrap)
        {
            return LayoutHorizontalRow(panel, visibleItems, content.X, content.Y, content.Width);
        }

        List<UiLayoutPlacement<T>> placements = [];
        List<UiLayoutItem<T>> rowItems = [];
        float rowY = content.Y;
        float rowHeight = 0;
        float rowWidth = 0;

        foreach (UiLayoutItem<T> item in visibleItems)
        {
            float preferredWidth = EstimateButtonWidth(item.Label, item.MinWidth, item.HorizontalPadding, item.MaxWidth);
            float projectedWidth = rowItems.Count == 0
                ? preferredWidth
                : rowWidth + panel.Spacing + preferredWidth;
            if (rowItems.Count > 0 && projectedWidth > content.Width)
            {
                placements.AddRange(LayoutHorizontalRow(panel, rowItems, content.X, rowY, content.Width));
                rowY += rowHeight + panel.Spacing;
                rowItems.Clear();
                rowHeight = 0;
                rowWidth = 0;
            }

            rowItems.Add(item);
            rowHeight = Math.Max(rowHeight, item.Height);
            rowWidth = rowItems.Count == 1 ? preferredWidth : rowWidth + panel.Spacing + preferredWidth;
        }

        if (rowItems.Count > 0)
        {
            placements.AddRange(LayoutHorizontalRow(panel, rowItems, content.X, rowY, content.Width));
        }

        return placements;
    }

    private static IReadOnlyList<UiLayoutPlacement<T>> LayoutHorizontalRow<T>(UiPanel panel, IReadOnlyList<UiLayoutItem<T>> items, float startX, float y, float availableWidth)
    {
        if (items.Count == 0)
        {
            return [];
        }

        float spacingWidth = panel.Spacing * Math.Max(items.Count - 1, 0);
        float widthBudget = Math.Max(availableWidth - spacingWidth, 0);
        float[] widths = items
            .Select(item => EstimateButtonWidth(item.Label, item.MinWidth, item.HorizontalPadding, item.MaxWidth))
            .ToArray();
        float totalPreferredWidth = widths.Sum();
        float overflow = Math.Max(totalPreferredWidth - widthBudget, 0);

        if (overflow > 0)
        {
            foreach (int index in items
                         .Select((item, index) => new { item, index })
                         .OrderBy(entry => entry.item.Priority)
                         .ThenBy(entry => entry.index)
                         .Select(entry => entry.index))
            {
                float minimumWidth = Math.Max(items[index].MinWidth, 0);
                float shrinkableWidth = Math.Max(widths[index] - minimumWidth, 0);
                if (shrinkableWidth <= 0)
                {
                    continue;
                }

                float reduction = Math.Min(shrinkableWidth, overflow);
                widths[index] -= reduction;
                overflow -= reduction;
                if (overflow <= 0)
                {
                    break;
                }
            }
        }

        List<UiLayoutPlacement<T>> placements = [];
        float x = startX;
        for (int index = 0; index < items.Count; index++)
        {
            UiLayoutItem<T> item = items[index];
            float resolvedHeight = EnsureTextContainerHeight(Math.Max(UiBodyTextHeight, UiStatusTextHeight), 12f, item.Height);
            UiRect rect = new(x, y, widths[index], resolvedHeight);
            placements.Add(new UiLayoutPlacement<T>
            {
                Id = item.Id,
                ParentId = item.ParentId ?? panel.Id,
                Visible = item.Visible,
                Rect = ClampToBounds(rect, panel.Bounds),
                Value = item.Value
            });
            x += widths[index] + panel.Spacing;
        }

        return placements;
    }

    private static IReadOnlyList<UiLayoutPlacement<T>> LayoutVertical<T>(UiPanel panel, IReadOnlyList<UiLayoutItem<T>> items)
    {
        List<UiLayoutPlacement<T>> placements = [];
        UiRect content = panel.ContentRect;
        float y = content.Y;

        foreach (UiLayoutItem<T> item in items.Where(entry => entry.Visible))
        {
            float resolvedHeight = EnsureTextContainerHeight(Math.Max(UiBodyTextHeight, UiStatusTextHeight), 12f, item.Height);
            UiRect rect = new(content.X, y, Math.Min(content.Width, item.MaxWidth), resolvedHeight);
            placements.Add(new UiLayoutPlacement<T>
            {
                Id = item.Id,
                ParentId = item.ParentId ?? panel.Id,
                Visible = item.Visible,
                Rect = ClampToBounds(rect, panel.Bounds),
                Value = item.Value
            });

            y += resolvedHeight + panel.Spacing;
        }

        return placements;
    }

    private static UiRect Inset(UiRect rect, float padding)
    {
        return new UiRect(
            rect.X + padding,
            rect.Y + padding,
            Math.Max(rect.Width - (padding * 2), 0),
            Math.Max(rect.Height - (padding * 2), 0));
    }

    private static UiRect ClampToBounds(UiRect rect, UiRect bounds)
    {
        float x = Math.Max(rect.X, bounds.X);
        float y = Math.Max(rect.Y, bounds.Y);
        float right = Math.Min(rect.X + rect.Width, bounds.X + bounds.Width);
        float bottom = Math.Min(rect.Y + rect.Height, bounds.Y + bounds.Height);
        return SnapRect(new UiRect(x, y, Math.Max(right - x, 0), Math.Max(bottom - y, 0)));
    }

    private static UiRect GetPanelHeaderRect(UiRect panelRect)
    {
        if (IsCollapsedRect(panelRect))
        {
            return SnapRect(panelRect);
        }

        float headerHeight = Math.Min(Math.Max(PixelPanelHeaderHeight, EnsureTextContainerHeight(UiTitleTextHeight, 14f)), panelRect.Height);
        return SnapRect(new UiRect(panelRect.X, panelRect.Y, panelRect.Width, headerHeight));
    }

    private static UiRect GetPanelBodyRect(UiRect panelRect)
    {
        if (IsCollapsedRect(panelRect))
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

    private static ScrollRegionLayout CreateScrollRegion(UiRect viewportRect, int totalRows, float rowHeight, float rowGap, int requestedStartRow)
    {
        totalRows = Math.Max(totalRows, 0);
        if (viewportRect.Width <= 0 || viewportRect.Height <= 0 || totalRows == 0)
        {
            return new ScrollRegionLayout
            {
                ContentRect = viewportRect,
                StartRow = 0,
                VisibleRows = 0
            };
        }

        int visibleRows = Math.Max((int)MathF.Floor((viewportRect.Height + rowGap) / (rowHeight + rowGap)), 0);
        if (visibleRows <= 0)
        {
            return new ScrollRegionLayout
            {
                ContentRect = viewportRect,
                StartRow = 0,
                VisibleRows = 0
            };
        }

        bool needsScroll = totalRows > visibleRows;
        float trackWidth = needsScroll ? 10f : 0f;
        UiRect contentRect = SnapRect(new UiRect(
            viewportRect.X,
            viewportRect.Y,
            Math.Max(viewportRect.Width - trackWidth - (needsScroll ? 4f : 0f), 0),
            viewportRect.Height));

        if (!needsScroll)
        {
            return new ScrollRegionLayout
            {
                ContentRect = contentRect,
                StartRow = 0,
                VisibleRows = visibleRows
            };
        }

        int maxStartRow = Math.Max(totalRows - visibleRows, 0);
        int startRow = Math.Clamp(requestedStartRow, 0, maxStartRow);
        UiRect trackRect = SnapRect(new UiRect(contentRect.X + contentRect.Width + 4, viewportRect.Y, trackWidth, viewportRect.Height));
        float thumbHeight = Math.Max((visibleRows / (float)Math.Max(totalRows, 1)) * trackRect.Height, 18);
        float thumbTravel = Math.Max(trackRect.Height - thumbHeight, 0);
        float thumbY = trackRect.Y + (maxStartRow == 0 ? 0 : (startRow / (float)maxStartRow) * thumbTravel);

        return new ScrollRegionLayout
        {
            ContentRect = contentRect,
            TrackRect = trackRect,
            ThumbRect = SnapRect(new UiRect(trackRect.X + 1, thumbY, Math.Max(trackRect.Width - 2, 4), thumbHeight)),
            StartRow = startRow,
            VisibleRows = visibleRows
        };
    }

    private static UiRect SnapRect(UiRect rect)
    {
        return new UiRect(
            MathF.Round(rect.X),
            MathF.Round(rect.Y),
            MathF.Round(Math.Max(rect.Width, 0)),
            MathF.Round(Math.Max(rect.Height, 0)));
    }

    private static bool IsCollapsedRect(UiRect rect)
    {
        return rect.Width <= CollapsedPanelThreshold;
    }

    private static float MeasureTextWidth(string text, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        Font font = ResolveLayoutFont(fontSize);
        return TextMeasurer.MeasureBounds(text, new TextOptions(font)).Width;
    }

    private static float MeasureTextHeight(float fontSize)
    {
        Font font = ResolveLayoutFont(fontSize);
        HorizontalMetrics metrics = font.FontMetrics.HorizontalMetrics;
        float scale = font.Size / MathF.Max(font.FontMetrics.UnitsPerEm, 1f);
        float ascent = MathF.Ceiling(Math.Max(metrics.Ascender * scale, font.Size * 0.72f));
        float descent = MathF.Ceiling(Math.Max(MathF.Abs(metrics.Descender * scale), font.Size * 0.24f));
        return MathF.Ceiling(ascent + descent);
    }

    private static Font ResolveLayoutFont(float size)
    {
        if (SystemFonts.TryGet("Segoe UI", out FontFamily family))
        {
            return family.CreateFont(size, FontStyle.Regular);
        }

        FontFamily fallbackFamily = SystemFonts.Collection.Families.First();
        return fallbackFamily.CreateFont(size, FontStyle.Regular);
    }

    private static PixelStudioLayoutSnapshot CreatePixelStudioLayout(UiRect workspaceRect, PixelStudioViewState pixelStudio)
    {
        const float padding = 20;
        const float sectionGap = 18;
        const float headerHeight = 42;
        const float commandBarHeight = 88;
        const float timelineHeight = 176;
        const float palettePanelGap = 10;
        const float toolSettingsPanelWidth = 72;
        const float toolSettingsPanelHeight = 208;

        UiRect headerRect = new(workspaceRect.X + padding, workspaceRect.Y + 24, workspaceRect.Width - (padding * 2), headerHeight);
        UiRect commandBarRect = new(workspaceRect.X + padding, headerRect.Y + headerRect.Height + 12, workspaceRect.Width - (padding * 2), commandBarHeight);

        float contentTop = commandBarRect.Y + commandBarRect.Height + 16;
        float contentBottom = workspaceRect.Y + workspaceRect.Height - padding;
        float panelAreaWidth = Math.Max(workspaceRect.Width - (padding * 2), 180);
        float preferredGapWidth = sectionGap * 2;
        float availableContentWidth = Math.Max(panelAreaWidth - preferredGapWidth, 180);
        bool timelineVisible = pixelStudio.TimelineVisible;
        float resolvedToolSettingsWidth = toolSettingsPanelWidth;
        float preferredToolsWidth = pixelStudio.ToolsPanelCollapsed ? CollapsedPanelWidth : Math.Max(pixelStudio.ToolsPanelPreferredWidth, 84);
        float preferredSidebarWidth = pixelStudio.SidebarCollapsed ? CollapsedPanelWidth : Math.Max(pixelStudio.SidebarPreferredWidth, 320);
        IReadOnlyList<AdaptivePanelAllocation> pixelPanelAllocations = AllocateAdaptiveWidths(
            availableContentWidth,
            [
                new AdaptivePanelSpec
                {
                    Id = "Pixel.Tools",
                    DockSide = DockSide.Left,
                    MinWidth = pixelStudio.ToolsPanelCollapsed ? CollapsedPanelWidth : 132,
                    PreferredWidth = preferredToolsWidth,
                    FlexibleWidth = true,
                    Priority = 0,
                    AllowCollapse = true,
                    CollapsedWidth = CollapsedPanelWidth
                },
                new AdaptivePanelSpec
                {
                    Id = "Pixel.Canvas",
                    DockSide = DockSide.Center,
                    MinWidth = 260,
                    PreferredWidth = Math.Max(availableContentWidth - preferredToolsWidth - preferredSidebarWidth, 420),
                    FlexibleWidth = true,
                    Priority = 4
                },
                new AdaptivePanelSpec
                {
                    Id = "Pixel.Sidebar",
                    DockSide = DockSide.Right,
                    MinWidth = pixelStudio.SidebarCollapsed ? CollapsedPanelWidth : 248,
                    PreferredWidth = preferredSidebarWidth,
                    FlexibleWidth = true,
                    Priority = 2,
                    AllowCollapse = true,
                    CollapsedWidth = CollapsedPanelWidth
                }
            ]);
        float toolbarWidth = MathF.Round(pixelPanelAllocations.First(panel => panel.Id == "Pixel.Tools").Width);
        float sidebarWidth = MathF.Round(pixelPanelAllocations.First(panel => panel.Id == "Pixel.Sidebar").Width);
        bool toolsCollapsed = pixelStudio.ToolsPanelCollapsed || toolbarWidth <= CollapsedPanelThreshold;
        bool sidebarCollapsed = pixelStudio.SidebarCollapsed || sidebarWidth <= CollapsedPanelThreshold;
        toolbarWidth = toolsCollapsed ? CollapsedPanelWidth : toolbarWidth;
        sidebarWidth = sidebarCollapsed ? CollapsedPanelWidth : sidebarWidth;
        float leftGap = toolsCollapsed ? 0f : sectionGap;
        float rightGap = sidebarCollapsed ? 0f : sectionGap;
        float canvasWidth = Math.Max(
            panelAreaWidth
            - toolbarWidth
            - sidebarWidth
            - leftGap
            - rightGap,
            0);

        float timelineResolvedHeight = timelineVisible ? timelineHeight : 0f;
        UiRect timelineRect = SnapRect(new UiRect(
            workspaceRect.X + padding + toolbarWidth + leftGap,
            contentBottom - timelineResolvedHeight,
            Math.Max(panelAreaWidth - toolbarWidth - leftGap, 0),
            timelineResolvedHeight));
        float canvasBottom = timelineVisible
            ? timelineRect.Y - sectionGap
            : contentBottom;
        UiRect toolbarRect = SnapRect(new UiRect(
            workspaceRect.X + padding,
            contentTop,
            toolbarWidth,
            Math.Max(canvasBottom - contentTop, UiHeaderHeight)));

        float canvasStartX = toolbarRect.X + toolbarRect.Width + leftGap;
        float canvasRightX = canvasStartX + canvasWidth;
        float sidebarX = canvasRightX + rightGap;
        if (canvasWidth <= 0)
        {
            sidebarX = workspaceRect.X + padding + toolbarWidth + leftGap;
        }

        float sidebarAvailableHeight = Math.Max(canvasBottom - contentTop, 0);
        float remainingSidebarHeight = Math.Max(sidebarAvailableHeight - sectionGap, 0);
        float minimumLayersPanelHeight = pixelStudio.PaletteLibraryVisible ? 92f : 124f;
        float paletteHeightCap = Math.Max(sidebarAvailableHeight - minimumLayersPanelHeight - sectionGap, 160f);
        float desiredPalettePanelHeight = pixelStudio.PaletteLibraryVisible
            ? Math.Max(remainingSidebarHeight * 0.68f, 272f)
            : Math.Max(remainingSidebarHeight * 0.56f, 204f);
        float palettePanelHeight = Math.Min(desiredPalettePanelHeight, paletteHeightCap);
        if (sidebarAvailableHeight <= minimumLayersPanelHeight + sectionGap)
        {
            palettePanelHeight = sidebarAvailableHeight;
        }

        UiRect palettePanelRect = SnapRect(new UiRect(sidebarX, contentTop, sidebarWidth, Math.Min(palettePanelHeight, sidebarAvailableHeight)));
        float layersPanelY = palettePanelRect.Y + palettePanelRect.Height + sectionGap;
        UiRect layersPanelRect = SnapRect(new UiRect(sidebarX, layersPanelY, sidebarWidth, Math.Max(canvasBottom - layersPanelY, 0)));
        UiRect canvasPanelRect = SnapRect(new UiRect(canvasStartX, contentTop, canvasWidth, Math.Max(canvasBottom - contentTop, 0)));
        UiRect leftSplitterRect = SnapRect(new UiRect(canvasPanelRect.X - 4, contentTop, 8, Math.Max(canvasBottom - contentTop, 0)));
        UiRect rightSplitterRect = SnapRect(new UiRect(sidebarX - 4, contentTop, 8, Math.Max(canvasBottom - contentTop, 0)));
        const float collapseHandleWidth = 12f;
        const float collapseHandleHeight = 38f;
        UiRect leftCollapseHandleRect = SnapRect(new UiRect(toolbarRect.X + toolbarRect.Width - collapseHandleWidth, toolbarRect.Y + Math.Max((toolbarRect.Height - collapseHandleHeight) * 0.5f, 12), collapseHandleWidth, collapseHandleHeight));
        UiRect rightCollapseHandleRect = SnapRect(new UiRect(palettePanelRect.X, palettePanelRect.Y + Math.Max((palettePanelRect.Height - collapseHandleHeight) * 0.86f, 44), collapseHandleWidth, collapseHandleHeight));

        UiRect toolbarBodyRect = GetPanelBodyRect(toolbarRect);
        UiRect toolbarButtonRegion = toolbarBodyRect;

        UiRect canvasHeaderRect = GetPanelHeaderRect(canvasPanelRect);
        UiRect canvasBodyRect = GetPanelBodyRect(canvasPanelRect);
        float resolvedToolSettingsHeight = Math.Min(toolSettingsPanelHeight, Math.Max(canvasBodyRect.Height - 20, 120));
        float defaultToolSettingsX = Math.Max(canvasBodyRect.Width - resolvedToolSettingsWidth - 12, 12);
        float defaultToolSettingsY = 12f;
        float toolSettingsOffsetX = float.IsFinite(pixelStudio.ToolSettingsPanelOffsetX)
            ? pixelStudio.ToolSettingsPanelOffsetX
            : defaultToolSettingsX;
        float toolSettingsOffsetY = float.IsFinite(pixelStudio.ToolSettingsPanelOffsetY)
            ? pixelStudio.ToolSettingsPanelOffsetY
            : defaultToolSettingsY;
        float maxToolSettingsOffsetX = Math.Max(canvasBodyRect.Width - resolvedToolSettingsWidth, 0);
        float maxToolSettingsOffsetY = Math.Max(canvasBodyRect.Height - resolvedToolSettingsHeight, 0);
        toolSettingsOffsetX = Math.Clamp(toolSettingsOffsetX, 0, maxToolSettingsOffsetX);
        toolSettingsOffsetY = Math.Clamp(toolSettingsOffsetY, 0, maxToolSettingsOffsetY);
        float toolSettingsX = canvasBodyRect.X + toolSettingsOffsetX;
        float toolSettingsY = canvasBodyRect.Y + toolSettingsOffsetY;
        UiRect toolSettingsPanelRect = SnapRect(new UiRect(
            toolSettingsX,
            toolSettingsY,
            resolvedToolSettingsWidth,
            resolvedToolSettingsHeight));
        UiRect? navigatorPanelRect = null;
        UiRect? navigatorPreviewRect = null;
        if (pixelStudio.NavigatorVisible)
        {
            GetNavigatorPanelSizeLimits(canvasBodyRect, out float minNavigatorWidth, out float minNavigatorHeight, out float maxNavigatorWidth, out float maxNavigatorHeight);
            float resolvedNavigatorWidth = float.IsFinite(pixelStudio.NavigatorPanelWidth)
                ? pixelStudio.NavigatorPanelWidth
                : minNavigatorWidth;
            float resolvedNavigatorHeight = float.IsFinite(pixelStudio.NavigatorPanelHeight)
                ? pixelStudio.NavigatorPanelHeight
                : minNavigatorHeight;
            resolvedNavigatorWidth = Math.Clamp(resolvedNavigatorWidth, minNavigatorWidth, maxNavigatorWidth);
            resolvedNavigatorHeight = Math.Clamp(resolvedNavigatorHeight, minNavigatorHeight, maxNavigatorHeight);
            float defaultNavigatorX = 12f;
            float defaultNavigatorY = Math.Max(canvasBodyRect.Height - resolvedNavigatorHeight - 12f, 12f);
            float navigatorOffsetX = float.IsFinite(pixelStudio.NavigatorPanelOffsetX)
                ? pixelStudio.NavigatorPanelOffsetX
                : defaultNavigatorX;
            float navigatorOffsetY = float.IsFinite(pixelStudio.NavigatorPanelOffsetY)
                ? pixelStudio.NavigatorPanelOffsetY
                : defaultNavigatorY;
            float maxNavigatorOffsetX = Math.Max(canvasBodyRect.Width - resolvedNavigatorWidth, 0f);
            float maxNavigatorOffsetY = Math.Max(canvasBodyRect.Height - resolvedNavigatorHeight, 0f);
            navigatorOffsetX = Math.Clamp(navigatorOffsetX, 0f, maxNavigatorOffsetX);
            navigatorOffsetY = Math.Clamp(navigatorOffsetY, 0f, maxNavigatorOffsetY);
            navigatorPanelRect = SnapRect(new UiRect(
                canvasBodyRect.X + navigatorOffsetX,
                canvasBodyRect.Y + navigatorOffsetY,
                resolvedNavigatorWidth,
                resolvedNavigatorHeight));
            navigatorPreviewRect = GetNavigatorPreviewRect(navigatorPanelRect.Value);
        }

        UiRect? animationPreviewPanelRect = null;
        UiRect? animationPreviewContentRect = null;

        UiRect canvasViewportRegion = new(
            canvasBodyRect.X,
            canvasBodyRect.Y,
            canvasBodyRect.Width,
            canvasBodyRect.Height);

        UiRect toolSettingsBodyRect = Inset(toolSettingsPanelRect, 10f);
        UiRect paletteBodyRect = GetPanelBodyRect(palettePanelRect);
        UiRect layersBodyRect = GetPanelBodyRect(layersPanelRect);
        UiRect layersButtonRegion = new(
            layersBodyRect.X,
            layersBodyRect.Y,
            layersBodyRect.Width,
            Math.Min(28, layersBodyRect.Height));

        UiRect timelineHeaderRect = GetPanelHeaderRect(timelineRect);
        UiRect timelineBodyRect = GetPanelBodyRect(timelineRect);
        bool expandedPlaybackPreview = false;
        float timelineControlsHeight = 68f;
        UiRect timelinePreviewRect = expandedPlaybackPreview
            ? new UiRect(
                timelineBodyRect.X,
                timelineBodyRect.Y + timelineControlsHeight + 16f,
                Math.Max(timelineBodyRect.Width, 120f),
                Math.Max(timelineBodyRect.Height - timelineControlsHeight - 22f, 84f))
            : new UiRect(
                timelineBodyRect.X + Math.Max(timelineBodyRect.Width - 126, 0),
                timelineBodyRect.Y + 6,
                Math.Min(120, Math.Max(timelineBodyRect.Width, 120)),
                110);
        UiRect timelineControlsRect = expandedPlaybackPreview
            ? new UiRect(
                timelineBodyRect.X,
                timelineBodyRect.Y + 6,
                Math.Max(timelineBodyRect.Width, 120f),
                timelineControlsHeight)
            : new UiRect(
                timelineBodyRect.X,
                timelineBodyRect.Y + 6,
                Math.Max(timelinePreviewRect.X - timelineBodyRect.X - 12, 120),
                timelineControlsHeight);

        List<ActionRect<PixelStudioToolKind>> toolButtons = toolsCollapsed ? [] : CreatePixelToolButtons(toolbarButtonRegion, pixelStudio.SelectionMode);
        List<ActionRect<PixelStudioAction>> toolSettingsButtons = [];
        List<ActionRect<PixelStudioAction>> documentButtons = [];
        List<ActionRect<PixelStudioAction>> selectionButtons = [];
        documentButtons.AddRange(CreateButtonRow(
            commandBarRect.X + 16,
            commandBarRect.Y + 12,
            28,
            10,
            [
                PixelStudioAction.NewBlankDocument,
                PixelStudioAction.SaveProjectDocument,
                PixelStudioAction.LoadProjectDocument,
                PixelStudioAction.ImportImage,
                PixelStudioAction.ExportPng,
                PixelStudioAction.ExportSpriteStrip
            ],
            118,
            30));
        documentButtons.AddRange(CreateButtonRow(
            commandBarRect.X + 16,
            commandBarRect.Y + 48,
            28,
            10,
            [
                PixelStudioAction.LoadDemoDocument,
                PixelStudioAction.ResizeCanvas16,
                PixelStudioAction.ResizeCanvas32,
                PixelStudioAction.ResizeCanvas64,
                PixelStudioAction.ResizeCanvas128,
                PixelStudioAction.ResizeCanvas256,
                PixelStudioAction.ResizeCanvas512,
                PixelStudioAction.OpenCanvasResizeDialog
            ],
            76,
            24));

        UiRect canvasHeaderControlsRect = new(
            canvasHeaderRect.X + 92,
            canvasHeaderRect.Y,
            Math.Max(canvasHeaderRect.Width - 104, 0),
            Math.Max(canvasHeaderRect.Height - 1, 24));
        List<ActionRect<PixelStudioAction>> canvasButtons = CreateRightAlignedButtonRow(
            canvasHeaderControlsRect,
            26,
            8,
            [
                PixelStudioAction.FitCanvas,
                PixelStudioAction.ResetView,
                PixelStudioAction.ToggleNavigatorPanel,
                PixelStudioAction.ToggleTimelinePanel,
                PixelStudioAction.ZoomOut,
                PixelStudioAction.ZoomIn,
                PixelStudioAction.ToggleGrid,
                PixelStudioAction.CycleMirrorMode
            ],
            92,
            28);

        if (pixelStudio.HasSelection || pixelStudio.HasClipboardSelection)
        {
            List<PixelStudioAction> selectionActions = [];
            if (pixelStudio.HasSelection)
            {
                selectionActions.Add(PixelStudioAction.ClearSelection);
                selectionActions.Add(PixelStudioAction.ToggleSelectionTransformMode);
                selectionActions.Add(PixelStudioAction.CopySelection);
                selectionActions.Add(PixelStudioAction.CutSelection);
                selectionActions.Add(PixelStudioAction.RotateSelectionCounterClockwise);
                selectionActions.Add(PixelStudioAction.RotateSelectionClockwise);
                selectionActions.Add(PixelStudioAction.ScaleSelectionDown);
                selectionActions.Add(PixelStudioAction.ScaleSelectionUp);
            }

            if (pixelStudio.HasClipboardSelection)
            {
                selectionActions.Add(PixelStudioAction.PasteSelection);
            }

            float selectionEndX = canvasButtons.Count > 0
                ? canvasButtons.Min(button => button.Rect.X) - 10f
                : canvasHeaderRect.X + canvasHeaderRect.Width - 12f;
            float selectionStartX = canvasHeaderRect.X + 94f;
            float selectionWidth = Math.Max(selectionEndX - selectionStartX, 0f);
            if (selectionWidth > 80f)
            {
                selectionButtons = CreateButtonRow(
                    selectionStartX,
                    canvasHeaderRect.Y + 1f,
                    26,
                    8,
                    selectionActions,
                    54,
                    10)
                    .Where(button => button.Rect.X + button.Rect.Width <= selectionEndX)
                    .ToList();
            }
        }

        UiRect? brushSizeSliderRect = null;
        UiRect? brushSizeFillRect = null;
        UiRect? brushSizeKnobRect = null;
        UiRect? brushPreviewRect = null;
        if (toolSettingsPanelRect.Width > CollapsedPanelThreshold)
        {
            float previewSize = Math.Clamp(toolSettingsBodyRect.Width - 24, 18, 24);
            float sliderWidth = 8f;
            float sliderHeight = Math.Max(Math.Min(toolSettingsBodyRect.Height - previewSize - 54, 126), 92);
            float sliderX = toolSettingsBodyRect.X + MathF.Round((toolSettingsBodyRect.Width - sliderWidth) * 0.5f);
            float sliderY = toolSettingsBodyRect.Y + 26;
            brushSizeSliderRect = new UiRect(sliderX, sliderY, sliderWidth, sliderHeight);
            float brushRatio = Math.Clamp((pixelStudio.BrushSize - 1) / 15f, 0f, 1f);
            float fillHeight = Math.Max((brushSizeSliderRect.Value.Height * brushRatio), 0);
            brushSizeFillRect = new UiRect(
                brushSizeSliderRect.Value.X,
                brushSizeSliderRect.Value.Y + brushSizeSliderRect.Value.Height - fillHeight,
                brushSizeSliderRect.Value.Width,
                fillHeight);
            float knobY = brushSizeSliderRect.Value.Y + ((1f - brushRatio) * Math.Max(brushSizeSliderRect.Value.Height - 16, 0));
            brushSizeKnobRect = new UiRect(brushSizeSliderRect.Value.X - 4, knobY, 16, 14);
            float previewY = brushSizeSliderRect.Value.Y + brushSizeSliderRect.Value.Height + 12;
            brushPreviewRect = new UiRect(
                toolSettingsBodyRect.X + Math.Max((toolSettingsBodyRect.Width - previewSize) * 0.5f, 0),
                previewY,
                previewSize,
                previewSize);
        }

        float paletteInnerX = paletteBodyRect.X;
        float paletteInnerWidth = paletteBodyRect.Width;
        float activeLabelHeight = 18;
        float activeSectionY = paletteBodyRect.Y;
        UiRect activeColorRect = new(paletteInnerX, activeSectionY + activeLabelHeight + 6, 84, 84);
        UiRect secondaryColorRect = new(
            activeColorRect.X + activeColorRect.Width - 34,
            activeColorRect.Y + activeColorRect.Height - 34,
            32,
            32);
        UiRect? paletteColorFieldRect = null;
        UiRect? paletteColorWheelRect = null;
        UiRect? paletteColorWheelFieldRect = null;
        UiRect? paletteAlphaSliderRect = null;
        UiRect? paletteAlphaFillRect = null;
        UiRect? paletteAlphaKnobRect = null;
        List<ActionRect<PixelStudioAction>> paletteButtons = [];
        List<IndexedRect> recentColorSwatches = [];
        float rgbButtonWidth = 32;
        float rgbButtonGap = 4;
        float rgbButtonRightX = paletteBodyRect.X + paletteBodyRect.Width - rgbButtonWidth;
        float rgbButtonLeftX = rgbButtonRightX - rgbButtonWidth - rgbButtonGap;
        float rgbRowY = activeColorRect.Y + 4;
        bool useColorWheel = pixelStudio.ColorPickerMode == PixelStudioColorPickerMode.Wheel;
        if (!sidebarCollapsed)
        {
            if (useColorWheel)
            {
                float wheelSize = Math.Min(
                    Math.Clamp(MathF.Min(paletteInnerWidth, paletteBodyRect.Height * 0.34f), 128f, 172f),
                    paletteInnerWidth);
                float wheelY = activeColorRect.Y + activeColorRect.Height + 16f;
                float wheelX = paletteInnerX + Math.Max((paletteInnerWidth - wheelSize) * 0.5f, 0f);
                paletteColorWheelRect = new UiRect(wheelX, wheelY, wheelSize, wheelSize);
                paletteColorWheelFieldRect = GetPaletteColorWheelFieldRect(paletteColorWheelRect.Value);
            }
            else
            {
                foreach ((PixelStudioAction MinusAction, PixelStudioAction PlusAction) row in new[]
                         {
                             (PixelStudioAction.DecreaseRed, PixelStudioAction.IncreaseRed),
                             (PixelStudioAction.DecreaseGreen, PixelStudioAction.IncreaseGreen),
                             (PixelStudioAction.DecreaseBlue, PixelStudioAction.IncreaseBlue)
                         })
                {
                    paletteButtons.Add(new ActionRect<PixelStudioAction>
                    {
                        Action = row.MinusAction,
                        Rect = new UiRect(rgbButtonLeftX, rgbRowY, rgbButtonWidth, 28)
                    });
                    paletteButtons.Add(new ActionRect<PixelStudioAction>
                    {
                        Action = row.PlusAction,
                        Rect = new UiRect(rgbButtonRightX, rgbRowY, rgbButtonWidth, 28)
                    });
                    rgbRowY += 32;
                }

                float colorFieldY = activeColorRect.Y + activeColorRect.Height + 18;
                float colorFieldHeight = 76;
                paletteColorFieldRect = new UiRect(
                    paletteInnerX,
                    colorFieldY,
                    paletteInnerWidth,
                    colorFieldHeight);
            }

            float infoStartX = activeColorRect.X + activeColorRect.Width + 12f;
            float infoRightX = useColorWheel
                ? paletteBodyRect.X + paletteBodyRect.Width
                : rgbButtonLeftX - 10f;
            float swapButtonWidth = Math.Clamp(infoRightX - infoStartX, 56f, 92f);
            paletteButtons.Add(new ActionRect<PixelStudioAction>
            {
                Action = PixelStudioAction.SwapSecondaryColor,
                Rect = new UiRect(
                    infoStartX,
                    activeColorRect.Y + activeColorRect.Height - 28f,
                    swapButtonWidth,
                    24f)
            });
        }

        List<ActionRect<PixelStudioAction>> paletteLibraryButtons = [];
        List<ActionRect<PixelStudioAction>> palettePromptButtons = [];
        List<IndexedRect> savedPaletteRows = [];
        UiRect? paletteSwatchViewportRect = null;
        UiRect? paletteSwatchScrollTrackRect = null;
        UiRect? paletteSwatchScrollThumbRect = null;
        UiRect? savedPaletteViewportRect = null;
        UiRect? savedPaletteScrollTrackRect = null;
        UiRect? savedPaletteScrollThumbRect = null;
        UiRect? paletteLibraryRect = null;
        UiRect? paletteRenameFieldRect = null;
        UiRect? layerRenameFieldRect = null;
        UiRect? frameRenameFieldRect = null;
        UiRect? palettePromptRect = null;
        UiRect? contextMenuRect = null;
        UiRect? canvasResizeDialogRect = null;
        UiRect? onionOpacitySliderRect = null;
        UiRect? onionOpacityFillRect = null;
        UiRect? onionOpacityKnobRect = null;
        UiRect? layerOpacitySliderRect = null;
        UiRect? layerOpacityFillRect = null;
        UiRect? layerOpacityKnobRect = null;
        UiRect? layerAlphaLockButtonRect = null;
        List<ActionRect<PixelStudioAction>> canvasResizeDialogButtons = [];

        float librarySectionHeight = pixelStudio.PaletteLibraryVisible
            ? Math.Clamp(paletteBodyRect.Height * 0.46f, 188f, 244f)
            : 0;
        float librarySectionY = pixelStudio.PaletteLibraryVisible
            ? paletteBodyRect.Y + paletteBodyRect.Height - librarySectionHeight
            : 0;
        float actionsY = pixelStudio.PaletteLibraryVisible
            ? librarySectionY - palettePanelGap - 34
            : paletteBodyRect.Y + paletteBodyRect.Height - 34;
        float actionButtonWidth = MathF.Floor((paletteInnerWidth - (palettePanelGap * 2)) / 3f);

        if (!sidebarCollapsed)
        {
            paletteButtons.Add(new ActionRect<PixelStudioAction>
            {
                Action = PixelStudioAction.AddPaletteSwatch,
                Rect = new UiRect(paletteInnerX, actionsY, actionButtonWidth, 34)
            });
            paletteButtons.Add(new ActionRect<PixelStudioAction>
            {
                Action = PixelStudioAction.GeneratePaletteFromImage,
                Rect = new UiRect(paletteInnerX + actionButtonWidth + palettePanelGap, actionsY, actionButtonWidth, 34)
            });
            paletteButtons.Add(new ActionRect<PixelStudioAction>
            {
                Action = PixelStudioAction.TogglePaletteLibrary,
                Rect = new UiRect(paletteInnerX + ((actionButtonWidth + palettePanelGap) * 2), actionsY, actionButtonWidth, 34)
            });
        }

        if (!sidebarCollapsed && pixelStudio.PaletteLibraryVisible)
        {
            paletteLibraryRect = new UiRect(paletteInnerX, librarySectionY, paletteInnerWidth, librarySectionHeight);
            float libraryButtonWidth = MathF.Floor((paletteLibraryRect.Value.Width - 16 - (palettePanelGap * 2)) / 3f);
            float libraryButtonX = paletteLibraryRect.Value.X + 8;
            float libraryButtonY = paletteLibraryRect.Value.Y + 30;
            float libraryButtonRowTwoY = libraryButtonY + 36;

            paletteLibraryButtons =
            [
                new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.SaveCurrentPalette,
                    Rect = new UiRect(libraryButtonX, libraryButtonY, libraryButtonWidth, 30)
                },
                new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.DuplicateSelectedPalette,
                    Rect = new UiRect(libraryButtonX + libraryButtonWidth + palettePanelGap, libraryButtonY, libraryButtonWidth, 30)
                },
                new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.ExportPalette,
                    Rect = new UiRect(libraryButtonX + ((libraryButtonWidth + palettePanelGap) * 2), libraryButtonY, libraryButtonWidth, 30)
                },
                new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.ImportPalette,
                    Rect = new UiRect(libraryButtonX, libraryButtonRowTwoY, libraryButtonWidth, 30)
                },
                new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.RenameSelectedPalette,
                    Rect = new UiRect(libraryButtonX + libraryButtonWidth + palettePanelGap, libraryButtonRowTwoY, libraryButtonWidth, 30)
                },
                new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.DeleteSelectedPalette,
                    Rect = new UiRect(libraryButtonX + ((libraryButtonWidth + palettePanelGap) * 2), libraryButtonRowTwoY, libraryButtonWidth, 30)
                }
            ];

            float libraryContentY = libraryButtonRowTwoY + 38;

            if (pixelStudio.PaletteRenameActive)
            {
                paletteRenameFieldRect = new UiRect(paletteLibraryRect.Value.X + 8, libraryContentY, paletteLibraryRect.Value.Width - 16, 30);
                libraryContentY += 38;
            }

            if (pixelStudio.PalettePromptVisible)
            {
                palettePromptRect = new UiRect(paletteLibraryRect.Value.X + 8, libraryContentY, paletteLibraryRect.Value.Width - 16, 72);
                float promptButtonWidth = MathF.Floor((palettePromptRect.Value.Width - 24 - (palettePanelGap * 2)) / 3f);
                float promptButtonY = palettePromptRect.Value.Y + 38;
                palettePromptButtons =
                [
                    new ActionRect<PixelStudioAction>
                    {
                        Action = PixelStudioAction.PalettePromptGenerate,
                        Rect = new UiRect(palettePromptRect.Value.X + 8, promptButtonY, promptButtonWidth, 26)
                    },
                    new ActionRect<PixelStudioAction>
                    {
                        Action = PixelStudioAction.PalettePromptDismiss,
                        Rect = new UiRect(palettePromptRect.Value.X + 8 + promptButtonWidth + palettePanelGap, promptButtonY, promptButtonWidth, 26)
                    },
                    new ActionRect<PixelStudioAction>
                    {
                        Action = PixelStudioAction.PalettePromptDismissForever,
                        Rect = new UiRect(palettePromptRect.Value.X + 8 + ((promptButtonWidth + palettePanelGap) * 2), promptButtonY, promptButtonWidth, 26)
                    }
                ];
                libraryContentY += palettePromptRect.Value.Height + 8;
            }

            float savedRowHeight = 28;
            float savedRowGap = 6;
            float libraryRowsBottom = paletteLibraryRect.Value.Y + paletteLibraryRect.Value.Height - 8;
            savedPaletteViewportRect = new UiRect(
                paletteLibraryRect.Value.X + 8,
                libraryContentY,
                paletteLibraryRect.Value.Width - 16,
                Math.Max(libraryRowsBottom - libraryContentY, 0));
            int totalSavedRows = pixelStudio.SavedPalettes.Count + 1;
            ScrollRegionLayout savedPaletteScroll = CreateScrollRegion(savedPaletteViewportRect.Value, totalSavedRows, savedRowHeight, savedRowGap, pixelStudio.SavedPaletteScrollRow);
            savedPaletteViewportRect = savedPaletteScroll.ContentRect;
            savedPaletteScrollTrackRect = savedPaletteScroll.TrackRect;
            savedPaletteScrollThumbRect = savedPaletteScroll.ThumbRect;
            savedPaletteRows.Add(new IndexedRect
            {
                Index = -1,
                Rect = new UiRect(
                    savedPaletteViewportRect.Value.X,
                    savedPaletteViewportRect.Value.Y,
                    savedPaletteViewportRect.Value.Width,
                    savedRowHeight)
            });
            int savedPaletteStartIndex = Math.Max(savedPaletteScroll.StartRow - 1, 0);
            int visibleSavedPaletteCapacity = Math.Max(savedPaletteScroll.VisibleRows - 1, 0);
            int visibleSavedPaletteCount = Math.Min(visibleSavedPaletteCapacity, Math.Max(pixelStudio.SavedPalettes.Count - savedPaletteStartIndex, 0));
            for (int visibleIndex = 0; visibleIndex < visibleSavedPaletteCount; visibleIndex++)
            {
                int paletteIndex = savedPaletteStartIndex + visibleIndex;
                savedPaletteRows.Add(new IndexedRect
                {
                    Index = paletteIndex,
                    Rect = new UiRect(
                        savedPaletteViewportRect.Value.X,
                        savedPaletteViewportRect.Value.Y + ((visibleIndex + 1) * (savedRowHeight + savedRowGap)),
                        savedPaletteViewportRect.Value.Width,
                        savedRowHeight)
                });
            }
        }

        List<IndexedRect> paletteSwatches = [];
        if (!sidebarCollapsed)
        {
            float pickerBottom = paletteColorFieldRect is not null
                ? paletteColorFieldRect.Value.Y + paletteColorFieldRect.Value.Height
                : paletteColorWheelRect is not null
                    ? paletteColorWheelRect.Value.Y + paletteColorWheelRect.Value.Height
                    : activeColorRect.Y + activeColorRect.Height;
            float alphaSliderY = pickerBottom + 24f;
            float alphaSliderHeight = 18f;
            paletteAlphaSliderRect = new UiRect(
                paletteInnerX,
                alphaSliderY,
                Math.Max(paletteInnerWidth, 0f),
                alphaSliderHeight);
            float alphaRatio = Math.Clamp(pixelStudio.ActiveColorAlpha, 0f, 1f);
            float alphaTrackWidth = Math.Max(paletteAlphaSliderRect.Value.Width - 6f, 0f);
            float alphaFillWidth = alphaTrackWidth * alphaRatio;
            paletteAlphaFillRect = new UiRect(
                paletteAlphaSliderRect.Value.X + 3f,
                paletteAlphaSliderRect.Value.Y + 3f,
                alphaFillWidth,
                Math.Max(paletteAlphaSliderRect.Value.Height - 6f, 0f));
            float alphaKnobX = paletteAlphaSliderRect.Value.X + 3f + alphaFillWidth - 6f;
            alphaKnobX = Math.Clamp(alphaKnobX, paletteAlphaSliderRect.Value.X, paletteAlphaSliderRect.Value.X + Math.Max(paletteAlphaSliderRect.Value.Width - 12f, 0f));
            paletteAlphaKnobRect = new UiRect(
                alphaKnobX,
                paletteAlphaSliderRect.Value.Y + 2f,
                12f,
                Math.Max(paletteAlphaSliderRect.Value.Height - 4f, 0f));

            float recentColorsY = paletteAlphaSliderRect.Value.Y + paletteAlphaSliderRect.Value.Height + 10f;
            float recentSwatchSize = 22f;
            float recentSwatchGap = 6f;
            int maxRecentColors = Math.Max((int)MathF.Floor((paletteInnerWidth + recentSwatchGap) / (recentSwatchSize + recentSwatchGap)), 1);
            int visibleRecentColorCount = Math.Min(pixelStudio.RecentColors.Count, maxRecentColors);
            for (int recentIndex = 0; recentIndex < visibleRecentColorCount; recentIndex++)
            {
                recentColorSwatches.Add(new IndexedRect
                {
                    Index = recentIndex,
                    Rect = new UiRect(
                        paletteInnerX + (recentIndex * (recentSwatchSize + recentSwatchGap)),
                        recentColorsY,
                        recentSwatchSize,
                        recentSwatchSize)
                });
            }

            float recentBottom = visibleRecentColorCount > 0
                ? recentColorsY + recentSwatchSize
                : paletteAlphaSliderRect.Value.Y + paletteAlphaSliderRect.Value.Height;
            float swatchViewportY = recentBottom + 28f;
            float swatchViewportHeight = Math.Max(actionsY - palettePanelGap - swatchViewportY, 54);
            UiRect swatchViewportFrameRect = new(paletteInnerX, swatchViewportY, paletteInnerWidth, swatchViewportHeight);
            int paletteColumns = Math.Max((int)MathF.Floor((Math.Max(swatchViewportFrameRect.Width - 14, 48) + 6) / 36), 3);
            float swatchGap = 6;
            float swatchSize = Math.Clamp(MathF.Floor((Math.Max(swatchViewportFrameRect.Width - 14, 48) - ((paletteColumns - 1) * swatchGap)) / paletteColumns), 18, 30);
            int totalPaletteRows = Math.Max((int)Math.Ceiling(pixelStudio.Palette.Count / (float)paletteColumns), 1);
            ScrollRegionLayout swatchScroll = CreateScrollRegion(swatchViewportFrameRect, totalPaletteRows, swatchSize, swatchGap, pixelStudio.PaletteSwatchScrollRow);
            paletteSwatchViewportRect = swatchScroll.ContentRect;
            paletteSwatchScrollTrackRect = swatchScroll.TrackRect;
            paletteSwatchScrollThumbRect = swatchScroll.ThumbRect;
            paletteColumns = Math.Max((int)MathF.Floor((Math.Max(paletteSwatchViewportRect.Value.Width, 48) + swatchGap) / Math.Max(swatchSize + swatchGap, 1)), 1);
            for (int visibleRow = 0; visibleRow < swatchScroll.VisibleRows; visibleRow++)
            {
                int paletteRow = swatchScroll.StartRow + visibleRow;
                for (int column = 0; column < paletteColumns; column++)
                {
                    int paletteIndex = (paletteRow * paletteColumns) + column;
                    if (paletteIndex >= pixelStudio.Palette.Count)
                    {
                        break;
                    }

                    paletteSwatches.Add(new IndexedRect
                    {
                        Index = paletteIndex,
                        Rect = new UiRect(
                            paletteSwatchViewportRect.Value.X + (column * (swatchSize + swatchGap)),
                            paletteSwatchViewportRect.Value.Y + (visibleRow * (swatchSize + swatchGap)),
                            swatchSize,
                            swatchSize)
                    });
                }
            }
        }

        List<ActionRect<PixelStudioAction>> layerButtons = sidebarCollapsed
            ? []
            : CreateButtonRow(
                layersButtonRegion.X,
                layersButtonRegion.Y,
                28,
                8,
                [
                    PixelStudioAction.AddLayer,
                    PixelStudioAction.ToggleLayerOpacityControls,
                    PixelStudioAction.ToggleLayerAlphaLock
                ],
                96,
                24);

        List<ActionRect<PixelStudioContextMenuAction>> contextMenuButtons = [];

        UiRect? layerListViewportRect = null;
        UiRect? layerScrollTrackRect = null;
        UiRect? layerScrollThumbRect = null;
        List<IndexedRect> layerRows = [];
        List<IndexedRect> layerVisibilityButtons = [];
        float layerListY = layersButtonRegion.Y + layersButtonRegion.Height + 12;
        if (!sidebarCollapsed)
        {
            layerAlphaLockButtonRect = layerButtons.FirstOrDefault(button => button.Action == PixelStudioAction.ToggleLayerAlphaLock)?.Rect;
            if (pixelStudio.LayerOpacityControlsVisible)
            {
                float layerSliderX = layersBodyRect.X + 14f;
                float layerSliderWidth = Math.Max(layersBodyRect.Width - 20f, 72f);
                layerOpacitySliderRect = new UiRect(layerSliderX, layersButtonRegion.Y + layersButtonRegion.Height + 18f, layerSliderWidth, 10f);
                float layerOpacityRatio = Math.Clamp(pixelStudio.ActiveLayerOpacity, 0f, 1f);
                float layerOpacityTrackWidth = Math.Max(layerOpacitySliderRect.Value.Width - 4f, 0f);
                float layerOpacityFillWidth = layerOpacityTrackWidth * layerOpacityRatio;
                layerOpacityFillRect = new UiRect(
                    layerOpacitySliderRect.Value.X + 2f,
                    layerOpacitySliderRect.Value.Y + 2f,
                    layerOpacityFillWidth,
                    Math.Max(layerOpacitySliderRect.Value.Height - 4f, 0f));
                float layerKnobX = layerOpacitySliderRect.Value.X + 2f + layerOpacityFillWidth - 6f;
                layerKnobX = Math.Clamp(
                    layerKnobX,
                    layerOpacitySliderRect.Value.X,
                    layerOpacitySliderRect.Value.X + Math.Max(layerOpacitySliderRect.Value.Width - 12f, 0f));
                layerOpacityKnobRect = new UiRect(
                    layerKnobX,
                    layerOpacitySliderRect.Value.Y - 3f,
                    12f,
                    Math.Max(layerOpacitySliderRect.Value.Height + 6f, 0f));
                layerListY = layerOpacitySliderRect.Value.Y + layerOpacitySliderRect.Value.Height + 18f;
            }
            if (pixelStudio.LayerRenameActive)
            {
                layerRenameFieldRect = new UiRect(layersBodyRect.X, layerListY, layersBodyRect.Width, 30);
                layerListY += 38;
            }

            UiRect layerViewportFrameRect = new(layersBodyRect.X, layerListY, layersBodyRect.Width, Math.Max(layersBodyRect.Y + layersBodyRect.Height - layerListY, 0));
            ScrollRegionLayout layerScroll = CreateScrollRegion(layerViewportFrameRect, pixelStudio.Layers.Count, 32, 8, pixelStudio.LayerScrollRow);
            layerListViewportRect = layerScroll.ContentRect;
            layerScrollTrackRect = layerScroll.TrackRect;
            layerScrollThumbRect = layerScroll.ThumbRect;

            for (int visibleIndex = 0; visibleIndex < layerScroll.VisibleRows; visibleIndex++)
            {
                int layerIndex = layerScroll.StartRow + visibleIndex;
                if (layerIndex >= pixelStudio.Layers.Count)
                {
                    break;
                }

                float layerY = layerListViewportRect.Value.Y + (visibleIndex * 40);
                layerVisibilityButtons.Add(new IndexedRect
                {
                    Index = layerIndex,
                    Rect = new UiRect(layerListViewportRect.Value.X, layerY, 38, 32)
                });
                layerRows.Add(new IndexedRect
                {
                    Index = layerIndex,
                    Rect = new UiRect(layerListViewportRect.Value.X + 46, layerY, Math.Max(layerListViewportRect.Value.Width - 46, 48), 32)
                });
            }
        }

        if (pixelStudio.ContextMenuVisible && pixelStudio.ContextMenuItems.Count > 0)
        {
            const float contextMenuItemHeight = 32f;
            const float contextMenuPadding = 10f;
            float contextMenuWidth = pixelStudio.ContextMenuItems
                .Select(item => EstimateButtonWidth(item.Label, 196, 56, 360))
                .DefaultIfEmpty(196f)
                .Max();
            float menuHeight = (pixelStudio.ContextMenuItems.Count * contextMenuItemHeight) + (contextMenuPadding * 2);
            float maxMenuX = Math.Max(workspaceRect.X + workspaceRect.Width - contextMenuWidth - 8, workspaceRect.X + 8);
            float maxMenuY = Math.Max(workspaceRect.Y + workspaceRect.Height - menuHeight - 8, workspaceRect.Y + 8);
            float menuX = Math.Clamp(pixelStudio.ContextMenuX, workspaceRect.X + 8, maxMenuX);
            float menuY = Math.Clamp(pixelStudio.ContextMenuY, workspaceRect.Y + 8, maxMenuY);
            contextMenuRect = new UiRect(menuX, menuY, contextMenuWidth, menuHeight);

            for (int index = 0; index < pixelStudio.ContextMenuItems.Count; index++)
            {
                PixelStudioContextMenuItemView item = pixelStudio.ContextMenuItems[index];
                contextMenuButtons.Add(new ActionRect<PixelStudioContextMenuAction>
                {
                    Action = item.Action,
                    Rect = new UiRect(
                        menuX + contextMenuPadding,
                        menuY + contextMenuPadding + (index * contextMenuItemHeight),
                        contextMenuWidth - (contextMenuPadding * 2),
                        contextMenuItemHeight)
                });
            }
        }

        UiRect playbackPreviewRect = timelinePreviewRect;
        List<ActionRect<PixelStudioAction>> timelineButtons = [];
        if (timelineVisible)
        {
            timelineButtons.AddRange(CreateButtonRow(
                timelineControlsRect.X,
                timelineControlsRect.Y,
                30,
                6,
                [
                    PixelStudioAction.TogglePlayback,
                    PixelStudioAction.ToggleOnionSkin,
                    PixelStudioAction.ToggleOnionPrevious,
                    PixelStudioAction.ToggleOnionNext,
                    PixelStudioAction.DecreaseFrameRate,
                    PixelStudioAction.IncreaseFrameRate,
                    PixelStudioAction.DecreaseFrameDuration,
                    PixelStudioAction.IncreaseFrameDuration
                ],
                54,
                16));
            timelineButtons.AddRange(CreateButtonRow(
                timelineControlsRect.X,
                timelineControlsRect.Y + 38,
                30,
                6,
                [
                    PixelStudioAction.CopyFrame,
                    PixelStudioAction.PasteFrame,
                    PixelStudioAction.AddFrame,
                    PixelStudioAction.DuplicateFrame,
                    PixelStudioAction.DeleteFrame,
                    PixelStudioAction.ExportGif,
                    PixelStudioAction.ExportPngSequence
                ],
                54,
                16));

            if (pixelStudio.ShowOnionSkin)
            {
                float sliderWidth = Math.Clamp(MathF.Min(timelineControlsRect.Width * 0.18f, 70f), 46f, 70f);
                float sliderHeight = 8f;
                onionOpacitySliderRect = new UiRect(
                    Math.Max(timelineControlsRect.X + timelineControlsRect.Width - sliderWidth - 10f, timelineControlsRect.X + 72f),
                    timelineControlsRect.Y + 14f,
                    sliderWidth,
                    sliderHeight);
                float onionRatio = Math.Clamp(pixelStudio.OnionOpacity, 0f, 1f);
                float onionTrackWidth = Math.Max(onionOpacitySliderRect.Value.Width - 4f, 0f);
                float onionFillWidth = onionTrackWidth * onionRatio;
                onionOpacityFillRect = new UiRect(
                    onionOpacitySliderRect.Value.X + 2f,
                    onionOpacitySliderRect.Value.Y + 2f,
                    onionFillWidth,
                    Math.Max(onionOpacitySliderRect.Value.Height - 4f, 0f));
                float onionKnobX = onionOpacitySliderRect.Value.X + 2f + onionFillWidth - 5f;
                onionKnobX = Math.Clamp(
                    onionKnobX,
                    onionOpacitySliderRect.Value.X,
                    onionOpacitySliderRect.Value.X + Math.Max(onionOpacitySliderRect.Value.Width - 10f, 0f));
                onionOpacityKnobRect = new UiRect(
                    onionKnobX,
                    onionOpacitySliderRect.Value.Y - 3f,
                    10f,
                    Math.Max(onionOpacitySliderRect.Value.Height + 6f, 0f));
            }
        }

        UiRect? frameListViewportRect = null;
        UiRect? frameScrollTrackRect = null;
        UiRect? frameScrollThumbRect = null;
        List<IndexedRect> frameRows = [];
        float frameListStartY = timelineControlsRect.Y + timelineControlsRect.Height + 10;
        if (timelineVisible && !expandedPlaybackPreview && pixelStudio.FrameRenameActive)
        {
            frameRenameFieldRect = new UiRect(timelineBodyRect.X, frameListStartY, Math.Max(playbackPreviewRect.X - 18 - timelineBodyRect.X, 120), 30);
            frameListStartY += 38;
        }

        if (timelineVisible && !expandedPlaybackPreview)
        {
            UiRect frameViewportFrameRect = new(
                timelineBodyRect.X,
                frameListStartY,
                Math.Max(playbackPreviewRect.X - 18 - timelineBodyRect.X, 120),
                Math.Max(timelineBodyRect.Y + timelineBodyRect.Height - frameListStartY, 40));
            frameListViewportRect = new(frameViewportFrameRect.X, frameViewportFrameRect.Y, Math.Max(frameViewportFrameRect.Width - 14, 0), frameViewportFrameRect.Height);
            float frameRowHeight = 36;
            float frameRowGap = 8;
            float frameX = frameListViewportRect.Value.X;
            float framesRightEdge = frameListViewportRect.Value.X + frameListViewportRect.Value.Width;
            List<List<(int FrameIndex, float Width)>> frameRowGroups = [];
            List<(int FrameIndex, float Width)> currentRow = [];
            for (int index = 0; index < pixelStudio.Frames.Count; index++)
            {
                string frameLabel = pixelStudio.Frames[index].Name;
                float frameWidth = EstimateButtonWidth(frameLabel, 110, 34);
                float projectedRight = currentRow.Count == 0
                    ? frameListViewportRect.Value.X + frameWidth
                    : frameX + 10 + frameWidth;
                if (currentRow.Count > 0 && projectedRight > framesRightEdge)
                {
                    frameRowGroups.Add(currentRow);
                    currentRow = [];
                    frameX = frameListViewportRect.Value.X;
                }
                currentRow.Add((index, frameWidth));
                frameX = currentRow.Count == 1
                    ? frameListViewportRect.Value.X + frameWidth
                    : frameX + 10 + frameWidth;
            }
            if (currentRow.Count > 0)
            {
                frameRowGroups.Add(currentRow);
            }

            ScrollRegionLayout frameScroll = CreateScrollRegion(frameViewportFrameRect, frameRowGroups.Count, frameRowHeight, frameRowGap, pixelStudio.FrameScrollRow);
            frameListViewportRect = frameScroll.ContentRect;
            frameScrollTrackRect = frameScroll.TrackRect;
            frameScrollThumbRect = frameScroll.ThumbRect;
            for (int visibleRow = 0; visibleRow < frameScroll.VisibleRows; visibleRow++)
            {
                int rowIndex = frameScroll.StartRow + visibleRow;
                if (rowIndex >= frameRowGroups.Count)
                {
                    break;
                }

                float rowX = frameListViewportRect.Value.X;
                float rowY = frameListViewportRect.Value.Y + (visibleRow * (frameRowHeight + frameRowGap));
                foreach ((int frameIndex, float frameWidth) in frameRowGroups[rowIndex])
                {
                    frameRows.Add(new IndexedRect
                    {
                        Index = frameIndex,
                        Rect = new UiRect(rowX, rowY, frameWidth, frameRowHeight)
                    });
                    rowX += frameWidth + 10;
                }
            }
        }

        if (pixelStudio.WarningDialogVisible || pixelStudio.CanvasResizeDialogVisible)
        {
            if (pixelStudio.WarningDialogVisible)
            {
                float dialogWidth = Math.Clamp(MathF.Min(workspaceRect.Width - 72f, 432f), 332f, 432f);
                float dialogHeight = Math.Clamp(MathF.Min(workspaceRect.Height - 88f, 252f), 212f, 252f);
                float dialogX = workspaceRect.X + Math.Max((workspaceRect.Width - dialogWidth) * 0.5f, 16f);
                float dialogY = workspaceRect.Y + Math.Max((workspaceRect.Height - dialogHeight) * 0.5f, 28f);
                canvasResizeDialogRect = SnapRect(new UiRect(dialogX, dialogY, dialogWidth, dialogHeight));

                UiRect dialogContentRect = Inset(canvasResizeDialogRect.Value, 18f);
                float footerGap = 12f;
                float footerButtonHeight = 36f;
                float footerButtonWidth = Math.Max(MathF.Floor((dialogContentRect.Width - footerGap) * 0.5f), 118f);
                float footerY = (canvasResizeDialogRect.Value.Y + canvasResizeDialogRect.Value.Height) - 54f;
                canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.ConfirmWarningDialog,
                    Rect = new UiRect(dialogContentRect.X, footerY, footerButtonWidth, footerButtonHeight)
                });
                canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.CancelWarningDialog,
                    Rect = new UiRect(dialogContentRect.X + footerButtonWidth + footerGap, footerY, footerButtonWidth, footerButtonHeight)
                });
            }
            else
            {
                float dialogWidth = Math.Clamp(MathF.Min(workspaceRect.Width - 56f, 388f), 320f, 388f);
                float dialogHeight = Math.Clamp(MathF.Min(workspaceRect.Height - 56f, 332f), 304f, 332f);
                float dialogX = workspaceRect.X + Math.Max((workspaceRect.Width - dialogWidth) * 0.5f, 16f);
                float dialogY = workspaceRect.Y + Math.Max((workspaceRect.Height - dialogHeight) * 0.5f, 28f);
                canvasResizeDialogRect = SnapRect(new UiRect(dialogX, dialogY, dialogWidth, dialogHeight));

                UiRect dialogContentRect = Inset(canvasResizeDialogRect.Value, 16f);
                float fieldHeight = 40f;
                float fieldGap = 12f;
                float fieldWidth = Math.Max(MathF.Floor((dialogContentRect.Width - fieldGap) * 0.5f), 108f);
                float fieldY = dialogContentRect.Y + 22f;
                canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.ActivateCanvasResizeWidthField,
                    Rect = new UiRect(dialogContentRect.X, fieldY, fieldWidth, fieldHeight)
                });
                canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.ActivateCanvasResizeHeightField,
                    Rect = new UiRect(dialogContentRect.X + fieldWidth + fieldGap, fieldY, fieldWidth, fieldHeight)
                });

                float anchorButtonGap = 8f;
                float anchorButtonSize = Math.Clamp(MathF.Floor((dialogContentRect.Width - (anchorButtonGap * 2f)) / 6f), 34f, 40f);
                float anchorGridWidth = (anchorButtonSize * 3f) + (anchorButtonGap * 2f);
                float anchorStartX = dialogContentRect.X + Math.Max((dialogContentRect.Width - anchorGridWidth) * 0.5f, 0f);
                float anchorStartY = fieldY + fieldHeight + 58f;
                PixelStudioAction[,] anchorActions =
                {
                    {
                        PixelStudioAction.SetCanvasResizeAnchorTopLeft,
                        PixelStudioAction.SetCanvasResizeAnchorTop,
                        PixelStudioAction.SetCanvasResizeAnchorTopRight
                    },
                    {
                        PixelStudioAction.SetCanvasResizeAnchorLeft,
                        PixelStudioAction.SetCanvasResizeAnchorCenter,
                        PixelStudioAction.SetCanvasResizeAnchorRight
                    },
                    {
                        PixelStudioAction.SetCanvasResizeAnchorBottomLeft,
                        PixelStudioAction.SetCanvasResizeAnchorBottom,
                        PixelStudioAction.SetCanvasResizeAnchorBottomRight
                    }
                };

                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                        {
                            Action = anchorActions[row, column],
                            Rect = new UiRect(
                                anchorStartX + (column * (anchorButtonSize + anchorButtonGap)),
                                anchorStartY + (row * (anchorButtonSize + anchorButtonGap)),
                                anchorButtonSize,
                                anchorButtonSize)
                        });
                    }
                }

                float footerGap = 10f;
                float footerButtonHeight = 34f;
                float footerButtonWidth = Math.Max(MathF.Floor((dialogContentRect.Width - footerGap) * 0.5f), 118f);
                float footerY = anchorStartY + (anchorButtonSize * 3f) + (anchorButtonGap * 2f) + 16f;
                canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.ApplyCanvasResize,
                    Rect = new UiRect(dialogContentRect.X, footerY, footerButtonWidth, footerButtonHeight)
                });
                canvasResizeDialogButtons.Add(new ActionRect<PixelStudioAction>
                {
                    Action = PixelStudioAction.CancelCanvasResize,
                    Rect = new UiRect(dialogContentRect.X + footerButtonWidth + footerGap, footerY, footerButtonWidth, footerButtonHeight)
                });
            }
        }

        PixelStudioCameraState camera = PixelStudioCameraMath.Compute(
            canvasViewportRegion,
            pixelStudio.CanvasWidth,
            pixelStudio.CanvasHeight,
            pixelStudio.Zoom,
            pixelStudio.CanvasPanX,
            pixelStudio.CanvasPanY);
        int cellSize = camera.Zoom;
        UiRect canvasViewportRect = camera.ViewportRect;

        List<IndexedRect> canvasCells = [];
        BuildSelectionTransformOverlay(
            pixelStudio,
            canvasViewportRect,
            cellSize,
            out UiRect? selectionTransformPreviewRect,
            out UiRect? selectionTransformAngleFieldRect,
            out List<PixelStudioSelectionHandleRect> selectionHandleRects);

        return new PixelStudioLayoutSnapshot
        {
            HeaderRect = headerRect,
            CommandBarRect = commandBarRect,
            ToolbarRect = toolbarRect,
            CanvasPanelRect = canvasPanelRect,
            CanvasClipRect = canvasViewportRegion,
            CanvasViewportRect = canvasViewportRect,
            LeftSplitterRect = leftSplitterRect,
            RightSplitterRect = rightSplitterRect,
            LeftCollapseHandleRect = leftCollapseHandleRect,
            RightCollapseHandleRect = rightCollapseHandleRect,
            PalettePanelRect = palettePanelRect,
            ToolSettingsPanelRect = toolSettingsPanelRect,
            NavigatorPanelRect = navigatorPanelRect,
            NavigatorPreviewRect = navigatorPreviewRect,
            AnimationPreviewPanelRect = animationPreviewPanelRect,
            AnimationPreviewContentRect = animationPreviewContentRect,
            LayersPanelRect = layersPanelRect,
            TimelinePanelRect = timelineRect,
            ActiveColorRect = activeColorRect,
            SecondaryColorRect = secondaryColorRect,
            PaletteColorFieldRect = paletteColorFieldRect,
            PaletteColorWheelRect = paletteColorWheelRect,
            PaletteColorWheelFieldRect = paletteColorWheelFieldRect,
            PaletteAlphaSliderRect = paletteAlphaSliderRect,
            PaletteAlphaFillRect = paletteAlphaFillRect,
            PaletteAlphaKnobRect = paletteAlphaKnobRect,
            OnionOpacitySliderRect = onionOpacitySliderRect,
            OnionOpacityFillRect = onionOpacityFillRect,
            OnionOpacityKnobRect = onionOpacityKnobRect,
            LayerOpacitySliderRect = layerOpacitySliderRect,
            LayerOpacityFillRect = layerOpacityFillRect,
            LayerOpacityKnobRect = layerOpacityKnobRect,
            LayerAlphaLockButtonRect = layerAlphaLockButtonRect,
            PlaybackPreviewRect = playbackPreviewRect,
            PaletteSwatchViewportRect = paletteSwatchViewportRect,
            PaletteSwatchScrollTrackRect = paletteSwatchScrollTrackRect,
            PaletteSwatchScrollThumbRect = paletteSwatchScrollThumbRect,
            SavedPaletteViewportRect = savedPaletteViewportRect,
            SavedPaletteScrollTrackRect = savedPaletteScrollTrackRect,
            SavedPaletteScrollThumbRect = savedPaletteScrollThumbRect,
            LayerListViewportRect = layerListViewportRect,
            LayerScrollTrackRect = layerScrollTrackRect,
            LayerScrollThumbRect = layerScrollThumbRect,
            FrameListViewportRect = frameListViewportRect,
            FrameScrollTrackRect = frameScrollTrackRect,
            FrameScrollThumbRect = frameScrollThumbRect,
            PaletteLibraryRect = paletteLibraryRect,
            PaletteRenameFieldRect = paletteRenameFieldRect,
            LayerRenameFieldRect = layerRenameFieldRect,
            FrameRenameFieldRect = frameRenameFieldRect,
            PalettePromptRect = palettePromptRect,
            ContextMenuRect = contextMenuRect,
            CanvasResizeDialogRect = canvasResizeDialogRect,
            BrushSizeSliderRect = brushSizeSliderRect,
            BrushSizeFillRect = brushSizeFillRect,
            BrushSizeKnobRect = brushSizeKnobRect,
            BrushPreviewRect = brushPreviewRect,
            SelectionTransformPreviewRect = selectionTransformPreviewRect,
            SelectionTransformAngleFieldRect = selectionTransformAngleFieldRect,
            CameraZoom = camera.Zoom,
            CameraPanX = camera.PanX,
            CameraPanY = camera.PanY,
            CanvasCellSize = cellSize,
            ToolButtons = toolButtons,
            DocumentButtons = documentButtons,
            CanvasButtons = canvasButtons,
            SelectionButtons = selectionButtons,
            PaletteButtons = paletteButtons,
            ToolSettingsButtons = toolSettingsButtons,
            PaletteLibraryButtons = paletteLibraryButtons,
            PalettePromptButtons = palettePromptButtons,
            CanvasResizeDialogButtons = canvasResizeDialogButtons,
            ContextMenuButtons = contextMenuButtons,
            LayerButtons = layerButtons,
            TimelineButtons = timelineButtons,
            PaletteSwatches = paletteSwatches,
            RecentColorSwatches = recentColorSwatches,
            SavedPaletteRows = savedPaletteRows,
            LayerRows = layerRows,
            LayerVisibilityButtons = layerVisibilityButtons,
            FrameRows = frameRows,
            CanvasCells = canvasCells,
            SelectionHandleRects = selectionHandleRects
        };
    }

    public static void BuildSelectionTransformOverlay(
        PixelStudioViewState pixelStudio,
        UiRect canvasViewportRect,
        int cellSize,
        out UiRect? selectionTransformPreviewRect,
        out UiRect? selectionTransformAngleFieldRect,
        out List<PixelStudioSelectionHandleRect> selectionHandleRects)
    {
        selectionTransformPreviewRect = null;
        selectionTransformAngleFieldRect = null;
        selectionHandleRects = [];
        if (!pixelStudio.HasSelection || cellSize <= 0)
        {
            return;
        }

        int selectionLeft = pixelStudio.SelectionTransformPreviewVisible ? pixelStudio.SelectionTransformPreviewX : pixelStudio.SelectionX;
        int selectionTop = pixelStudio.SelectionTransformPreviewVisible ? pixelStudio.SelectionTransformPreviewY : pixelStudio.SelectionY;
        int selectionWidth = pixelStudio.SelectionTransformPreviewVisible ? pixelStudio.SelectionTransformPreviewWidth : pixelStudio.SelectionWidth;
        int selectionHeight = pixelStudio.SelectionTransformPreviewVisible ? pixelStudio.SelectionTransformPreviewHeight : pixelStudio.SelectionHeight;
        if (selectionWidth <= 0 || selectionHeight <= 0)
        {
            return;
        }

        UiRect selectionBoundsRect = SnapRect(new UiRect(
            canvasViewportRect.X + (selectionLeft * cellSize),
            canvasViewportRect.Y + (selectionTop * cellSize),
            Math.Max(selectionWidth * cellSize, 1),
            Math.Max(selectionHeight * cellSize, 1)));
        if (pixelStudio.SelectionTransformPreviewVisible)
        {
            selectionTransformPreviewRect = selectionBoundsRect;
        }

        if (!pixelStudio.SelectionTransformModeActive || pixelStudio.ActiveTool != PixelStudioToolKind.Select)
        {
            return;
        }

        float rotationDegrees = pixelStudio.SelectionTransformPreviewVisible
            ? pixelStudio.SelectionTransformPreviewRotationDegrees
            : 0f;
        bool rotateOverlay = MathF.Abs(rotationDegrees) > 0.01f;
        float handleSize = Math.Clamp(MathF.Min(Math.Max(cellSize * 0.65f, 14f), 20f), 14f, 20f);
        float handleHalf = handleSize * 0.5f;
        float pivotCenterX = canvasViewportRect.X + (pixelStudio.SelectionTransformPivotX * cellSize);
        float pivotCenterY = canvasViewportRect.Y + (pixelStudio.SelectionTransformPivotY * cellSize);
        float overlayWidth = Math.Max((rotateOverlay ? pixelStudio.SelectionWidth : selectionWidth) * cellSize, 1);
        float overlayHeight = Math.Max((rotateOverlay ? pixelStudio.SelectionHeight : selectionHeight) * cellSize, 1);
        float overlayHalfWidth = overlayWidth * 0.5f;
        float overlayHalfHeight = overlayHeight * 0.5f;
        float radians = rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        float rotateHandleSize = Math.Clamp(handleSize + 2f, 16f, 22f);
        float rotateHalf = rotateHandleSize * 0.5f;
        float rotateGap = Math.Clamp((handleSize * 2.2f) + 6f, 26f, 44f);
        static (float X, float Y) RotatePoint(float originX, float originY, float localX, float localY, float cos, float sin)
        {
            return (
                originX + (localX * cos) - (localY * sin),
                originY + (localX * sin) + (localY * cos));
        }

        static UiRect CenterRectAt(float centerX, float centerY, float halfSize, float size)
        {
            return SnapRect(new UiRect(centerX - halfSize, centerY - halfSize, size, size));
        }

        float localCenterX = selectionBoundsRect.X + (selectionBoundsRect.Width * 0.5f) - pivotCenterX;
        float localCenterY = selectionBoundsRect.Y + (selectionBoundsRect.Height * 0.5f) - pivotCenterY;
        (float pivotHandleX, float pivotHandleY) = (pivotCenterX, pivotCenterY);
        (float rotateCenterX, float rotateCenterY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX, localCenterY - overlayHalfHeight - rotateGap, cos, sin);
        (float topLeftX, float topLeftY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX - overlayHalfWidth, localCenterY - overlayHalfHeight, cos, sin);
        (float topCenterX, float topCenterY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX, localCenterY - overlayHalfHeight, cos, sin);
        (float topRightX, float topRightY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX + overlayHalfWidth, localCenterY - overlayHalfHeight, cos, sin);
        (float rightCenterX, float rightCenterY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX + overlayHalfWidth, localCenterY, cos, sin);
        (float bottomLeftX, float bottomLeftY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX - overlayHalfWidth, localCenterY + overlayHalfHeight, cos, sin);
        (float bottomCenterX, float bottomCenterY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX, localCenterY + overlayHalfHeight, cos, sin);
        (float bottomRightX, float bottomRightY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX + overlayHalfWidth, localCenterY + overlayHalfHeight, cos, sin);
        (float leftCenterX, float leftCenterY) = RotatePoint(pivotCenterX, pivotCenterY, localCenterX - overlayHalfWidth, localCenterY, cos, sin);
        float angleFieldWidth = 72f;
        float angleFieldHeight = 18f;
        float angleFieldX = Math.Clamp(
            rotateCenterX - (angleFieldWidth * 0.5f),
            canvasViewportRect.X + 4f,
            canvasViewportRect.X + Math.Max(canvasViewportRect.Width - angleFieldWidth - 4f, 4f));
        float angleFieldY = Math.Clamp(
            rotateCenterY - rotateHandleSize - 26f,
            canvasViewportRect.Y + 4f,
            canvasViewportRect.Y + Math.Max(canvasViewportRect.Height - angleFieldHeight - 4f, 4f));
        selectionTransformAngleFieldRect = SnapRect(new UiRect(angleFieldX, angleFieldY, angleFieldWidth, angleFieldHeight));
        selectionHandleRects =
        [
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.Pivot,
                Rect = CenterRectAt(pivotHandleX, pivotHandleY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.Rotate,
                Rect = CenterRectAt(rotateCenterX, rotateCenterY, rotateHalf, rotateHandleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.TopLeft,
                Rect = CenterRectAt(topLeftX, topLeftY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.Top,
                Rect = CenterRectAt(topCenterX, topCenterY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.TopRight,
                Rect = CenterRectAt(topRightX, topRightY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.Right,
                Rect = CenterRectAt(rightCenterX, rightCenterY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.BottomLeft,
                Rect = CenterRectAt(bottomLeftX, bottomLeftY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.Bottom,
                Rect = CenterRectAt(bottomCenterX, bottomCenterY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.BottomRight,
                Rect = CenterRectAt(bottomRightX, bottomRightY, handleHalf, handleSize)
            },
            new PixelStudioSelectionHandleRect
            {
                Kind = PixelStudioSelectionHandleKind.Left,
                Rect = CenterRectAt(leftCenterX, leftCenterY, handleHalf, handleSize)
            }
        ];
    }

    private static UiRect GetPaletteColorWheelFieldRect(UiRect wheelRect)
    {
        float centerX = wheelRect.X + (wheelRect.Width * 0.5f);
        float centerY = wheelRect.Y + (wheelRect.Height * 0.5f);
        float outerRadius = MathF.Min(wheelRect.Width, wheelRect.Height) * 0.5f;
        float ringThickness = Math.Clamp(outerRadius * 0.28f, 12f, 18f);
        float innerRadius = Math.Max(outerRadius - ringThickness - 3f, 0f);
        float fieldHalfSize = innerRadius / MathF.Sqrt(2f);
        float fieldSize = Math.Max(fieldHalfSize * 2f, 0f);
        return new UiRect(
            centerX - fieldHalfSize,
            centerY - fieldHalfSize,
            fieldSize,
            fieldSize);
    }

    private static void GetNavigatorPanelSizeLimits(UiRect canvasBodyRect, out float minWidth, out float minHeight, out float maxWidth, out float maxHeight)
    {
        minWidth = Math.Clamp(MathF.Min(canvasBodyRect.Width * 0.24f, 196f), 144f, 196f);
        minHeight = Math.Clamp(MathF.Min(canvasBodyRect.Height * 0.30f, 196f), 144f, 196f);
        minWidth = Math.Min(minWidth, Math.Max(canvasBodyRect.Width, 0f));
        minHeight = Math.Min(minHeight, Math.Max(canvasBodyRect.Height, 0f));
        maxWidth = Math.Min(Math.Max(minWidth, MathF.Min(canvasBodyRect.Width * 0.58f, 420f)), Math.Max(canvasBodyRect.Width, minWidth));
        maxHeight = Math.Min(Math.Max(minHeight, MathF.Min(canvasBodyRect.Height * 0.58f, 420f)), Math.Max(canvasBodyRect.Height, minHeight));
    }

    private static UiRect GetNavigatorPreviewRect(UiRect panelRect)
    {
        const float previewInset = 16f;
        return new UiRect(
            panelRect.X + previewInset,
            panelRect.Y + previewInset,
            Math.Max(panelRect.Width - (previewInset * 2f), 0f),
            Math.Max(panelRect.Height - (previewInset * 2f), 0f));
    }

    private static void GetAnimationPreviewPanelSizeLimits(UiRect canvasBodyRect, out float minWidth, out float minHeight, out float maxWidth, out float maxHeight)
    {
        minWidth = Math.Clamp(MathF.Min(canvasBodyRect.Width * 0.24f, 260f), 200f, 260f);
        minHeight = Math.Clamp(MathF.Min(canvasBodyRect.Height * 0.28f, 228f), 176f, 228f);
        minWidth = Math.Min(minWidth, Math.Max(canvasBodyRect.Width, 0f));
        minHeight = Math.Min(minHeight, Math.Max(canvasBodyRect.Height, 0f));
        maxWidth = Math.Min(Math.Max(minWidth, MathF.Min(canvasBodyRect.Width * 0.68f, 620f)), Math.Max(canvasBodyRect.Width, minWidth));
        maxHeight = Math.Min(Math.Max(minHeight, MathF.Min(canvasBodyRect.Height * 0.72f, 520f)), Math.Max(canvasBodyRect.Height, minHeight));
    }

    private static UiRect GetAnimationPreviewContentRect(UiRect panelRect)
    {
        const float horizontalInset = 14f;
        const float topInset = 34f;
        const float bottomInset = 14f;
        return new UiRect(
            panelRect.X + horizontalInset,
            panelRect.Y + topInset,
            Math.Max(panelRect.Width - (horizontalInset * 2f), 0f),
            Math.Max(panelRect.Height - topInset - bottomInset, 0f));
    }

    private static List<ActionRect<PixelStudioToolKind>> CreatePixelToolButtons(UiRect toolbarRect, PixelStudioSelectionMode selectionMode)
    {
        PixelStudioToolKind[] toolOrder =
        [
            PixelStudioToolKind.Select,
            PixelStudioToolKind.Hand,
            PixelStudioToolKind.Pencil,
            PixelStudioToolKind.Eraser,
            PixelStudioToolKind.Line,
            PixelStudioToolKind.Rectangle,
            PixelStudioToolKind.Ellipse,
            PixelStudioToolKind.Shape,
            PixelStudioToolKind.Fill,
            PixelStudioToolKind.Picker
        ];

        UiPanel toolPanel = new()
        {
            Id = "PixelStudio.Tools.Buttons",
            Bounds = toolbarRect,
            Padding = 0,
            Spacing = 2
        };

        IReadOnlyList<UiLayoutPlacement<PixelStudioToolKind>> placements = LayoutVertical(
            toolPanel,
            toolOrder.Select(tool => new UiLayoutItem<PixelStudioToolKind>
            {
                Id = $"PixelStudio.Tool.{tool}",
                Label = GetPixelToolLabel(tool, selectionMode),
                Value = tool,
                MinWidth = toolbarRect.Width,
                MaxWidth = toolbarRect.Width,
                Height = 28,
                HorizontalPadding = 0
            }).ToList());

        return placements
            .Select(entry => new ActionRect<PixelStudioToolKind>
            {
                Action = entry.Value,
                Rect = entry.Rect
            })
            .ToList();
    }

    private static List<ActionRect<PixelStudioAction>> CreateButtonRow(
        float startX,
        float y,
        float height,
        float gap,
        IReadOnlyList<PixelStudioAction> actions,
        float minimumWidth,
        float padding)
    {
        UiPanel rowPanel = new()
        {
            Id = $"Row.{startX:F0}.{y:F0}",
            Bounds = new UiRect(startX, y, 2000, height),
            Padding = 0,
            Spacing = gap
        };

        IReadOnlyList<UiLayoutPlacement<PixelStudioAction>> placements = LayoutHorizontal(
            rowPanel,
            actions.Select(action => new UiLayoutItem<PixelStudioAction>
            {
                Id = $"Action.{action}",
                Label = GetPixelStudioActionLabel(action),
                Value = action,
                MinWidth = Math.Max(minimumWidth, EstimateButtonWidth(GetPixelStudioActionLabel(action), 0, padding + 16, 280)),
                MaxWidth = 280,
                Height = height,
                HorizontalPadding = padding
            }).ToList(),
            wrap: false);

        return placements
            .Select(entry => new ActionRect<PixelStudioAction>
            {
                Action = entry.Value,
                Rect = entry.Rect
            })
            .ToList();
    }

    private static List<ActionRect<PixelStudioAction>> CreateRightAlignedButtonRow(
        UiRect bounds,
        float height,
        float gap,
        IReadOnlyList<PixelStudioAction> actions,
        float minimumWidth,
        float padding)
    {
        List<(PixelStudioAction Action, float Width)> measurements = actions
            .Select(action =>
            {
                float labelMinimumWidth = GetHeaderActionMinimumWidth(action, padding);
                float width = EstimateButtonWidth(
                    GetPixelStudioActionLabel(action),
                    Math.Max(minimumWidth, labelMinimumWidth),
                    padding + 14,
                    260);
                return (action, width);
            })
            .ToList();
        if (measurements.Count == 0)
        {
            return [];
        }

        float rowWidth = measurements.Sum(entry => entry.Width) + (gap * Math.Max(measurements.Count - 1, 0));
        float startX = bounds.X + Math.Max(bounds.Width - rowWidth, 0);
        float y = bounds.Y + Math.Max((bounds.Height - height) * 0.5f, 0);
        List<ActionRect<PixelStudioAction>> row = [];
        foreach ((PixelStudioAction action, float width) in measurements)
        {
            row.Add(new ActionRect<PixelStudioAction>
            {
                Action = action,
                Rect = ClampToBounds(new UiRect(startX, y, width, height), bounds)
            });
            startX += width + gap;
        }

        return row;
    }

    private static float GetHeaderActionMinimumWidth(PixelStudioAction action, float padding)
    {
        string label = GetPixelStudioActionLabel(action);
        float readableMinimumWidth = EstimateButtonWidth(label, 0, padding, 220);
        return action switch
        {
            PixelStudioAction.ZoomOut => Math.Max(readableMinimumWidth, 36),
            PixelStudioAction.ZoomIn => Math.Max(readableMinimumWidth, 36),
            PixelStudioAction.ToggleGrid => Math.Max(readableMinimumWidth, 58),
            PixelStudioAction.CycleMirrorMode => Math.Max(readableMinimumWidth, 78),
            _ => readableMinimumWidth
        };
    }

    private static string GetPixelToolLabel(PixelStudioToolKind tool, PixelStudioSelectionMode selectionMode)
    {
        return tool switch
        {
            PixelStudioToolKind.Select => selectionMode switch
            {
                PixelStudioSelectionMode.AutoGlobal => "AG",
                PixelStudioSelectionMode.AutoLocal => "AL",
                _ => "S"
            },
            PixelStudioToolKind.Hand => "H",
            PixelStudioToolKind.Pencil => "P",
            PixelStudioToolKind.Eraser => "E",
            PixelStudioToolKind.Line => "/",
            PixelStudioToolKind.Rectangle => "[]",
            PixelStudioToolKind.Ellipse => "()",
            PixelStudioToolKind.Shape => "*",
            PixelStudioToolKind.Fill => "F",
            PixelStudioToolKind.Picker => "I",
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
            PixelStudioAction.ExportSpriteStrip => "Strip",
            PixelStudioAction.ExportPngSequence => "PNGs",
            PixelStudioAction.ExportGif => "GIF",
            PixelStudioAction.ToggleOnionSkin => "Onion",
            PixelStudioAction.OpenCanvasResizeDialog => "Custom",
            PixelStudioAction.ResizeCanvas16 => "16px",
            PixelStudioAction.ResizeCanvas32 => "32px",
            PixelStudioAction.ResizeCanvas64 => "64px",
            PixelStudioAction.ResizeCanvas128 => "128px",
            PixelStudioAction.ResizeCanvas256 => "256px",
            PixelStudioAction.ResizeCanvas512 => "512px",
            PixelStudioAction.ActivateCanvasResizeWidthField => "Width",
            PixelStudioAction.ActivateCanvasResizeHeightField => "Height",
            PixelStudioAction.SetCanvasResizeAnchorTopLeft => "TL",
            PixelStudioAction.SetCanvasResizeAnchorTop => "T",
            PixelStudioAction.SetCanvasResizeAnchorTopRight => "TR",
            PixelStudioAction.SetCanvasResizeAnchorLeft => "L",
            PixelStudioAction.SetCanvasResizeAnchorCenter => "C",
            PixelStudioAction.SetCanvasResizeAnchorRight => "R",
            PixelStudioAction.SetCanvasResizeAnchorBottomLeft => "BL",
            PixelStudioAction.SetCanvasResizeAnchorBottom => "B",
            PixelStudioAction.SetCanvasResizeAnchorBottomRight => "BR",
            PixelStudioAction.ApplyCanvasResize => "Apply",
            PixelStudioAction.CancelCanvasResize => "Cancel",
            PixelStudioAction.ZoomOut => "-",
            PixelStudioAction.ZoomIn => "+",
            PixelStudioAction.ToggleGrid => "Grid",
            PixelStudioAction.CycleMirrorMode => "Mirror",
            PixelStudioAction.FitCanvas => "Fit",
            PixelStudioAction.ResetView => "Reset",
            PixelStudioAction.ExportPng => "Export",
            PixelStudioAction.ToggleNavigatorPanel => "Nav",
            PixelStudioAction.ToggleAnimationPreviewPanel => "Preview",
            PixelStudioAction.ClearSelection => "Deselect",
            PixelStudioAction.ToggleSelectionTransformMode => "Transform",
            PixelStudioAction.CopySelection => "Copy",
            PixelStudioAction.CutSelection => "Cut",
            PixelStudioAction.PasteSelection => "Paste",
            PixelStudioAction.FlipSelectionHorizontal => "Flip H",
            PixelStudioAction.FlipSelectionVertical => "Flip V",
            PixelStudioAction.RotateSelectionClockwise => "Rot+",
            PixelStudioAction.RotateSelectionCounterClockwise => "Rot-",
            PixelStudioAction.ScaleSelectionUp => "2x",
            PixelStudioAction.ScaleSelectionDown => "/2",
            PixelStudioAction.ConfirmWarningDialog => "Continue",
            PixelStudioAction.CancelWarningDialog => "Cancel",
            PixelStudioAction.NudgeSelectionLeft => "Left",
            PixelStudioAction.NudgeSelectionRight => "Right",
            PixelStudioAction.NudgeSelectionUp => "Up",
            PixelStudioAction.NudgeSelectionDown => "Down",
            PixelStudioAction.DockToolSettingsLeft => "Dock Left",
            PixelStudioAction.DockToolSettingsRight => "Dock Right",
            PixelStudioAction.DecreaseBrushSize => "Brush -",
            PixelStudioAction.IncreaseBrushSize => "Brush +",
            PixelStudioAction.ToggleTimelinePanel => "Frames",
            PixelStudioAction.TogglePaletteLibrary => "Library",
            PixelStudioAction.AddPaletteSwatch => "Add",
            PixelStudioAction.SaveCurrentPalette => "Save",
            PixelStudioAction.DuplicateSelectedPalette => "Dup",
            PixelStudioAction.ImportPalette => "Import",
            PixelStudioAction.ExportPalette => "Export",
            PixelStudioAction.GeneratePaletteFromImage => "Generate",
            PixelStudioAction.RenameSelectedPalette => "Rename",
            PixelStudioAction.DeleteSelectedPalette => "Delete",
            PixelStudioAction.PalettePromptGenerate => "Yes",
            PixelStudioAction.PalettePromptDismiss => "No",
            PixelStudioAction.PalettePromptDismissForever => "Don't Ask",
            PixelStudioAction.SwapSecondaryColor => "Swap",
            PixelStudioAction.DecreaseRed => "R-",
            PixelStudioAction.IncreaseRed => "R+",
            PixelStudioAction.DecreaseGreen => "G-",
            PixelStudioAction.IncreaseGreen => "G+",
            PixelStudioAction.DecreaseBlue => "B-",
            PixelStudioAction.IncreaseBlue => "B+",
            PixelStudioAction.AddLayer => "Layer +",
            PixelStudioAction.ToggleLayerOpacityControls => "Opacity",
            PixelStudioAction.ToggleLayerAlphaLock => "Alpha",
            PixelStudioAction.DeleteLayer => "Layer -",
            PixelStudioAction.AddFrame => "Frame +",
            PixelStudioAction.DuplicateFrame => "Dup",
            PixelStudioAction.CopyFrame => "Copy",
            PixelStudioAction.PasteFrame => "Paste",
            PixelStudioAction.DeleteFrame => "Frame -",
            PixelStudioAction.TogglePlayback => "Play",
            PixelStudioAction.DecreaseFrameRate => "FPS -",
            PixelStudioAction.IncreaseFrameRate => "FPS +",
            PixelStudioAction.DecreaseFrameDuration => "Dur -",
            PixelStudioAction.IncreaseFrameDuration => "Dur +",
            _ => action.ToString()
        };
    }
}

using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Shell;

namespace ProjectSPlus.App.Editor;

public static partial class EditorLayoutEngine
{
    private static EditorLayoutSnapshot CreateUnified(int width, int height, ShellLayout shellLayout, EditorUiState uiState)
    {
        int menuHeight = shellLayout.MenuBarHeight;
        int tabHeight = shellLayout.TabStripHeight;
        int statusHeight = shellLayout.StatusBarHeight;
        int contentHeight = Math.Max(height - menuHeight - statusHeight, 120);
        EditorPageKind currentPage = uiState.Tabs.FirstOrDefault(tab => tab.Id == uiState.SelectedTabId)?.Page ?? EditorPageKind.Home;

        float preferredLeftWidth = uiState.LeftPanelCollapsed
            ? CollapsedPanelWidth
            : Math.Clamp(uiState.LeftPanelPreferredWidth, 220f, Math.Max(220f, width / 2.6f));
        float preferredRightWidth = uiState.RightPanelCollapsed
            ? CollapsedPanelWidth
            : Math.Clamp(uiState.RightPanelPreferredWidth, 240f, Math.Max(240f, width / 2.4f));

        IReadOnlyList<AdaptivePanelAllocation> rootAllocations = AllocateAdaptiveWidths(
            width,
            [
                new AdaptivePanelSpec
                {
                    Id = "Shell.Left",
                    DockSide = DockSide.Left,
                    MinWidth = uiState.LeftPanelCollapsed ? CollapsedPanelWidth : 220,
                    PreferredWidth = preferredLeftWidth,
                    FlexibleWidth = true,
                    Priority = 0,
                    AllowCollapse = true,
                    CollapsedWidth = CollapsedPanelWidth
                },
                new AdaptivePanelSpec
                {
                    Id = "Shell.Workspace",
                    DockSide = DockSide.Center,
                    MinWidth = currentPage == EditorPageKind.PixelStudio ? 360 : 320,
                    PreferredWidth = Math.Max(width - preferredLeftWidth - preferredRightWidth, 520),
                    FlexibleWidth = true,
                    Priority = 5
                },
                new AdaptivePanelSpec
                {
                    Id = "Shell.Right",
                    DockSide = DockSide.Right,
                    MinWidth = uiState.RightPanelCollapsed ? CollapsedPanelWidth : 240,
                    PreferredWidth = preferredRightWidth,
                    FlexibleWidth = true,
                    Priority = 1,
                    AllowCollapse = true,
                    CollapsedWidth = CollapsedPanelWidth
                }
            ]);

        AdaptivePanelAllocation leftAllocation = rootAllocations.First(panel => panel.Id == "Shell.Left");
        AdaptivePanelAllocation workspaceAllocation = rootAllocations.First(panel => panel.Id == "Shell.Workspace");
        AdaptivePanelAllocation rightAllocation = rootAllocations.First(panel => panel.Id == "Shell.Right");
        bool leftCollapsed = uiState.LeftPanelCollapsed || leftAllocation.IsCollapsed;
        bool rightCollapsed = uiState.RightPanelCollapsed || rightAllocation.IsCollapsed;
        float leftWidth = MathF.Round(Math.Max(leftCollapsed ? CollapsedPanelWidth : leftAllocation.Width, 0));
        float rightWidth = MathF.Round(Math.Max(rightCollapsed ? CollapsedPanelWidth : rightAllocation.Width, 0));
        float workspaceWidth = Math.Max(width - leftWidth - rightWidth, 0);
        float workspaceX = leftWidth;
        float workspaceHeight = Math.Max(contentHeight - tabHeight, 120);
        float contentTop = menuHeight;

        UiRect leftPanelRect = SnapRect(new UiRect(0, menuHeight, leftWidth, contentHeight));
        UiRect rightPanelRect = SnapRect(new UiRect(width - rightWidth, menuHeight, rightWidth, contentHeight));
        UiRect workspaceRect = SnapRect(new UiRect(workspaceX, menuHeight + tabHeight, workspaceWidth, workspaceHeight));
        UiRect leftPanelHeaderRect = GetPanelHeaderRect(leftPanelRect);
        UiRect leftPanelBodyRect = GetPanelBodyRect(leftPanelRect);
        UiRect rightPanelHeaderRect = GetPanelHeaderRect(rightPanelRect);
        UiRect rightPanelBodyRect = GetPanelBodyRect(rightPanelRect);
        UiRect leftSplitterRect = SnapRect(new UiRect(Math.Max(leftPanelRect.X + leftPanelRect.Width - 4, 0), contentTop, 8, contentHeight));
        UiRect rightSplitterRect = SnapRect(new UiRect(Math.Max(rightPanelRect.X - 4, 0), contentTop, 8, contentHeight));
        const float collapseHandleWidth = 12f;
        const float collapseHandleHeight = 38f;
        UiRect leftCollapseHandleRect = SnapRect(new UiRect(leftPanelRect.X + leftPanelRect.Width - collapseHandleWidth, leftPanelRect.Y + Math.Max((leftPanelRect.Height - collapseHandleHeight) * 0.5f, 18), collapseHandleWidth, collapseHandleHeight));
        UiRect rightCollapseHandleRect = SnapRect(new UiRect(rightPanelRect.X, rightPanelRect.Y + Math.Max((rightPanelRect.Height - collapseHandleHeight) * 0.5f, 18), collapseHandleWidth, collapseHandleHeight));

        const float pagePadding = 22f;
        const float pageGap = 18f;
        UiRect workspaceInnerRect = new(
            workspaceRect.X + pagePadding,
            workspaceRect.Y + pagePadding,
            Math.Max(workspaceRect.Width - (pagePadding * 2), 0),
            Math.Max(workspaceRect.Height - (pagePadding * 2), 0));

        const float menuButtonHeight = 28f;
        float menuButtonTop = MathF.Round(Math.Max((menuHeight - menuButtonHeight) * 0.5f, 8f));
        float logoWidth = EstimateButtonWidth(EditorBranding.EngineName, 188, 88, 198, 18f);
        UiPanel menuPanel = new()
        {
            Id = "TopBar.MenuStrip",
            Bounds = new UiRect(14, menuButtonTop, Math.Max(width - logoWidth - 22, 120), menuButtonHeight),
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
                    MaxWidth = 168,
                    Height = menuButtonHeight,
                    HorizontalPadding = 38
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
        float logoX = Math.Max(width - logoWidth - 8, 14);
        UiRect menuLogoRect = new(logoX, 6, logoWidth, 46);

        UiPanel tabPanel = new()
        {
            Id = "TopBar.TabStrip",
            Bounds = new UiRect(workspaceX + 10, menuHeight + 6, Math.Max(workspaceWidth - 20, 120), Math.Max(tabHeight - 12, 22)),
            Padding = 0,
            Spacing = 6
        };
        IReadOnlyList<UiLayoutPlacement<EditorWorkspaceTab>> tabPlacements = LayoutHorizontal(
            tabPanel,
            uiState.Tabs
                .Select(tab => new UiLayoutItem<EditorWorkspaceTab>
                {
                    MinWidth = EstimateButtonWidth(
                        tab.Title,
                        tab.Page == EditorPageKind.Scratch ? 124 : 92,
                        tab.Page == EditorPageKind.Scratch ? 56 : 34,
                        float.PositiveInfinity,
                        16f),
                    Id = $"Tab.{tab.Id}",
                    Label = tab.Title,
                    Value = tab,
                    MaxWidth = float.PositiveInfinity,
                    Height = Math.Max(tabHeight - 12, 22),
                    HorizontalPadding = tab.Page == EditorPageKind.Scratch ? 48 : 32,
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

            if (tabPlacement.Value.Page == EditorPageKind.Scratch && tabPlacement.Rect.Width >= 110)
            {
                tabCloseButtons.Add(new NamedRect
                {
                    Id = tabPlacement.Value.Id,
                    Rect = ClampToBounds(new UiRect(tabPlacement.Rect.X + tabPlacement.Rect.Width - 22, tabPlacement.Rect.Y + 5, 14, 14), tabPlacement.Rect)
                });
            }
        }

        List<IndexedRect> leftPanelRecentRows = [];
        UiRect? leftPanelRecentViewportRect = null;
        UiRect? leftPanelRecentScrollTrackRect = null;
        UiRect? leftPanelRecentScrollThumbRect = null;
        if (!leftCollapsed)
        {
            float leftInfoHeight = leftPanelBodyRect.Width >= 180 ? 136 : 96;
            UiRect viewportFrame = new(
                leftPanelBodyRect.X,
                leftPanelBodyRect.Y + leftInfoHeight,
                leftPanelBodyRect.Width,
                Math.Max(leftPanelBodyRect.Height - leftInfoHeight, 0));
            ScrollRegionLayout sideRecentScroll = CreateScrollRegion(viewportFrame, uiState.RecentProjects.Count, 44, 8, uiState.LeftPanelRecentScrollRow);
            leftPanelRecentViewportRect = sideRecentScroll.ContentRect;
            leftPanelRecentScrollTrackRect = sideRecentScroll.TrackRect;
            leftPanelRecentScrollThumbRect = sideRecentScroll.ThumbRect;
            for (int visibleIndex = 0; visibleIndex < sideRecentScroll.VisibleRows; visibleIndex++)
            {
                int projectIndex = sideRecentScroll.StartRow + visibleIndex;
                if (projectIndex >= uiState.RecentProjects.Count)
                {
                    break;
                }

                leftPanelRecentRows.Add(new IndexedRect
                {
                    Index = projectIndex,
                    Rect = new UiRect(
                        leftPanelRecentViewportRect.Value.X,
                        leftPanelRecentViewportRect.Value.Y + (visibleIndex * 52),
                        leftPanelRecentViewportRect.Value.Width,
                        44)
                });
            }
        }

        List<ActionRect<EditorHomeAction>> homeCards = [];
        List<IndexedRect> recentRows = [];
        UiRect? homeHeroPanelRect = null;
        UiRect? homeActionsPanelRect = null;
        UiRect? homeRecentPanelRect = null;
        UiRect? homeRecentViewportRect = null;
        UiRect? homeRecentScrollTrackRect = null;
        UiRect? homeRecentScrollThumbRect = null;

        List<ActionRect<ProjectFormAction>> projectFormActions = [];
        List<IndexedRect> projectRows = [];
        UiRect? projectsFormPanelRect = null;
        UiRect? projectsRecentPanelRect = null;
        UiRect? projectsRecentViewportRect = null;
        UiRect? projectsRecentScrollTrackRect = null;
        UiRect? projectsRecentScrollThumbRect = null;

        List<ActionRect<EditorPreferenceAction>> preferenceActions = [];
        List<IndexedRect> preferenceRows = [];
        UiRect? preferencesGeneralPanelRect = null;
        UiRect? preferencesShortcutPanelRect = null;
        UiRect? preferenceViewportRect = null;
        UiRect? preferenceScrollTrackRect = null;
        UiRect? preferenceScrollThumbRect = null;
        List<ActionRect<EditorThemeStudioAction>> themeStudioButtons = [];
        List<ActionRect<EditorThemeColorRole>> themeStudioRoleButtons = [];
        UiRect? themeStudioDialogRect = null;
        UiRect? themeStudioNameFieldRect = null;
        UiRect? themeStudioWheelRect = null;
        UiRect? themeStudioWheelFieldRect = null;
        UiRect? themeStudioPreviewRect = null;

        UiRect? layoutInfoPanelRect = null;
        UiRect? scratchInfoPanelRect = null;

        UiRect? folderPickerRect = null;
        UiRect? folderPickerHeaderRect = null;
        UiRect? folderPickerBodyRect = null;
        UiRect? folderPickerViewportRect = null;
        UiRect? folderPickerScrollTrackRect = null;
        UiRect? folderPickerScrollThumbRect = null;
        IReadOnlyList<ActionRect<EditorFolderPickerAction>> folderPickerActions = [];
        IReadOnlyList<IndexedRect> folderPickerRows = [];

        switch (currentPage)
        {
            case EditorPageKind.Home:
            {
                float heroHeight = Math.Clamp(workspaceInnerRect.Height * 0.18f, 112, 132);
                float actionsHeight = Math.Clamp(workspaceInnerRect.Height * 0.28f, 184, 236);
                homeHeroPanelRect = new UiRect(workspaceInnerRect.X, workspaceInnerRect.Y, workspaceInnerRect.Width, heroHeight);
                homeActionsPanelRect = new UiRect(workspaceInnerRect.X, homeHeroPanelRect.Value.Y + homeHeroPanelRect.Value.Height + pageGap, workspaceInnerRect.Width, actionsHeight);
                homeRecentPanelRect = new UiRect(
                    workspaceInnerRect.X,
                    homeActionsPanelRect.Value.Y + homeActionsPanelRect.Value.Height + pageGap,
                    workspaceInnerRect.Width,
                    Math.Max(workspaceInnerRect.Y + workspaceInnerRect.Height - (homeActionsPanelRect.Value.Y + homeActionsPanelRect.Value.Height + pageGap), 160));

                UiPanel actionPanel = new()
                {
                    Id = "Home.Actions",
                    Bounds = GetPanelBodyRect(homeActionsPanelRect.Value),
                    Padding = 0,
                    Spacing = 14
                };
                IReadOnlyList<UiLayoutPlacement<EditorHomeAction>> cardPlacements = LayoutHorizontal(
                    actionPanel,
                    new[]
                    {
                        new UiLayoutItem<EditorHomeAction> { Id = "Home.Action.NewProject", Label = "New Project Slot", Value = EditorHomeAction.CreateProjectSlot, MinWidth = 196, MaxWidth = 248, Height = 118, HorizontalPadding = 28, Priority = 3 },
                        new UiLayoutItem<EditorHomeAction> { Id = "Home.Action.PixelStudio", Label = EditorBranding.PixelToolName, Value = EditorHomeAction.OpenPixelStudio, MinWidth = 196, MaxWidth = 244, Height = 118, HorizontalPadding = 28, Priority = 3 },
                        new UiLayoutItem<EditorHomeAction> { Id = "Home.Action.Projects", Label = "Projects", Value = EditorHomeAction.OpenProjects, MinWidth = 170, MaxWidth = 220, Height = 118, HorizontalPadding = 28, Priority = 2 },
                        new UiLayoutItem<EditorHomeAction> { Id = "Home.Action.Preferences", Label = "Preferences", Value = EditorHomeAction.OpenPreferences, MinWidth = 188, MaxWidth = 238, Height = 118, HorizontalPadding = 28, Priority = 2 }
                    },
                    wrap: true);
                homeCards = cardPlacements
                    .Select(card => new ActionRect<EditorHomeAction> { Action = card.Value, Rect = card.Rect })
                    .ToList();

                ScrollRegionLayout homeRecentScroll = CreateScrollRegion(GetPanelBodyRect(homeRecentPanelRect.Value), uiState.RecentProjects.Count, 50, 8, uiState.HomeRecentScrollRow);
                homeRecentViewportRect = homeRecentScroll.ContentRect;
                homeRecentScrollTrackRect = homeRecentScroll.TrackRect;
                homeRecentScrollThumbRect = homeRecentScroll.ThumbRect;
                for (int visibleIndex = 0; visibleIndex < homeRecentScroll.VisibleRows; visibleIndex++)
                {
                    int projectIndex = homeRecentScroll.StartRow + visibleIndex;
                    if (projectIndex >= uiState.RecentProjects.Count)
                    {
                        break;
                    }

                    recentRows.Add(new IndexedRect
                    {
                        Index = projectIndex,
                        Rect = new UiRect(
                            homeRecentViewportRect.Value.X,
                            homeRecentViewportRect.Value.Y + (visibleIndex * 58),
                            homeRecentViewportRect.Value.Width,
                            50)
                    });
                }

                break;
            }
            case EditorPageKind.Projects:
            {
                float formHeight = Math.Clamp(workspaceInnerRect.Height * 0.42f, 252, 328);
                projectsFormPanelRect = new UiRect(workspaceInnerRect.X, workspaceInnerRect.Y, workspaceInnerRect.Width, formHeight);
                projectsRecentPanelRect = new UiRect(
                    workspaceInnerRect.X,
                    projectsFormPanelRect.Value.Y + projectsFormPanelRect.Value.Height + pageGap,
                    workspaceInnerRect.Width,
                    Math.Max(workspaceInnerRect.Y + workspaceInnerRect.Height - (projectsFormPanelRect.Value.Y + projectsFormPanelRect.Value.Height + pageGap), 160));

                UiRect formBodyRect = GetPanelBodyRect(projectsFormPanelRect.Value);
                float nameFieldWidth = Math.Min(Math.Max(formBodyRect.Width * 0.46f, 280), formBodyRect.Width);
                UiRect projectNameRect = new(formBodyRect.X, formBodyRect.Y + 36, nameFieldWidth, 46);
                float browseWidth = EstimateButtonWidth("Browse Folder", 164, 36, 208);
                float pathFieldWidth = Math.Max(formBodyRect.Width - browseWidth - 12, 220);
                UiRect projectPathRect = new(formBodyRect.X, projectNameRect.Y + 92, pathFieldWidth, 46);
                float browseX = Math.Min(projectPathRect.X + projectPathRect.Width + 12, formBodyRect.X + Math.Max(formBodyRect.Width - browseWidth, 0));
                UiRect browseRect = new(browseX, projectPathRect.Y, Math.Min(browseWidth, Math.Max(formBodyRect.X + formBodyRect.Width - browseX, 0)), 46);
                projectFormActions.Add(new ActionRect<ProjectFormAction> { Action = ProjectFormAction.ActivateProjectName, Rect = projectNameRect });
                projectFormActions.Add(new ActionRect<ProjectFormAction> { Action = ProjectFormAction.ActivateProjectLibraryPath, Rect = projectPathRect });
                projectFormActions.Add(new ActionRect<ProjectFormAction> { Action = ProjectFormAction.OpenFolderPicker, Rect = ClampToBounds(browseRect, formBodyRect) });

                UiPanel projectActionPanel = new()
                {
                    Id = "Projects.Actions",
                    Bounds = new UiRect(formBodyRect.X, projectPathRect.Y + 82, formBodyRect.Width, Math.Max(formBodyRect.Y + formBodyRect.Height - (projectPathRect.Y + 82), 56)),
                    Padding = 0,
                    Spacing = 12
                };
                IReadOnlyList<UiLayoutPlacement<ProjectFormAction>> actionPlacements = LayoutHorizontal(
                    projectActionPanel,
                    new[]
                    {
                        new UiLayoutItem<ProjectFormAction> { Id = "Projects.Create", Label = "Create Project", Value = ProjectFormAction.CreateProject, MinWidth = 170, MaxWidth = 220, Height = 48, HorizontalPadding = 34, Priority = 3 },
                        new UiLayoutItem<ProjectFormAction> { Id = "Projects.Documents", Label = "Use Documents", Value = ProjectFormAction.UseDocumentsFolder, MinWidth = 166, MaxWidth = 214, Height = 48, HorizontalPadding = 30, Priority = 2 },
                        new UiLayoutItem<ProjectFormAction> { Id = "Projects.Desktop", Label = "Use Desktop", Value = ProjectFormAction.UseDesktopFolder, MinWidth = 152, MaxWidth = 202, Height = 48, HorizontalPadding = 30, Priority = 2 }
                    },
                    wrap: true);
                projectFormActions.AddRange(actionPlacements.Select(action => new ActionRect<ProjectFormAction> { Action = action.Value, Rect = action.Rect }));

                ScrollRegionLayout projectRecentScroll = CreateScrollRegion(GetPanelBodyRect(projectsRecentPanelRect.Value), uiState.RecentProjects.Count, 50, 8, uiState.ProjectRecentScrollRow);
                projectsRecentViewportRect = projectRecentScroll.ContentRect;
                projectsRecentScrollTrackRect = projectRecentScroll.TrackRect;
                projectsRecentScrollThumbRect = projectRecentScroll.ThumbRect;
                for (int visibleIndex = 0; visibleIndex < projectRecentScroll.VisibleRows; visibleIndex++)
                {
                    int projectIndex = projectRecentScroll.StartRow + visibleIndex;
                    if (projectIndex >= uiState.RecentProjects.Count)
                    {
                        break;
                    }

                    projectRows.Add(new IndexedRect
                    {
                        Index = projectIndex,
                        Rect = new UiRect(
                            projectsRecentViewportRect.Value.X,
                            projectsRecentViewportRect.Value.Y + (visibleIndex * 58),
                            projectsRecentViewportRect.Value.Width,
                            50)
                    });
                }

                if (uiState.ProjectForm.FolderPickerVisible)
                {
                    float pickerWidth = Math.Min(430, Math.Max(workspaceInnerRect.Width - 28, 280));
                    float pickerHeight = Math.Min(360, Math.Max(workspaceInnerRect.Height - 24, 220));
                    folderPickerRect = new UiRect(
                        workspaceInnerRect.X + Math.Max(workspaceInnerRect.Width - pickerWidth, 0),
                        workspaceInnerRect.Y + 10,
                        pickerWidth,
                        pickerHeight);
                    folderPickerHeaderRect = GetPanelHeaderRect(folderPickerRect.Value);
                    folderPickerBodyRect = GetPanelBodyRect(folderPickerRect.Value);

                    folderPickerActions =
                    [
                        new ActionRect<EditorFolderPickerAction>
                        {
                            Action = EditorFolderPickerAction.NavigateUp,
                            Rect = new UiRect(folderPickerBodyRect.Value.X, folderPickerBodyRect.Value.Y, 92, 32)
                        },
                        new ActionRect<EditorFolderPickerAction>
                        {
                            Action = EditorFolderPickerAction.SelectCurrent,
                            Rect = new UiRect(folderPickerBodyRect.Value.X + Math.Max(folderPickerBodyRect.Value.Width - 148, 0), folderPickerBodyRect.Value.Y, 148, 32)
                        }
                    ];

                    UiRect pickerViewportFrame = new(
                        folderPickerBodyRect.Value.X,
                        folderPickerBodyRect.Value.Y + 70,
                        folderPickerBodyRect.Value.Width,
                        Math.Max(folderPickerBodyRect.Value.Height - 70, 0));
                    ScrollRegionLayout pickerScroll = CreateScrollRegion(pickerViewportFrame, uiState.ProjectForm.FolderPickerEntries.Count, 30, 6, uiState.FolderPickerScrollRow);
                    folderPickerViewportRect = pickerScroll.ContentRect;
                    folderPickerScrollTrackRect = pickerScroll.TrackRect;
                    folderPickerScrollThumbRect = pickerScroll.ThumbRect;
                    List<IndexedRect> pickerRows = [];
                    for (int visibleIndex = 0; visibleIndex < pickerScroll.VisibleRows; visibleIndex++)
                    {
                        int entryIndex = pickerScroll.StartRow + visibleIndex;
                        if (entryIndex >= uiState.ProjectForm.FolderPickerEntries.Count)
                        {
                            break;
                        }

                        pickerRows.Add(new IndexedRect
                        {
                            Index = entryIndex,
                            Rect = new UiRect(
                                folderPickerViewportRect.Value.X,
                                folderPickerViewportRect.Value.Y + (visibleIndex * 36),
                                folderPickerViewportRect.Value.Width,
                                30)
                        });
                    }

                    folderPickerRows = pickerRows;
                }

                break;
            }
            case EditorPageKind.Preferences:
            {
                float generalHeight = Math.Clamp(workspaceInnerRect.Height * 0.38f, 258, 324);
                preferencesGeneralPanelRect = new UiRect(workspaceInnerRect.X, workspaceInnerRect.Y, workspaceInnerRect.Width, generalHeight);
                preferencesShortcutPanelRect = new UiRect(
                    workspaceInnerRect.X,
                    preferencesGeneralPanelRect.Value.Y + preferencesGeneralPanelRect.Value.Height + pageGap,
                    workspaceInnerRect.Width,
                    Math.Max(workspaceInnerRect.Y + workspaceInnerRect.Height - (preferencesGeneralPanelRect.Value.Y + preferencesGeneralPanelRect.Value.Height + pageGap), 180));

                UiRect generalBodyRect = GetPanelBodyRect(preferencesGeneralPanelRect.Value);
                UiPanel preferenceActionPanel = new()
                {
                    Id = "Preferences.Actions",
                    Bounds = new UiRect(generalBodyRect.X, generalBodyRect.Y + 54, generalBodyRect.Width, 220),
                    Padding = 0,
                    Spacing = 12
                };
                IReadOnlyList<UiLayoutPlacement<EditorPreferenceAction>> preferencePlacements = LayoutHorizontal(
                    preferenceActionPanel,
                    new[]
                    {
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.Theme", Label = $"Theme: {uiState.ThemeLabel}", Value = EditorPreferenceAction.ToggleTheme, MinWidth = 188, MaxWidth = 242, Height = 50, HorizontalPadding = 36, Priority = 3 },
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.Size", Label = $"Text: {uiState.FontSizeLabel}", Value = EditorPreferenceAction.CycleFontSize, MinWidth = 178, MaxWidth = 232, Height = 50, HorizontalPadding = 36, Priority = 3 },
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.Font", Label = $"Font: {uiState.FontFamily}", Value = EditorPreferenceAction.CycleFontFamily, MinWidth = 224, MaxWidth = 320, Height = 50, HorizontalPadding = 36, Priority = 2 },
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.Picker", Label = $"Picker: {GetColorPickerModeLabel(uiState.PixelStudio.ColorPickerMode)}", Value = EditorPreferenceAction.CycleColorPickerMode, MinWidth = 216, MaxWidth = 284, Height = 50, HorizontalPadding = 30, Priority = 3 },
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.Sound", Label = $"Sounds: {uiState.NotificationSoundLabel}", Value = EditorPreferenceAction.CycleNotificationSoundMode, MinWidth = 212, MaxWidth = 280, Height = 50, HorizontalPadding = 30, Priority = 3 },
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.TransformSnap", Label = $"Rotate Snap: {uiState.TransformRotationSnapLabel}", Value = EditorPreferenceAction.CycleTransformRotationSnap, MinWidth = 236, MaxWidth = 320, Height = 50, HorizontalPadding = 30, Priority = 3 },
                        new UiLayoutItem<EditorPreferenceAction> { Id = "Preferences.ThemeStudio", Label = "Theme Studio", Value = EditorPreferenceAction.OpenThemeStudio, MinWidth = 198, MaxWidth = 250, Height = 50, HorizontalPadding = 30, Priority = 3 }
                    },
                    wrap: true);
                preferenceActions = preferencePlacements
                    .Select(action => new ActionRect<EditorPreferenceAction> { Action = action.Value, Rect = action.Rect })
                    .ToList();

                if (uiState.ThemeStudio.Visible)
                {
                    float dialogWidth = Math.Min(760f, Math.Max(workspaceInnerRect.Width - 28f, 360f));
                    float dialogHeight = Math.Min(452f, Math.Max(workspaceInnerRect.Height - 28f, 320f));
                    themeStudioDialogRect = new UiRect(
                        workspaceInnerRect.X + Math.Max((workspaceInnerRect.Width - dialogWidth) * 0.5f, 0f),
                        workspaceInnerRect.Y + Math.Max((workspaceInnerRect.Height - dialogHeight) * 0.5f, 0f),
                        dialogWidth,
                        dialogHeight);

                    UiRect dialogContentRect = Inset(themeStudioDialogRect.Value, 16f);
                    themeStudioNameFieldRect = new UiRect(dialogContentRect.X, dialogContentRect.Y + 60f, dialogContentRect.Width - 160f, 38f);
                    themeStudioPreviewRect = new UiRect(dialogContentRect.X + dialogContentRect.Width - 116f, dialogContentRect.Y + 48f, 96f, 96f);

                    float roleAreaTop = themeStudioNameFieldRect.Value.Y + themeStudioNameFieldRect.Value.Height + 24f;
                    float roleColumnWidth = Math.Min(168f, Math.Max((dialogContentRect.Width * 0.42f) - 12f, 132f));
                    float roleButtonHeight = 32f;
                    float roleGap = 8f;
                    float roleSecondColumnX = dialogContentRect.X + roleColumnWidth + 10f;
                    EditorThemeColorRole[] roles = Enum.GetValues<EditorThemeColorRole>();
                    for (int index = 0; index < roles.Length; index++)
                    {
                        int column = index % 2;
                        int row = index / 2;
                        float buttonX = column == 0 ? dialogContentRect.X : roleSecondColumnX;
                        float buttonWidth = column == 0
                            ? roleColumnWidth
                            : Math.Max(roleColumnWidth - 4f, 128f);
                        themeStudioRoleButtons.Add(new ActionRect<EditorThemeColorRole>
                        {
                            Action = roles[index],
                            Rect = new UiRect(
                                buttonX,
                                roleAreaTop + (row * (roleButtonHeight + roleGap)),
                                buttonWidth,
                                roleButtonHeight)
                        });
                    }

                    float roleAreaWidth = roleSecondColumnX + Math.Max(roleColumnWidth - 4f, 128f) - dialogContentRect.X;
                    float wheelAvailableWidth = Math.Max(dialogContentRect.Width - roleAreaWidth - 28f, 140f);
                    float wheelSize = Math.Clamp(MathF.Min(wheelAvailableWidth, dialogContentRect.Height - 168f), 140f, 208f);
                    float wheelX = dialogContentRect.X + dialogContentRect.Width - wheelSize - 20f;
                    float wheelY = roleAreaTop + 10f;
                    themeStudioWheelRect = new UiRect(wheelX, wheelY, wheelSize, wheelSize);
                    themeStudioWheelFieldRect = GetPaletteColorWheelFieldRect(themeStudioWheelRect.Value);

                    float dialogButtonWidth = 124f;
                    float buttonGap = 10f;
                    float buttonY = themeStudioDialogRect.Value.Y + themeStudioDialogRect.Value.Height - 54f;
                    float buttonRight = themeStudioDialogRect.Value.X + themeStudioDialogRect.Value.Width - 16f;
                    themeStudioButtons.Add(new ActionRect<EditorThemeStudioAction>
                    {
                        Action = EditorThemeStudioAction.CancelThemeStudio,
                        Rect = new UiRect(buttonRight - dialogButtonWidth, buttonY, dialogButtonWidth, 32f)
                    });
                    themeStudioButtons.Add(new ActionRect<EditorThemeStudioAction>
                    {
                        Action = EditorThemeStudioAction.SaveTheme,
                        Rect = new UiRect(buttonRight - ((dialogButtonWidth + buttonGap) * 2), buttonY, dialogButtonWidth, 32f)
                    });
                    if (uiState.ThemeStudio.CanDelete)
                    {
                        themeStudioButtons.Add(new ActionRect<EditorThemeStudioAction>
                        {
                            Action = EditorThemeStudioAction.DeleteTheme,
                            Rect = new UiRect(buttonRight - ((dialogButtonWidth + buttonGap) * 3), buttonY, dialogButtonWidth, 32f)
                        });
                    }
                }

                UiRect shortcutBodyRect = GetPanelBodyRect(preferencesShortcutPanelRect.Value);
                UiRect shortcutViewportFrame = new(
                    shortcutBodyRect.X,
                    shortcutBodyRect.Y + 24,
                    shortcutBodyRect.Width,
                    Math.Max(shortcutBodyRect.Height - 24, 0));
                ScrollRegionLayout shortcutScroll = CreateScrollRegion(shortcutViewportFrame, uiState.Shortcuts.Count, 52, 8, uiState.PreferenceScrollRow);
                preferenceViewportRect = shortcutScroll.ContentRect;
                preferenceScrollTrackRect = shortcutScroll.TrackRect;
                preferenceScrollThumbRect = shortcutScroll.ThumbRect;
                for (int visibleIndex = 0; visibleIndex < shortcutScroll.VisibleRows; visibleIndex++)
                {
                    int shortcutIndex = shortcutScroll.StartRow + visibleIndex;
                    if (shortcutIndex >= uiState.Shortcuts.Count)
                    {
                        break;
                    }

                    preferenceRows.Add(new IndexedRect
                    {
                        Index = shortcutIndex,
                        Rect = new UiRect(
                            preferenceViewportRect.Value.X,
                            preferenceViewportRect.Value.Y + (visibleIndex * 60),
                            preferenceViewportRect.Value.Width,
                            52)
                    });
                }

                break;
            }
            case EditorPageKind.Layout:
                layoutInfoPanelRect = new UiRect(workspaceInnerRect.X, workspaceInnerRect.Y, workspaceInnerRect.Width, workspaceInnerRect.Height);
                break;
            case EditorPageKind.Scratch:
                scratchInfoPanelRect = new UiRect(workspaceInnerRect.X, workspaceInnerRect.Y, workspaceInnerRect.Width, workspaceInnerRect.Height);
                break;
        }

        IReadOnlyList<ActionRect<EditorMenuAction>> menuEntries = [];
        UiRect? menuDropdownRect = null;
        if (!string.IsNullOrWhiteSpace(uiState.OpenMenuName))
        {
            IReadOnlyList<EditorMenuEntry> entries = GetMenuEntries(uiState.OpenMenuName);
            NamedRect? button = menuButtons.FirstOrDefault(entry => entry.Id == uiState.OpenMenuName);
            if (button is not null && entries.Count > 0)
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

        PixelStudioLayoutSnapshot? pixelStudioLayout = uiState.PixelStudio.CanvasWidth > 0 && uiState.PixelStudio.CanvasHeight > 0
            ? CreatePixelStudioLayout(workspaceRect, uiState.PixelStudio)
            : null;

        return new EditorLayoutSnapshot
        {
            LeftPanelRect = leftPanelRect,
            LeftPanelHeaderRect = leftPanelHeaderRect,
            LeftPanelBodyRect = leftPanelBodyRect,
            RightPanelRect = rightPanelRect,
            RightPanelHeaderRect = rightPanelHeaderRect,
            RightPanelBodyRect = rightPanelBodyRect,
            WorkspaceRect = workspaceRect,
            StatusBarRect = new UiRect(0, height - statusHeight, width, statusHeight),
            MenuBarRect = new UiRect(0, 0, width, menuHeight),
            MenuLogoRect = menuLogoRect,
            TabStripRect = new UiRect(workspaceX, menuHeight, workspaceWidth, tabHeight),
            LeftSplitterRect = leftSplitterRect,
            RightSplitterRect = rightSplitterRect,
            LeftCollapseHandleRect = leftCollapseHandleRect,
            RightCollapseHandleRect = rightCollapseHandleRect,
            MenuButtons = menuButtons,
            TabButtons = tabButtons,
            HomeHeroPanelRect = homeHeroPanelRect,
            HomeActionsPanelRect = homeActionsPanelRect,
            HomeRecentPanelRect = homeRecentPanelRect,
            HomeCards = homeCards,
            RecentProjectRows = recentRows,
            HomeRecentViewportRect = homeRecentViewportRect,
            HomeRecentScrollTrackRect = homeRecentScrollTrackRect,
            HomeRecentScrollThumbRect = homeRecentScrollThumbRect,
            ProjectsFormPanelRect = projectsFormPanelRect,
            ProjectsRecentPanelRect = projectsRecentPanelRect,
            ProjectRows = projectRows,
            ProjectsRecentViewportRect = projectsRecentViewportRect,
            ProjectsRecentScrollTrackRect = projectsRecentScrollTrackRect,
            ProjectsRecentScrollThumbRect = projectsRecentScrollThumbRect,
            PreferencesGeneralPanelRect = preferencesGeneralPanelRect,
            PreferencesShortcutPanelRect = preferencesShortcutPanelRect,
            PreferenceRows = preferenceRows,
            PreferenceViewportRect = preferenceViewportRect,
            PreferenceScrollTrackRect = preferenceScrollTrackRect,
            PreferenceScrollThumbRect = preferenceScrollThumbRect,
            ThemeStudioDialogRect = themeStudioDialogRect,
            ThemeStudioNameFieldRect = themeStudioNameFieldRect,
            ThemeStudioWheelRect = themeStudioWheelRect,
            ThemeStudioWheelFieldRect = themeStudioWheelFieldRect,
            ThemeStudioPreviewRect = themeStudioPreviewRect,
            LayoutInfoPanelRect = layoutInfoPanelRect,
            ScratchInfoPanelRect = scratchInfoPanelRect,
            LeftPanelRecentProjectRows = leftPanelRecentRows,
            LeftPanelRecentViewportRect = leftPanelRecentViewportRect,
            LeftPanelRecentScrollTrackRect = leftPanelRecentScrollTrackRect,
            LeftPanelRecentScrollThumbRect = leftPanelRecentScrollThumbRect,
            MenuEntries = menuEntries,
            MenuDropdownRect = menuDropdownRect,
            TabCloseButtons = tabCloseButtons,
            ProjectFormActions = projectFormActions,
            PreferenceActions = preferenceActions,
            ThemeStudioButtons = themeStudioButtons,
            ThemeStudioRoleButtons = themeStudioRoleButtons,
            FolderPickerActions = folderPickerActions,
            FolderPickerRows = folderPickerRows,
            FolderPickerRect = folderPickerRect,
            FolderPickerHeaderRect = folderPickerHeaderRect,
            FolderPickerBodyRect = folderPickerBodyRect,
            FolderPickerViewportRect = folderPickerViewportRect,
            FolderPickerScrollTrackRect = folderPickerScrollTrackRect,
            FolderPickerScrollThumbRect = folderPickerScrollThumbRect,
            PixelStudio = pixelStudioLayout
        };
    }

    private static string GetColorPickerModeLabel(PixelStudioColorPickerMode mode)
    {
        return mode == PixelStudioColorPickerMode.Wheel
            ? "Wheel"
            : "RGB+Field";
    }
}

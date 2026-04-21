namespace ProjectSPlus.App.Editor;

public sealed partial class EditorWindowScene
{
    private enum EditableTextTarget
    {
        None,
        ProjectName,
        ProjectLibraryPath,
        ThemeStudioName,
        PaletteRename,
        LayerRename,
        FrameRename,
        FrameDuration,
        TransformAngle,
        TransformScaleX,
        TransformScaleY,
        CanvasResizeWidth,
        CanvasResizeHeight
    }

    private const long BackspaceRepeatDelayMs = 360;
    private const long BackspaceRepeatIntervalMs = 46;

    private EditableTextTarget _selectedTextTarget;
    private EditableTextTarget _backspaceRepeatTarget;
    private bool _backspaceRepeatActive;
    private long _backspaceRepeatStartTick;
    private long _lastBackspaceRepeatTick;

    private void UpdateHeldTextEditing()
    {
        if (!_backspaceRepeatActive)
        {
            return;
        }

        if (!IsTextTargetActive(_backspaceRepeatTarget))
        {
            StopBackspaceRepeat();
            return;
        }

        long now = Environment.TickCount64;
        if (now - _backspaceRepeatStartTick < BackspaceRepeatDelayMs
            || now - _lastBackspaceRepeatTick < BackspaceRepeatIntervalMs)
        {
            return;
        }

        DeleteTextForTarget(_backspaceRepeatTarget, fromRepeat: true);
        _lastBackspaceRepeatTick = now;
    }

    private void SelectAllText(EditableTextTarget target)
    {
        _selectedTextTarget = target;
        StopBackspaceRepeat();
    }

    private void ClearSelectedText(EditableTextTarget target)
    {
        if (_selectedTextTarget == target)
        {
            _selectedTextTarget = EditableTextTarget.None;
        }

        if (_backspaceRepeatActive && _backspaceRepeatTarget == target)
        {
            StopBackspaceRepeat();
        }
    }

    private void ClearAllSelectedText()
    {
        _selectedTextTarget = EditableTextTarget.None;
        StopBackspaceRepeat();
    }

    private bool IsTextSelected(EditableTextTarget target)
    {
        return _selectedTextTarget == target;
    }

    private bool ConsumeSelectedText(EditableTextTarget target)
    {
        if (!IsTextSelected(target))
        {
            return false;
        }

        ClearSelectedText(target);
        return true;
    }

    private bool HandleTextBackspace(EditableTextTarget target)
    {
        if (!IsTextTargetActive(target))
        {
            return false;
        }

        if (!_backspaceRepeatActive || _backspaceRepeatTarget != target)
        {
            DeleteTextForTarget(target, fromRepeat: false);
            StartBackspaceRepeat(target);
        }

        return true;
    }

    private void StartBackspaceRepeat(EditableTextTarget target)
    {
        if (!IsTextTargetActive(target))
        {
            return;
        }

        long now = Environment.TickCount64;
        _backspaceRepeatActive = true;
        _backspaceRepeatTarget = target;
        _backspaceRepeatStartTick = now;
        _lastBackspaceRepeatTick = now;
    }

    private void StopBackspaceRepeat()
    {
        _backspaceRepeatActive = false;
        _backspaceRepeatTarget = EditableTextTarget.None;
        _backspaceRepeatStartTick = 0;
        _lastBackspaceRepeatTick = 0;
    }

    private bool IsTextTargetActive(EditableTextTarget target)
    {
        return target switch
        {
            EditableTextTarget.ProjectName => _uiState.ProjectForm.ActiveField == EditorTextField.ProjectName,
            EditableTextTarget.ProjectLibraryPath => _uiState.ProjectForm.ActiveField == EditorTextField.ProjectLibraryPath,
            EditableTextTarget.ThemeStudioName => _themeStudioVisible && _themeStudioNameActive,
            EditableTextTarget.PaletteRename => _paletteRenameActive,
            EditableTextTarget.LayerRename => _layerRenameActive,
            EditableTextTarget.FrameRename => _frameRenameActive,
            EditableTextTarget.FrameDuration => _frameDurationFieldActive,
            EditableTextTarget.TransformAngle => _selectionTransformAngleFieldActive,
            EditableTextTarget.TransformScaleX => _selectionTransformScaleXFieldActive,
            EditableTextTarget.TransformScaleY => _selectionTransformScaleYFieldActive,
            EditableTextTarget.CanvasResizeWidth => _canvasResizeDialogVisible && _canvasResizeActiveField == CanvasResizeInputField.Width,
            EditableTextTarget.CanvasResizeHeight => _canvasResizeDialogVisible && _canvasResizeActiveField == CanvasResizeInputField.Height,
            _ => false
        };
    }

    private void DeleteTextForTarget(EditableTextTarget target, bool fromRepeat)
    {
        switch (target)
        {
            case EditableTextTarget.ProjectName:
                DeleteProjectNameText();
                break;
            case EditableTextTarget.ProjectLibraryPath:
                DeleteProjectLibraryText();
                break;
            case EditableTextTarget.ThemeStudioName:
                DeleteThemeStudioNameText();
                break;
            case EditableTextTarget.PaletteRename:
                DeletePaletteRenameText();
                break;
            case EditableTextTarget.LayerRename:
                DeleteLayerRenameText();
                break;
            case EditableTextTarget.FrameRename:
                DeleteFrameRenameText();
                break;
            case EditableTextTarget.FrameDuration:
                DeleteFrameDurationText();
                break;
            case EditableTextTarget.TransformAngle:
                DeleteSelectionTransformAngleText();
                break;
            case EditableTextTarget.TransformScaleX:
                DeleteSelectionTransformScaleText(horizontal: true);
                break;
            case EditableTextTarget.TransformScaleY:
                DeleteSelectionTransformScaleText(horizontal: false);
                break;
            case EditableTextTarget.CanvasResizeWidth:
            case EditableTextTarget.CanvasResizeHeight:
                DeleteCanvasResizeText();
                break;
        }

        if (!fromRepeat && !IsTextTargetActive(target))
        {
            StopBackspaceRepeat();
        }
    }

    private void DeleteProjectNameText()
    {
        if (ConsumeSelectedText(EditableTextTarget.ProjectName))
        {
            if (_uiState.ProjectForm.ProjectName.Length == 0)
            {
                return;
            }

            _uiState.ProjectForm.ProjectName = string.Empty;
            SyncUiState("Editing project name.");
            return;
        }

        if (_uiState.ProjectForm.ProjectName.Length == 0)
        {
            return;
        }

        _uiState.ProjectForm.ProjectName = _uiState.ProjectForm.ProjectName[..^1];
        SyncUiState("Editing project name.");
    }

    private void DeleteProjectLibraryText()
    {
        if (ConsumeSelectedText(EditableTextTarget.ProjectLibraryPath))
        {
            if (_uiState.ProjectForm.ProjectLibraryPath.Length == 0)
            {
                return;
            }

            _uiState.ProjectForm.ProjectLibraryPath = string.Empty;
        }
        else
        {
            if (_uiState.ProjectForm.ProjectLibraryPath.Length == 0)
            {
                return;
            }

            _uiState.ProjectForm.ProjectLibraryPath = _uiState.ProjectForm.ProjectLibraryPath[..^1];
        }

        _uiState.ProjectForm.FolderPickerPath = NormalizeDirectoryPath(_uiState.ProjectForm.ProjectLibraryPath);
        _uiState.ProjectForm.FolderPickerEntries = GetDirectoryEntries(_uiState.ProjectForm.FolderPickerPath);
        SyncUiState("Editing project library path.");
    }

    private void DeleteThemeStudioNameText()
    {
        if (ConsumeSelectedText(EditableTextTarget.ThemeStudioName))
        {
            if (_themeStudioNameBuffer.Length == 0)
            {
                return;
            }

            _themeStudioNameBuffer = string.Empty;
            ApplyThemeStudioPreview("Editing custom theme name.");
            return;
        }

        if (_themeStudioNameBuffer.Length == 0)
        {
            return;
        }

        _themeStudioNameBuffer = _themeStudioNameBuffer[..^1];
        ApplyThemeStudioPreview("Editing custom theme name.");
    }

    private void DeletePaletteRenameText()
    {
        if (ConsumeSelectedText(EditableTextTarget.PaletteRename))
        {
            if (_paletteRenameBuffer.Length == 0)
            {
                return;
            }

            _paletteRenameBuffer = string.Empty;
            RefreshPixelStudioView("Editing palette name.");
            return;
        }

        if (_paletteRenameBuffer.Length == 0)
        {
            return;
        }

        _paletteRenameBuffer = _paletteRenameBuffer[..^1];
        RefreshPixelStudioView("Editing palette name.");
    }

    private void DeleteLayerRenameText()
    {
        if (ConsumeSelectedText(EditableTextTarget.LayerRename))
        {
            if (_layerRenameBuffer.Length == 0)
            {
                return;
            }

            _layerRenameBuffer = string.Empty;
            RefreshPixelStudioView(_layerRenameTargetsGroup ? "Editing group name." : "Editing layer name.");
            return;
        }

        if (_layerRenameBuffer.Length == 0)
        {
            return;
        }

        _layerRenameBuffer = _layerRenameBuffer[..^1];
        RefreshPixelStudioView(_layerRenameTargetsGroup ? "Editing group name." : "Editing layer name.");
    }

    private void DeleteFrameRenameText()
    {
        if (ConsumeSelectedText(EditableTextTarget.FrameRename))
        {
            if (_frameRenameBuffer.Length == 0)
            {
                return;
            }

            _frameRenameBuffer = string.Empty;
            RefreshPixelStudioView("Editing frame name.");
            return;
        }

        if (_frameRenameBuffer.Length == 0)
        {
            return;
        }

        _frameRenameBuffer = _frameRenameBuffer[..^1];
        RefreshPixelStudioView("Editing frame name.");
    }

    private void DeleteFrameDurationText()
    {
        if (ConsumeSelectedText(EditableTextTarget.FrameDuration))
        {
            if (_frameDurationBuffer.Length == 0)
            {
                return;
            }

            _frameDurationBuffer = string.Empty;
        }
        else
        {
            if (_frameDurationBuffer.Length == 0)
            {
                return;
            }

            _frameDurationBuffer = _frameDurationBuffer[..^1];
        }

        RefreshPixelStudioView("Editing frame duration.");
    }

    private void DeleteSelectionTransformAngleText()
    {
        if (ConsumeSelectedText(EditableTextTarget.TransformAngle))
        {
            if (_selectionTransformAngleBuffer.Length == 0)
            {
                return;
            }

            _selectionTransformAngleBuffer = string.Empty;
        }
        else
        {
            if (_selectionTransformAngleBuffer.Length == 0)
            {
                return;
            }

            _selectionTransformAngleBuffer = _selectionTransformAngleBuffer[..^1];
        }

        UpdateSelectionTransformAngleFromBuffer();
    }

    private void DeleteSelectionTransformScaleText(bool horizontal)
    {
        EditableTextTarget target = horizontal ? EditableTextTarget.TransformScaleX : EditableTextTarget.TransformScaleY;
        string buffer = horizontal ? _selectionTransformScaleXBuffer : _selectionTransformScaleYBuffer;

        if (ConsumeSelectedText(target))
        {
            if (buffer.Length == 0)
            {
                return;
            }

            buffer = string.Empty;
        }
        else
        {
            if (buffer.Length == 0)
            {
                return;
            }

            buffer = buffer[..^1];
        }

        if (horizontal)
        {
            _selectionTransformScaleXBuffer = buffer;
        }
        else
        {
            _selectionTransformScaleYBuffer = buffer;
        }

        UpdateSelectionTransformScaleFromBuffer(horizontal);
    }

    private void DeleteCanvasResizeText()
    {
        ref string buffer = ref GetCanvasResizeActiveBuffer();
        EditableTextTarget target = _canvasResizeActiveField == CanvasResizeInputField.Height
            ? EditableTextTarget.CanvasResizeHeight
            : EditableTextTarget.CanvasResizeWidth;
        if (ConsumeSelectedText(target))
        {
            if (buffer.Length == 0)
            {
                return;
            }

            buffer = string.Empty;
        }
        else
        {
            if (buffer.Length == 0)
            {
                return;
            }

            buffer = buffer[..^1];
        }

        UpdateCanvasResizePreviewState();
        RefreshPixelStudioView("Editing canvas size.", rebuildLayout: true);
    }
}

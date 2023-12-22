using System;
using DrumGame.Game.Channels;
using DrumGame.Game.Modifiers;
using osu.Framework.Input.Bindings;

namespace DrumGame.Game.Commands;

public enum Command
{
    None,
    ShowAvailableCommands,
    ShowAllCommands,
    Help,
    OpenSettings,
    EditKeybinds,
    OpenKeyboardView,
    OpenKeyboardDrumEditor,
    ViewDrumLegend,
    ViewMidi,
    ViewRepositories,
    ConfigureMapLibraries,
    Notifications,
    Save,
    SelectMods,
    ClearMods,
    ToggleMod,
    SwitchMode,
    SwitchModeBack,
    ToggleEditMode,
    ToggleFillMode,
    ToggleRecordMode,
    Copy,
    Paste,
    Cut,
    Rename,
    SetBeatmapOffset,
    SetBeatmapOffsetHere,
    AddBeatToOffset,
    SetLocalOffset,
    TimingWizard,
    OffsetWizard,
    AutoMapperPlot,
    FrequencyImage,
    AutoMapper,
    SetBeatmapLeadIn,
    EditBeatmapMetadata,
    SetAllEmptyMappers,
    AddTagsToAll,
    SetDifficultyName,
    SetBeatmapPreviewTime,
    SetNormalizedRelativeVolume,
    CreateNewBeatmap,
    JumpToMap,
    SetEditorSnapping,
    ToggleSnapIndicator,
    ToggleSongCursor,
    ModifyCurrentBPM,
    SnapNotes,
    MultiplyBPM,
    DoubleTime,
    MultiplySectionBPM,
    ConvertRolls,
    RemoveDuplicateNotes,
    CollapseFlams,
    SetLeftBassSticking,
    StackDrumChannel,
    InsertRoll,
    EditTiming,
    EditBeatsPerMeasure,
    ToggleBookmark,
    ToggleAnnotation,
    TogglePlayback,
    Play,
    Pause,
    PauseOnNextNote,
    SeekToNextTimelineMark,
    SeekToPreviousTimelineMark,
    SeekToNextBookmark,
    SeekToPreviousBookmark,
    SetZoom,
    AdjustZoom,
    SetPlaybackSpeed,
    SetPlaybackBPM,
    IncreasePlaybackSpeed,
    DecreasePlaybackSpeed,
    ToggleMeasureLines,
    ToggleMute,
    Delete,
    DeleteNotes,
    CropMeasure,
    InsertMeasure,
    InsertMeasureAtStart,
    ApplyFilter,
    CycleModifier,
    CycleSticking,
    EditMode,
    EditorTools,
    ToggleAutoPlayHitSounds,
    ToggleMetronome,
    ToggleVolumeControls,
    IncreaseVolume,
    DecreaseVolume,
    Mute,
    Unmute,
    CursorUndo,
    MarkCurrentPosition,
    Close,
    CloseWithoutSaving,
    QuitGame,
    ForceQuitGame,
    ToggleFullscreen, // FrameworkAction
    MaximizeWindow,
    CycleFrameSync, // FrameworkAction
    CycleFrameStatistics, // FrameworkAction
    ToggleLogOverlay, // FrameworkAction
    ToggleExecutionMode, // FrameworkAction
    OpenFile,
    SelectAll,
    SelectToEnd,
    SelectRight,
    SelectLeft,
    SelectXBeats,
    Select,
    SelectAutostart,
    SetReplaySort,
    SelectDisplayMode,
    SelectCollection,
    AddToCollection,
    RemoveFromCollection,
    NewCollection,
    ConvertSearchToCollection,
    ExportSearchToFile,
    HighlightRandom,
    HighlightRandomPrevious,
    Next,
    Previous,
    Up,
    Down,
    PageUp,
    PageDown,
    Undo,
    Redo,
    Refresh,
    UpvoteMap,
    DownvoteMap,
    ToggleTernary,
    Ternary0,
    Ternary1,
    Ternary2,
    TernaryBackspace,
    ClearSearch,
    DeleteMap,
    UpdateMap,
    SyncMaps,
    AddLink,
    DownloadFile,
    SeekToNextSnapPoint,
    SeekToPreviousSnapPoint,
    SeekXBeats,
    SeekToMeasureStart,
    SeekToMeasureEnd,
    SeekToStart,
    SeekToEnd,
    SeekToTime,
    SeekToBeat,
    ShowEndScreen,
    ToggleWaveform,
    SetWaveformTempo,
    ToggleVideo,
    AddCameraFeed,
    SetCameraOffset,
    SaveAudioRecording,
    SetRecordingDevices,
    LoadReplayVideo,
    ABLoop,
    ImportMidi,
    ShowEventLog,
    ControlCamera,
    RevealInFileExplorer,
    RevealAudioInFileExplorer,
    OpenResourcesFolder,
    OpenLogFolder,
    OpenDrumGameDiscord,
    ExportToMidi,
    ExportToDtx,
    ConvertAudioToOgg,
    LoadYouTubeAudio,
    FixAudio,
    NewMapFromYouTube,
    SearchSpotifyForMap,
    OpenExternally,
    ToggleDrumChannel,
    EditKeybind,
    CleanStorage,
    ReloadKeybindsFromFile,
    Screenshot,
    About,
    SubmitFeedback,
    ToggleScreencastMode,
    RecordVideo,
    GenerateThumbnail,
    RefreshMidi,
    ReloadSkin,
    ExportCurrentSkin,
    ReloadSoundFont,
    SetWindowSize,
    EnableDebugLogging,
    EnableDebugAndPerformanceLogging,
    ForceGarbageCollection,
    TriggerRandomMidiNote,
    MAX_VALUE
}
public static class CommandList
{
    // There's around a 4ms overhead for the first register command,
    // After the first 10 registrations, the next 30 take < 0.1ms
    // this method gets called when reloading commands, so be careful with putting 1-time run logic in here
    public static void RegisterCommands(CommandController controller)
    {
        controller.RegisterCommand(Command.ShowAvailableCommands,
            InputKey.P,
            new KeyCombo(ModifierKey.CtrlShift, InputKey.P),
            InputKey.Tilde);
        controller.RegisterCommand(Command.ShowAllCommands);
        controller.RegisterCommand(Command.Help, InputKey.F1);
        controller.RegisterCommand(Command.OpenSettings, new KeyCombo(ModifierKey.Ctrl, InputKey.Comma));
        controller.RegisterCommand(Command.EditKeybinds, new KeyCombo(ModifierKey.CtrlShift, InputKey.Comma));
        controller.RegisterCommand(Command.OpenKeyboardView, new KeyCombo(ModifierKey.Shift, InputKey.F1));
        controller.RegisterCommand(Command.OpenKeyboardDrumEditor);
        controller.RegisterCommand(Command.ViewDrumLegend);
        controller.RegisterCommand(Command.ViewMidi);
        controller.RegisterCommand(Command.ViewRepositories);
        controller.RegisterCommand(Command.ConfigureMapLibraries);
        controller.RegisterCommand(Command.Notifications);
        controller.RegisterCommand(Command.Save, new KeyCombo(ModifierKey.Ctrl, InputKey.S));

        controller.RegisterCommand(Command.SelectMods, DrumChannel.RideBell);
        controller.RegisterCommand(Command.ClearMods);

        controller.RegisterCommand(Command.ToggleMod);
        var p = controller.SetParameterInfo(Command.ToggleMod, typeof(BeatmapModifier));
        p.GetName = (_, p) => $"Toggle {((BeatmapModifier)p[0]).FullName} Mod";
        controller.RegisterCommandInfo(CommandInfo.From(Command.ToggleMod, DrumChannel.BassDrum, BeatmapModifier.Instance<DoubleBassModifier>()));
        controller.RegisterCommandInfo(CommandInfo.From(Command.ToggleMod, DrumChannel.SmallTom, BeatmapModifier.Instance<HiddenModifier>()));

        controller.RegisterCommand(Command.SwitchMode, InputKey.Tab);
        controller.RegisterCommand(Command.SwitchModeBack, new KeyCombo(ModifierKey.Shift, InputKey.Tab));
        controller.RegisterCommand(Command.ToggleEditMode, InputKey.E);
        controller.RegisterCommand(Command.ToggleFillMode, new KeyCombo(ModifierKey.Ctrl, InputKey.E));
        controller.RegisterCommand(Command.ToggleRecordMode, new KeyCombo(ModifierKey.Shift, InputKey.R));
        controller.RegisterCommand(Command.Copy, new KeyCombo(ModifierKey.Ctrl, InputKey.C));
        controller.RegisterCommand(Command.Paste, new KeyCombo(ModifierKey.Ctrl, InputKey.V));
        controller.RegisterCommand(Command.Cut, new KeyCombo(ModifierKey.Ctrl, InputKey.X));
        controller.RegisterCommand(Command.Rename, InputKey.F2);
        controller.RegisterCommand(Command.SetBeatmapOffset, InputKey.O);
        controller.RegisterCommand(Command.SetBeatmapOffsetHere);
        controller.RegisterCommand(Command.AddBeatToOffset);
        controller.RegisterCommand(Command.SetLocalOffset);
        controller.RegisterCommand(Command.TimingWizard, new KeyCombo(ModifierKey.Ctrl, InputKey.T));
        controller.RegisterCommand(Command.OffsetWizard, new KeyCombo(ModifierKey.CtrlShift, InputKey.T));
        controller.RegisterCommand(Command.AutoMapperPlot);
        controller.RegisterCommand(Command.FrequencyImage);
        controller.RegisterCommand(Command.AutoMapper);
        controller.RegisterCommand(Command.SetBeatmapLeadIn);
        controller.RegisterCommand(Command.EditBeatmapMetadata);
        controller.RegisterCommand(Command.SetAllEmptyMappers);
        controller.RegisterCommand(Command.AddTagsToAll);
        controller.RegisterCommand(Command.SetDifficultyName);
        controller.RegisterCommand(Command.SetBeatmapPreviewTime);
        controller.RegisterCommand(Command.SetNormalizedRelativeVolume);
        controller[Command.SetNormalizedRelativeVolume].HelperMarkup =
            "This will measure the loudness for the current track (using EBU R 128)\n"
            + $"and set the map relative volume such that the loudness is {Beatmaps.Editor.BeatmapEditor.TargetLufs} LUFS";
        controller.RegisterCommand(Command.CreateNewBeatmap, new KeyCombo(ModifierKey.Ctrl, InputKey.N));
        controller.RegisterCommand(Command.JumpToMap);

        controller.RegisterCommand(Command.SetEditorSnapping);
        controller.SetParameterInfo(Command.SetEditorSnapping, typeof(double));
        controller.RegisterCommandInfo(new CommandInfo(Command.SetEditorSnapping, "Set Quarter Note Snapping",
            InputKey.Number1)
        {
            Parameters = [1.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SetEditorSnapping, "Set 8th Note Snapping",
            InputKey.Number2)
        {
            Parameters = [2.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SetEditorSnapping, "Set 16th Note Snapping",
            InputKey.Number3)
        {
            Parameters = [4.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SetEditorSnapping, "Set Half Note Snapping",
            InputKey.Number4)
        {
            Parameters = [0.5]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SetEditorSnapping, "Set Whole Note Snapping",
            InputKey.Number5)
        {
            Parameters = [0.25]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SetEditorSnapping, "Set 24th Note Snapping",
            InputKey.Number6)
        {
            Parameters = [6.0]
        });

        controller.RegisterCommand(Command.ToggleSnapIndicator);
        controller.RegisterCommand(Command.ToggleSongCursor);
        controller.RegisterCommand(Command.ModifyCurrentBPM, "Modify Current BPM", InputKey.B);
        controller.RegisterCommand(Command.SnapNotes);
        controller.RegisterCommand(Command.MultiplyBPM, "Multiply BPM");
        controller.RegisterCommand(Command.DoubleTime);
        controller.RegisterCommand(Command.MultiplySectionBPM, "Multiply Section BPM");
        controller.RegisterCommand(Command.ConvertRolls);
        controller.RegisterCommand(Command.RemoveDuplicateNotes);
        controller.RegisterCommand(Command.CollapseFlams);
        controller.RegisterCommand(Command.SetLeftBassSticking);
        controller.RegisterCommand(Command.StackDrumChannel);
        controller.SetParameterInfo(Command.StackDrumChannel, typeof(DrumChannel));
        controller.RegisterCommand(Command.InsertRoll, InputKey.R);
        controller.RegisterCommand(Command.EditTiming, InputKey.T);
        controller.RegisterCommand(Command.EditBeatsPerMeasure);
        controller.RegisterCommand(Command.ToggleBookmark, new KeyCombo(ModifierKey.Ctrl, InputKey.B));
        controller.RegisterCommand(Command.ToggleAnnotation);
        controller.RegisterCommand(Command.TogglePlayback, InputKey.Space);
        controller.RegisterCommand(Command.Play);
        controller.RegisterCommand(Command.Pause);
        controller.RegisterCommand(Command.PauseOnNextNote);

        controller.RegisterCommand(Command.SeekToNextSnapPoint, InputKey.Period, InputKey.KeypadDecimal);
        controller.RegisterCommand(Command.SeekToPreviousSnapPoint, InputKey.Comma, new KeyCombo(ModifierKey.Ctrl, InputKey.KeypadDecimal));

        controller.RegisterCommand(Command.SeekXBeats);
        controller.SetParameterInfo(Command.SeekXBeats, typeof(double));
        controller.RegisterCommandInfo(new CommandInfo(Command.SeekXBeats, "Seek Forward 4 Beats",
            InputKey.Right)
        {
            Parameters = [4.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SeekXBeats, "Seek Forward 8 Beats",
            InputKey.Up)
        {
            Parameters = [8.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SeekXBeats, "Seek Back 4 Beats",
            InputKey.Left)
        {
            Parameters = [-4.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SeekXBeats, "Seek Back 8 Beats",
            InputKey.Down)
        {
            Parameters = [-8.0]
        });

        controller.RegisterCommand(Command.SeekToNextTimelineMark, InputKey.PageUp);
        controller.RegisterCommand(Command.SeekToPreviousTimelineMark, InputKey.PageDown);
        controller.RegisterCommand(Command.SeekToNextBookmark);
        controller.RegisterCommand(Command.SeekToPreviousBookmark);

        controller.RegisterCommand(Command.SeekToMeasureStart, InputKey.Home);
        controller.RegisterCommand(Command.SeekToMeasureEnd, InputKey.End);
        controller.RegisterCommand(Command.SeekToStart, new KeyCombo(ModifierKey.Ctrl, InputKey.Home));
        controller.RegisterCommand(Command.SeekToEnd, new KeyCombo(ModifierKey.Ctrl, InputKey.End));
        controller.RegisterCommand(Command.SeekToTime, new KeyCombo(ModifierKey.Ctrl, InputKey.G));
        controller.RegisterCommand(Command.SeekToBeat);
        controller.RegisterCommand(Command.ShowEndScreen);

        controller.RegisterCommand(Command.SelectAll, new KeyCombo(ModifierKey.Ctrl, InputKey.A));
        controller.RegisterCommand(Command.SelectToEnd, new KeyCombo(ModifierKey.CtrlShift, InputKey.End));
        controller.RegisterCommand(Command.SelectRight, new KeyCombo(ModifierKey.Shift, InputKey.Right));
        controller.RegisterCommand(Command.SelectLeft, new KeyCombo(ModifierKey.Shift, InputKey.Left));
        controller.RegisterCommand(Command.SelectXBeats);
        controller.SetParameterInfo(Command.SelectXBeats, typeof(double));
        controller.RegisterCommandInfo(new CommandInfo(Command.SelectXBeats, "Select Forward 4 Beats",
            new KeyCombo(ModifierKey.CtrlShift, InputKey.Right))
        {
            Parameters = [4.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SelectXBeats, "Select Forward 8 Beats",
            new KeyCombo(ModifierKey.CtrlShift, InputKey.Up))
        {
            Parameters = [8.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SelectXBeats, "Select Back 4 Beats",
            new KeyCombo(ModifierKey.CtrlShift, InputKey.Left))
        {
            Parameters = [-4.0]
        });
        controller.RegisterCommandInfo(new CommandInfo(Command.SelectXBeats, "Select Back 8 Beats",
            new KeyCombo(ModifierKey.CtrlShift, InputKey.Down))
        {
            Parameters = [-8.0]
        });

        controller.RegisterCommand(Command.Select, InputKey.Enter, InputKey.KeypadEnter);
        controller.RegisterCommand(Command.SelectAutostart, DrumChannel.Crash, new KeyCombo(ModifierKey.Ctrl, InputKey.Enter));
        controller.RegisterCommand(Command.SetReplaySort);
        controller.RegisterCommand(Command.SelectDisplayMode);
        controller.RegisterCommand(Command.SelectCollection, new KeyCombo(ModifierKey.CtrlShift, InputKey.C));
        controller.SetParameterInfo(Command.SelectCollection, typeof(string));
        controller.RegisterCommand(Command.AddToCollection, new KeyCombo(ModifierKey.Alt, InputKey.D));
        controller.SetParameterInfo(Command.AddToCollection, typeof(string));
        controller.RegisterCommandInfo(new CommandInfo(Command.AddToCollection, "Add to Favorites", new KeyCombo(ModifierKey.Ctrl, InputKey.D))
        { Parameter = "Favorites" });
        controller.RegisterCommand(Command.RemoveFromCollection, new KeyCombo(ModifierKey.CtrlShift, InputKey.D));
        controller.RegisterCommand(Command.NewCollection);
        controller.RegisterCommand(Command.ConvertSearchToCollection);
        controller[Command.SelectCollection].HelperMarkup =
            $"To create new collections, use the command <command>{controller[Command.ConvertSearchToCollection].Name}</>";

        controller.RegisterCommand(Command.ExportSearchToFile);
        controller.RegisterCommand(Command.HighlightRandom, InputKey.F2, DrumChannel.HiHatPedal, DrumChannel.OpenHiHat);
        controller.RegisterCommand(Command.HighlightRandomPrevious, new KeyCombo(ModifierKey.Shift, InputKey.F2), DrumChannel.ClosedHiHat);
        controller.RegisterCommand(Command.Next, InputKey.MouseWheelUp);
        controller.RegisterCommand(Command.Previous, InputKey.MouseWheelDown);
        controller.RegisterCommand(Command.Up, InputKey.Up, DrumChannel.SmallTom);
        controller.RegisterCommand(Command.Down, InputKey.Down, DrumChannel.MediumTom);
        controller.RegisterCommand(Command.PageUp, InputKey.PageUp, DrumChannel.Snare);
        controller.RegisterCommand(Command.PageDown, InputKey.PageDown, DrumChannel.LargeTom);
        controller.RegisterCommand(Command.Undo, new KeyCombo(ModifierKey.Ctrl, InputKey.Z));
        controller.RegisterCommand(Command.Redo, new KeyCombo(ModifierKey.Ctrl, InputKey.Y));
        controller.RegisterCommand(Command.Refresh, InputKey.F5);
        controller.RegisterCommand(Command.UpvoteMap);
        controller.RegisterCommand(Command.DownvoteMap);
        controller.RegisterCommand(Command.ToggleTernary, DrumChannel.BassDrum);
        controller.RegisterCommand(Command.Ternary0, DrumChannel.SmallTom);
        controller.RegisterCommand(Command.Ternary1, DrumChannel.MediumTom);
        controller.RegisterCommand(Command.Ternary2, DrumChannel.LargeTom);
        controller.RegisterCommand(Command.TernaryBackspace, DrumChannel.Snare);
        controller.RegisterCommand(Command.ClearSearch);
        controller.RegisterCommand(Command.DeleteMap);
        controller.RegisterCommand(Command.UpdateMap);
        controller.RegisterCommand(Command.SyncMaps);
        controller.SetParameterInfo(Command.SyncMaps, typeof(Browsers.BeatmapSelector.SyncOptions));
        controller.RegisterCommand(Command.AddLink);
        controller.RegisterCommand(Command.DownloadFile);

        controller.RegisterCommand(Command.SetZoom);
        controller.RegisterCommandInfo(new CommandInfo(Command.SetZoom, "Restore Default Zoom", new KeyCombo(ModifierKey.Ctrl, InputKey.Number0))
        { Parameters = [1.0] });
        controller.RegisterCommandInfo(new CommandInfo(Command.SetZoom, "Set 1.5x Zoom", new KeyCombo(ModifierKey.Ctrl, InputKey.Number1))
        { Parameters = [1.5] });

        controller.RegisterCommand(Command.AdjustZoom);
        controller.RegisterCommandInfo(new CommandInfo(Command.AdjustZoom, "Increase Zoom", new KeyCombo(ModifierKey.Ctrl, InputKey.MouseWheelUp))
        { Parameters = [1.0] });
        controller.RegisterCommandInfo(new CommandInfo(Command.AdjustZoom, "Decrease Zoom", new KeyCombo(ModifierKey.Ctrl, InputKey.MouseWheelDown))
        { Parameters = [-1.0] });

        controller.RegisterCommand(Command.SetPlaybackSpeed);
        controller.RegisterCommand(Command.IncreasePlaybackSpeed, InputKey.BracketRight,
            InputKey.Plus, InputKey.KeypadPlus);
        controller.RegisterCommand(Command.DecreasePlaybackSpeed, InputKey.BracketLeft,
            InputKey.Minus, InputKey.KeypadMinus);
        controller.RegisterCommandInfo(new CommandInfo(Command.SetPlaybackSpeed, "Reset Playback Speed",
            InputKey.BackSpace)
        { Parameters = [1.0] });
        controller.RegisterCommand(Command.SetPlaybackBPM, "Set Playback BPM");
        controller.RegisterCommand(Command.ABLoop, "Set/Clear A-B Loop", InputKey.L);
        controller.RegisterCommand(Command.ImportMidi);

        controller.RegisterCommand(Command.ToggleMeasureLines);
        controller.RegisterCommand(Command.ToggleMute, new KeyCombo(ModifierKey.Ctrl, InputKey.M));
        controller.RegisterCommand(Command.ShowEventLog);
        controller.RegisterCommand(Command.ControlCamera);
        controller.RegisterCommand(Command.DeleteNotes, InputKey.Delete);
        controller.RegisterCommand(Command.Delete, new KeyCombo(ModifierKey.Ctrl, InputKey.Delete));
        controller.RegisterCommand(Command.CropMeasure, new KeyCombo(ModifierKey.CtrlShift, InputKey.Delete));
        controller.RegisterCommand(Command.InsertMeasure, new KeyCombo(ModifierKey.Ctrl, InputKey.Enter));
        controller.RegisterCommand(Command.InsertMeasureAtStart);
        controller.RegisterCommand(Command.ApplyFilter);
        controller.SetParameterInfo(Command.ApplyFilter, typeof(string));
        controller.RegisterCommand(Command.CycleModifier, InputKey.KeypadMultiply);
        controller.RegisterCommand(Command.CycleSticking, InputKey.C);
        controller.RegisterCommand(Command.EditMode);
        controller.RegisterCommand(Command.EditorTools);
        controller.RegisterCommand(Command.ToggleAutoPlayHitSounds);
        controller.RegisterCommand(Command.ToggleMetronome);
        controller.RegisterCommand(Command.ToggleWaveform);
        controller.RegisterCommand(Command.SetWaveformTempo);
        controller.RegisterCommand(Command.ToggleVideo);
        controller.RegisterCommand(Command.AddCameraFeed);
        controller.SetParameterInfo(Command.AddCameraFeed, typeof(string));
        controller.RegisterCommand(Command.SetCameraOffset);
        controller.RegisterCommand(Command.SaveAudioRecording);
        controller.RegisterCommand(Command.SetRecordingDevices);
        controller.RegisterCommand(Command.LoadReplayVideo);
        controller.RegisterCommand(Command.ToggleVolumeControls);
        controller.RegisterCommand(Command.IncreaseVolume, new KeyCombo(ModifierKey.Alt, InputKey.MouseWheelUp),
            new KeyCombo(ModifierKey.Alt, InputKey.Up));
        controller.RegisterCommand(Command.DecreaseVolume, new KeyCombo(ModifierKey.Alt, InputKey.MouseWheelDown),
            new KeyCombo(ModifierKey.Alt, InputKey.Down));
        controller.RegisterCommand(Command.Mute);
        controller.RegisterCommand(Command.Unmute);
        controller.RegisterCommand(Command.CursorUndo, new KeyCombo(ModifierKey.Ctrl, InputKey.U));
        controller.RegisterCommand(Command.MarkCurrentPosition, new KeyCombo(ModifierKey.Shift, InputKey.BackSpace));
        controller.RegisterCommand(Command.Close, InputKey.Escape);
        controller.RegisterCommand(Command.CloseWithoutSaving);
        controller.RegisterCommand(Command.QuitGame, new KeyCombo(ModifierKey.Alt, InputKey.F4),
            new KeyCombo(ModifierKey.Ctrl, InputKey.W), new KeyCombo(ModifierKey.Ctrl, InputKey.Q));
        controller.RegisterCommand(Command.ForceQuitGame);
        controller.RegisterCommand(Command.ToggleFullscreen, InputKey.F11, new KeyCombo(ModifierKey.Alt, InputKey.Enter));
        controller.RegisterCommand(Command.MaximizeWindow, DrumChannel.Ride);
        controller.RegisterCommand(Command.CycleFrameSync, new KeyCombo(ModifierKey.Ctrl, InputKey.F7));
        controller.RegisterCommand(Command.CycleFrameStatistics, new KeyCombo(ModifierKey.Ctrl, InputKey.F11));
        controller.RegisterCommand(Command.ToggleLogOverlay, new KeyCombo(ModifierKey.Ctrl, InputKey.F10));
        controller.RegisterCommand(Command.ToggleExecutionMode, new KeyCombo(ModifierKey.CtrlAlt, InputKey.F7));
        controller.RegisterCommand(Command.OpenFile, new KeyCombo(ModifierKey.Ctrl, InputKey.O));
        controller.RegisterCommand(Command.RevealInFileExplorer, new KeyCombo(ModifierKey.CtrlAlt, InputKey.R));
        controller.RegisterCommand(Command.RevealAudioInFileExplorer);
        controller.RegisterCommand(Command.OpenResourcesFolder);
        controller.RegisterCommand(Command.OpenLogFolder);
        controller.RegisterCommand(Command.OpenDrumGameDiscord);
        controller.RegisterCommand(Command.ExportToMidi);
        controller.RegisterCommand(Command.ExportToDtx);
        controller.RegisterCommand(Command.ConvertAudioToOgg);
        controller.RegisterCommand(Command.LoadYouTubeAudio, "Load YouTube Audio");
        controller.RegisterCommand(Command.FixAudio);
        controller.RegisterCommand(Command.NewMapFromYouTube);
        controller.RegisterCommand(Command.SearchSpotifyForMap);
        controller.RegisterCommand(Command.OpenExternally, new KeyCombo(ModifierKey.CtrlShiftAlt, InputKey.R));
        controller.RegisterCommand(Command.EditKeybind, new KeyCombo(ModifierKey.Ctrl, InputKey.Enter));
        controller.RegisterCommand(Command.CleanStorage);
        controller.RegisterCommand(Command.ReloadKeybindsFromFile);
        controller.RegisterCommand(Command.Screenshot);
        controller.RegisterCommand(Command.About);
        controller.RegisterCommand(Command.SubmitFeedback);
        controller.RegisterCommand(Command.ToggleScreencastMode);
        controller.RegisterCommand(Command.RecordVideo);
        controller.RegisterCommand(Command.GenerateThumbnail);
        controller.RegisterCommand(Command.RefreshMidi);
        controller.RegisterCommand(Command.ReloadSkin);
        controller.RegisterCommand(Command.ExportCurrentSkin);
        controller.RegisterCommand(Command.ReloadSoundFont);
        controller.RegisterCommand(Command.SetWindowSize);
        controller.RegisterCommand(Command.EnableDebugLogging);
        controller.RegisterCommand(Command.EnableDebugAndPerformanceLogging);
        controller.RegisterCommand(Command.ForceGarbageCollection);
        controller.RegisterCommand(Command.TriggerRandomMidiNote);

        // Drum controls, these don't get bound to, they just get added to the keybinding dictionary
        controller.RegisterCommand(Command.ToggleDrumChannel);
        controller.SetParameterInfo(Command.ToggleDrumChannel, typeof(DrumChannel));
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Bass Drum", InputKey.Keypad0)
        { Parameter = DrumChannel.BassDrum });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Closed Hi-Hat", InputKey.Keypad1)
        { Parameter = DrumChannel.ClosedHiHat });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Ride", InputKey.Keypad2)
        { Parameter = DrumChannel.Ride });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Ride Bell", new KeyCombo(ModifierKey.Ctrl, InputKey.Keypad2))
        { Parameter = DrumChannel.RideBell });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Snare", InputKey.Keypad3)
        { Parameter = DrumChannel.Snare });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Small Tom", InputKey.Keypad4)
        { Parameter = DrumChannel.SmallTom });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Medium Tom", InputKey.Keypad5)
        { Parameter = DrumChannel.MediumTom });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Large Tom", InputKey.Keypad6)
        { Parameter = DrumChannel.LargeTom });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Open Hi-Hat", InputKey.Keypad7)
        { Parameter = DrumChannel.OpenHiHat });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Hi-Hat Pedal", InputKey.Keypad8)
        { Parameter = DrumChannel.HiHatPedal });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Side Stick", InputKey.KeypadDivide)
        { Parameter = DrumChannel.SideStick });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Crash", InputKey.KeypadEnter)
        { Parameter = DrumChannel.Crash });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Splash Cymbal")
        { Parameter = DrumChannel.Splash });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle China Cymbal")
        { Parameter = DrumChannel.China });
        controller.RegisterCommandInfo(new CommandInfo(Command.ToggleDrumChannel, "Toggle Rim")
        { Parameter = DrumChannel.Rim });
    }

    public static Command[] EditorTools => new Command[] {
        Command.ShowAvailableCommands,
        Command.Save,
        Command.SwitchMode,

        Command.ToggleDrumChannel,

        Command.OffsetWizard,
        Command.SetBeatmapOffset,
        Command.TimingWizard,
        Command.EditBeatmapMetadata,
        Command.SetEditorSnapping,
        Command.ToggleWaveform,

        Command.ModifyCurrentBPM,
        Command.EditTiming,
        Command.EditBeatsPerMeasure,

        Command.Undo,
        Command.Redo,

        Command.SetPlaybackSpeed, // TODO add helper text

        // Selection commands
        Command.Copy,
        Command.Cut,
        Command.Paste,
        Command.SelectAll,
        Command.SelectRight,
        Command.SelectLeft,
        Command.SelectXBeats, // TODO add helper text to request modal
        Command.DeleteNotes,
        Command.CropMeasure,
        Command.InsertMeasure,

        // Note commands
        Command.CycleModifier,
        Command.CycleSticking,

        // Seek commands
        Command.TogglePlayback,
        Command.SeekToNextSnapPoint,
        Command.SeekToPreviousSnapPoint,
        Command.SeekXBeats, // TODO add helper text to the request modal, same as SetEditorSnapping
        Command.SeekToNextTimelineMark,
        Command.SeekToPreviousTimelineMark,
        Command.SeekToMeasureStart,
        Command.SeekToMeasureEnd,
        Command.SeekToTime,
        Command.SeekToBeat,
        Command.Next,
        Command.Previous,

        // Rare but useful commands
        Command.ToggleAutoPlayHitSounds,
        Command.SetZoom,
        Command.InsertRoll,
        Command.StackDrumChannel,
        Command.SetBeatmapLeadIn,
        Command.Rename,
        Command.ImportMidi,
        Command.ToggleEditMode,
        Command.ToggleAnnotation,
        Command.ToggleBookmark,
    };
}

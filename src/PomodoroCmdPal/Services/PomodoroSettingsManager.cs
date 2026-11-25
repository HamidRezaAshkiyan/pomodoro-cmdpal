using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Core;

namespace PomodoroCmdPal.Services;

internal sealed class PomodoroSettingsManager : JsonSettingsManager
{
    private readonly TextSetting _focusMinutes = new(
        key: "focusMinutes",
        label: "Focus duration (minutes)",
        description: "Length of a focus session in minutes (1–180).",
        initialValue: "25");

    private readonly TextSetting _shortBreakMinutes = new(
        key: "shortBreakMinutes",
        label: "Short break duration (minutes)",
        description: "Length of a short break in minutes (1–60).",
        initialValue: "5");

    private readonly TextSetting _longBreakMinutes = new(
        key: "longBreakMinutes",
        label: "Long break duration (minutes)",
        description: "Length of a long break after a full cycle in minutes (1–90).",
        initialValue: "15");

    private readonly TextSetting _cyclesBeforeLongBreak = new(
        key: "cyclesBeforeLongBreak",
        label: "Focus cycles before long break",
        description: "Number of focus sessions before taking a long break (1–12).",
        initialValue: "4");

    private readonly ToggleSetting _autoStartNextPhase = new(
        key: "autoStartNextPhase",
        label: "Auto-start next phase",
        description: "Automatically start breaks and focus sessions when the previous phase ends.",
        initialValue: false);

    private readonly ToggleSetting _enableSound = new(
        key: "enableSound",
        label: "Play sound on completion",
        description: "Play a Windows reminder chime when a focus or break timer completes.",
        initialValue: true);

    public event Action<PomodoroConfig>? ConfigChanged;

    public PomodoroSettingsManager()
    {
        FilePath = GetSettingsFilePath();

        Settings.Add(_focusMinutes);
        Settings.Add(_shortBreakMinutes);
        Settings.Add(_longBreakMinutes);
        Settings.Add(_cyclesBeforeLongBreak);
        Settings.Add(_autoStartNextPhase);
        Settings.Add(_enableSound);

        LoadSettings();

        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            ConfigChanged?.Invoke(BuildConfig());
        };
    }

    public PomodoroConfig BuildConfig() => new PomodoroConfig(
        FocusMinutes: ParseInt(_focusMinutes.Value, 25),
        ShortBreakMinutes: ParseInt(_shortBreakMinutes.Value, 5),
        LongBreakMinutes: ParseInt(_longBreakMinutes.Value, 15),
        CyclesBeforeLongBreak: ParseInt(_cyclesBeforeLongBreak.Value, 4),
        AutoStartNextPhase: _autoStartNextPhase.Value,
        EnableSound: _enableSound.Value).Sanitized();

    public void ApplyPreset(PomodoroConfig preset)
    {
        var sanitized = preset.Sanitized();
        _focusMinutes.Value = sanitized.FocusMinutes.ToString();
        _shortBreakMinutes.Value = sanitized.ShortBreakMinutes.ToString();
        _longBreakMinutes.Value = sanitized.LongBreakMinutes.ToString();
        _cyclesBeforeLongBreak.Value = sanitized.CyclesBeforeLongBreak.ToString();
        SaveSettings();
        ConfigChanged?.Invoke(BuildConfig());
    }

    public static string GetDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "PomodoroCmdPal");
    }

    private static string GetSettingsFilePath()
    {
        var dir = GetDataDirectory();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    private static int ParseInt(string? text, int fallback) =>
        int.TryParse(text?.Trim(), out var parsed) && parsed > 0 ? parsed : fallback;
}

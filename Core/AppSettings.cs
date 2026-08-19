using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StayAwake.Core;

public sealed class AppSettings
{
    // Pulso
    public PulseMethod Method { get; set; } = PulseMethod.MouseNudge;
    public int IntervalSeconds { get; set; } = 60;

    // Modo inteligente: só age quando você realmente parou de mexer
    public bool SmartMode { get; set; } = true;
    public int SmartIdleSeconds { get; set; } = 30;

    // Energia
    public bool KeepSystemAwake { get; set; } = true;
    public bool KeepDisplayOn { get; set; } = true;

    // Desligamento automático
    public bool AutoStopEnabled { get; set; }
    public int AutoStopMinutes { get; set; } = 480;

    // Janela de horário
    public bool ScheduleEnabled { get; set; }
    public string ScheduleStart { get; set; } = "08:00";
    public string ScheduleEnd { get; set; } = "18:00";

    // Comportamento do app
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool AutoStartEngine { get; set; } = true;

    [JsonIgnore]
    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Clamp(IntervalSeconds, 5, 3600));

    public TimeSpan? ParseTime(string value) =>
        TimeSpan.TryParse(value, out var t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1) ? t : null;

    public bool IsInsideSchedule(DateTime now)
    {
        if (!ScheduleEnabled) return true;

        var start = ParseTime(ScheduleStart);
        var end = ParseTime(ScheduleEnd);
        if (start is null || end is null) return true;

        var atual = now.TimeOfDay;

        // Janela que atravessa a meia-noite, ex.: 22:00 -> 06:00
        if (end <= start)
            return atual >= start || atual < end;

        return atual >= start && atual < end;
    }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StayAwake");

    public static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Arquivo corrompido não pode impedir o app de abrir.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Sem permissão de escrita: o app continua funcionando na sessão atual.
        }
    }
}

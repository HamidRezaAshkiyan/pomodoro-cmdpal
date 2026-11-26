using System.Text.Json;

namespace PomodoroCmdPal.Core;

public interface IStatsStore
{
    DailyFocusStats Load(DateOnly today);
    void Save(DailyFocusStats stats);
}

public sealed class JsonFileStatsStore : IStatsStore
{
    private readonly string _filePath;

    public JsonFileStatsStore(string filePath)
    {
        _filePath = filePath;
    }

    public DailyFocusStats Load(DateOnly today)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return DailyFocusStats.Empty(today);
            }

            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<DailyFocusStats>(json);
            return loaded is null
                ? DailyFocusStats.Empty(today)
                : loaded.ForDate(today);
        }
        catch
        {
            return DailyFocusStats.Empty(today);
        }
    }

    public void Save(DailyFocusStats stats)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(stats);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore disk write errors to keep timer reliable.
        }
    }
}

using Common.Serialization;
using GameInterface.Services;
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace GameInterface.Services.UI;

public interface ICoopOptionsStore : IGameAbstraction
{
    string FilePath { get; }
    bool TryLoad(out CoopOptions options);
    CoopOptions LoadOrDefault();
    void Save(CoopOptions options);
}

public class CoopOptionsStore : ICoopOptionsStore
{
    private readonly JsonFileIO jsonFileIO = new JsonFileIO();

    public string FilePath { get; }

    public CoopOptionsStore() : this(GetDefaultFilePath())
    {
    }

    public CoopOptionsStore(string filePath)
    {
        FilePath = filePath;
    }

    public bool TryLoad(out CoopOptions options)
    {
        options = null;

        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return false;

        try
        {
            options = jsonFileIO.ReadFromFile<CoopOptions>(FilePath);
            return options != null;
        }
        catch
        {
            options = null;
            return false;
        }
    }

    public CoopOptions LoadOrDefault()
    {
        if (TryLoad(out var options))
        {
            return options;
        }

        return new CoopOptions();
    }

    public void Save(CoopOptions options)
    {
        jsonFileIO.WriteToFile(FilePath, options ?? new CoopOptions());
    }

    private static string GetDefaultFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "BannerlordCoop",
            "coop_options.json");
    }
}

public class CoopOptions
{
    private static readonly PlayerKillFeedColor DefaultKillFeedColor = new PlayerKillFeedColor(59, 130, 246);

    [JsonPropertyName("killFeedColor")]
    public CoopOptionsColor KillFeedColor { get; set; }

    public PlayerKillFeedColor GetKillFeedColorOrDefault()
    {
        if (TryGetKillFeedColor(out var color))
        {
            return color;
        }

        return DefaultKillFeedColor;
    }

    public bool TryGetKillFeedColor(out PlayerKillFeedColor color)
    {
        if (KillFeedColor == null)
        {
            color = default;
            return false;
        }

        return PlayerKillFeedColor.TryCreate(KillFeedColor.Red, KillFeedColor.Green, KillFeedColor.Blue, out color);
    }

    public void SetKillFeedColor(PlayerKillFeedColor color)
    {
        KillFeedColor = new CoopOptionsColor
        {
            Red = color.Red,
            Green = color.Green,
            Blue = color.Blue
        };
    }
}

public class CoopOptionsColor
{
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }
}

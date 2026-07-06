using Common.Serialization;
using GameInterface.Services;
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace GameInterface.Services.UI;

public interface IPlayerKillFeedColorOptionsStore : IGameAbstraction
{
    string FilePath { get; }
    bool TryLoad(out PlayerKillFeedColor color);
    PlayerKillFeedColor LoadOrDefault();
    void Save(PlayerKillFeedColor color);
}

public class PlayerKillFeedColorOptionsStore : IPlayerKillFeedColorOptionsStore
{
    private static readonly PlayerKillFeedColor DefaultColor = new PlayerKillFeedColor(59, 130, 246);

    private readonly JsonFileIO jsonFileIO = new JsonFileIO();

    public string FilePath { get; }

    public PlayerKillFeedColorOptionsStore() : this(GetDefaultFilePath())
    {
    }

    public PlayerKillFeedColorOptionsStore(string filePath)
    {
        FilePath = filePath;
    }

    public bool TryLoad(out PlayerKillFeedColor color)
    {
        color = default;

        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return false;

        try
        {
            var options = jsonFileIO.ReadFromFile<PlayerKillFeedColorOptions>(FilePath);
            if (options == null) return false;

            var killFeedColor = options.KillFeedColor;
            if (killFeedColor == null) return false;

            return PlayerKillFeedColor.TryCreate(killFeedColor.Red, killFeedColor.Green, killFeedColor.Blue, out color);
        }
        catch
        {
            return false;
        }
    }

    public PlayerKillFeedColor LoadOrDefault()
    {
        if (TryLoad(out var color))
        {
            return color;
        }

        return DefaultColor;
    }

    public void Save(PlayerKillFeedColor color)
    {
        jsonFileIO.WriteToFile(FilePath, new PlayerKillFeedColorOptions
        {
            KillFeedColor = new PlayerKillFeedColorOptionsColor
            {
                Red = color.Red,
                Green = color.Green,
                Blue = color.Blue
            }
        });
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

    private class PlayerKillFeedColorOptions
    {
        [JsonPropertyName("killFeedColor")]
        public PlayerKillFeedColorOptionsColor KillFeedColor { get; set; }
    }

    private class PlayerKillFeedColorOptionsColor
    {
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
    }
}

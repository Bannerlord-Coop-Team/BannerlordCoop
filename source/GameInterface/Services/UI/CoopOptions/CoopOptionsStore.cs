using Common.Serialization;
using GameInterface.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions;

public interface ICoopOptionsStore : IGameAbstraction
{
    string FilePath { get; }
    bool TryLoad(out CoopOptionsData options);
    CoopOptionsData LoadOrDefault();
    void Save(CoopOptionsData options);
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

    public bool TryLoad(out CoopOptionsData options)
    {
        options = null;

        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return false;

        try
        {
            options = jsonFileIO.ReadFromFile<CoopOptionsData>(FilePath);
            return options != null;
        }
        catch
        {
            options = null;
            return false;
        }
    }

    public CoopOptionsData LoadOrDefault()
    {
        if (TryLoad(out var options))
        {
            return options;
        }

        return new CoopOptionsData();
    }

    public void Save(CoopOptionsData options)
    {
        jsonFileIO.WriteToFile(FilePath, options ?? new CoopOptionsData());
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

public class CoopOptionsData
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
    };

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Tabs { get; set; } = new Dictionary<string, JsonElement>();

    public bool TryGetSection<TSection>(string tabId, string sectionId, out TSection section) where TSection : class
    {
        section = null;

        if (string.IsNullOrEmpty(tabId) || string.IsNullOrEmpty(sectionId) || Tabs == null) return false;
        if (!Tabs.TryGetValue(tabId, out var tab) || tab.ValueKind != JsonValueKind.Object) return false;
        if (!tab.TryGetProperty(sectionId, out var sectionElement) || sectionElement.ValueKind == JsonValueKind.Null) return false;

        try
        {
            section = JsonSerializer.Deserialize<TSection>(sectionElement.GetRawText(), JsonOptions);
            return section != null;
        }
        catch
        {
            section = null;
            return false;
        }
    }

    public TSection GetSectionOrDefault<TSection>(string tabId, string sectionId, TSection defaultSection) where TSection : class
    {
        if (TryGetSection<TSection>(tabId, sectionId, out var section))
        {
            return section;
        }

        return defaultSection;
    }

    public void SetSection<TSection>(string tabId, string sectionId, TSection section) where TSection : class
    {
        if (string.IsNullOrEmpty(tabId)) throw new ArgumentException("Tab id cannot be empty.", nameof(tabId));
        if (string.IsNullOrEmpty(sectionId)) throw new ArgumentException("Section id cannot be empty.", nameof(sectionId));

        Tabs ??= new Dictionary<string, JsonElement>();

        var sections = new Dictionary<string, JsonElement>();
        if (Tabs.TryGetValue(tabId, out var tab) && tab.ValueKind == JsonValueKind.Object)
        {
            sections = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tab.GetRawText(), JsonOptions)
                ?? new Dictionary<string, JsonElement>();
        }

        sections[sectionId] = ToJsonElement(section);
        Tabs[tabId] = ToJsonElement(sections);
    }

    private static JsonElement ToJsonElement<TValue>(TValue value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameInterface.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameInterface.Tests.Configuration;

/// <summary>Checks and regenerates the managed options shipped with the mod.</summary>
public class ManagedConfigCatalogTests
{
    [Fact]
    public void ShippedCatalogMatchesManagedOptions()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "mod-config.default.json");
        using var template = new JsonTextReader(File.OpenText(templatePath)) { DateParseHandling = DateParseHandling.None };
        var defaults = JObject.Load(template)["modOptions"] as JObject
            ?? throw new InvalidDataException("modOptions is missing from the default config");

        var options = BuildOptions(defaults);
        // The website hashes JSON.stringify(options), so Newtonsoft has to write every value the same way.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(options.ToString(Formatting.None)));
        var catalog = new JObject
        {
            ["contentDigest"] = Convert.ToHexString(digest).ToLowerInvariant(),
            ["options"] = options,
        };
        var expectedText = catalog.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        var catalogPath = Path.Combine(StubMsBuild.FindRepoRoot(), "deploy", "managed-config-catalog.json");
        var oldText = File.Exists(catalogPath) ? Encoding.UTF8.GetString(File.ReadAllBytes(catalogPath)) : null;

        if (expectedText != oldText) File.WriteAllText(catalogPath, expectedText);
        Assert.Equal(expectedText, oldText);
    }

    private static JArray BuildOptions(JObject defaults)
    {
        var options = new JArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        // Read non-public and static properties too so misplaced metadata fails the test.
        var properties = typeof(ModOptionsData).GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var property in properties.OrderBy(property => property.MetadataToken))
        {
            string key = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            var metadata = property.GetCustomAttribute<ManagedConfigOptionAttribute>();
            if (property.Name == nameof(ModOptionsData.UnknownKeys) ||
                property.GetMethod is not { IsPublic: true, IsStatic: false })
            {
                if (metadata != null) throw new InvalidDataException($"{key} has metadata without a managed property");
                continue;
            }

            if (!keys.Add(key)) throw new InvalidDataException($"{key} is declared more than once");
            if (metadata == null) throw new InvalidDataException($"{key} is missing ManagedConfigOption metadata");
            if (!defaults.TryGetValue(key, out var value)) throw new InvalidDataException($"Default is missing for {key}");
            options.Add(BuildOption(property.PropertyType, key, metadata, value));
        }

        return options;
    }

    private static JObject BuildOption(Type propertyType, string key, ManagedConfigOptionAttribute metadata, JToken value)
    {
        if (!Regex.IsMatch(metadata.AvailableSince ?? "", @"^[0-9]+\.[0-9]+\.[0-9]+\z"))
            throw new InvalidDataException($"{key} needs explicit availableSince metadata");

        var clrType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var type = ToCatalogType(clrType, key);
        var option = new JObject { ["key"] = key, ["type"] = type, ["availableSince"] = metadata.AvailableSince };
        AddConstraints(metadata, key, type, option);
        var enumNames = Array.Empty<string>();
        if (clrType.IsEnum)
        {
            enumNames = clrType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray();
            option.Add("values", new JArray(enumNames));
        }
        option.Add("default", ToCatalogDefault(key, type, metadata, enumNames, value));
        CheckStringFormatting(option, key);
        return option;
    }

    private static string ToCatalogType(Type clrType, string key) => clrType switch
    {
        _ when clrType == typeof(bool) => "boolean",
        _ when clrType == typeof(int) => "integer",
        _ when clrType == typeof(float) || clrType == typeof(double) => "number",
        _ when clrType == typeof(List<string>) => "stringList",
        _ when clrType.IsEnum => "enum",
        _ => throw new InvalidDataException($"Unsupported managed option type for {key}: {clrType}")
    };

    private static void AddConstraints(ManagedConfigOptionAttribute metadata, string key, string type, JObject option)
    {
        int threshold = metadata.CoercePositiveBelow;
        bool hasMin = !double.IsNaN(metadata.Min);
        bool hasMax = !double.IsNaN(metadata.Max);
        if (type is not ("integer" or "number") && (hasMin || hasMax || threshold != 0))
            throw new InvalidDataException($"Invalid numeric metadata for {type} option {key}");
        if (type != "stringList" && (metadata.ItemPattern != null || metadata.ItemFormat != null))
            throw new InvalidDataException($"Invalid list metadata for {type} option {key}");
        if (metadata.ItemFormat is not (null or "steamId64"))
            throw new InvalidDataException($"Invalid itemFormat for {key}");
        if (threshold < 0) throw new InvalidDataException($"coercePositiveBelow must be positive for {key}");

        if (hasMin) option.Add("min", CatalogNumber(metadata.Min, key));
        if (hasMax) option.Add("max", CatalogNumber(metadata.Max, key));
        if (metadata.ItemPattern != null) option.Add("itemPattern", metadata.ItemPattern);
        if (metadata.ItemFormat != null) option.Add("itemFormat", metadata.ItemFormat);
        // Values from 1 to threshold - 1 are raised to threshold.
        if (threshold > 1) option.Add("coerceRanges", new JArray { new JArray(1, threshold - 1, threshold) });
    }

    private static JToken ToCatalogDefault(string key, string type, ManagedConfigOptionAttribute metadata, string[] enumNames, JToken value)
    {
        double? number = value.Type is JTokenType.Integer or JTokenType.Float ? (double)value : null;
        bool valid = type switch
        {
            "boolean" => value.Type == JTokenType.Boolean,
            "integer" => number is >= int.MinValue and <= int.MaxValue && Math.Truncate(number.Value) == number.Value,
            "number" => number.HasValue && double.IsFinite(number.Value),
            "enum" => value.Type == JTokenType.String && enumNames.Contains((string)value!, StringComparer.Ordinal),
            "stringList" => value is JArray items && items.All(item => item.Type == JTokenType.String),
            _ => false
        };
        if (!valid) throw new InvalidDataException($"Invalid default for {type} option {key}");
        if (number is not double numeric) return value.DeepClone();
        if (numeric < metadata.Min || numeric > metadata.Max)
            throw new InvalidDataException($"Default for {key} is outside its bounds");
        return CatalogNumber(numeric, key);
    }

    private static JValue CatalogNumber(double value, string key)
    {
        // JSON.stringify writes -0 as 0.
        string text = value == 0 ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        if (!double.IsFinite(value) || text.Contains('E') || value < long.MinValue || value >= (double)long.MaxValue)
            throw new InvalidDataException($"Number {text} for {key} needs JSON.stringify-compatible formatting");

        var number = value % 1 == 0 ? new JValue((long)value) : new JValue(value);
        if (number.ToString(Formatting.None) != text)
            throw new InvalidDataException($"Number {text} for {key} needs JSON.stringify-compatible formatting");
        return number;
    }

    private static void CheckStringFormatting(JObject option, string key)
    {
        foreach (var value in option.Descendants().OfType<JValue>())
        {
            if (value.Value is not string text) continue;
            for (int i = 0; i < text.Length;)
            {
                // Newtonsoft escapes these scalars, while JSON.stringify emits them directly.
                if (!Rune.TryGetRuneAt(text, i, out var rune) || rune.Value is 0x0085 or 0x2028 or 0x2029)
                    throw new InvalidDataException($"String value for {key} needs JSON.stringify-compatible formatting");
                i += rune.Utf16SequenceLength;
            }
        }
    }
}

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
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(options, Formatting.None)));
        var catalog = new JObject
        {
            ["contentDigest"] = Convert.ToHexString(digest).ToLowerInvariant(),
            ["options"] = options,
        };
        var expected = Encoding.UTF8.GetBytes(Serialize(catalog, Formatting.Indented) + "\n");
        var catalogPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "deploy", "managed-config-catalog.json"));

        if (File.Exists(catalogPath) && File.ReadAllBytes(catalogPath).AsSpan().SequenceEqual(expected)) return;

        File.WriteAllBytes(catalogPath, expected);
        Assert.Fail("The managed config catalog was stale and has been regenerated at " + catalogPath);
    }

    private static JArray BuildOptions(JObject defaults)
    {
        var options = new JArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var properties = typeof(ModOptionsData).GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var property in properties.OrderBy(property => property.MetadataToken))
        {
            string key = ToCatalogKey(property.Name);
            var metadata = property.GetCustomAttribute<ManagedConfigOptionAttribute>();
            if (property.Name == nameof(ModOptionsData.UnknownKeys) ||
                property.GetMethod is not { IsPublic: true, IsStatic: false })
            {
                if (metadata != null) throw new InvalidDataException($"{key} has metadata without a managed property");
                continue;
            }

            if (!keys.Add(key)) throw new InvalidDataException($"{key} is declared more than once");
            if (metadata == null) throw new InvalidDataException($"{key} is missing ManagedConfigOption metadata");
            if (!Regex.IsMatch(metadata.AvailableSince ?? "", @"^[0-9]+\.[0-9]+\.[0-9]+\z"))
                throw new InvalidDataException($"{key} needs explicit availableSince metadata");
            if (!defaults.TryGetValue(key, out var value)) throw new InvalidDataException($"Default is missing for {key}");

            var clrType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var type = clrType switch
            {
                _ when clrType == typeof(bool) => "boolean",
                _ when clrType == typeof(int) => "integer",
                _ when clrType == typeof(float) || clrType == typeof(double) => "number",
                _ when clrType == typeof(List<string>) => "stringList",
                _ when clrType.IsEnum => "enum",
                _ => throw new InvalidDataException($"Unsupported managed option type for {key}: {clrType}")
            };

            var option = new JObject { ["key"] = key, ["type"] = type, ["availableSince"] = metadata.AvailableSince };
            AddConstraints(property, metadata, key, type, option);
            var enumNames = clrType.IsEnum
                ? clrType.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray()
                : Array.Empty<string>();
            if (clrType.IsEnum) option.Add("values", new JArray(enumNames));
            option.Add("default", ValidateDefault(key, type, metadata, enumNames, value));
            options.Add(option);
        }

        if (options.Count == 0) throw new InvalidDataException("modOptions has no managed options");
        foreach (var property in defaults.Properties())
        {
            if (!keys.Contains(property.Name)) throw new InvalidDataException($"No managed property for default {property.Name}");
        }
        return options;
    }

    private static void AddConstraints(PropertyInfo property, ManagedConfigOptionAttribute metadata, string key, string type, JObject option)
    {
        bool numeric = type is "integer" or "number";
        bool list = type == "stringList";
        // Named arguments distinguish omitted fields from explicit false, zero and null.
        var arguments = property.CustomAttributes.Single(attribute => attribute.AttributeType == typeof(ManagedConfigOptionAttribute)).NamedArguments;
        foreach (var argument in arguments.OrderBy(argument => argument.MemberInfo.MetadataToken))
        {
            string name = ToCatalogKey(argument.MemberName);
            if (name == "availableSince") continue;

            var value = argument.TypedValue.Value;
            bool valid = name switch
            {
                "min" or "max" => numeric && value is double bound && double.IsFinite(bound),
                "itemPattern" => list && value is string,
                "itemFormat" => list && value is "steamId64",
                "uniqueItems" => list && value is bool,
                "coercePositiveBelow" => numeric && value is int and > 0,
                _ => throw new InvalidDataException($"{key} has unknown metadata {name}")
            };
            if (!valid) throw new InvalidDataException($"Invalid {name} metadata for {type} option {key}");
            if (name != "coercePositiveBelow") option.Add(name, JToken.FromObject(value!));
        }

        int threshold = metadata.CoercePositiveBelow;
        if (threshold > 1) option.Add("coerceRanges", new JArray { new JArray(1, threshold - 1, threshold) });
    }

    private static JToken ValidateDefault(string key, string type, ManagedConfigOptionAttribute metadata, string[] enumNames, JToken value)
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

        if (number is double numeric)
        {
            if (numeric < metadata.Min || numeric > metadata.Max)
                throw new InvalidDataException($"Default for {key} is outside its bounds");
            return new JValue(numeric);
        }

        if (value is JArray list)
        {
            var items = list.Values<string>().ToArray();
            if (metadata.UniqueItems && items.Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new InvalidDataException($"Default for {key} is not unique");
            if (metadata.ItemPattern != null)
            {
                Regex pattern;
                try
                {
                    // JavaScript's end anchor requires the actual end of the string.
                    string expression = Regex.Replace(metadata.ItemPattern, @"\\.|\[(?:\\.|[^\]\\])*\]|\$", match => match.Value == "$" ? @"\z" : match.Value);
                    pattern = new Regex(expression, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException error)
                {
                    throw new InvalidDataException($"Invalid itemPattern for {key}", error);
                }
                if (items.Any(item => !pattern.IsMatch(item!)))
                    throw new InvalidDataException($"Default for {key} violates itemPattern");
            }
        }
        return value.DeepClone();
    }

    private static string ToCatalogKey(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string Serialize(JToken value, Formatting formatting)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        using var writer = new CatalogJsonWriter(text) { Formatting = formatting };
        value.WriteTo(writer);
        writer.Flush();
        return text.ToString();
    }

    /// <summary>Writes catalog values the way JSON.stringify does for the consumer digest.</summary>
    private sealed class CatalogJsonWriter : JsonTextWriter
    {
        public CatalogJsonWriter(TextWriter writer) : base(writer) { }

        public override void WriteValue(double value)
        {
            if (value == 0)
            {
                WriteRawValue("0");
                return;
            }

            string text = value.ToString("R", CultureInfo.InvariantCulture);
            int separator = text.IndexOf('E');
            if (separator < 0)
            {
                WriteRawValue(text);
                return;
            }

            string mantissa = text[..separator];
            int exponent = int.Parse(text.AsSpan(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (exponent is < -6 or >= 21)
            {
                WriteRawValue(mantissa + "e" + (exponent >= 0 ? "+" : "") + exponent.ToString(CultureInfo.InvariantCulture));
                return;
            }

            string sign = value < 0 ? "-" : "";
            string digits = mantissa[sign.Length..].Replace(".", "");
            int point = exponent + 1;
            WriteRawValue(sign + (point <= 0 ? "0." + new string('0', -point) + digits
                : point >= digits.Length ? digits.PadRight(point, '0') : digits.Insert(point, ".")));
        }

        public override void WriteValue(string? value)
        {
            if (value == null)
            {
                WriteNull();
                return;
            }

            string escaped = Regex.Replace(value, "[\"\\\\\u0000-\u001f]|[\ud800-\udbff](?![\udc00-\udfff])|(?<![\ud800-\udbff])[\udc00-\udfff]",
                match => match.Value[0] switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\b' => "\\b",
                    '\f' => "\\f",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    var character => $"\\u{(int)character:x4}"
                });
            WriteRawValue("\"" + escaped + "\"");
        }
    }
}

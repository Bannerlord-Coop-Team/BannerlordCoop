using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VerificationHarness.Planning;

namespace VerificationHarness.Tests.Planning;

public sealed class VerificationReportSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] TierIds =
    {
        "unit",
        "deterministic-peer",
        "process-peer",
        "dedicated-server-synthetic",
        "rendered-smoke",
        "full-live"
    };

    private static readonly string[] CheckIds =
    {
        "unit",
        "wire-copy-e2e",
        "poller-game-thread",
        "deterministic-peer",
        "process-peer",
        "dedicated-server-synthetic",
        "rendered-smoke",
        "full-live"
    };

    [Fact]
    public void SchemaAcceptsSerializedPlannerContracts()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(GetSchemaPath()));
        JsonElement schemaRoot = schema.RootElement;
        JsonElement definitions = schemaRoot.GetProperty("$defs");

        var builder = new VerificationPlanBuilder();
        var source = new VerificationSourceIdentity(new string('1', 40), new string('2', 40));
        var writer = new VerificationPlanWriter();
        VerificationPlan plan = builder.Build(source, new[] { "README.md" });
        JsonObject completedReport = JsonNode.Parse(writer.Serialize(plan))!.AsObject();
        completedReport["startedAtUtc"] = "2026-09-04T00:00:00+00:00";
        completedReport["completedAtUtc"] = "2026-09-04T00:00:01+00:00";
        completedReport["artifactHashes"] = JsonSerializer.SerializeToNode(
            new[] { new ArtifactHash("artifacts/result.json", "sha256", new string('a', 64)) },
            JsonOptions);
        completedReport["processExits"] = JsonSerializer.SerializeToNode(
            new[] { new ProcessExitIdentity("server", 123, 0) },
            JsonOptions);
        JsonObject firstCheck = completedReport["checks"]![0]!.AsObject();
        firstCheck["stateDigest"] = new string('b', 64);
        firstCheck["startedAtUtc"] = "2026-09-04T00:00:00+00:00";
        firstCheck["completedAtUtc"] = "2026-09-04T00:00:01+00:00";
        firstCheck["artifactHashes"] = completedReport["artifactHashes"]!.DeepClone();
        firstCheck["processExits"] = completedReport["processExits"]!.DeepClone();
        using JsonDocument report = JsonDocument.Parse(completedReport.ToJsonString());

        AssertObjectContract(schemaRoot, report.RootElement);
        Assert.Equal(
            VerificationPlan.CurrentSchemaVersion,
            schemaRoot.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
        Assert.Equal(TierIds, ReadStrings(definitions.GetProperty("tier").GetProperty("enum")));
        Assert.Equal(CheckIds, ReadStrings(definitions.GetProperty("checkId").GetProperty("enum")));
        Assert.Equal(TierIds, plan.Profiles.Select(profile => profile.Id));
        Assert.Equal(CheckIds, plan.Checks.Select(check => check.Id));

        AssertObjectContract(
            schemaRoot.GetProperty("properties").GetProperty("source"),
            report.RootElement.GetProperty("source"));
        AssertObjectContract(definitions.GetProperty("profile"), report.RootElement.GetProperty("profiles")[0]);
        AssertObjectContract(definitions.GetProperty("check"), report.RootElement.GetProperty("checks")[0]);
        AssertObjectContract(
            definitions.GetProperty("topology"),
            report.RootElement.GetProperty("checks")[0].GetProperty("topology"));
        AssertObjectContract(definitions.GetProperty("reason"), report.RootElement.GetProperty("reasons")[0]);
        AssertConforms(schemaRoot, schemaRoot, report.RootElement);

        VerificationPlan rejectedPlan = builder.Build(source, new[] { "../outside" });
        using JsonDocument rejectedReport = JsonDocument.Parse(writer.Serialize(rejectedPlan));
        AssertObjectContract(
            definitions.GetProperty("rejectedPath"),
            rejectedReport.RootElement.GetProperty("rejectedPaths")[0]);
        AssertConforms(schemaRoot, schemaRoot, rejectedReport.RootElement);
    }

    private static void AssertObjectContract(JsonElement schema, JsonElement value)
    {
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        string[] properties = schema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] required = ReadStrings(schema.GetProperty("required"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] serialized = value.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(properties, required);
        Assert.Equal(properties, serialized);
    }

    private static void AssertConforms(JsonElement root, JsonElement schema, JsonElement value)
    {
        Assert.True(IsConformant(root, schema, value), $"Value does not conform to schema: {value.GetRawText()}");
    }

    private static bool IsConformant(JsonElement root, JsonElement schema, JsonElement value)
    {
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            schema = ResolveReference(root, reference.GetString()!);
        }

        if (schema.TryGetProperty("oneOf", out JsonElement oneOf))
        {
            return oneOf.EnumerateArray().Count(option => IsConformant(root, option, value)) == 1;
        }

        if (schema.TryGetProperty("type", out JsonElement type) && !MatchesType(type.GetString()!, value))
        {
            return false;
        }

        if (schema.TryGetProperty("const", out JsonElement constant) && !JsonElement.DeepEquals(constant, value))
        {
            return false;
        }

        if (schema.TryGetProperty("enum", out JsonElement choices) &&
            !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(choice, value)))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && !StringConforms(schema, value.GetString()!))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            schema.TryGetProperty("minimum", out JsonElement minimum) &&
            value.GetInt64() < minimum.GetInt64())
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            schema.TryGetProperty("maximum", out JsonElement maximum) &&
            value.GetInt64() > maximum.GetInt64())
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Array && !ArrayConforms(root, schema, value))
        {
            return false;
        }

        return value.ValueKind != JsonValueKind.Object || ObjectConforms(root, schema, value);
    }

    private static bool StringConforms(JsonElement schema, string value)
    {
        if (schema.TryGetProperty("minLength", out JsonElement minLength) && value.Length < minLength.GetInt32())
        {
            return false;
        }

        if (schema.TryGetProperty("pattern", out JsonElement pattern) &&
            !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant))
        {
            return false;
        }

        return !schema.TryGetProperty("format", out JsonElement format) ||
               format.GetString() != "date-time" ||
               DateTimeOffset.TryParse(value, out _);
    }

    private static bool ArrayConforms(JsonElement root, JsonElement schema, JsonElement value)
    {
        int count = value.GetArrayLength();
        if (schema.TryGetProperty("minItems", out JsonElement minItems) && count < minItems.GetInt32())
        {
            return false;
        }

        if (schema.TryGetProperty("maxItems", out JsonElement maxItems) && count > maxItems.GetInt32())
        {
            return false;
        }

        if (schema.TryGetProperty("uniqueItems", out JsonElement uniqueItems) && uniqueItems.GetBoolean() &&
            value.EnumerateArray().Select(item => item.GetRawText()).Distinct(StringComparer.Ordinal).Count() != count)
        {
            return false;
        }

        return !schema.TryGetProperty("items", out JsonElement itemSchema) ||
               value.EnumerateArray().All(item => IsConformant(root, itemSchema, item));
    }

    private static bool ObjectConforms(JsonElement root, JsonElement schema, JsonElement value)
    {
        if (schema.TryGetProperty("required", out JsonElement required) &&
            required.EnumerateArray().Any(name => !value.TryGetProperty(name.GetString()!, out _)))
        {
            return false;
        }

        if (!schema.TryGetProperty("properties", out JsonElement properties))
        {
            return true;
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!properties.TryGetProperty(property.Name, out JsonElement propertySchema))
            {
                if (schema.TryGetProperty("additionalProperties", out JsonElement additional) &&
                    !additional.GetBoolean())
                {
                    return false;
                }

                continue;
            }

            if (!IsConformant(root, propertySchema, property.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesType(string type, JsonElement value)
    {
        return type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };
    }

    private static JsonElement ResolveReference(JsonElement root, string reference)
    {
        const string prefix = "#/$defs/";
        Assert.StartsWith(prefix, reference, StringComparison.Ordinal);
        return root.GetProperty("$defs").GetProperty(reference[prefix.Length..]);
    }

    private static string[] ReadStrings(JsonElement array)
    {
        return array.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static string GetSchemaPath()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        return Path.Combine(
            repositoryRoot,
            "doc",
            "automated-testing",
            "verification-report-v1.schema.json");
    }
}

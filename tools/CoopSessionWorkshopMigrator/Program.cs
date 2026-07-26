using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const string WorkshopPlayerDataName = "WorkshopPlayerData";
const string WarehouseDataName = "PlayerWarehouseRosterPerSettlement";
const string DeprecatedWorkshopDataName = "WorkshopDataByWorkshopId";

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: CoopSessionWorkshopMigrator <branch-format input.json> <correct-format output.json>");
    return 1;
}

string inputPath = Path.GetFullPath(args[0]);
string outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(inputPath))
    throw new FileNotFoundException("The input CoopSession JSON file was not found.", inputPath);
if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Input and output paths must differ.");

byte[] inputBytes = File.ReadAllBytes(inputPath);
bool hasUtf8Bom = inputBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
ReadOnlySpan<byte> jsonBytes = hasUtf8Bom
    ? inputBytes.AsSpan(Encoding.UTF8.Preamble.Length)
    : inputBytes;
string inputJson = new UTF8Encoding(false, true).GetString(jsonBytes);
string newLine = inputJson.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
bool endsWithNewLine =
    inputJson.EndsWith("\r\n", StringComparison.Ordinal) ||
    inputJson.EndsWith("\n", StringComparison.Ordinal);

JsonObject root = ParseRoot(inputJson, inputPath);
JsonObject workshopPlayerData = RequireObject(root, WorkshopPlayerDataName, "$");
JsonObject warehouseData = RequireObject(
    workshopPlayerData,
    WarehouseDataName,
    $"$.{WorkshopPlayerDataName}");
ValidateWarehouseData(warehouseData);

JsonNode originalDocument = root.DeepClone();
JsonNode originalWarehouseData = warehouseData.DeepClone();

bool removedDeprecatedProperty = false;
int removedSnapshotCount = 0;
if (workshopPlayerData.TryGetPropertyValue(DeprecatedWorkshopDataName, out JsonNode? deprecatedNode))
{
    if (deprecatedNode is not JsonObject deprecatedWorkshopData)
    {
        throw new InvalidDataException(
            $"$.{WorkshopPlayerDataName}.{DeprecatedWorkshopDataName} must be an object.");
    }

    ValidateDeprecatedWorkshopData(deprecatedWorkshopData);
    removedSnapshotCount = deprecatedWorkshopData.Count;
    workshopPlayerData.Remove(DeprecatedWorkshopDataName);
    removedDeprecatedProperty = true;
}

VerifyOnlyDeprecatedWorkshopDataWasRemoved(originalDocument, root, originalWarehouseData);

if (!removedDeprecatedProperty)
{
    WriteAtomically(outputPath, inputBytes);
    Console.WriteLine(
        $"{inputPath} already uses the correct workshop JSON format; copied it unchanged to {outputPath}.");
    return 0;
}

string outputJson = root.ToJsonString(new JsonSerializerOptions
{
    WriteIndented = true,
});
if (newLine == "\r\n")
    outputJson = outputJson.Replace("\n", "\r\n", StringComparison.Ordinal);
if (endsWithNewLine)
    outputJson += newLine;

byte[] jsonOutputBytes = new UTF8Encoding(false, true).GetBytes(outputJson);
byte[] outputBytes = AddUtf8Bom(jsonOutputBytes, hasUtf8Bom);
WriteAtomically(outputPath, outputBytes);
VerifyWrittenOutput(outputPath, root);

Console.WriteLine(
    $"Removed {removedSnapshotCount} deprecated workshop snapshot record(s) and wrote {outputPath}. " +
    "Warehouse data and all non-workshop data were preserved.");
return 0;

static JsonObject ParseRoot(string json, string path)
{
    try
    {
        return JsonNode.Parse(
            json,
            documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            }) as JsonObject
            ?? throw new InvalidDataException($"The root of {path} must be a JSON object.");
    }
    catch (JsonException exception)
    {
        throw new InvalidDataException($"{path} is not valid JSON.", exception);
    }
}

static JsonObject RequireObject(JsonObject parent, string propertyName, string parentPath)
{
    if (!parent.TryGetPropertyValue(propertyName, out JsonNode? node) || node is not JsonObject value)
        throw new InvalidDataException($"{parentPath}.{propertyName} must be an object.");

    return value;
}

static void ValidateWarehouseData(JsonObject warehouseData)
{
    foreach ((string heroId, JsonNode? heroNode) in warehouseData)
    {
        if (string.IsNullOrWhiteSpace(heroId) || heroNode is not JsonArray slots)
        {
            throw new InvalidDataException(
                $"$.{WorkshopPlayerDataName}.{WarehouseDataName} entries must be hero-id arrays.");
        }

        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index] is not JsonObject slot ||
                !slot.TryGetPropertyValue("Key", out JsonNode? keyNode) ||
                !slot.TryGetPropertyValue("Value", out JsonNode? valueNode))
            {
                throw new InvalidDataException(
                    $"Warehouse slot {index} for {heroId} must contain Key and Value properties.");
            }

            bool keyIsNull = keyNode == null;
            bool valueIsNull = valueNode == null;
            if (keyIsNull != valueIsNull)
            {
                throw new InvalidDataException(
                    $"Warehouse slot {index} for {heroId} must have both Key and Value null, or neither.");
            }

            if (!keyIsNull &&
                (keyNode is not JsonValue keyValue ||
                 !keyValue.TryGetValue(out string? settlementId) ||
                 string.IsNullOrWhiteSpace(settlementId) ||
                 valueNode is not JsonArray))
            {
                throw new InvalidDataException(
                    $"Warehouse slot {index} for {heroId} must contain a settlement id and an item array.");
            }
        }
    }
}

static void ValidateDeprecatedWorkshopData(JsonObject deprecatedWorkshopData)
{
    string[] expectedProperties =
    {
        "IsGettingInputsFromWarehouse",
        "ProductionProgressForWarehouse",
        "ProductionProgressForTown",
        "StockProductionInWarehouseRatio",
    };
    var expectedPropertySet = new HashSet<string>(expectedProperties, StringComparer.Ordinal);

    foreach ((string workshopId, JsonNode? snapshotNode) in deprecatedWorkshopData)
    {
        if (string.IsNullOrWhiteSpace(workshopId) || snapshotNode is not JsonObject snapshot)
            throw new InvalidDataException($"Workshop snapshot {workshopId} must be an object.");

        var actualPropertySet = snapshot
            .Select(property => property.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualPropertySet.SetEquals(expectedPropertySet))
        {
            throw new InvalidDataException(
                $"Workshop snapshot {workshopId} does not have the expected branch-format fields.");
        }

        if (snapshot["IsGettingInputsFromWarehouse"] is not JsonValue inputFlag ||
            !inputFlag.TryGetValue(out bool _))
        {
            throw new InvalidDataException(
                $"Workshop snapshot {workshopId} has an invalid IsGettingInputsFromWarehouse value.");
        }

        foreach (string propertyName in expectedProperties.Skip(1))
        {
            if (snapshot[propertyName]?.GetValueKind() != JsonValueKind.Number)
            {
                throw new InvalidDataException(
                    $"Workshop snapshot {workshopId} has an invalid {propertyName} value.");
            }
        }
    }
}

static void VerifyOnlyDeprecatedWorkshopDataWasRemoved(
    JsonNode originalDocument,
    JsonObject migratedDocument,
    JsonNode originalWarehouseData)
{
    JsonObject expectedDocument = originalDocument.DeepClone().AsObject();
    JsonObject expectedWorkshopPlayerData =
        RequireObject(expectedDocument, WorkshopPlayerDataName, "$");
    expectedWorkshopPlayerData.Remove(DeprecatedWorkshopDataName);

    if (!JsonNode.DeepEquals(expectedDocument, migratedDocument))
        throw new InvalidDataException("The migration changed data outside the deprecated workshop snapshots.");

    JsonObject migratedWorkshopPlayerData =
        RequireObject(migratedDocument, WorkshopPlayerDataName, "$");
    JsonObject migratedWarehouseData = RequireObject(
        migratedWorkshopPlayerData,
        WarehouseDataName,
        $"$.{WorkshopPlayerDataName}");
    if (!JsonNode.DeepEquals(originalWarehouseData, migratedWarehouseData))
        throw new InvalidDataException("The migration changed player warehouse data.");
}

static void WriteAtomically(string outputPath, byte[] outputBytes)
{
    string? outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);

    string temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
    try
    {
        File.WriteAllBytes(temporaryPath, outputBytes);
        File.Move(temporaryPath, outputPath, true);
    }
    finally
    {
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
    }
}

static byte[] AddUtf8Bom(byte[] jsonBytes, bool addBom)
{
    if (!addBom)
        return jsonBytes;

    byte[] preamble = Encoding.UTF8.GetPreamble();
    byte[] bytesWithBom = new byte[preamble.Length + jsonBytes.Length];
    Buffer.BlockCopy(preamble, 0, bytesWithBom, 0, preamble.Length);
    Buffer.BlockCopy(jsonBytes, 0, bytesWithBom, preamble.Length, jsonBytes.Length);
    return bytesWithBom;
}

static void VerifyWrittenOutput(string outputPath, JsonObject expectedDocument)
{
    byte[] outputBytes = File.ReadAllBytes(outputPath);
    bool hasUtf8Bom = outputBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
    ReadOnlySpan<byte> jsonBytes = hasUtf8Bom
        ? outputBytes.AsSpan(Encoding.UTF8.Preamble.Length)
        : outputBytes;
    string outputJson = new UTF8Encoding(false, true).GetString(jsonBytes);
    JsonObject writtenDocument = ParseRoot(outputJson, outputPath);

    JsonObject workshopPlayerData =
        RequireObject(writtenDocument, WorkshopPlayerDataName, "$");
    if (workshopPlayerData.ContainsKey(DeprecatedWorkshopDataName))
        throw new InvalidDataException("The written output still contains deprecated workshop snapshots.");
    if (!JsonNode.DeepEquals(expectedDocument, writtenDocument))
        throw new InvalidDataException("The written output did not preserve the migrated JSON document.");
}

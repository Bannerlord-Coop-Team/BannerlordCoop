using FModBankParser;
using FModBankParser.Enums;
using FModBankParser.Nodes.Instruments;
using FModBankParser.Objects;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: VanillaOrderVoiceManifestGenerator <Native module path> <output manifest path>");
    return 1;
}

string nativeModulePath = Path.GetFullPath(args[0]);
string outputPath = Path.GetFullPath(args[1]);
string soundsPath = Path.Combine(nativeModulePath, "Sounds", "PC");
string voiceBankPath = Path.Combine(soundsPath, "voice.bank");
string stringsBankPath = Path.Combine(soundsPath, "MasterBank.strings.bank");
string definitionsPath = Path.Combine(nativeModulePath, "ModuleData", "voice_definitions.xml");

RequireFile(voiceBankPath);
RequireFile(stringsBankPath);
RequireFile(definitionsPath);

FModReader voiceBank = FModBankParser.FModBankParser.LoadSoundBank(new FileInfo(voiceBankPath));
FModReader stringsBank = FModBankParser.FModBankParser.LoadSoundBank(new FileInfo(stringsBankPath));
var stringTree = stringsBank.StringTable?.RadixTree;
if (stringTree == null)
    throw new InvalidDataException($"The FMOD string table is missing from {stringsBankPath}.");

var guidByPath = new Dictionary<string, FModGuid>(StringComparer.Ordinal);
for (int i = 0; i < stringTree.Guids.Length; i++)
{
    if (stringTree.TryGetStringByIndex(i, out string eventPath))
        guidByPath[eventPath] = stringTree.Guids[i];
}

XDocument definitions = XDocument.Load(definitionsPath);
XElement? definitionsRoot = definitions.Root;
if (definitionsRoot == null ||
    !string.Equals(definitionsRoot.Name.LocalName, "voice_definitions", StringComparison.Ordinal))
{
    throw new InvalidDataException($"Invalid vanilla voice definitions document {definitionsPath}.");
}

string[] orderEventPaths = definitionsRoot
    .Elements()
    .Where(element => string.Equals(
        element.Name.LocalName,
        "voice_definition",
        StringComparison.Ordinal))
    .SelectMany(definition => definition.Elements()
        .Where(element => string.Equals(element.Name.LocalName, "voice", StringComparison.Ordinal)))
    .Select(voice => (string?)voice.Attribute("path"))
    .Where(path => path?.Contains("/commands/", StringComparison.Ordinal) == true)
    .Cast<string>()
    .Distinct(StringComparer.Ordinal)
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToArray();

var events = new SortedDictionary<string, object>(StringComparer.Ordinal);
int weightedEventCount = 0;
int emptyEventCount = 0;
foreach (string eventPath in orderEventPaths)
{
    if (!guidByPath.TryGetValue(eventPath, out FModGuid eventGuid))
        throw new InvalidDataException($"FMOD has no event GUID for {eventPath}.");
    if (!voiceBank.EventNodes.TryGetValue(eventGuid, out var eventNode))
        throw new InvalidDataException($"The vanilla voice bank has no event data for {eventPath}.");

    var instrumentGuids = new List<FModGuid>();
    if (voiceBank.TimelineNodes.TryGetValue(eventNode.TimelineGuid, out var timeline))
    {
        instrumentGuids.AddRange(timeline.TriggerBoxes.Select(trigger => trigger.Guid));
        instrumentGuids.AddRange(timeline.TimeLockedTriggerBoxes.Select(trigger => trigger.Guid));
    }
    instrumentGuids.AddRange(eventNode.EventTriggeredInstruments);

    MultiInstrumentNode[] playlists = instrumentGuids
        .Distinct()
        .Select(guid => voiceBank.InstrumentNodes.TryGetValue(guid, out var instrument)
            ? instrument
            : null)
        .OfType<MultiInstrumentNode>()
        .Where(instrument => instrument.PlaylistBody != null)
        .ToArray();
    if (playlists.Length != 1)
    {
        throw new InvalidDataException(
            $"Expected one direct FMOD playlist for {eventPath}, found {playlists.Length}.");
    }

    var playlist = playlists[0].PlaylistBody!;
    if (playlist.PlayMode != EPlaylistPlayMode.PlaylistPlayMode_SmartRandom ||
        playlist.SelectionMode != EPlaylistSelectionMode.PlaylistSelectionMode_SelectNormal)
    {
        throw new InvalidDataException(
            $"Unsupported playlist mode for {eventPath}: " +
            $"{playlist.PlayMode}/{playlist.SelectionMode}.");
    }

    var samples = playlist.Entries.Select(entry =>
    {
        if (!voiceBank.InstrumentNodes.TryGetValue(entry.Guid, out var instrument) ||
            !(instrument is WaveformInstrumentNode waveform) ||
            !voiceBank.WavEntries.TryGetValue(waveform.WaveformResourceGuid, out var resource) ||
            resource.SoundBankIndex < 0 ||
            resource.SoundBankIndex >= voiceBank.SoundBankData.Count ||
            resource.SubsoundIndex < 0 ||
            resource.SubsoundIndex >= voiceBank.SoundBankData[resource.SoundBankIndex].Samples.Count)
        {
            throw new InvalidDataException(
                $"Order voice playlist {eventPath} contains unresolved entry {entry.Guid}.");
        }

        string? sampleName = voiceBank.SoundBankData[resource.SoundBankIndex]
            .Samples[resource.SubsoundIndex]
            .Name;
        if (string.IsNullOrEmpty(sampleName))
            throw new InvalidDataException($"Order voice playlist {eventPath} contains an unnamed sample.");

        return new { Name = sampleName, Weight = entry.Weight };
    }).ToArray();

    bool usesPlayPercentages = samples
        .Select(sample => sample.Weight)
        .Distinct()
        .Skip(1)
        .Any();
    if (usesPlayPercentages) weightedEventCount++;
    if (samples.Length == 0) emptyEventCount++;

    events.Add(eventPath, new
    {
        UsesPlayPercentages = usesPlayPercentages,
        Samples = samples,
    });
}

string voiceBankSha256;
using (FileStream stream = File.OpenRead(voiceBankPath))
    voiceBankSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

byte[] manifestJson = JsonSerializer.SerializeToUtf8Bytes(new
{
    SchemaVersion = 2,
    BankFormatVersion = voiceBank.BankInfo.FileVersion,
    VoiceBankSha256 = voiceBankSha256,
    PlayMode = "SmartRandom",
    SelectionMode = "Normal",
    Events = events,
});

string outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);
File.WriteAllBytes(outputPath, manifestJson);

Console.WriteLine(
    $"Wrote {events.Count} order voice events to {outputPath}; " +
    $"{weightedEventCount} weighted, {emptyEventCount} empty, bank SHA-256 {voiceBankSha256}.");
return 0;

static void RequireFile(string path)
{
    if (!File.Exists(path)) throw new FileNotFoundException("Required vanilla data was not found.", path);
}

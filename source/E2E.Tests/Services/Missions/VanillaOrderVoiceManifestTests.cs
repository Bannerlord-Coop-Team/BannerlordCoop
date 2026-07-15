using Missions.Agents.Voice;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class VanillaOrderVoiceManifestTests
{
    [Fact]
    public void EmbeddedManifest_ContainsOnlyTheGeneratedEventCatalog()
    {
        using Stream stream = Assert.IsAssignableFrom<Stream>(
            typeof(VanillaOrderVoiceService).Assembly.GetManifestResourceStream(
                "Missions.Agents.Voice.vanilla_order_voice_manifest.json"));

        using var reader = new StreamReader(stream);
        JObject manifest = JObject.Parse(reader.ReadToEnd());
        var events = Assert.IsType<JObject>(manifest["Events"]);

        Assert.Equal(2, manifest.Value<int>("SchemaVersion"));
        Assert.Null(manifest["VoiceDefinitions"]);
        Assert.Matches("^[0-9a-f]{64}$", manifest.Value<string>("VoiceBankSha256"));
        Assert.Equal("SmartRandom", manifest.Value<string>("PlayMode"));
        Assert.Equal("Normal", manifest.Value<string>("SelectionMode"));
        Assert.Equal(208, events.Count);
    }

    [Fact]
    public void ParseVoiceDefinitions_PreservesAliasesAndSkipsIncompleteVoices()
    {
        const string xml = @"
            <voice_definitions>
              <voice_definition name=""male_01"">
                <voice type=""Charge"" path=""event:/voice/combat/male/04/commands/charge"" face_anim=""attack"" />
                <voice type=""MissingPath"" />
              </voice_definition>
              <voice_definition name=""male_02"">
                <voice type=""Charge"" path=""event:/voice/combat/male/04/commands/charge"" />
              </voice_definition>
              <voice_definition>
                <voice type=""Charge"" path=""event:/ignored"" />
              </voice_definition>
            </voice_definitions>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        VanillaOrderVoiceService.VoiceDefinitionEntry[] entries =
            VanillaOrderVoiceService.ParseVoiceDefinitions(stream);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("male_01", entry.VoiceDefinition);
                Assert.Equal("Charge", entry.VoiceType);
                Assert.Equal("event:/voice/combat/male/04/commands/charge", entry.EventPath);
                Assert.Equal("attack", entry.FaceAnimation);
            },
            entry =>
            {
                Assert.Equal("male_02", entry.VoiceDefinition);
                Assert.Equal("Charge", entry.VoiceType);
                Assert.Equal("event:/voice/combat/male/04/commands/charge", entry.EventPath);
                Assert.Null(entry.FaceAnimation);
            });
    }

    [Fact]
    public void ParseVoiceDefinitions_InvalidRootThrows()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<voices />"));

        Assert.Throws<InvalidDataException>(() =>
            VanillaOrderVoiceService.ParseVoiceDefinitions(stream));
    }

    [Fact]
    public void ComputeSha256_ReturnsLowercaseContentHash()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            VanillaOrderVoiceService.ComputeSha256(stream));
    }

    [Fact]
    public void EmbeddedManifest_ChargeContainsTheKnownVanillaVariations()
    {
        using Stream stream = Assert.IsAssignableFrom<Stream>(
            typeof(VanillaOrderVoiceService).Assembly.GetManifestResourceStream(
                "Missions.Agents.Voice.vanilla_order_voice_manifest.json"));

        using var reader = new StreamReader(stream);
        JObject manifest = JObject.Parse(reader.ReadToEnd());
        JToken charge = Assert.IsAssignableFrom<JToken>(
            manifest["Events"]["event:/voice/combat/male/04/commands/charge"]);
        var samples = Assert.IsType<JArray>(charge["Samples"]);
        string[] sampleNames = samples.Select(sample => sample.Value<string>("Name")).ToArray();

        Assert.False(charge.Value<bool>("UsesPlayPercentages"));
        Assert.Equal(7, sampleNames.Length);
        Assert.Contains("rick_charge_03", sampleNames);
        Assert.Contains("rm_attack_order_02", sampleNames);
    }

    [Fact]
    public void SmartRandom_UsesEverySampleBeforeRepeating()
    {
        var voiceEvent = new VanillaOrderVoiceService.VoiceEvent
        {
            UsesPlayPercentages = false,
            Samples = new[]
            {
                new VanillaOrderVoiceService.VoiceSample { Name = "one", Weight = 1 },
                new VanillaOrderVoiceService.VoiceSample { Name = "two", Weight = 1 },
                new VanillaOrderVoiceService.VoiceSample { Name = "three", Weight = 1 },
            },
        };
        voiceEvent.Prepare(new Dictionary<string, byte[]>
        {
            ["one"] = Array.Empty<byte>(),
            ["two"] = Array.Empty<byte>(),
            ["three"] = Array.Empty<byte>(),
        });

        var random = new Random(1234);
        string[] selected = Enumerable.Range(0, 6)
            .Select(_ =>
            {
                Assert.True(voiceEvent.TrySelect(random, out string sampleName));
                return sampleName;
            })
            .ToArray();

        Assert.Equal(3, selected.Take(3).Distinct().Count());
        Assert.Equal(3, selected.Skip(3).Distinct().Count());
        Assert.DoesNotContain(Enumerable.Range(1, selected.Length - 1),
            index => selected[index] == selected[index - 1]);
    }

    [Fact]
    public void WeightedRandom_UsesAuthoredPlayPercentages()
    {
        var voiceEvent = new VanillaOrderVoiceService.VoiceEvent
        {
            UsesPlayPercentages = true,
            Samples = new[]
            {
                new VanillaOrderVoiceService.VoiceSample { Name = "never", Weight = 0 },
                new VanillaOrderVoiceService.VoiceSample { Name = "always", Weight = 100 },
            },
        };
        voiceEvent.Prepare(new Dictionary<string, byte[]>
        {
            ["never"] = Array.Empty<byte>(),
            ["always"] = Array.Empty<byte>(),
        });

        var random = new Random(1234);
        for (int i = 0; i < 20; i++)
        {
            Assert.True(voiceEvent.TrySelect(random, out string sampleName));
            Assert.Equal("always", sampleName);
        }
    }

    [Fact]
    public async Task ClipFileCache_ConcurrentInstancesPublishOneCompleteFile()
    {
        string cacheDirectory = Path.Combine(
            Path.GetTempPath(),
            "BannerlordCoop.Tests",
            Guid.NewGuid().ToString("N"));
        byte[] soundData = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 251))
            .ToArray();

        try
        {
            var firstCache = new VanillaOrderVoiceService.ClipFileCache(cacheDirectory);
            var secondCache = new VanillaOrderVoiceService.ClipFileCache(cacheDirectory);
            using var start = new ManualResetEventSlim(false);

            Task<string> firstWrite = Task.Run(() =>
            {
                start.Wait();
                return firstCache.GetOrCreatePath("charge", soundData);
            });
            Task<string> secondWrite = Task.Run(() =>
            {
                start.Wait();
                return secondCache.GetOrCreatePath("charge", soundData);
            });

            start.Set();
            string[] paths = await Task.WhenAll(firstWrite, secondWrite);

            Assert.Equal(paths[0], paths[1]);
            Assert.Equal(soundData, File.ReadAllBytes(paths[0]));
            Assert.Single(Directory.GetFiles(cacheDirectory, "*.ogg"));
            Assert.Empty(Directory.GetFiles(cacheDirectory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}

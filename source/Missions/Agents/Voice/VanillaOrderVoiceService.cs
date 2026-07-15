using Common.Logging;
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TaleWorlds.Engine;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Voice;

public interface IVanillaOrderVoiceService : IDisposable
{
    void WarmUp();

    bool TrySelectClip(string voiceDefinition, string voiceTypeId, out VanillaOrderVoiceClip clip);

    bool TryGetClip(
        string sampleName,
        string voiceTypeId,
        string preferredVoiceDefinition,
        out VanillaOrderVoiceClip clip);

    bool TryPlay(Agent agent, VanillaOrderVoiceClip clip);

    void Tick();
}

public sealed class VanillaOrderVoiceClip
{
    public string SampleName { get; }
    public string VoiceTypeId { get; }
    public string FaceAnimation { get; }

    internal byte[] SoundData { get; }

    internal VanillaOrderVoiceClip(
        string sampleName,
        string voiceTypeId,
        string faceAnimation,
        byte[] soundData)
    {
        SampleName = sampleName;
        VoiceTypeId = voiceTypeId;
        FaceAnimation = faceAnimation;
        SoundData = soundData;
    }
}

/// <summary>
/// Selects and plays exact order recordings from Bannerlord's installed voice bank.
/// </summary>
public sealed class VanillaOrderVoiceService : IVanillaOrderVoiceService
{
    private const string ManifestResourceName =
        "Missions.Agents.Voice.vanilla_order_voice_manifest.json";
    private const string ProgrammerEventPath = "event:/Extra/voiceover";
    private const float OrderVoiceMaxDistance = 450f;

    private static readonly ILogger Logger = LogManager.GetLogger<VanillaOrderVoiceService>();

    private readonly Random random = new Random();
    private readonly Dictionary<string, VoiceMapping> entriesByVoice;
    private readonly Dictionary<string, List<VoiceMapping>> entriesBySample;
    private readonly Dictionary<string, byte[]> audioBySample;
    private readonly ClipFileCache clipFileCache;
    private readonly List<ActiveVoice> activeVoices = new List<ActiveVoice>();

    private bool loadAttempted;
    private bool disposed;

    public VanillaOrderVoiceService()
    {
        entriesByVoice = new Dictionary<string, VoiceMapping>(StringComparer.Ordinal);
        entriesBySample = new Dictionary<string, List<VoiceMapping>>(StringComparer.Ordinal);
        audioBySample = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        clipFileCache = new ClipFileCache(GetDefaultCacheDirectory());
    }

    public void WarmUp()
    {
        EnsureLoaded();
    }

    public bool TrySelectClip(
        string voiceDefinition,
        string voiceTypeId,
        out VanillaOrderVoiceClip clip)
    {
        clip = null;
        if (disposed || string.IsNullOrEmpty(voiceDefinition) || string.IsNullOrEmpty(voiceTypeId))
            return false;
        if (!EnsureLoaded()) return false;

        if (!entriesByVoice.TryGetValue(GetVoiceKey(voiceDefinition, voiceTypeId), out var entry))
            return false;

        if (!entry.Event.TrySelect(random, out string sampleName)) return false;
        return TryCreateClip(sampleName, voiceTypeId, entry, out clip);
    }

    public bool TryGetClip(
        string sampleName,
        string voiceTypeId,
        string preferredVoiceDefinition,
        out VanillaOrderVoiceClip clip)
    {
        clip = null;
        if (disposed || string.IsNullOrEmpty(sampleName) || string.IsNullOrEmpty(voiceTypeId))
            return false;
        if (!EnsureLoaded()) return false;

        VoiceMapping entry = null;
        if (!string.IsNullOrEmpty(preferredVoiceDefinition))
        {
            entriesByVoice.TryGetValue(GetVoiceKey(preferredVoiceDefinition, voiceTypeId), out entry);
            if (entry != null && !entry.Event.Contains(sampleName))
                entry = null;
        }

        if (entry == null && entriesBySample.TryGetValue(sampleName, out var candidates))
        {
            entry = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.VoiceType, voiceTypeId, StringComparison.Ordinal));
        }

        if (entry == null) return false;

        return TryCreateClip(sampleName, voiceTypeId, entry, out clip);
    }

    public bool TryPlay(Agent agent, VanillaOrderVoiceClip clip)
    {
        if (disposed || agent == null || clip == null || agent.Mission == null || !agent.IsActive())
            return false;

        StopActiveVoices(agent);

        SoundEvent soundEvent = null;
        try
        {
            // Vanilla's external-file programmer event owns its audio independently of the managed heap.
            string soundPath = clipFileCache.GetOrCreatePath(clip.SampleName, clip.SoundData);
            soundEvent = SoundEvent.CreateEventFromExternalFile(
                ProgrammerEventPath,
                soundPath,
                agent.Mission.Scene,
                is3d: true,
                isBlocking: false);

            if (soundEvent == null || !soundEvent.IsValid)
            {
                Logger.Warning("Failed to create exact order voice sample {SampleName} from its local cache",
                    clip.SampleName);
                return false;
            }

            Vec3 voiceDistance = soundEvent.GetEventMinMaxDistance();
            voiceDistance.y = OrderVoiceMaxDistance;
            soundEvent.SetEventMinMaxDistance(voiceDistance);
            soundEvent.SetPosition(agent.GetEyeGlobalPosition());
            if (!soundEvent.Play())
            {
                Stop(soundEvent);
                Logger.Warning("Failed to start exact order voice sample {SampleName}", clip.SampleName);
                return false;
            }

            if (!string.IsNullOrEmpty(clip.FaceAnimation))
            {
                agent.SetAgentFacialAnimation(
                    Agent.FacialAnimChannel.High,
                    clip.FaceAnimation,
                    loop: false);
            }

            activeVoices.Add(new ActiveVoice(agent, soundEvent));
            return true;
        }
        catch (Exception ex)
        {
            Stop(soundEvent);
            Logger.Warning(ex,
                "Failed to prepare exact order voice sample {SampleName}; using native voice playback",
                clip.SampleName);
            return false;
        }
    }

    public void Tick()
    {
        if (disposed) return;

        for (int i = activeVoices.Count - 1; i >= 0; i--)
        {
            ActiveVoice activeVoice = activeVoices[i];
            SoundEvent soundEvent = activeVoice.SoundEvent;

            if (!soundEvent.IsValid)
            {
                activeVoices.RemoveAt(i);
                continue;
            }

            Agent agent = activeVoice.Agent;
            if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
            {
                Stop(soundEvent);
                activeVoices.RemoveAt(i);
                continue;
            }

            if (!soundEvent.IsPlaying())
            {
                Stop(soundEvent);
                activeVoices.RemoveAt(i);
                continue;
            }

            soundEvent.SetPosition(agent.GetEyeGlobalPosition());
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (ActiveVoice activeVoice in activeVoices)
            Stop(activeVoice.SoundEvent);

        activeVoices.Clear();
        entriesByVoice.Clear();
        entriesBySample.Clear();
        audioBySample.Clear();
    }

    private bool EnsureLoaded()
    {
        if (loadAttempted) return audioBySample.Count > 0;
        loadAttempted = true;

        try
        {
            LoadVoiceClips();
            return audioBySample.Count > 0;
        }
        catch (Exception ex)
        {
            entriesByVoice.Clear();
            entriesBySample.Clear();
            audioBySample.Clear();
            Logger.Error(ex, "Failed to load exact vanilla order voices; using native voice playback");
            return false;
        }
    }

    private void LoadVoiceClips()
    {
        VoiceManifest manifest = LoadManifest();
        if (manifest.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported vanilla order voice manifest schema {manifest.SchemaVersion}.");
        if (manifest.VoiceDefinitions == null || manifest.VoiceDefinitions.Count == 0)
            throw new InvalidDataException("The vanilla order voice manifest has no voice definitions.");
        if (manifest.Events == null || manifest.Events.Count == 0)
            throw new InvalidDataException("The vanilla order voice manifest has no events.");
        if (!string.Equals(manifest.PlayMode, "SmartRandom", StringComparison.Ordinal) ||
            !string.Equals(manifest.SelectionMode, "Normal", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported vanilla order voice playlist {manifest.PlayMode}/{manifest.SelectionMode}.");
        }

        HashSet<string> requestedSamples = new HashSet<string>(StringComparer.Ordinal);
        foreach (VoiceEvent voiceEvent in manifest.Events.Values)
        {
            if (voiceEvent?.Samples == null) continue;
            foreach (VoiceSample sample in voiceEvent.Samples)
            {
                if (!string.IsNullOrEmpty(sample?.Name))
                    requestedSamples.Add(sample.Name);
            }
        }

        string nativeModulePath = ModuleHelper.GetModuleFullPath("Native");
        string bankPath = System.IO.Path.Combine(nativeModulePath, "Sounds", "PC", "voice.bank");
        byte[] fsbBytes = ReadEmbeddedFsb(bankPath);
        FmodSoundBank soundBank = FsbLoader.LoadFsbFromByteArray(fsbBytes);

        int foundSamples = 0;
        int failedSamples = 0;
        foreach (FmodSample sample in soundBank.Samples)
        {
            string sampleName = sample.Name;
            if (string.IsNullOrEmpty(sampleName) || !requestedSamples.Contains(sampleName))
                continue;

            foundSamples++;
            if (!sample.RebuildAsStandardFileFormat(out var soundData, out var extension) ||
                soundData == null ||
                !string.Equals(extension, "ogg", StringComparison.OrdinalIgnoreCase))
            {
                failedSamples++;
                continue;
            }

            audioBySample[sampleName] = soundData;
        }

        int missingSamples = requestedSamples.Count - foundSamples;
        foreach (VoiceEvent voiceEvent in manifest.Events.Values)
            voiceEvent?.Prepare(audioBySample);

        foreach (var definitionPair in manifest.VoiceDefinitions)
        {
            string voiceDefinition = definitionPair.Key;
            if (definitionPair.Value == null) continue;

            foreach (var voicePair in definitionPair.Value)
            {
                string voiceType = voicePair.Key;
                VoiceDefinition voiceDefinitionEntry = voicePair.Value;
                if (voiceDefinitionEntry == null ||
                    string.IsNullOrEmpty(voiceDefinitionEntry.Event) ||
                    !manifest.Events.TryGetValue(voiceDefinitionEntry.Event, out var voiceEvent) ||
                    voiceEvent.AvailableSamples.Length == 0)
                {
                    continue;
                }

                var mapping = new VoiceMapping(
                    voiceType,
                    voiceDefinitionEntry.FaceAnimation,
                    voiceEvent);
                entriesByVoice[GetVoiceKey(voiceDefinition, voiceType)] = mapping;

                foreach (VoiceSample sample in voiceEvent.AvailableSamples)
                {
                    if (!entriesBySample.TryGetValue(sample.Name, out var entries))
                    {
                        entries = new List<VoiceMapping>();
                        entriesBySample.Add(sample.Name, entries);
                    }

                    entries.Add(mapping);
                }
            }
        }

        Logger.Information(
            "Loaded {ClipCount} exact vanilla order voice clips from FSB version {FsbVersion} " +
            "using manifest bank version {ManifestBankVersion}; {MissingCount} missing, {FailedCount} failed",
            audioBySample.Count,
            soundBank.Header.Version,
            manifest.BankVersion,
            missingSamples,
            failedSamples);
    }

    private static VoiceManifest LoadManifest()
    {
        Assembly assembly = typeof(VanillaOrderVoiceService).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
            throw new InvalidDataException($"Missing embedded resource {ManifestResourceName}.");

        using StreamReader reader = new StreamReader(stream);
        VoiceManifest manifest = JsonConvert.DeserializeObject<VoiceManifest>(reader.ReadToEnd());
        if (manifest == null)
            throw new InvalidDataException("Failed to deserialize the vanilla order voice manifest.");

        return manifest;
    }

    private static byte[] ReadEmbeddedFsb(string bankPath)
    {
        using FileStream stream = File.OpenRead(bankPath);
        long fsbOffset = FindFsbOffset(stream);
        if (fsbOffset < 0)
            throw new InvalidDataException($"No embedded FSB5 bank was found in {bankPath}.");

        long fsbLength = stream.Length - fsbOffset;
        if (fsbLength <= 0 || fsbLength > int.MaxValue)
            throw new InvalidDataException($"The embedded FSB5 bank in {bankPath} has an invalid length.");

        byte[] fsbBytes = new byte[(int)fsbLength];
        stream.Position = fsbOffset;

        int bytesRead = 0;
        while (bytesRead < fsbBytes.Length)
        {
            int read = stream.Read(fsbBytes, bytesRead, fsbBytes.Length - bytesRead);
            if (read == 0)
                throw new EndOfStreamException($"Unexpected end of {bankPath} while reading its FSB5 bank.");

            bytesRead += read;
        }

        return fsbBytes;
    }

    private static long FindFsbOffset(Stream stream)
    {
        byte[] magic = { (byte)'F', (byte)'S', (byte)'B', (byte)'5' };
        byte[] buffer = new byte[8192];
        long scanned = 0;
        int matched = 0;
        int read;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                byte current = buffer[i];
                if (current == magic[matched])
                {
                    matched++;
                    if (matched == magic.Length)
                        return scanned + i - magic.Length + 1;
                }
                else
                {
                    matched = current == magic[0] ? 1 : 0;
                }
            }

            scanned += read;
        }

        return -1;
    }

    private bool TryCreateClip(
        string sampleName,
        string voiceTypeId,
        VoiceMapping entry,
        out VanillaOrderVoiceClip clip)
    {
        clip = null;
        if (!audioBySample.TryGetValue(sampleName, out var soundData))
            return false;

        clip = new VanillaOrderVoiceClip(
            sampleName,
            voiceTypeId,
            entry.FaceAnimation,
            soundData);
        return true;
    }

    private static string GetVoiceKey(string voiceDefinition, string voiceTypeId)
    {
        return voiceDefinition + "\0" + voiceTypeId;
    }

    private static string GetDefaultCacheDirectory()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localApplicationData))
            localApplicationData = System.IO.Path.GetTempPath();

        return System.IO.Path.Combine(localApplicationData, "BannerlordCoop", "VanillaOrderVoices");
    }

    private void StopActiveVoices(Agent agent)
    {
        for (int i = activeVoices.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(activeVoices[i].Agent, agent)) continue;

            Stop(activeVoices[i].SoundEvent);
            activeVoices.RemoveAt(i);
        }
    }

    private static void Stop(SoundEvent soundEvent)
    {
        if (soundEvent == null) return;

        soundEvent.Stop();
    }

    private sealed class ActiveVoice
    {
        public Agent Agent { get; }
        public SoundEvent SoundEvent { get; }

        public ActiveVoice(Agent agent, SoundEvent soundEvent)
        {
            Agent = agent;
            SoundEvent = soundEvent;
        }
    }

    internal sealed class ClipFileCache
    {
        private readonly object sync = new object();
        private readonly string cacheDirectory;
        private readonly Dictionary<string, string> pathsBySample =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public ClipFileCache(string cacheDirectory)
        {
            if (string.IsNullOrEmpty(cacheDirectory))
                throw new ArgumentException("A cache directory is required.", nameof(cacheDirectory));

            this.cacheDirectory = cacheDirectory;
        }

        public string GetOrCreatePath(string sampleName, byte[] soundData)
        {
            if (string.IsNullOrEmpty(sampleName))
                throw new ArgumentException("A sample name is required.", nameof(sampleName));
            if (soundData == null || soundData.Length == 0)
                throw new ArgumentException("Sound data is required.", nameof(soundData));

            lock (sync)
            {
                if (pathsBySample.TryGetValue(sampleName, out string cachedPath))
                    return cachedPath;

                string path = Materialize(soundData);
                pathsBySample.Add(sampleName, path);
                return path;
            }
        }

        private string Materialize(byte[] soundData)
        {
            Directory.CreateDirectory(cacheDirectory);

            string contentHash;
            using (SHA256 sha256 = SHA256.Create())
            {
                contentHash = BitConverter.ToString(sha256.ComputeHash(soundData))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }

            string soundPath = System.IO.Path.Combine(cacheDirectory, contentHash + ".ogg");
            if (File.Exists(soundPath)) return soundPath;

            string temporaryPath = System.IO.Path.Combine(
                cacheDirectory,
                contentHash + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.Write(soundData, 0, soundData.Length);
                }

                try
                {
                    File.Move(temporaryPath, soundPath);
                }
                catch (IOException) when (File.Exists(soundPath))
                {
                    // Another client published the same complete content first.
                }

                return soundPath;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }

    private sealed class VoiceMapping
    {
        public string VoiceType { get; }
        public string FaceAnimation { get; }
        public VoiceEvent Event { get; }

        public VoiceMapping(string voiceType, string faceAnimation, VoiceEvent voiceEvent)
        {
            VoiceType = voiceType;
            FaceAnimation = faceAnimation;
            Event = voiceEvent;
        }
    }

    private sealed class VoiceManifest
    {
        [JsonProperty("SchemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("BankVersion")]
        public int BankVersion { get; set; }

        [JsonProperty("PlayMode")]
        public string PlayMode { get; set; }

        [JsonProperty("SelectionMode")]
        public string SelectionMode { get; set; }

        [JsonProperty("VoiceDefinitions")]
        public Dictionary<string, Dictionary<string, VoiceDefinition>> VoiceDefinitions { get; set; }

        [JsonProperty("Events")]
        public Dictionary<string, VoiceEvent> Events { get; set; }
    }

    private sealed class VoiceDefinition
    {
        [JsonProperty("Event")]
        public string Event { get; set; }

        [JsonProperty("FaceAnimation")]
        public string FaceAnimation { get; set; }
    }

    internal sealed class VoiceEvent
    {
        [JsonProperty("UsesPlayPercentages")]
        public bool UsesPlayPercentages { get; set; }

        [JsonProperty("Samples")]
        public VoiceSample[] Samples { get; set; }

        [JsonIgnore]
        public VoiceSample[] AvailableSamples { get; private set; } = Array.Empty<VoiceSample>();

        private HashSet<string> availableNames = new HashSet<string>(StringComparer.Ordinal);
        private VoiceSample[] shuffledSamples = Array.Empty<VoiceSample>();
        private int shuffledIndex;
        private string lastSampleName;

        public void Prepare(Dictionary<string, byte[]> audioBySample)
        {
            var available = new List<VoiceSample>();
            availableNames = new HashSet<string>(StringComparer.Ordinal);

            if (Samples != null)
            {
                foreach (VoiceSample sample in Samples)
                {
                    if (sample == null ||
                        string.IsNullOrEmpty(sample.Name) ||
                        !audioBySample.ContainsKey(sample.Name) ||
                        !availableNames.Add(sample.Name))
                    {
                        continue;
                    }

                    available.Add(sample);
                }
            }

            AvailableSamples = available.ToArray();
            shuffledSamples = Array.Empty<VoiceSample>();
            shuffledIndex = 0;
            lastSampleName = null;
        }

        public bool Contains(string sampleName)
        {
            return availableNames.Contains(sampleName);
        }

        public bool TrySelect(Random random, out string sampleName)
        {
            sampleName = null;
            if (AvailableSamples.Length == 0) return false;

            if (UsesPlayPercentages && TrySelectWeighted(random, out sampleName))
                return true;

            if (shuffledIndex >= shuffledSamples.Length)
                RefillShuffledSamples(random);

            sampleName = shuffledSamples[shuffledIndex++].Name;
            lastSampleName = sampleName;
            return true;
        }

        private bool TrySelectWeighted(Random random, out string sampleName)
        {
            sampleName = null;
            double totalWeight = 0;
            foreach (VoiceSample sample in AvailableSamples)
                totalWeight += Math.Max(0, sample.Weight);

            if (totalWeight <= 0) return false;

            double selection = random.NextDouble() * totalWeight;
            foreach (VoiceSample sample in AvailableSamples)
            {
                selection -= Math.Max(0, sample.Weight);
                if (selection > 0) continue;

                sampleName = sample.Name;
                lastSampleName = sampleName;
                return true;
            }

            sampleName = AvailableSamples[AvailableSamples.Length - 1].Name;
            lastSampleName = sampleName;
            return true;
        }

        private void RefillShuffledSamples(Random random)
        {
            shuffledSamples = (VoiceSample[])AvailableSamples.Clone();
            for (int i = shuffledSamples.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                VoiceSample value = shuffledSamples[i];
                shuffledSamples[i] = shuffledSamples[swapIndex];
                shuffledSamples[swapIndex] = value;
            }

            if (shuffledSamples.Length > 1 &&
                string.Equals(shuffledSamples[0].Name, lastSampleName, StringComparison.Ordinal))
            {
                VoiceSample value = shuffledSamples[0];
                shuffledSamples[0] = shuffledSamples[1];
                shuffledSamples[1] = value;
            }

            shuffledIndex = 0;
        }
    }

    internal sealed class VoiceSample
    {
        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Weight")]
        public float Weight { get; set; }
    }
}

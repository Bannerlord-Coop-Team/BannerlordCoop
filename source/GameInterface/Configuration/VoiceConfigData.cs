using Common.Logging;
using Common.Voice;

namespace GameInterface.Configuration;

public sealed class VoiceConfigData
{
    public float MapFullVolumeDistance { get; set; } = 5;
    public float MapMaximumDistance { get; set; } = 20;
    public float SceneFullVolumeDistance { get; set; } = 10;
    public float SceneMaximumDistance { get; set; } = 50;

    public VoiceRanges ToRanges()
    {
        var ranges = new VoiceRanges(MapFullVolumeDistance, MapMaximumDistance,
            SceneFullVolumeDistance, SceneMaximumDistance);
        if (ranges.IsValid) return ranges;
        LogManager.GetLogger<VoiceConfigData>().Warning("Invalid voice distances in mod-config.json; using defaults");
        return new VoiceRanges();
    }
}

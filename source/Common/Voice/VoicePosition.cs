using ProtoBuf;

namespace Common.Voice;

[ProtoContract]
public sealed class VoicePosition
{
    public const string CampaignContext = "campaign";
    [ProtoMember(1)] public string Context { get; private set; }
    [ProtoMember(2)] public long Epoch { get; private set; }
    [ProtoMember(3)] public float X { get; private set; }
    [ProtoMember(4)] public float Y { get; private set; }
    [ProtoMember(5)] public float Z { get; private set; }
    [ProtoMember(6)] public bool CanSpeak { get; private set; }
    [ProtoMember(7)] public bool CanHear { get; private set; }

    private VoicePosition() { }

    public VoicePosition(string context, long epoch, float x, float y, float z, bool canSpeak, bool canHear)
    {
        Context = context;
        Epoch = epoch;
        X = x;
        Y = y;
        Z = z;
        CanSpeak = canSpeak;
        CanHear = canHear;
    }

    public bool IsFinite => !float.IsNaN(X) && !float.IsInfinity(X) &&
        !float.IsNaN(Y) && !float.IsInfinity(Y) && !float.IsNaN(Z) && !float.IsInfinity(Z);
}

[ProtoContract]
public sealed class VoiceRanges
{
    [ProtoMember(1)] public float MapFull { get; private set; }
    [ProtoMember(2)] public float MapMaximum { get; private set; }
    [ProtoMember(3)] public float SceneFull { get; private set; }
    [ProtoMember(4)] public float SceneMaximum { get; private set; }

    private VoiceRanges() { }

    public VoiceRanges(float mapFull = 5, float mapMaximum = 20, float sceneFull = 10, float sceneMaximum = 50)
    {
        MapFull = mapFull;
        MapMaximum = mapMaximum;
        SceneFull = sceneFull;
        SceneMaximum = sceneMaximum;
    }

    public bool IsValid => ValidPair(MapFull, MapMaximum) && ValidPair(SceneFull, SceneMaximum);

    private bool ValidPair(float full, float maximum)
    {
        return full >= 0 && maximum > full && !float.IsInfinity(maximum);
    }
}

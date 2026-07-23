using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct AtmosphereInfoSurrogate
{
    [ProtoMember(1)]  public uint Seed { get; set; }
    [ProtoMember(2)]  public string AtmosphereName { get; set; }
    [ProtoMember(3)]  public SunInformationSurrogate SunInfo { get; set; }
    [ProtoMember(4)]  public RainInformationSurrogate RainInfo { get; set; }
    [ProtoMember(5)]  public SnowInformationSurrogate SnowInfo { get; set; }
    [ProtoMember(6)]  public AmbientInformationSurrogate AmbientInfo { get; set; }
    [ProtoMember(7)]  public FogInformationSurrogate FogInfo { get; set; }
    [ProtoMember(8)]  public SkyInformationSurrogate SkyInfo { get; set; }
    [ProtoMember(9)]  public NauticalInformationSurrogate NauticalInfo { get; set; }
    [ProtoMember(10)] public TimeInformationSurrogate TimeInfo { get; set; }
    [ProtoMember(11)] public AreaInformationSurrogate AreaInfo { get; set; }
    [ProtoMember(12)] public PostProcessInformationSurrogate PostProInfo { get; set; }
    [ProtoMember(13)] public string InterpolatedAtmosphereName { get; set; }

    public AtmosphereInfoSurrogate(AtmosphereInfo a)
    {
        Seed = a.Seed;
        AtmosphereName = a.AtmosphereName;
        SunInfo = a.SunInfo;
        RainInfo = a.RainInfo;
        SnowInfo = a.SnowInfo;
        AmbientInfo = a.AmbientInfo;
        FogInfo = a.FogInfo;
        SkyInfo = a.SkyInfo;
        NauticalInfo = a.NauticalInfo;
        TimeInfo = a.TimeInfo;
        AreaInfo = a.AreaInfo;
        PostProInfo = a.PostProInfo;
        InterpolatedAtmosphereName = a.InterpolatedAtmosphereName;
    }

    public static implicit operator AtmosphereInfoSurrogate(AtmosphereInfo a) => new AtmosphereInfoSurrogate(a);

    public static implicit operator AtmosphereInfo(AtmosphereInfoSurrogate s) => new AtmosphereInfo
    {
        Seed = s.Seed,
        AtmosphereName = s.AtmosphereName,
        SunInfo = s.SunInfo,
        RainInfo = s.RainInfo,
        SnowInfo = s.SnowInfo,
        AmbientInfo = s.AmbientInfo,
        FogInfo = s.FogInfo,
        SkyInfo = s.SkyInfo,
        NauticalInfo = s.NauticalInfo,
        TimeInfo = s.TimeInfo,
        AreaInfo = s.AreaInfo,
        PostProInfo = s.PostProInfo,
        InterpolatedAtmosphereName = s.InterpolatedAtmosphereName,
    };
}

[ProtoContract]
internal struct SunInformationSurrogate
{
    [ProtoMember(1)] public float Altitude { get; set; }
    [ProtoMember(2)] public float Angle { get; set; }
    [ProtoMember(3)] public Vec3Surrogate Color { get; set; }
    [ProtoMember(4)] public float Brightness { get; set; }
    [ProtoMember(5)] public float MaxBrightness { get; set; }
    [ProtoMember(6)] public float Size { get; set; }
    [ProtoMember(7)] public float RayStrength { get; set; }

    public SunInformationSurrogate(SunInformation s)
    {
        Altitude = s.Altitude;
        Angle = s.Angle;
        Color = s.Color;
        Brightness = s.Brightness;
        MaxBrightness = s.MaxBrightness;
        Size = s.Size;
        RayStrength = s.RayStrength;
    }

    public static implicit operator SunInformationSurrogate(SunInformation s) => new SunInformationSurrogate(s);

    public static implicit operator SunInformation(SunInformationSurrogate s) => new SunInformation
    {
        Altitude = s.Altitude,
        Angle = s.Angle,
        Color = s.Color,
        Brightness = s.Brightness,
        MaxBrightness = s.MaxBrightness,
        Size = s.Size,
        RayStrength = s.RayStrength,
    };
}

[ProtoContract]
internal struct RainInformationSurrogate
{
    [ProtoMember(1)] public float Density { get; set; }

    public RainInformationSurrogate(RainInformation r) => Density = r.Density;

    public static implicit operator RainInformationSurrogate(RainInformation r) => new RainInformationSurrogate(r);

    public static implicit operator RainInformation(RainInformationSurrogate s) =>
        new RainInformation { Density = s.Density };
}

[ProtoContract]
internal struct SnowInformationSurrogate
{
    [ProtoMember(1)] public float Density { get; set; }

    public SnowInformationSurrogate(SnowInformation sn) => Density = sn.Density;

    public static implicit operator SnowInformationSurrogate(SnowInformation sn) => new SnowInformationSurrogate(sn);

    public static implicit operator SnowInformation(SnowInformationSurrogate s) =>
        new SnowInformation { Density = s.Density };
}

[ProtoContract]
internal struct AmbientInformationSurrogate
{
    [ProtoMember(1)] public float EnvironmentMultiplier { get; set; }
    [ProtoMember(2)] public Vec3Surrogate AmbientColor { get; set; }
    [ProtoMember(3)] public float MieScatterStrength { get; set; }
    [ProtoMember(4)] public float RayleighConstant { get; set; }

    public AmbientInformationSurrogate(AmbientInformation a)
    {
        EnvironmentMultiplier = a.EnvironmentMultiplier;
        AmbientColor = a.AmbientColor;
        MieScatterStrength = a.MieScatterStrength;
        RayleighConstant = a.RayleighConstant;
    }

    public static implicit operator AmbientInformationSurrogate(AmbientInformation a) => new AmbientInformationSurrogate(a);

    public static implicit operator AmbientInformation(AmbientInformationSurrogate s) => new AmbientInformation
    {
        EnvironmentMultiplier = s.EnvironmentMultiplier,
        AmbientColor = s.AmbientColor,
        MieScatterStrength = s.MieScatterStrength,
        RayleighConstant = s.RayleighConstant,
    };
}

[ProtoContract]
internal struct FogInformationSurrogate
{
    [ProtoMember(1)] public float Density { get; set; }
    [ProtoMember(2)] public Vec3Surrogate Color { get; set; }
    [ProtoMember(3)] public float Falloff { get; set; }

    public FogInformationSurrogate(FogInformation f)
    {
        Density = f.Density;
        Color = f.Color;
        Falloff = f.Falloff;
    }

    public static implicit operator FogInformationSurrogate(FogInformation f) => new FogInformationSurrogate(f);

    public static implicit operator FogInformation(FogInformationSurrogate s) => new FogInformation
    {
        Density = s.Density,
        Color = s.Color,
        Falloff = s.Falloff,
    };
}

[ProtoContract]
internal struct SkyInformationSurrogate
{
    [ProtoMember(1)] public float Brightness { get; set; }

    public SkyInformationSurrogate(SkyInformation sk) => Brightness = sk.Brightness;

    public static implicit operator SkyInformationSurrogate(SkyInformation sk) => new SkyInformationSurrogate(sk);

    public static implicit operator SkyInformation(SkyInformationSurrogate s) =>
        new SkyInformation { Brightness = s.Brightness };
}

[ProtoContract]
internal struct NauticalInformationSurrogate
{
    [ProtoMember(1)] public float WaveStrength { get; set; }
    [ProtoMember(2)] public Vec2Surrogate WindVector { get; set; }
    [ProtoMember(3)] public int CanUseLowAltitudeAtmosphere { get; set; }
    [ProtoMember(4)] public int UseSceneWindDirection { get; set; }
    [ProtoMember(5)] public int IsRiverBattle { get; set; }
    [ProtoMember(6)] public int IsInsideStorm { get; set; }
    [ProtoMember(7)] public int UsesNavalSimulatedWater { get; set; }

    public NauticalInformationSurrogate(NauticalInformation n)
    {
        WaveStrength = n.WaveStrength;
        WindVector = n.WindVector;
        CanUseLowAltitudeAtmosphere = n.CanUseLowAltitudeAtmosphere;
        UseSceneWindDirection = n.UseSceneWindDirection;
        IsRiverBattle = n.IsRiverBattle;
        IsInsideStorm = n.IsInsideStorm;
        UsesNavalSimulatedWater = n.UsesNavalSimulatedWater;
    }

    public static implicit operator NauticalInformationSurrogate(NauticalInformation n) => new NauticalInformationSurrogate(n);

    public static implicit operator NauticalInformation(NauticalInformationSurrogate s) => new NauticalInformation
    {
        WaveStrength = s.WaveStrength,
        WindVector = s.WindVector,
        CanUseLowAltitudeAtmosphere = s.CanUseLowAltitudeAtmosphere,
        UseSceneWindDirection = s.UseSceneWindDirection,
        IsRiverBattle = s.IsRiverBattle,
        IsInsideStorm = s.IsInsideStorm,
        UsesNavalSimulatedWater = s.UsesNavalSimulatedWater,
    };
}

[ProtoContract]
internal struct TimeInformationSurrogate
{
    [ProtoMember(1)] public float TimeOfDay { get; set; }
    [ProtoMember(2)] public float NightTimeFactor { get; set; }
    [ProtoMember(3)] public float DrynessFactor { get; set; }
    [ProtoMember(4)] public float WinterTimeFactor { get; set; }
    [ProtoMember(5)] public int Season { get; set; }

    public TimeInformationSurrogate(TimeInformation t)
    {
        TimeOfDay = t.TimeOfDay;
        NightTimeFactor = t.NightTimeFactor;
        DrynessFactor = t.DrynessFactor;
        WinterTimeFactor = t.WinterTimeFactor;
        Season = t.Season;
    }

    public static implicit operator TimeInformationSurrogate(TimeInformation t) => new TimeInformationSurrogate(t);

    public static implicit operator TimeInformation(TimeInformationSurrogate s) => new TimeInformation
    {
        TimeOfDay = s.TimeOfDay,
        NightTimeFactor = s.NightTimeFactor,
        DrynessFactor = s.DrynessFactor,
        WinterTimeFactor = s.WinterTimeFactor,
        Season = s.Season,
    };
}

[ProtoContract]
internal struct AreaInformationSurrogate
{
    [ProtoMember(1)] public float Temperature { get; set; }
    [ProtoMember(2)] public float Humidity { get; set; }

    public AreaInformationSurrogate(AreaInformation a)
    {
        Temperature = a.Temperature;
        Humidity = a.Humidity;
    }

    public static implicit operator AreaInformationSurrogate(AreaInformation a) => new AreaInformationSurrogate(a);

    public static implicit operator AreaInformation(AreaInformationSurrogate s) => new AreaInformation
    {
        Temperature = s.Temperature,
        Humidity = s.Humidity,
    };
}

[ProtoContract]
internal struct PostProcessInformationSurrogate
{
    [ProtoMember(1)] public float MinExposure { get; set; }
    [ProtoMember(2)] public float MaxExposure { get; set; }
    [ProtoMember(3)] public float BrightpassThreshold { get; set; }
    [ProtoMember(4)] public float MiddleGray { get; set; }

    public PostProcessInformationSurrogate(PostProcessInformation p)
    {
        MinExposure = p.MinExposure;
        MaxExposure = p.MaxExposure;
        BrightpassThreshold = p.BrightpassThreshold;
        MiddleGray = p.MiddleGray;
    }

    public static implicit operator PostProcessInformationSurrogate(PostProcessInformation p) => new PostProcessInformationSurrogate(p);

    public static implicit operator PostProcessInformation(PostProcessInformationSurrogate s) => new PostProcessInformation
    {
        MinExposure = s.MinExposure,
        MaxExposure = s.MaxExposure,
        BrightpassThreshold = s.BrightpassThreshold,
        MiddleGray = s.MiddleGray,
    };
}

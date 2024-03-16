using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct BasicCultureObjectSurrogate
{

    [ProtoMember(1)]
    public string StringId { get; set; }
    [ProtoMember(2)]
    public bool IsMainCulture { get; private set; }
    [ProtoMember(3)]
    public bool IsBandit { get; private set; }
    [ProtoMember(4)]
    public bool CanHaveSettlement { get; private set; }
    [ProtoMember(5)]
    public uint Color { get; private set; }
    [ProtoMember(6)]
    public uint Color2 { get; }
    [ProtoMember(7)]
    public uint ClothAlternativeColor { get; }
    [ProtoMember(8)]
    public uint ClothAlternativeColor2 { get; }
    [ProtoMember(9)]
    public uint BackgroundColor1 { get; }
    [ProtoMember(10)]
    public uint BackgroundColor2 { get; }
    [ProtoMember(11)]
    public uint ForegroundColor1 { get; private set; }
    [ProtoMember(12)]
    public uint ForegroundColor2 { get; private set; }
    [ProtoMember(13)]
    public string EncounterBackgroundMesh { get; }
    [ProtoMember(14)]
    public string BannerKey { get; }

    public BasicCultureObjectSurrogate(BasicCultureObject cultureObject)
    {
        StringId = cultureObject.StringId;

        IsMainCulture = cultureObject.IsMainCulture;

        IsBandit = cultureObject.IsBandit;

        CanHaveSettlement = cultureObject.CanHaveSettlement;

        Color = cultureObject.Color;
        Color2 = cultureObject.Color2;

        ClothAlternativeColor = cultureObject.ClothAlternativeColor;
        ClothAlternativeColor2 = cultureObject.ClothAlternativeColor2;

        BackgroundColor1 = cultureObject.BackgroundColor1;
        BackgroundColor2 = cultureObject.BackgroundColor2;

        ForegroundColor1 = cultureObject.ForegroundColor1;
        ForegroundColor2 = cultureObject.ForegroundColor2;

        EncounterBackgroundMesh = cultureObject.EncounterBackgroundMesh;
        BannerKey = cultureObject.BannerKey;

    }


    public static implicit operator BasicCultureObjectSurrogate(BasicCultureObject cultureObject)
    {
        return new BasicCultureObjectSurrogate(cultureObject);
    }

    public static implicit operator BasicCultureObject(BasicCultureObjectSurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (objectManager.TryGetObject<BasicCultureObject>(surrogate.StringId, out var cultureObject) == false) return null;


        cultureObject.IsMainCulture = surrogate.IsMainCulture;

        cultureObject.IsBandit = surrogate.IsBandit;

        cultureObject.CanHaveSettlement = surrogate.CanHaveSettlement;

        cultureObject.Color = surrogate.Color;
        cultureObject.Color2 = surrogate.Color2;

        cultureObject.ClothAlternativeColor = surrogate.ClothAlternativeColor;
        cultureObject.ClothAlternativeColor2 = surrogate.ClothAlternativeColor2;

        cultureObject.BackgroundColor1 = surrogate.BackgroundColor1;
        cultureObject.BackgroundColor2 = surrogate.BackgroundColor2;

        cultureObject.ForegroundColor1 = surrogate.ForegroundColor1;
        cultureObject.ForegroundColor2 = surrogate.ForegroundColor2;

        cultureObject.EncounterBackgroundMesh = surrogate.EncounterBackgroundMesh;
        cultureObject.BannerKey = surrogate.BannerKey;
        return cultureObject;
    }
}

using System;
using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

internal class SurrogateCollection : ISurrogateCollection
{
    public SurrogateCollection()
    {
        if (RuntimeTypeModel.Default.CanSerialize(typeof(Vec2)) == false)
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(TextObject)) == false)
            RuntimeTypeModel.Default.SetSurrogate<TextObject, TextObjectSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(ItemModifier)) == false)
            RuntimeTypeModel.Default.SetSurrogate<ItemModifier, ItemModifierSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(ItemModifierGroup)) == false)
            RuntimeTypeModel.Default.SetSurrogate<ItemModifierGroup, ItemModifierGroupSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(CampaignTime)) == false)
            RuntimeTypeModel.Default.SetSurrogate<CampaignTime, CampaignTimeSurrogate>();
            
        if (RuntimeTypeModel.Default.CanSerialize(typeof(Banner)) == false)
            RuntimeTypeModel.Default.SetSurrogate<Banner, BannerSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(Vec3)) == false)
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(ItemObject)) == false)
            RuntimeTypeModel.Default.SetSurrogate<ItemObject, ItemObjectSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(ItemComponent)) == false)
            RuntimeTypeModel.Default.SetSurrogate<ItemComponent, ItemComponentSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(ItemCategory)) == false)
            RuntimeTypeModel.Default.SetSurrogate<ItemCategory, ItemCategorySurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(SkeletonScale)) == false)
            RuntimeTypeModel.Default.SetSurrogate<SkeletonScale, SkeletonScaleSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(Monster)) == false)
            RuntimeTypeModel.Default.SetSurrogate<Monster, MonsterSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(WeaponComponent)) == false)
            RuntimeTypeModel.Default.SetSurrogate<WeaponComponent, WeaponComponentSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(SaddleComponent)) == false)
            RuntimeTypeModel.Default.SetSurrogate<SaddleComponent, SaddleComponentSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(HorseComponent)) == false)
            RuntimeTypeModel.Default.SetSurrogate<HorseComponent, HorseComponentSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(ArmorComponent)) == false)
            RuntimeTypeModel.Default.SetSurrogate<ArmorComponent, ArmorComponentSurrogate>();
    }
}

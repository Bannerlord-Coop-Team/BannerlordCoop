using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Equipments.Messages;

/// <summary>
/// The main hero's horse died in a mission and the roll lost or lamed it, for the server to apply to the hero's equipment.
/// </summary>
public readonly struct MainHeroMountKilled : IEvent
{
    public readonly Hero Hero;
    public readonly bool UsesCivilianEquipment;
    public readonly bool HorseLost;

    public MainHeroMountKilled(Hero hero, bool usesCivilianEquipment, bool horseLost)
    {
        Hero = hero;
        UsesCivilianEquipment = usesCivilianEquipment;
        HorseLost = horseLost;
    }
}

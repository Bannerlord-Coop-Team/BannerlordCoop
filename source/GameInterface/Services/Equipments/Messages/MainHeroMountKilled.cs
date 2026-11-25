using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Equipments.Messages;

public enum MountLoss
{
    Lost,
    Lamed,
}

/// <summary>
/// The main hero's horse died in a mission and the roll lost or lamed it, for the server to apply to the hero's equipment.
/// </summary>
public readonly struct MainHeroMountKilled : IEvent
{
    public const string LameHorseModifierId = "lame_horse";

    public readonly Hero Hero;
    public readonly bool UsesCivilianEquipment;
    public readonly MountLoss Loss;

    public MainHeroMountKilled(Hero hero, bool usesCivilianEquipment, MountLoss loss)
    {
        Hero = hero;
        UsesCivilianEquipment = usesCivilianEquipment;
        Loss = loss;
    }
}

using GameInterface.Services.MobileParties.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Equipments.Interfaces;

public interface IEquipmentInterface : IGameAbstraction
{
    /// <summary>
    /// Redraws the map party and mission agent of the hero wearing <paramref name="equipment"/>.
    /// </summary>
    void RefreshOwnerVisuals(Equipment equipment);
}

internal class EquipmentInterface : IEquipmentInterface
{
    public void RefreshOwnerVisuals(Equipment equipment)
    {
        var owner = Hero.AllAliveHeroes.FirstOrDefault(hero =>
            hero._battleEquipment == equipment ||
            hero._civilianEquipment == equipment ||
            hero._stealthEquipment == equipment);
        if (owner == null) return;

        var party = owner.PartyBelongedTo;
        if (party == null) return;

        party.Party.SetVisualAsDirty();

        if (Mission.Current == null || !party.IsPlayerParty()) return;

        var missionEquipment = Mission.Current.DoesMissionRequireCivilianEquipment ? owner.CivilianEquipment : owner.BattleEquipment;
        if (missionEquipment != equipment) return;

        foreach (var agent in Mission.Current.Agents)
        {
            if (agent.Character == owner.CharacterObject)
            {
                agent.UpdateSpawnEquipmentAndRefreshVisuals(equipment);
            }
        }
    }
}

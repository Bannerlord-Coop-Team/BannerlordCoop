using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Interfaces;

public interface IDefaultTroopSacrificeModelInterface : IGameAbstraction
{
    int GetNumberOfTroopsSacrificedForTryingToGetAway(Hero mainHero, MobileParty mainParty, BattleSideEnum playerBattleSide, MapEvent mapEvent);
}

public class DefaultTroopSacrificeModelInterface : IDefaultTroopSacrificeModelInterface
{
    // Re-implement with replaced MainHero & MainParty usage to work for any player party.
    // Instead of returning -1 on failure the result is clamped to the troops that can still be sacrificed.
    public int GetNumberOfTroopsSacrificedForTryingToGetAway(Hero mainHero, MobileParty mainParty, BattleSideEnum playerBattleSide, MapEvent mapEvent)
    {
        mapEvent.RecalculateStrengthOfSides();
        MapEventSide mapEventSide = mapEvent.GetMapEventSide(playerBattleSide);
        float playerStrength = mapEvent.StrengthOfSide[(int)playerBattleSide] + 1f;
        float strengthRatio = mapEvent.StrengthOfSide[(int)playerBattleSide.GetOppositeSide()] / playerStrength;
        int numRegularMembers = mainParty.Party.NumberOfRegularMembers;
        if (mainParty.Army != null)
        {
            foreach (MobileParty attachedParty in mainParty.Army.LeaderParty.AttachedParties)
            {
                numRegularMembers += attachedParty.Party.NumberOfRegularMembers;
            }
        }

        int activeTroops = mapEventSide.CountTroops(x => x.State == RosterTroopState.Active && !x.Troop.IsHero);
        float baseNumber = (numRegularMembers * MathF.Pow(MathF.Min(strengthRatio, 3f), 1.3f) * 0.1f) + 5f;
        ExplainedNumber explainedNumber = new(baseNumber);
        SkillHelper.AddSkillBonusForCharacter(DefaultSkillEffects.TacticsTroopSacrificeReduction, mainHero.CharacterObject, ref explainedNumber);
        explainedNumber = new ExplainedNumber(MathF.Max(1, MathF.Round(explainedNumber.ResultNumber)));
        if (!mainParty.IsCurrentlyAtSea)
        {
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Tactics.SwiftRegroup, mainParty, false, ref explainedNumber);
        }

        return MathF.Min(MathF.Round(explainedNumber.ResultNumber), MathF.Min(activeTroops, numRegularMembers));
    }
}

using Common;
using Common.Logging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Interfaces;

public interface IHeroRelationsInterface : IGameAbstraction
{
    /// <summary>
    /// Updates the relations of a notable with non-player heroes
    /// </summary>
    void UpdateNotableRelations(Hero notable);

    /// <summary>
    /// Updates the supporter status of a notable.
    /// Supporters stop supporting player clans when their relation drops below 50.
    /// </summary>
    void UpdateNotableSupport(Hero notable);

    /// <summary>
    /// Accepting notable support happens when a player pays gold to a notable
    /// to get them as a declared supporter of their clan.
    /// </summary>
    void AcceptNotableSupport(Hero mainHero, Hero notable, Clan playerClan, int cost);

    /// <summary>
    /// Ending notable support by agreement happens when a player deliberately
    /// talks to a notable to get them to stop supporting their clan.
    /// </summary>
    void EndNotableSupportByAgreement(Hero notable);
}

internal class HeroRelationsInterface : IHeroRelationsInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroRelationsInterface>();
    private readonly IObjectManager objectManager;

    public HeroRelationsInterface(
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void UpdateNotableRelations(Hero notable)
    {
        foreach (Clan clan in Clan.All)
        {
            UpdateRelation(clan, notable);
        }
    }

    public void UpdateNotableSupport(Hero notable)
    {
        foreach (var clan in Clan.NonBanditFactions)
        {
            ApplySupporter(clan, notable);
        }
    }

    public void EndNotableSupportByAgreement(Hero notable)
    {
        GameThread.RunSafe(() =>
        {
            notable.SupporterOf = null;
        });
    }

    public void AcceptNotableSupport(Hero mainHero, Hero notable, Clan playerClan, int cost)
    {
        GameThread.RunSafe(() =>
        {
            notable.SupporterOf = playerClan;
            GiveGoldAction.ApplyBetweenCharacters(mainHero, notable, cost, false);
            //TODO notify player of changed gold

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(mainHero, notable, 5, false);
            // TODO notify player of changed relation
        });
    }

    private void UpdateRelation(Clan clan, Hero notable)
    {
        GameThread.RunSafe(() =>
        {
            if (!clan.IsPlayerClan() && clan.Leader != null && !clan.IsEliminated)
            {
                int relation = notable.GetRelation(clan.Leader);
                if (relation > 0)
                {
                    float num = (float)relation / 1000f;
                    if (MBRandom.RandomFloat < num)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(notable, clan.Leader, -20, true);
                    }
                }
                else if (relation < 0)
                {
                    float num2 = (float)(-(float)relation) / 1000f;
                    if (MBRandom.RandomFloat < num2)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(notable, clan.Leader, 20, true);
                    }
                }
            }
        });
    }

    private void ApplySupporter(Clan clan, Hero notable)
    {
        GameThread.RunSafe(() =>
        {
            if (clan.Leader != null && !clan.IsPlayerClan()) // Instead of Clan.PlayerClan
            {
                int relation = notable.GetRelation(clan.Leader);
                if (relation > 50)
                {
                    float num = (float)(relation - 50) / 2000f;
                    if (MBRandom.RandomFloat < num)
                    {
                        notable.SupporterOf = clan;
                    }
                }
            }

            int relation2 = notable.GetRelation(notable.SupporterOf.Leader);
            if (relation2 < 0 || MBRandom.RandomFloat < (50f - (float)relation2) / 500f)
            {
                bool flag = notable.SupporterOf.IsPlayerClan(); // Instead of Clan.PlayerClan
                notable.SupporterOf = null;
                if (flag)
                {
                    // TODO Notify player of notable no longer supporting clan
                    //TextObject textObject = new TextObject("{=aaOIjHeP}{NOTABLE.NAME} no longer supports your clan as your relationship deteriorated too much.", null);
                    //textObject.SetCharacterProperties("NOTABLE", obj.What.Notable.CharacterObject, false);
                    //InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), new Color(0f, 1f, 0f, 1f)));
                }
            }
        });
    }
}

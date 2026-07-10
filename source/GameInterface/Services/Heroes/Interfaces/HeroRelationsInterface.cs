using Common;
using Common.Logging;
using Common.Network;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
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
    private readonly INetwork network;

    public HeroRelationsInterface(
        IObjectManager objectManager,
        INetwork network)
    {
        this.objectManager = objectManager;
        this.network = network;
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

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(mainHero, notable, 5, false);
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

            // Guard against updating supporterOf if the notable doesn't support a clan
            if (notable.SupporterOf == null) return;

            int relation2 = notable.GetRelation(notable.SupporterOf.Leader);
            if (relation2 < 0 || MBRandom.RandomFloat < (50f - (float)relation2) / 500f)
            {
                var supportedClan = notable.SupporterOf;

                bool flag = notable.SupporterOf.IsPlayerClan(); // Instead of Clan.PlayerClan
                notable.SupporterOf = null;
                if (flag)
                {
                    if (!objectManager.TryGetIdWithLogging(notable, out var notableId)) return;
                    if (!objectManager.TryGetIdWithLogging(supportedClan, out var supportedClanId)) return;

                    // Notify clients of a notable no longer supporting a player clan
                    network.SendAll(new NetworkNotifyRemovedSupporter(notableId, supportedClanId));
                }
            }
        });
    }
}

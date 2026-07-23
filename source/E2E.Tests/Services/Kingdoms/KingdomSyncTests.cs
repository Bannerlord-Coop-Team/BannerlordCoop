using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomSyncTests : SyncTestBase
{
    private string KingdomId;

    public KingdomSyncTests(ITestOutputHelper output) : base(output)
    {
        KingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<Clan>();
        TestEnvironment.CreateRegisteredObject<CultureObject>();
    }

    [Fact] 
    public void Server_Kingdom_Fields()
    {
        // PoliticalStagnation is randomly initialized in the Kingdom constructor
        Server.ObjectManager.TryGetObject<Kingdom>(KingdomId, out var kingdom);
        var initialPoliticalStagnation = kingdom.PoliticalStagnation;

        //TestEnvironment.AssertReferenceField<Kingdom, Settlement>(nameof(Kingdom._kingdomMidSettlement));
        TestEnvironment.AssertReferenceField<Kingdom, Clan>(nameof(Kingdom._rulingClan));
        TestEnvironment.AssertField<Kingdom, int>(nameof(Kingdom.PoliticalStagnation), 5, defaultValue: initialPoliticalStagnation);
        TestEnvironment.AssertField<Kingdom, float>(nameof(Kingdom._aggressiveness), 5f);
        TestEnvironment.AssertField<Kingdom, bool>(nameof(Kingdom._isEliminated), true);
        TestEnvironment.AssertField<Kingdom, int>(nameof(Kingdom._kingdomBudgetWallet), 5);
        TestEnvironment.AssertField<Kingdom, int>(nameof(Kingdom._tributeWallet), 5);
        TestEnvironment.AssertField<Kingdom, bool>(nameof(Kingdom._distanceToClosestNonAllyFortificationCacheDirty), false, defaultValue: true);
    }

    [Fact]
    public void Server_Kingdom_Collections()
    {
        AssertArmyListSyncs();
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Clan>(nameof(Kingdom._clans), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Town>(nameof(Kingdom._fiefsCache), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Hero>(nameof(Kingdom._heroesCache), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Hero>(nameof(Kingdom._aliveLordsCache), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Hero>(nameof(Kingdom._deadLordsCache), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Settlement>(nameof(Kingdom._settlementsCache), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Town>(nameof(Kingdom._townsCache), KingdomId);
        TestEnvironment.AssertCollectionReferenceField<Kingdom, Village>(nameof(Kingdom._villagesCache), KingdomId);
        AssertWarPartyComponentsCacheSyncs();
    }

    [Fact]
    public void Server_Kingdom_Properties()
    {
        var banner = new Banner();
        banner.BannerDataList.Add(new BannerData(1, 2, 3, Vec2.One, Vec2.Zero, true, true, 0f));

        Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom);

        //TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.AlternativeColor), 7);
        //TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.AlternativeColor2), 7);
        TestEnvironment.AssertProperty<Kingdom, Banner>(nameof(Kingdom.Banner), banner);
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.Color), 7);
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.Color2), 7);
        TestEnvironment.AssertReferenceProperty<Kingdom, CultureObject>(nameof(Kingdom.Culture));
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.EncyclopediaRulerTitle), new TextObject("ruler"), kingdom.EncyclopediaRulerTitle);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.EncyclopediaText), new TextObject("encycText"), kingdom.EncyclopediaText);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.EncyclopediaTitle), new TextObject("encycTitle"), kingdom.EncyclopediaTitle);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.InformalName), new TextObject("informalName"), kingdom.InformalName);
        //TestEnvironment.AssertReferenceProperty<Kingdom, Settlement>(nameof(Kingdom.InitialHomeLand));
        //TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.LabelColor), 7);
        TestEnvironment.AssertProperty<Kingdom, CampaignTime>(nameof(Kingdom.LastKingdomDecisionConclusionDate), new CampaignTime(54));
        TestEnvironment.AssertProperty<Kingdom, CampaignTime>(nameof(Kingdom.LastMercenaryOfferTime), new CampaignTime(54));
        TestEnvironment.AssertProperty<Kingdom, float>(nameof(Kingdom.MainHeroCrimeRating), 55f);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.Name), new TextObject("kingdomName"), kingdom.Name);
        TestEnvironment.AssertProperty<Kingdom, CampaignTime>(nameof(Kingdom.NotAttackableByPlayerUntilTime), new CampaignTime(54));
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.PrimaryBannerColor), 7);
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.SecondaryBannerColor), 7);
    }

    private void AssertArmyListSyncs()
    {
        var fieldInfo = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._armies));
        var firstArmyId = CreateRegisteredArmy();
        var secondArmyId = CreateRegisteredArmy();

        var setIntercept = TestEnvironment.GetIntercept(fieldInfo);
        var addIntercept = TestEnvironment.GetCollectionAddIntercept(fieldInfo);
        var removeIntercept = TestEnvironment.GetCollectionRemoveIntercept(fieldInfo);

        Assert.True(Server.ObjectManager.TryGetObject(firstArmyId, out Army firstArmy));
        var collection = new MBList<Army> { firstArmy };

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));

            setIntercept.Invoke(null, new object[] { kingdom, collection });

            Assert.Same(collection, kingdom._armies);
            Assert.Contains(firstArmy, kingdom._armies);
        });

        AssertKingdomCollectionContains(fieldInfo, collection);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));
            Assert.True(Server.ObjectManager.TryGetObject(secondArmyId, out Army secondArmy));

            addIntercept.Invoke(null, new object[] { kingdom._armies, secondArmy, kingdom });

            Assert.Contains(secondArmy, kingdom._armies);
            Assert.Equal(2, kingdom._armies.Count);
        });

        AssertKingdomCollectionContains(fieldInfo, collection);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));
            Assert.True(Server.ObjectManager.TryGetObject(firstArmyId, out Army firstArmy));

            removeIntercept.Invoke(null, new object[] { kingdom._armies, firstArmy, kingdom });

            Assert.DoesNotContain(firstArmy, kingdom._armies);
            Assert.Single(kingdom._armies);
        });

        AssertKingdomCollectionContains(fieldInfo, collection);
    }

    private void AssertWarPartyComponentsCacheSyncs()
    {
        var fieldInfo = AccessTools.Field(typeof(Kingdom), nameof(Kingdom._warPartyComponentsCache));
        var setIntercept = TestEnvironment.GetIntercept(fieldInfo);
        var addIntercept = TestEnvironment.GetCollectionAddIntercept(fieldInfo);
        var removeIntercept = TestEnvironment.GetCollectionRemoveIntercept(fieldInfo);
        var firstComponentId = TestEnvironment.CreateRegisteredObject<LordPartyComponent>();
        var secondComponentId = TestEnvironment.CreateRegisteredObject<LordPartyComponent>();

        Assert.True(Server.ObjectManager.TryGetObject(firstComponentId, out LordPartyComponent firstComponent));
        var collection = new MBList<WarPartyComponent> { firstComponent };

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));

            setIntercept.Invoke(null, new object[] { kingdom, collection });

            Assert.Same(collection, kingdom._warPartyComponentsCache);
            Assert.Contains(firstComponent, kingdom._warPartyComponentsCache);
        });

        AssertKingdomCollectionContains(fieldInfo, collection);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));
            Assert.True(Server.ObjectManager.TryGetObject(secondComponentId, out LordPartyComponent secondComponent));

            addIntercept.Invoke(null, new object[] { kingdom._warPartyComponentsCache, secondComponent, kingdom });

            Assert.Contains(secondComponent, kingdom._warPartyComponentsCache);
            Assert.Equal(2, kingdom._warPartyComponentsCache.Count);
        });

        AssertKingdomCollectionContains(fieldInfo, collection);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));
            Assert.True(Server.ObjectManager.TryGetObject(firstComponentId, out LordPartyComponent firstComponent));

            removeIntercept.Invoke(null, new object[] { kingdom._warPartyComponentsCache, firstComponent, kingdom });

            Assert.DoesNotContain(firstComponent, kingdom._warPartyComponentsCache);
            Assert.Single(kingdom._warPartyComponentsCache);
        });

        AssertKingdomCollectionContains(fieldInfo, collection);
    }

    private string CreateRegisteredArmy()
    {
        string armyId = null;

        Server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();

            mobileParty.LeaderHero.Clan = clan;

            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
        });

        Assert.NotNull(armyId);
        return armyId;
    }

    private void AssertKingdomCollectionContains<TField>(FieldInfo fieldInfo, IEnumerable<TField> serverCollection)
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom));
            var clientList = (IEnumerable<TField>)fieldInfo.GetValue(kingdom);

            Assert.Equal(serverCollection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(serverCollection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
    }
}

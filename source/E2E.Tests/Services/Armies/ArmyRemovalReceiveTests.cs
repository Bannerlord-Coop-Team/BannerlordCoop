using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyRemovalReceiveTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private static List<(bool Server, bool Allowed)>? attachmentWrites;

    public ArmyRemovalReceiveTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void QueuedRemoval_AppliesClientReceiveScopeAndKeepsServerPatchesLive(bool server, bool leader)
    {
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        string leaderId = null;
        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            Assert.True(environment.Server.ObjectManager.TryGetId(army.LeaderParty, out leaderId));
            var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
        });

        EnvironmentInstance recipient = server ? environment.Server : environment.Clients.First();
        var partyId = leader ? leaderId : memberId;
        var army = recipient.GetRegisteredObject<Army>(armyId);
        var party = recipient.GetRegisteredObject<MobileParty>(partyId);
        Assert.Contains(party, army.Parties);
        int removalMessagesBefore = environment.Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>().Count();
        var writes = new List<(bool Server, bool Allowed)>();
        var setter = AccessTools.PropertySetter(typeof(MobileParty), nameof(MobileParty.AttachedTo));
        var collectionRemoval = AccessTools.Method(typeof(MobileParty), "RemoveAttachedPartyInternal");
        var harmony = new Harmony($"army-removal-receive-{Guid.NewGuid()}");
        attachmentWrites = writes;
        harmony.Patch(setter, prefix: new HarmonyMethod(typeof(ArmyRemovalReceiveTests), nameof(RecordAttachmentWrite)));
        harmony.Patch(collectionRemoval, prefix: new HarmonyMethod(typeof(ArmyRemovalReceiveTests), nameof(RecordAttachmentWrite)));
        try
        {
            recipient.SimulateMessage(this, new NetworkRemovePartyInArmy(armyId, partyId, string.Empty), markGameThread: false);
            Assert.True(recipient.PendingGameThreadActionCount > 0);
            Assert.Contains(party, army.Parties);
            Assert.Empty(writes);

            recipient.PumpGameThread();

            Assert.Equal(0, recipient.PendingGameThreadActionCount);
            Assert.DoesNotContain(party, army.Parties);
            Assert.Null(party.Army);
            Assert.Null(party.AttachedTo);
            Assert.NotEmpty(writes);
            Assert.All(writes, write => Assert.Equal(!write.Server, write.Allowed));
            Assert.Contains(writes, write => write.Server == server);
            if (!leader)
            {
                Assert.DoesNotContain(party, army.LeaderParty.AttachedParties);
                Assert.True(writes.Count >= 2);
            }
            if (server)
            {
                Assert.True(environment.Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>().Count() > removalMessagesBefore);
                foreach (var client in environment.Clients)
                {
                    client.PumpGameThread();
                    var replica = client.GetRegisteredObject<MobileParty>(partyId);
                    Assert.Null(replica.Army);
                    Assert.Null(replica.AttachedTo);
                    if (leader)
                        Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out _));
                }
            }
        }
        finally
        {
            harmony.Unpatch(setter, HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(collectionRemoval, HarmonyPatchType.Prefix, harmony.Id);
            attachmentWrites = null;
        }
    }

    private static void RecordAttachmentWrite()
    {
        attachmentWrites?.Add((ModInformation.IsServer, AllowedThread.IsThisThreadAllowed()));
    }
}

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
    private static List<(MobileParty Party, Army Army)>? leaveEvents;

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

    [Fact]
    public void Removal_PartyAlreadyOutOfArmyButStillAttached_FinishesUnlinkOnEveryPeer()
    {
        // #3892: an earlier removal stopped after _parties.Remove, before AttachedTo and _army were cleared
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
        });
        var instances = environment.Clients.Append(environment.Server).ToArray();
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var army = instance.GetRegisteredObject<Army>(armyId);
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                using (new AllowedThread())
                    army._parties.Remove(member);
                Assert.Same(army, member.Army);
                Assert.Same(army.LeaderParty, member.AttachedTo);
            });
        }

        environment.Server.SimulateMessage(this, new NetworkRemovePartyInArmy(armyId, memberId, string.Empty), markGameThread: false);
        environment.Server.PumpGameThread();
        foreach (var client in environment.Clients)
            client.PumpGameThread();

        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var army = instance.GetRegisteredObject<Army>(armyId);
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                Assert.Null(member.Army);
                Assert.Null(member.AttachedTo);
                Assert.DoesNotContain(member, army.LeaderParty.AttachedParties);
            });
        }
    }

    [Fact]
    public void Removal_ForOldArmy_LeavesPartyInItsCurrentArmy()
    {
        var oldArmyId = environment.CreateRegisteredObject<Army>();
        var currentArmyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        environment.Server.Call(() =>
        {
            var currentArmy = environment.Server.GetRegisteredObject<Army>(currentArmyId);
            var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
            member.Army = currentArmy;
            member.AttachedTo = currentArmy.LeaderParty;
        });

        // a late removal for an army the party already left must not touch its current one
        environment.Server.SimulateMessage(this, new NetworkRemovePartyInArmy(oldArmyId, memberId, string.Empty), markGameThread: false);
        environment.Server.PumpGameThread();
        foreach (var client in environment.Clients)
            client.PumpGameThread();

        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                var currentArmy = instance.GetRegisteredObject<Army>(currentArmyId);
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                Assert.Same(currentArmy, member.Army);
                Assert.Same(currentArmy.LeaderParty, member.AttachedTo);
                Assert.Contains(member, currentArmy.Parties);
            });
        }
    }

    [Fact]
    public void Disband_WithHalfRemovedParty_ReleasesItOnEveryPeer()
    {
        // #3892 review: once the army disbands it is deregistered, so a half removed party has to be
        // released by the disband itself
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        var otherId = environment.CreateRegisteredObject<MobileParty>();
        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            foreach (var id in new[] { memberId, otherId })
            {
                var party = environment.Server.GetRegisteredObject<MobileParty>(id);
                party.Army = army;
                party.AttachedTo = army.LeaderParty;
            }
        });
        var instances = environment.Clients.Append(environment.Server).ToArray();
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var army = instance.GetRegisteredObject<Army>(armyId);
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                using (new AllowedThread())
                    army._parties.Remove(member);
            });
        }

        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            army.DisperseInternal(Army.ArmyDispersionReason.NotEnoughParty);
        });
        foreach (var client in environment.Clients)
            client.PumpGameThread();

        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                foreach (var id in new[] { memberId, otherId })
                {
                    var party = instance.GetRegisteredObject<MobileParty>(id);
                    Assert.Null(party.Army);
                    Assert.Null(party.AttachedTo);
                }
            });
        }
        Assert.False(environment.Server.ObjectManager.TryGetObject<Army>(armyId, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Removal_OfLeader_FiresOneLeaveEvent(bool withMember)
    {
        // removing the leader disbands the army inside the removal, and the disband must not finish
        // the leave of the party that is already being removed
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        string leaderId = null;
        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            Assert.True(environment.Server.ObjectManager.TryGetId(army.LeaderParty, out leaderId));
            // the disband looks for half removed parties in the campaign's party list
            if (!Campaign.Current.CampaignObjectManager._mobileParties.Contains(army.LeaderParty))
                Campaign.Current.CampaignObjectManager.AddMobileParty(army.LeaderParty);
            if (withMember)
            {
                var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
                member.Army = army;
                member.AttachedTo = army.LeaderParty;
            }
        });
        var leader = environment.Server.GetRegisteredObject<MobileParty>(leaderId);
        var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
        int removalMessagesBefore = environment.Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>()
            .Count(message => message.MobilePartyId == leaderId);

        var events = CountLeaveEvents(() =>
        {
            environment.Server.SimulateMessage(this, new NetworkRemovePartyInArmy(armyId, leaderId, string.Empty), markGameThread: false);
            environment.Server.PumpGameThread();
        });

        Assert.Single(events, leave => leave.Party == leader);
        Assert.Equal(withMember ? 1 : 0, events.Count(leave => leave.Party == member));
        // the removal ran to its end and was relayed to the clients
        Assert.True(environment.Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>()
            .Count(message => message.MobilePartyId == leaderId) > removalMessagesBefore);
        Assert.Null(leader.Army);
        Assert.False(environment.Server.ObjectManager.TryGetObject<Army>(armyId, out _));
    }

    [Fact]
    public void Removal_OfLastMemberOfPlayerArmy_FiresOneLeaveEvent()
    {
        // a player army left with only its leader disbands inside the removal of its last member
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        string leaderId = null;
        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            Assert.True(environment.Server.ObjectManager.TryGetId(army.LeaderParty, out leaderId));
            var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
            // the disband looks for half removed parties in the campaign's party list
            if (!Campaign.Current.CampaignObjectManager._mobileParties.Contains(member))
                Campaign.Current.CampaignObjectManager.AddMobileParty(member);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
        });
        var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);

        var events = CountLeaveEvents(() =>
        {
            environment.Server.SimulateMessage(this, new NetworkRemovePartyInArmy(armyId, memberId, leaderId), markGameThread: false);
            environment.Server.PumpGameThread();
        });

        Assert.Single(events, leave => leave.Party == member);
        Assert.Null(member.Army);
        Assert.Null(member.AttachedTo);
        Assert.False(environment.Server.ObjectManager.TryGetObject<Army>(armyId, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldArmyLeave_WithPartyAlreadyInNewArmy_KeepsNewMembership(bool disband)
    {
        // a party that moved to a new army while its attachment to the old leader was left behind
        var oldArmyId = environment.CreateRegisteredObject<Army>();
        var newArmyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        var otherId = environment.CreateRegisteredObject<MobileParty>();
        environment.Server.Call(() =>
        {
            var oldArmy = environment.Server.GetRegisteredObject<Army>(oldArmyId);
            var newArmy = environment.Server.GetRegisteredObject<Army>(newArmyId);
            var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
            var other = environment.Server.GetRegisteredObject<MobileParty>(otherId);
            other.Army = oldArmy;
            other.AttachedTo = oldArmy.LeaderParty;
            member.Army = newArmy;
            member.AttachedTo = oldArmy.LeaderParty;
        });
        var instances = environment.Clients.Append(environment.Server).ToArray();
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var oldArmy = instance.GetRegisteredObject<Army>(oldArmyId);
                var newArmy = instance.GetRegisteredObject<Army>(newArmyId);
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                Assert.Same(newArmy, member.Army);
                Assert.Contains(member, newArmy.Parties);
                Assert.Same(oldArmy.LeaderParty, member.AttachedTo);
            });
        }

        if (disband)
        {
            environment.Server.Call(() =>
                environment.Server.GetRegisteredObject<Army>(oldArmyId).DisperseInternal(Army.ArmyDispersionReason.NotEnoughParty));
        }
        else
        {
            environment.Server.SimulateMessage(this, new NetworkRemovePartyInArmy(oldArmyId, memberId, string.Empty), markGameThread: false);
            environment.Server.PumpGameThread();
        }
        foreach (var client in environment.Clients)
            client.PumpGameThread();

        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var newArmy = instance.GetRegisteredObject<Army>(newArmyId);
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                Assert.Same(newArmy, member.Army);
                Assert.Contains(member, newArmy.Parties);
                Assert.Null(member.AttachedTo);
            });
        }
    }

    private static void RecordAttachmentWrite()
    {
        attachmentWrites?.Add((ModInformation.IsServer, AllowedThread.IsThisThreadAllowed()));
    }

    private static List<(MobileParty Party, Army Army)> CountLeaveEvents(Action action)
    {
        var events = new List<(MobileParty Party, Army Army)>();
        var leftArmy = AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnPartyLeftArmy));
        var harmony = new Harmony($"army-removal-leave-count-{Guid.NewGuid()}");
        leaveEvents = events;
        harmony.Patch(leftArmy, prefix: new HarmonyMethod(typeof(ArmyRemovalReceiveTests), nameof(RecordLeaveEvent)));
        try
        {
            action();
        }
        finally
        {
            harmony.Unpatch(leftArmy, HarmonyPatchType.Prefix, harmony.Id);
            leaveEvents = null;
        }
        return events;
    }

    private static void RecordLeaveEvent(MobileParty party, Army army)
    {
        leaveEvents?.Add((party, army));
    }
}

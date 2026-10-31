using Common;
using Common.Commands;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Commands;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>coop.debug.party.list_ships addresses any party, so an NPC party's ships can be checked after a naval battle.</summary>
public class ListShipsCoopCommandTests : SyncTestBase
{
    public ListShipsCoopCommandTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ListShips_AcceptsAMobilePartyOrPartyBaseId()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var client = Clients.First();
        string partyBaseId = null!;
        string stringId = null!;
        client.Call(() =>
        {
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            stringId = party.StringId;
            Assert.True(client.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        var byMobileParty = ListShips(client, partyId);
        var byPartyBase = ListShips(client, partyBaseId);

        Assert.True(byMobileParty.Succeeded, byMobileParty.Output);
        Assert.Equal($"{stringId} ships=0", byMobileParty.Output);
        Assert.True(byPartyBase.Succeeded, byPartyBase.Output);
        Assert.Equal($"{stringId} ships=0", byPartyBase.Output);
    }

    [Fact]
    public void ListShips_FailsForAnUnknownId()
    {
        var result = ListShips(Clients.First(), "MobileParty_missing");

        Assert.False(result.Succeeded);
        Assert.Equal("No player, MobileParty, PartyBase or party/hero name matches MobileParty_missing.", result.Output);
    }

    private static CoopCommandResult ListShips(EnvironmentInstance instance, string id)
    {
        CoopCommandResult result = null!;
        bool originalNavalDlcActive = ModInformation.IsNavalDlcActive;
        try
        {
            ModInformation.IsNavalDlcActive = true;
            instance.Call(() => result = new PartyShipCommands.ListShipsCoopCommand()
                .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { id })));
        }
        finally
        {
            ModInformation.IsNavalDlcActive = originalNavalDlcActive;
        }

        return result;
    }
}

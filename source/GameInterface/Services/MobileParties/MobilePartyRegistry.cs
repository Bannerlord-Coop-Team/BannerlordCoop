using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Registry for <see cref="MobileParty"/> objects
/// </summary>
internal class MobilePartyRegistry : RegistryBase<MobileParty>
{
    private const string PartyStringIdPrefix = "CoopParty";
    private int InstanceCounter = 0;
    private readonly IMessageBroker messageBroker;

    public MobilePartyRegistry(IRegistryCollection collection, IMessageBroker messageBroker) : base(collection)
    {
        this.messageBroker = messageBroker;
    }

    public override void RegisterAll()
    {
        foreach (var party in MobileParty.All)
        {
            base.RegisterNewObject(party.StringId, out _);
        }
    }

    protected override string GetNewId(MobileParty party)
    {
        party.StringId = $"{PartyStringIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        return party.StringId;
    }
}

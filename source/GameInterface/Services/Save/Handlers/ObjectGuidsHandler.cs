using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ItemRosters.Data;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class ObjectGuidsHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public ObjectGuidsHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<PackageObjectGuids>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PackageObjectGuids>(Handle);
        }

        private void Handle(MessagePayload<PackageObjectGuids> obj)
        {
            var com = Campaign.Current?.CampaignObjectManager;

            ItemRosterOwner[] owners;

            if (com == null)
            {
                owners = System.Array.Empty<ItemRosterOwner>();
            }
            else
            {
                var list = new System.Collections.Generic.List<ItemRosterOwner>();

                foreach (var party in com.MobileParties)
                {
                    if (party == null) continue;
                    if (party.ItemRoster == null) continue;
                    var id = nameof(TaleWorlds.CampaignSystem.Roster.ItemRoster) + "_" + party.StringId;
                    list.Add(new ItemRosterOwner(id, party.StringId));
                }

                foreach (var settlement in com.Settlements)
                {
                    if (settlement == null) continue;
                    if (settlement.ItemRoster == null) continue;
                    var id = nameof(TaleWorlds.CampaignSystem.Roster.ItemRoster) + "_" + settlement.StringId;
                    list.Add(new ItemRosterOwner(id, settlement.StringId));
                }

                owners = list.ToArray();
            }

            var guids = new GameObjectGuids(System.Array.Empty<string>())
            {
                ItemRosterOwners = owners
            };

            var packaged = new ObjectGuidsPackaged(
                Campaign.Current?.UniqueGameId,
                guids);

            messageBroker.Respond(obj.Who, packaged);
        }
    }
}

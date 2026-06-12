using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.Clans.Extensions;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace GameInterface.Services.Buildings.Handlers;

internal class NotablesCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NotablesCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;

    public NotablesCampaignBehaviorHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<UpdateNotableRelations>(Handle_UpdateNotableRelations);
        messageBroker.Subscribe<UpdateNotableSupport>(Handle_UpdateNotableSupport);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateNotableRelations>(Handle_UpdateNotableRelations);
        messageBroker.Unsubscribe<UpdateNotableSupport>(Handle_UpdateNotableSupport);
    }

    private void Handle_UpdateNotableRelations(MessagePayload<UpdateNotableRelations> obj)
    {
        foreach (Clan clan in Clan.All)
        {
            if (!clan.IsPlayerClan() && clan.Leader != null && !clan.IsEliminated)
            {
                int relation = obj.What.Notable.GetRelation(clan.Leader);
                if (relation > 0)
                {
                    float num = (float)relation / 1000f;
                    if (MBRandom.RandomFloat < num)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(obj.What.Notable, clan.Leader, -20, true);
                    }
                }
                else if (relation < 0)
                {
                    float num2 = (float)(-(float)relation) / 1000f;
                    if (MBRandom.RandomFloat < num2)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(obj.What.Notable, clan.Leader, 20, true);
                    }
                }
            }
        }
    }

    private void Handle_UpdateNotableSupport(MessagePayload<UpdateNotableSupport> obj)
    {
        if (obj.What.Notable.SupporterOf == null)
        {
            using (IEnumerator<Clan> enumerator = Clan.NonBanditFactions.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    Clan clan = enumerator.Current;
                    if (clan.Leader != null && !clan.IsPlayerClan()) // Instead of Clan.PlayerClan
                    {
                        int relation = obj.What.Notable.GetRelation(clan.Leader);
                        if (relation > 50)
                        {
                            float num = (float)(relation - 50) / 2000f;
                            if (MBRandom.RandomFloat < num)
                            {
                                obj.What.Notable.SupporterOf = clan;
                            }
                        }
                    }
                }
                return;
            }
        }
        int relation2 = obj.What.Notable.GetRelation(obj.What.Notable.SupporterOf.Leader);
        if (relation2 < 0 || MBRandom.RandomFloat < (50f - (float)relation2) / 500f)
        {
            bool flag = obj.What.Notable.SupporterOf.IsPlayerClan(); // Instead of Clan.PlayerClan
            obj.What.Notable.SupporterOf = null;
            if (flag)
            {
                // TODO Notify player of notable no longer supporting clan
                //TextObject textObject = new TextObject("{=aaOIjHeP}{NOTABLE.NAME} no longer supports your clan as your relationship deteriorated too much.", null);
                //textObject.SetCharacterProperties("NOTABLE", obj.What.Notable.CharacterObject, false);
                //InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), new Color(0f, 1f, 0f, 1f)));
            }
        }
    }
}
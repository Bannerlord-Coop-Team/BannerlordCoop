using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CharacterDevelopers.Interfaces;
using GameInterface.Services.CharacterDevelopers.Messages;
using GameInterface.Services.CharacterDevelopers.Patches;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.Diamond;
using TaleWorlds.TwoDimension.Standalone;

namespace GameInterface.Services.CharacterDevelopers.Handlers
{
    internal class CharacterDeveloperHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperHandler>();

        private readonly ICharacterDeveloperInterface characterDeveloperInterface;
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CharacterDeveloperHandler(
            ICharacterDeveloperInterface characterDeveloperInterface,
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network)
        {
            this.characterDeveloperInterface = characterDeveloperInterface;
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ApplyChangesPressed>(Handle);
            messageBroker.Subscribe<NetworkApplyChangesServer>(Handle);
            messageBroker.Subscribe<NetworkApplyChangesClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ApplyChangesPressed>(Handle);
            messageBroker.Unsubscribe<NetworkApplyChangesServer>(Handle);
            messageBroker.Unsubscribe<NetworkApplyChangesClients>(Handle);
        }

        private void Handle(MessagePayload<ApplyChangesPressed> obj)
        {
            SendChanges(obj.What);
        }

        private void Handle(MessagePayload<NetworkApplyChangesServer> obj)
        {
            // Apply on server and send to all clients
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    NetworkApplyChangesClients changes = new(obj.What);
                    network.SendAll(changes);
                    ApplyChanges(changes);
                }
            });
        }

        private void Handle(MessagePayload<NetworkApplyChangesClients> obj)
        {
            ApplyChanges(obj.What);
        }

        private void SendChanges(ApplyChangesPressed obj)
        {
            // Get heroDeveloper id for transmission over the network
            var heroDeveloper = obj.HeroDeveloper;
            if (!objectManager.TryGetId(heroDeveloper, out var heroDeveloperId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", heroDeveloper?.GetType());
                return;
            }

            // Store perk ids in a list for transmission over the network
            var perkIds = new List<string>();
            foreach (var perk in obj.Perks._selectedPerks)
            {
                string currentPerkId;
                if (!objectManager.TryGetId(perk, out currentPerkId))
                {
                    Logger.Error("Unable to get network ID for instance of type {type}", perk?.GetType());
                    return;
                }

                perkIds.Add(currentPerkId);
            }

            // Store attribute ids and levels in lists for transmission over the network
            var attributeIds = new List<string>();
            var attributeIncreases = new List<int>();
            foreach (var attributeVM in obj.Attributes)
            {
                string currentAttributeId;
                if (!objectManager.TryGetId(attributeVM.AttributeType, out currentAttributeId))
                {
                    Logger.Error("Unable to get network ID for instance of type {type}", attributeVM.AttributeType?.GetType());
                    return;
                }

                attributeIds.Add(currentAttributeId);
                attributeIncreases.Add(attributeVM.AttributeValue - attributeVM._initialAttValue);
            }

            // Store skill ids and levels in lists for transmission over the network
            var skillIds = new List<string>();
            var skillFocusLevels = new List<int>();
            var skillOrgFocusAmounts = new List<int>();
            foreach (var skillVM in obj.Skills)
            {
                string currentSkillId;
                if (!objectManager.TryGetId(skillVM.Skill, out currentSkillId))
                {
                    Logger.Error("Unable to get network ID for instance of type {type}", skillVM.Skill?.GetType());
                    return;
                }

                skillIds.Add(currentSkillId);
                skillFocusLevels.Add(skillVM.CurrentFocusLevel);
                skillOrgFocusAmounts.Add(skillVM._orgFocusAmount);
            }

            // Send to server from client
            NetworkApplyChangesServer message = new(heroDeveloperId,
                perkIds,
                attributeIds,
                attributeIncreases,
                skillIds,
                skillFocusLevels,
                skillOrgFocusAmounts
            );
            network.SendAll(message);
        }

        private void ApplyChanges(NetworkApplyChangesClients obj)
        {
            // Get HeroDeveloper
            if (!objectManager.TryGetObject(obj.HeroDeveloperId, out HeroDeveloper heroDeveloper))
            {
                Logger.Error("Unable to get object for id {id}", heroDeveloper);
                return;
            }

            // Add perks to HeroDeveloper
            if (obj.PerkIds != null)
            {
                foreach (string perkId in obj.PerkIds)
                {
                    PerkObject currentPerk;
                    if (!objectManager.TryGetObject(perkId, out currentPerk))
                    {
                        Logger.Error("Unable to get object for id {id}", perkId);
                        return;
                    }
                    heroDeveloper.AddPerk(currentPerk);
                }
            }

            // Add attributes to HeroDeveloper
            if (obj.AttributeIds != null)
            {
                for (int i = 0; i < obj.AttributeIds.Count; i++)
                {
                    string attributeId = obj.AttributeIds[i];

                    CharacterAttribute currentAttribute;
                    if (!objectManager.TryGetObject(attributeId, out currentAttribute))
                    {
                        Logger.Error("Unable to get object for id {id}", attributeId);
                        return;
                    }

                    for (int j = 0; j < obj.AttributeIncreases[i]; j++)
                    {
                        heroDeveloper.AddAttribute(currentAttribute, 1);
                    }
                }
            }

            // Add focuses to HeroDeveloper
            if (obj.SkillIds != null)
            {
                for (int i = 0; i < obj.SkillIds.Count; i++)
                {
                    string skillId = obj.SkillIds[i];

                    SkillObject currentSkill;
                    if (!objectManager.TryGetObject(skillId, out currentSkill))
                    {
                        Logger.Error("Unable to get object for id {id}", skillId);
                        return;
                    }

                    for (int j = 0; j < obj.SkillFocusLevels[i] - obj.SkillOrgFocusAmounts[i]; j++)
                    {
                        heroDeveloper.AddFocus(currentSkill, 1);
                    }

                    // Needed to calculate remaining skill points correctly
                    obj.SkillOrgFocusAmounts[i] = obj.SkillFocusLevels[i];
                }
            }
        }
    }
}

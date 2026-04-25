using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
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
using TaleWorlds.CampaignSystem;
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
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CharacterDeveloperHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network)
        {
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
            // Send to all clients and apply on server
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
            var hero = obj.HeroDeveloper.Hero;
            if (!objectManager.TryGetId(hero, out var heroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", hero?.GetType());
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
            NetworkApplyChangesServer message = new(heroId,
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
            string heroId = obj.HeroId;
            if (!objectManager.TryGetObject(obj.HeroId, out Hero hero))
            {
                Logger.Error("Unable to get object for id {id}", heroId);
                return;
            }

            addPerks(obj.PerkIds, hero.HeroDeveloper);
            addAttributes(obj.AttributeIds, obj.AttributeIncreases, hero.HeroDeveloper);
            addFocuses(obj.SkillIds, obj.SkillFocusLevels, obj.SkillOrgFocusAmounts, hero.HeroDeveloper);
        }

        private void addPerks(List<string> perkIds, HeroDeveloper heroDeveloper)
        {
            if (perkIds != null)
            {
                foreach (string perkId in perkIds)
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
        }

        private void addAttributes(List<string> attributeIds, List<int> attributeIncreases, HeroDeveloper heroDeveloper)
        {
            if (attributeIds != null)
            {
                for (int i = 0; i < attributeIds.Count; i++)
                {
                    string attributeId = attributeIds[i];

                    CharacterAttribute currentAttribute;
                    if (!objectManager.TryGetObject(attributeId, out currentAttribute))
                    {
                        Logger.Error("Unable to get object for id {id}", attributeId);
                        return;
                    }

                    for (int j = 0; j < attributeIncreases[i]; j++)
                    {
                        heroDeveloper.AddAttribute(currentAttribute, 1);
                    }
                }
            }
        }

        private void addFocuses(List<string> skillIds, List<int> skillFocusLevels, List<int> skillOrgFocusAmounts, HeroDeveloper heroDeveloper)
        {
            if (skillIds != null)
            {
                for (int i = 0; i < skillIds.Count; i++)
                {
                    string skillId = skillIds[i];

                    SkillObject currentSkill;
                    if (!objectManager.TryGetObject(skillId, out currentSkill))
                    {
                        Logger.Error("Unable to get object for id {id}", skillId);
                        return;
                    }

                    for (int j = 0; j < skillFocusLevels[i] - skillOrgFocusAmounts[i]; j++)
                    {
                        heroDeveloper.AddFocus(currentSkill, 1);
                    }

                    // Needed to calculate remaining skill points correctly
                    skillOrgFocusAmounts[i] = skillFocusLevels[i];
                }
            }
        }
    }
}

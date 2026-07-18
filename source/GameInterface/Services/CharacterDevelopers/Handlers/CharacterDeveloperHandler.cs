using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CharacterDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

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

            messageBroker.Subscribe<ApplyChanges>(Handle_ApplyChanges);
            messageBroker.Subscribe<NetworkApplyChanges>(Handle_NetworkApplyChanges);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ApplyChanges>(Handle_ApplyChanges);
            messageBroker.Unsubscribe<NetworkApplyChanges>(Handle_NetworkApplyChanges);
        }

        private void Handle_ApplyChanges(MessagePayload<ApplyChanges> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                // Get hero id for transmission over the network
                if (!objectManager.TryGetIdWithLogging(data.HeroDeveloper, out var heroDeveloperId)) return;

                // Store perk ids in a list for transmission over the network
                var perkIds = new List<string>();
                foreach (var perk in data.Perks._selectedPerks)
                {
                    if (!objectManager.TryGetIdWithLogging(perk, out var currentPerkId)) continue;

                    perkIds.Add(currentPerkId);
                }

                // Store attribute ids and levels in lists for transmission over the network
                var attributeIds = new List<string>();
                var attributeIncreases = new List<int>();
                foreach (var attributeVM in data.Attributes)
                {
                    if (!objectManager.TryGetIdWithLogging(attributeVM.AttributeType, out var currentAttributeId)) continue;

                    attributeIds.Add(currentAttributeId);
                    attributeIncreases.Add(attributeVM.AttributeValue - attributeVM._initialAttValue);
                }

                // Store skill ids and levels in lists for transmission over the network
                var skillIds = new List<string>();
                var skillFocusLevels = new List<int>();
                foreach (var skillVM in data.Skills)
                {
                    if (!objectManager.TryGetId(skillVM.Skill, out var currentSkillId)) continue;

                    skillIds.Add(currentSkillId);
                    skillFocusLevels.Add(skillVM.CurrentFocusLevel);
                }

                // Send to server from client
                NetworkApplyChanges message = new(
                    heroDeveloperId,
                    perkIds,
                    attributeIds,
                    attributeIncreases,
                    skillIds,
                    skillFocusLevels
                );
                network.SendAll(message);
            });
        }

        private void Handle_NetworkApplyChanges(MessagePayload<NetworkApplyChanges> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging<HeroDeveloper>(data.HeroDeveloperId, out var heroDeveloper)) return;

                AddPerks(data.PerkIds, heroDeveloper);
                AddAttributes(data.AttributeIds, data.AttributeIncreases, heroDeveloper);
                AddFocuses(data.SkillIds, data.SkillFocusLevels, heroDeveloper);
            });
        }

        private void AddPerks(List<string> perkIds, HeroDeveloper heroDeveloper)
        {
            if (perkIds == null) return;

            foreach (string perkId in perkIds)
            {
                if (!objectManager.TryGetObjectWithLogging<PerkObject>(perkId, out var currentPerk)) continue;

                heroDeveloper.AddPerk(currentPerk);
            }
        }

        private void AddAttributes(List<string> attributeIds, List<int> attributeIncreases, HeroDeveloper heroDeveloper)
        {
            if (attributeIds == null) return;

            for (int i = 0; i < attributeIds.Count; i++)
            {
                string attributeId = attributeIds[i];
                if (!objectManager.TryGetObjectWithLogging<CharacterAttribute>(attributeId, out var currentAttribute)) continue;

                for (int j = 0; j < attributeIncreases[i]; j++)
                {
                    heroDeveloper.AddAttribute(currentAttribute, 1);
                }
            }
        }

        private void AddFocuses(List<string> skillIds, List<int> skillFocusLevels, HeroDeveloper heroDeveloper)
        {
            if (skillIds == null || skillFocusLevels == null) return;

            if (skillIds.Count != skillFocusLevels.Count)
            {
                Logger.Warning("Rejected focus changes with mismatched skill and focus counts");
                return;
            }

            var focusChanges = new List<(SkillObject Skill, int Increase)>();
            int requiredFocusPoints = 0;
            int maxFocusPerSkill = Campaign.Current.Models.CharacterDevelopmentModel.MaxFocusPerSkill;

            for (int i = 0; i < skillIds.Count; i++)
            {
                string skillId = skillIds[i];

                if (!objectManager.TryGetObjectWithLogging<SkillObject>(skillId, out var currentSkill)) return;

                int targetFocusLevel = skillFocusLevels[i];
                int focusIncrease = targetFocusLevel - heroDeveloper.GetFocus(currentSkill);
                if (focusIncrease <= 0) continue;

                if (targetFocusLevel > maxFocusPerSkill)
                {
                    Logger.Warning("Rejected focus target {TargetFocusLevel} for {SkillId}; maximum is {MaxFocusPerSkill}",
                        targetFocusLevel, skillId, maxFocusPerSkill);
                    return;
                }

                focusChanges.Add((currentSkill, focusIncrease));
                requiredFocusPoints += focusIncrease * heroDeveloper.GetRequiredFocusPointsToAddFocus(currentSkill);
            }

            if (requiredFocusPoints > heroDeveloper.UnspentFocusPoints)
            {
                Logger.Warning("Rejected focus changes requiring {RequiredFocusPoints} points; only {UnspentFocusPoints} remain",
                    requiredFocusPoints, heroDeveloper.UnspentFocusPoints);
                return;
            }

            foreach (var focusChange in focusChanges)
            {
                for (int i = 0; i < focusChange.Increase; i++)
                {
                    heroDeveloper.AddFocus(focusChange.Skill, 1);
                }
            }
        }
    }
}

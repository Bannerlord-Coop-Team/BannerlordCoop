using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using MathF = TaleWorlds.Library.MathF;

namespace GameInterface.Services.HeroDevelopers.Handlers
{
    internal class HeroDeveloperHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloperHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public HeroDeveloperHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<SkillXpSet>(Handle);
            messageBroker.Subscribe<SkillLevelChange>(Handle);
            messageBroker.Subscribe<RawXpGain>(Handle);
            messageBroker.Subscribe<HeroDeveloperBatch>(Handle);
            messageBroker.Subscribe<NetworkHeroDeveloperBatchServer>(Handle);
            messageBroker.Subscribe<NetworkHeroDeveloperBatchClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SkillXpSet>(Handle);
            messageBroker.Unsubscribe<SkillLevelChange>(Handle);
            messageBroker.Unsubscribe<RawXpGain>(Handle);
            messageBroker.Unsubscribe<HeroDeveloperBatch>(Handle);
            messageBroker.Unsubscribe<NetworkHeroDeveloperBatchServer>(Handle);
            messageBroker.Unsubscribe<NetworkHeroDeveloperBatchClients>(Handle);
        }

        private void Handle(MessagePayload<SkillXpSet> obj)
        {
            var data = obj.What;
            SendOperation(
                data.HeroDeveloper,
                HeroDeveloperOperation.SkillXpSet(data.SkillObject, data.Value));
        }

        private void Handle(MessagePayload<SkillLevelChange> obj)
        {
            var data = obj.What;
            SendOperation(
                data.HeroDeveloper,
                HeroDeveloperOperation.SkillLevelChange(
                    data.SkillObject,
                    data.ChangeAmount,
                    data.ShouldNotify));
        }

        private void Handle(MessagePayload<RawXpGain> obj)
        {
            var data = obj.What;
            SendOperation(
                data.HeroDeveloper,
                HeroDeveloperOperation.RawXpGain(data.RawXp, data.ShouldNotify));
        }

        private void Handle(MessagePayload<HeroDeveloperBatch> obj)
        {
            SendBatch(obj.What);
        }

        private void Handle(MessagePayload<NetworkHeroDeveloperBatchServer> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!TryValidateNetworkBatch(data.HeroId, data.Operations, out string reason))
                {
                    Logger.Warning("Rejected {message}: {reason}", nameof(NetworkHeroDeveloperBatchServer), reason);
                    return;
                }

                using (new AllowedThread())
                {
                    NetworkHeroDeveloperBatchClients changes = new(data);
                    network.SendAll(changes);
                    ApplyBatch(changes.HeroId, changes.Operations);
                }
            }, context: nameof(HeroDeveloperHandler));
        }

        private void Handle(MessagePayload<NetworkHeroDeveloperBatchClients> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!TryValidateNetworkBatch(data.HeroId, data.Operations, out string reason))
                {
                    Logger.Warning("Rejected {message}: {reason}", nameof(NetworkHeroDeveloperBatchClients), reason);
                    return;
                }

                ApplyBatch(data.HeroId, data.Operations);
            }, context: nameof(HeroDeveloperHandler));
        }

        private void SendOperation(
            HeroDeveloper heroDeveloper,
            HeroDeveloperOperation operation)
        {
            SendBatch(new HeroDeveloperBatch(heroDeveloper, new[] { operation }));
        }

        private void SendBatch(HeroDeveloperBatch batch)
        {
            if (!objectManager.TryGetId(batch.HeroDeveloper.Hero, out var heroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", batch.HeroDeveloper.Hero?.GetType());
                return;
            }

            heroId = Compact(heroId, typeof(Hero));
            var networkOperations = new List<NetworkHeroDeveloperOperation>(batch.Operations.Count);

            foreach (HeroDeveloperOperation operation in batch.Operations)
            {
                if (TryCreateNetworkOperation(operation, out NetworkHeroDeveloperOperation networkOperation))
                {
                    networkOperations.Add(networkOperation);
                }
            }

            if (networkOperations.Count == 0) return;

            for (int offset = 0; offset < networkOperations.Count; offset += NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage)
            {
                int count = Math.Min(
                    NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage,
                    networkOperations.Count - offset);
                network.SendAll(new NetworkHeroDeveloperBatchServer(heroId, networkOperations.GetRange(offset, count)));
            }
        }

        private bool TryCreateNetworkOperation(
            HeroDeveloperOperation operation,
            out NetworkHeroDeveloperOperation networkOperation)
        {
            networkOperation = null;

            if (operation.Type == HeroDeveloperOperationType.RawXpGain)
            {
                networkOperation = new NetworkHeroDeveloperOperation(
                    NetworkHeroDeveloperOperationType.RawXpGain,
                    null,
                    operation.Value,
                    0,
                    operation.ShouldNotify);
                return true;
            }

            if (!objectManager.TryGetId(operation.SkillObject, out var skillObjectId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", operation.SkillObject?.GetType());
                return false;
            }

            skillObjectId = Compact(skillObjectId, typeof(SkillObject));
            networkOperation = operation.Type switch
            {
                HeroDeveloperOperationType.SkillXpSet => new NetworkHeroDeveloperOperation(
                    NetworkHeroDeveloperOperationType.SkillXpSet,
                    skillObjectId,
                    operation.Value,
                    0,
                    false),
                HeroDeveloperOperationType.SkillLevelChange => new NetworkHeroDeveloperOperation(
                    NetworkHeroDeveloperOperationType.SkillLevelChange,
                    skillObjectId,
                    0f,
                    operation.ChangeAmount,
                    operation.ShouldNotify),
                _ => null,
            };

            return networkOperation != null;
        }

        private static bool TryValidateNetworkBatch(
            string heroId,
            List<NetworkHeroDeveloperOperation> operations,
            out string reason)
        {
            if (string.IsNullOrEmpty(heroId))
            {
                reason = "hero id is missing";
                return false;
            }

            if (operations == null || operations.Count == 0)
            {
                reason = "operation list is empty";
                return false;
            }

            if (operations.Count > NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage)
            {
                reason = $"operation count {operations.Count} exceeds " +
                    NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage;
                return false;
            }

            for (int index = 0; index < operations.Count; index++)
            {
                if (!TryValidateNetworkOperation(operations[index], out string operationReason))
                {
                    reason = $"operation {index} is invalid: {operationReason}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool TryValidateNetworkOperation(
            NetworkHeroDeveloperOperation operation,
            out string reason)
        {
            if (operation == null)
            {
                reason = "operation is null";
                return false;
            }

            switch (operation.Type)
            {
                case NetworkHeroDeveloperOperationType.RawXpGain:
                    if (operation.SkillObjectId != null ||
                        !IsFinite(operation.Value) ||
                        operation.Value <= 0f ||
                        operation.ChangeAmount != 0)
                    {
                        reason = "raw-XP operation has unexpected fields";
                        return false;
                    }
                    break;
                case NetworkHeroDeveloperOperationType.SkillXpSet:
                    if (string.IsNullOrEmpty(operation.SkillObjectId) ||
                        !IsFinite(operation.Value) ||
                        operation.Value < 0f ||
                        operation.ChangeAmount != 0 ||
                        operation.ShouldNotify)
                    {
                        reason = "skill-XP operation has unexpected fields";
                        return false;
                    }
                    break;
                case NetworkHeroDeveloperOperationType.SkillLevelChange:
                    if (string.IsNullOrEmpty(operation.SkillObjectId) ||
                        operation.Value != 0f ||
                        operation.ChangeAmount <= 0)
                    {
                        reason = "skill-level operation has unexpected fields";
                        return false;
                    }
                    break;
                default:
                    reason = $"unknown operation type {(int)operation.Type}";
                    return false;
            }

            reason = null;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private void ApplyBatch(string heroId, List<NetworkHeroDeveloperOperation> operations)
        {
            if (operations == null) return;

            for (int index = 0; index < operations.Count; index++)
            {
                NetworkHeroDeveloperOperation operation = operations[index];

                try
                {
                    switch (operation.Type)
                    {
                        case NetworkHeroDeveloperOperationType.RawXpGain:
                            ChangeRawXp(heroId, operation.Value, operation.ShouldNotify);
                            break;
                        case NetworkHeroDeveloperOperationType.SkillXpSet:
                            SetSkillXp(heroId, operation.SkillObjectId, operation.Value);
                            break;
                        case NetworkHeroDeveloperOperationType.SkillLevelChange:
                            ChangeSkillLevel(
                                heroId,
                                operation.SkillObjectId,
                                operation.ChangeAmount,
                                operation.ShouldNotify);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(
                        e,
                        "Failed to apply hero developer batch operation {index} ({type}) for hero {heroId}",
                        index,
                        operation?.Type,
                        heroId);
                }
            }
        }

        private void SetSkillXp(string heroId, string skillObjectId, float value)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObject(heroId, out Hero hero))
            {
                Logger.Error("Unable to get object for hero id {id}", heroId);
                return;
            }
            if (!objectManager.TryGetObject(skillObjectId, out SkillObject skillObject))
            {
                Logger.Error("Unable to get object for skill object id {id}", skillObjectId);
                return;
            }

            // Replace original TaleWorlds implementation
            if (!value.ApproximatelyEqualsTo(0f, 1E-05f))
            {
                hero.HeroDeveloper._skillXps[skillObject] = value;
                return;
            }
            if (hero.HeroDeveloper._skillXps.ContainsKey(skillObject))
            {
                hero.HeroDeveloper._skillXps.Remove(skillObject);
            }
        }

        private void ChangeSkillLevel(
            string heroId,
            string skillObjectId,
            int changeAmount,
            bool notifyRequested)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObject(heroId, out Hero hero))
            {
                Logger.Error("Unable to get object for hero id {id}", heroId);
                return;
            }
            if (!objectManager.TryGetObject(skillObjectId, out SkillObject skillObject))
            {
                Logger.Error("Unable to get object for skill object id {id}", skillObjectId);
                return;
            }

            // Replace original TaleWorlds implementation
            if (changeAmount != 0)
            {
                // Hero.SetSkillValue is patched and only runs the original on a client when the
                // thread is allowed, so the vanilla skill write must be marked as allowed here.
                using (new AllowedThread())
                {
                    int value = hero.GetSkillValue(skillObject) + changeAmount;
                    hero.SetSkillValue(skillObject, value);

                    // Only notify if running on client where the updated hero is their main hero
                    bool shouldNotify = (hero == Hero.MainHero) && notifyRequested;

                    CampaignEventDispatcher.Instance.OnHeroGainedSkill(hero, skillObject, changeAmount, shouldNotify);
                }
            }
        }

        private void ChangeRawXp(string heroId, float rawXp, bool notifyRequested)
        {
            var campaign = Campaign.Current;
            if (campaign == null) return;

            // Get hero from objectManager
            if (!objectManager.TryGetObjectWithLogging(heroId, out Hero hero)) return;

            var heroDeveloper = hero.HeroDeveloper;
            if (heroDeveloper == null) return;

            int maxSkillPoint = campaign.Models.CharacterDevelopmentModel.GetMaxSkillPoint();

            // Replace original TaleWorlds implementation
            // HeroDeveloper.CheckLevel is patched and only runs the original on a client when the
            // thread is allowed, so the vanilla level-up must be marked as allowed here.
            using (new AllowedThread())
            {
                if ((long)heroDeveloper._totalXp + (long)MathF.Round(rawXp) < (long)maxSkillPoint)
                {
                    heroDeveloper._totalXp += MathF.Round(rawXp);

                    // Only notify if running on client where the updated hero is their main hero
                    bool shouldNotify = (hero == Hero.MainHero) && notifyRequested;

                    heroDeveloper.CheckLevel(shouldNotify);
                    return;
                }
                heroDeveloper._totalXp = maxSkillPoint;
            }
        }
    }
}

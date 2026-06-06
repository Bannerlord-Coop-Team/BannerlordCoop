using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
            messageBroker.Subscribe<NetworkSetSkillXpServer>(Handle);
            messageBroker.Subscribe<NetworkSetSkillXpClients>(Handle);
            messageBroker.Subscribe<SkillLevelChange>(Handle);
            messageBroker.Subscribe<NetworkSkillLevelChangeServer>(Handle);
            messageBroker.Subscribe<NetworkSkillLevelChangeClients>(Handle);
            messageBroker.Subscribe<RawXpGain>(Handle);
            messageBroker.Subscribe<NetworkRawXpGainServer>(Handle);
            messageBroker.Subscribe<NetworkRawXpGainClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SkillXpSet>(Handle);
            messageBroker.Unsubscribe<NetworkSetSkillXpServer>(Handle);
            messageBroker.Unsubscribe<NetworkSetSkillXpClients>(Handle);
            messageBroker.Unsubscribe<SkillLevelChange>(Handle);
            messageBroker.Unsubscribe<NetworkSkillLevelChangeServer>(Handle);
            messageBroker.Unsubscribe<NetworkSkillLevelChangeClients>(Handle);
            messageBroker.Unsubscribe<RawXpGain>(Handle);
            messageBroker.Unsubscribe<NetworkRawXpGainServer>(Handle);
            messageBroker.Unsubscribe<NetworkRawXpGainClients>(Handle);
        }

        private void Handle(MessagePayload<SkillXpSet> obj)
        {
            SendSkillXp(obj.What);
        }

        private void Handle(MessagePayload<NetworkSetSkillXpServer> obj)
        {
            // Send to all clients and apply on server
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    NetworkSetSkillXpClients changes = new(obj.What);
                    network.SendAll(changes);
                    SetSkillXp(changes);
                }
            });
        }

        private void Handle(MessagePayload<NetworkSetSkillXpClients> obj)
        {
            SetSkillXp(obj.What);
        }

        private void Handle(MessagePayload<SkillLevelChange> obj)
        {
            SendSkillLevelChange(obj.What);
        }
        private void Handle(MessagePayload<NetworkSkillLevelChangeServer> obj)
        {
            // Send to all clients and apply on server
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    NetworkSkillLevelChangeClients changes = new(obj.What);
                    network.SendAll(changes);
                    ChangeSkillLevel(changes);
                }
            });
        }

        private void Handle(MessagePayload<NetworkSkillLevelChangeClients> obj)
        {
            ChangeSkillLevel(obj.What);
        }

        private void Handle(MessagePayload<RawXpGain> obj)
        {
            SendRawXpGain(obj.What);
        }
        private void Handle(MessagePayload<NetworkRawXpGainServer> obj)
        {
            // Send to all clients and apply on server
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    NetworkRawXpGainClients changes = new(obj.What);
                    network.SendAll(changes);
                    ChangeRawXp(changes);
                }
            });
        }

        private void Handle(MessagePayload<NetworkRawXpGainClients> obj)
        {
            ChangeRawXp(obj.What);
        }

        private void SendSkillXp(SkillXpSet obj)
        {
            // Get hero id for transmission over the network
            if (!objectManager.TryGetId(obj.HeroDeveloper.Hero, out var heroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.HeroDeveloper.Hero?.GetType());
                return;
            }

            // Get skill object id for transmission over the network
            if (!objectManager.TryGetId(obj.SkillObject, out var skillObjectId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.SkillObject?.GetType());
                return;
            }

            // Send to server from client
            NetworkSetSkillXpServer message = new(
                heroId,
                skillObjectId,
                obj.Value
            );
            network.SendAll(message);
        }

        private void SetSkillXp(NetworkSetSkillXpClients obj)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObject(obj.HeroId, out Hero hero))
            {
                Logger.Error("Unable to get object for hero id {id}", obj.HeroId);
                return;
            }
            if (!objectManager.TryGetObject(obj.SkillObjectId, out SkillObject skillObject))
            {
                Logger.Error("Unable to get object for skill object id {id}", obj.SkillObjectId);
                return;
            }

            // Replace original TaleWorlds implementation
            if (!obj.Value.ApproximatelyEqualsTo(0f, 1E-05f))
            {
                hero.HeroDeveloper._skillXps[skillObject] = obj.Value;
                return;
            }
            if (hero.HeroDeveloper._skillXps.ContainsKey(skillObject))
            {
                hero.HeroDeveloper._skillXps.Remove(skillObject);
            }
        }

        private void SendSkillLevelChange(SkillLevelChange obj)
        {
            // Get hero id for transmission over the network
            if (!objectManager.TryGetId(obj.HeroDeveloper.Hero, out var heroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.HeroDeveloper.Hero?.GetType());
                return;
            }

            // Get skill object id for transmission over the network
            if (!objectManager.TryGetId(obj.SkillObject, out var skillObjectId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.SkillObject?.GetType());
                return;
            }

            // Send to server from client
            NetworkSkillLevelChangeServer message = new(
                heroId,
                skillObjectId,
                obj.ChangeAmount,
                obj.ShouldNotify
            );
            network.SendAll(message);
        }

        private void ChangeSkillLevel(NetworkSkillLevelChangeClients obj)
        {
            // Get objects from objectManager
            if (!objectManager.TryGetObject(obj.HeroId, out Hero hero))
            {
                Logger.Error("Unable to get object for hero id {id}", obj.HeroId);
                return;
            }
            if (!objectManager.TryGetObject(obj.SkillObjectId, out SkillObject skillObject))
            {
                Logger.Error("Unable to get object for skill object id {id}", obj.SkillObjectId);
            }

            // Replace original TaleWorlds implementation
            if (obj.ChangeAmount != 0)
            {
                int value = hero.GetSkillValue(skillObject) + obj.ChangeAmount;
                hero.SetSkillValue(skillObject, value);

                // Only notify if running on client where the updated hero is their main hero
                bool shouldNotify = (hero == Hero.MainHero) && obj.ShouldNotify;

                CampaignEventDispatcher.Instance.OnHeroGainedSkill(hero, skillObject, obj.ChangeAmount, shouldNotify);
            }
        }

        private void SendRawXpGain(RawXpGain obj)
        {
            // Get hero id for transmission over the network
            if (!objectManager.TryGetId(obj.HeroDeveloper.Hero, out var heroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.HeroDeveloper.Hero?.GetType());
                return;
            }

            // Send to server from client
            NetworkRawXpGainServer message = new(
                heroId,
                obj.RawXp,
                obj.ShouldNotify
            );
            network.SendAll(message);
        }

        private void ChangeRawXp(NetworkRawXpGainClients obj)
        {
            // Get hero from objectManager
            if (!objectManager.TryGetObject(obj.HeroId, out Hero hero))
            {
                Logger.Error("Unable to get object for hero id {id}", obj.HeroId);
                return;
            }

            // Replace original TaleWorlds implementation
            if ((long)hero.HeroDeveloper._totalXp + (long)MathF.Round(obj.RawXp) < (long)Campaign.Current.Models.CharacterDevelopmentModel.GetMaxSkillPoint())
            {
                hero.HeroDeveloper._totalXp += MathF.Round(obj.RawXp);

                // Only notify if running on client where the updated hero is their main hero
                bool shouldNotify = (hero == Hero.MainHero) && obj.ShouldNotify;

                hero.HeroDeveloper.CheckLevel(shouldNotify);
                return;
            }
            hero.HeroDeveloper._totalXp = Campaign.Current.Models.CharacterDevelopmentModel.GetMaxSkillPoint();
        }
    }
}

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
using TaleWorlds.CampaignSystem;
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
            GameThread.Run(() =>
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
            var data = obj.What;

            GameThread.Run(() =>
            {
                try
                {
                    SetSkillXp(data);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkSetSkillXpClients");
                }
            });
        }

        private void Handle(MessagePayload<SkillLevelChange> obj)
        {
            SendSkillLevelChange(obj.What);
        }
        private void Handle(MessagePayload<NetworkSkillLevelChangeServer> obj)
        {
            // Send to all clients and apply on server
            GameThread.Run(() =>
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
            var data = obj.What;

            GameThread.Run(() =>
            {
                try
                {
                    ChangeSkillLevel(data);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkSkillLevelChangeClients");
                }
            });
        }

        private void Handle(MessagePayload<RawXpGain> obj)
        {
            SendRawXpGain(obj.What);
        }
        private void Handle(MessagePayload<NetworkRawXpGainServer> obj)
        {
            // Send to all clients and apply on server
            GameThread.RunSafe(() =>
            {
                using (new AllowedThread())
                {
                    NetworkRawXpGainClients changes = new(obj.What);
                    network.SendAll(changes);
                    ChangeRawXp(changes);
                }
            }, context: nameof(HeroDeveloperHandler));
        }

        private void Handle(MessagePayload<NetworkRawXpGainClients> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(
                () => ChangeRawXp(data),
                context: nameof(HeroDeveloperHandler));
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

            heroId = Compact(heroId, typeof(Hero));
            skillObjectId = Compact(skillObjectId, typeof(SkillObject));

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
                return;
            }

            // Replace original TaleWorlds implementation
            if (obj.ChangeAmount != 0)
            {
                // Hero.SetSkillValue is patched and only runs the original on a client when the
                // thread is allowed, so the vanilla skill write must be marked as allowed here.
                using (new AllowedThread())
                {
                    int value = hero.GetSkillValue(skillObject) + obj.ChangeAmount;
                    hero.SetSkillValue(skillObject, value);

                    // Only notify if running on client where the updated hero is their main hero
                    bool shouldNotify = (hero == Hero.MainHero) && obj.ShouldNotify;

                    CampaignEventDispatcher.Instance.OnHeroGainedSkill(hero, skillObject, obj.ChangeAmount, shouldNotify);
                }
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

            heroId = Compact(heroId, typeof(Hero));

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
            var campaign = Campaign.Current;
            if (campaign == null) return;

            // Get hero from objectManager
            if (!objectManager.TryGetObjectWithLogging(obj.HeroId, out Hero hero)) return;

            var heroDeveloper = hero.HeroDeveloper;
            if (heroDeveloper == null) return;

            int maxSkillPoint = campaign.Models.CharacterDevelopmentModel.GetMaxSkillPoint();

            // Replace original TaleWorlds implementation
            // HeroDeveloper.CheckLevel is patched and only runs the original on a client when the
            // thread is allowed, so the vanilla level-up must be marked as allowed here.
            using (new AllowedThread())
            {
                if ((long)heroDeveloper._totalXp + (long)MathF.Round(obj.RawXp) < (long)maxSkillPoint)
                {
                    heroDeveloper._totalXp += MathF.Round(obj.RawXp);

                    // Only notify if running on client where the updated hero is their main hero
                    bool shouldNotify = (hero == Hero.MainHero) && obj.ShouldNotify;

                    heroDeveloper.CheckLevel(shouldNotify);
                    return;
                }
                heroDeveloper._totalXp = maxSkillPoint;
            }
        }
    }
}

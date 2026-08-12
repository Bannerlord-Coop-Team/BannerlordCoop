using Common.Logging;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Transactions;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorResearchPointHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorResearchPointHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface;
        private readonly IPlayerManager playerManager;
        private readonly object gate = new();
        private readonly Dictionary<NetPeer, PendingResearch> pending = new();
        private readonly Dictionary<NetPeer, ResearchPermit> permits = new();
        private static CraftingCampaignBehaviorResearchPointHandler serverInstance;

        public CraftingCampaignBehaviorResearchPointHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface,
            IPlayerManager playerManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.sessionCraftingPlayerDataInterface = sessionCraftingPlayerDataInterface;
            this.playerManager = playerManager;
            if (ModInformation.IsServer)
            {
                serverInstance = this;
                ServerTransactionOutcome.Completed +=
                    HandleTransactionCompleted;
            }
            messageBroker.Subscribe<UpdateResearchPoints>(Handle);
            messageBroker.Subscribe<NetworkUpdateResearchPoints>(Handle);
            messageBroker.Subscribe<OpenCraftingPart>(Handle);
            messageBroker.Subscribe<NetworkOpenCraftingPart>(Handle);
            messageBroker.Subscribe<PlayerDisconnected>(HandleDisconnected);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UpdateResearchPoints>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateResearchPoints>(Handle);
            messageBroker.Unsubscribe<OpenCraftingPart>(Handle);
            messageBroker.Unsubscribe<NetworkOpenCraftingPart>(Handle);
            messageBroker.Unsubscribe<PlayerDisconnected>(HandleDisconnected);
            if (ReferenceEquals(serverInstance, this))
            {
                ServerTransactionOutcome.Completed -=
                    HandleTransactionCompleted;
                serverInstance = null;
            }
            lock (gate)
            {
                pending.Clear();
                permits.Clear();
            }
        }

        private void Handle(MessagePayload<UpdateResearchPoints> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out string playerHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.CraftingTemplate, out string craftingTemplateId)) return;

            network.SendAll(new NetworkUpdateResearchPoints(playerHeroId, craftingTemplateId, obj.What.NewXp));
        }

        private void Handle(MessagePayload<NetworkUpdateResearchPoints> obj)
        {
            if (ModInformation.IsServer && obj.Who is NetPeer peer)
            {
                GameThread.RunSafe(() => StageResearch(peer, obj.What));
                return;
            }
            sessionCraftingPlayerDataInterface.SetCraftingPieceXp(
                obj.What.PlayerHeroId,
                obj.What.CraftingTemplateId,
                obj.What.NewXp);
        }

        private void Handle(MessagePayload<OpenCraftingPart> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out string playerHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.CraftingTemplate, out string craftingTemplateId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.CraftingPiece, out string craftingPieceId)) return;

            network.SendAll(new NetworkOpenCraftingPart(playerHeroId, craftingTemplateId, craftingPieceId));
        }

        private void Handle(MessagePayload<NetworkOpenCraftingPart> obj)
        {
            if (ModInformation.IsServer && obj.Who is NetPeer peer)
            {
                GameThread.RunSafe(() => StageOpenedPart(peer, obj.What));
                return;
            }
            sessionCraftingPlayerDataInterface.UnlockCraftingPiece(
                obj.What.PlayerHeroId,
                obj.What.CraftingTemplateId,
                obj.What.CraftingPieceId);
        }

        private void HandleDisconnected(
            MessagePayload<PlayerDisconnected> obj)
        {
            if (!ModInformation.IsServer) return;
            lock (gate)
            {
                pending.Remove(obj.What.PlayerId);
                permits.Remove(obj.What.PlayerId);
            }
        }

        internal static void AllowAuthoritativeResearch(
            NetPeer peer,
            string playerHeroId,
            string craftingTemplateId,
            int gain)
        {
            serverInstance?.AllowResearch(
                peer, playerHeroId, craftingTemplateId, gain);
        }

        internal static void DiscardPendingResearch(NetPeer peer)
        {
            CraftingCampaignBehaviorResearchPointHandler current =
                serverInstance;
            if (current == null || peer == null)
                return;
            lock (current.gate)
            {
                current.pending.Remove(peer);
                current.permits.Remove(peer);
            }
        }

        private static void HandleTransactionCompleted(
            NetPeer peer,
            int kind,
            bool success,
            string message)
        {
            if (!success &&
                (kind == ServerTransactionOutcome.Smelt ||
                 kind == ServerTransactionOutcome.Craft))
                DiscardPendingResearch(peer);
        }

        private void StageResearch(
            NetPeer peer,
            NetworkUpdateResearchPoints message)
        {
            if (!TryAuthenticate(peer, message.PlayerHeroId) ||
                string.IsNullOrEmpty(message.CraftingTemplateId) ||
                float.IsNaN(message.NewXp) ||
                float.IsInfinity(message.NewXp) || message.NewXp < 0f)
                return;
            lock (gate)
            {
                PendingResearch state = GetPending(
                    peer, message.PlayerHeroId, message.CraftingTemplateId);
                state.NewXp = message.NewXp;
                state.HasXp = true;
            }
            TryCommit(peer);
        }

        private void StageOpenedPart(
            NetPeer peer,
            NetworkOpenCraftingPart message)
        {
            if (!TryAuthenticate(peer, message.PlayerHeroId) ||
                string.IsNullOrEmpty(message.CraftingTemplateId) ||
                string.IsNullOrEmpty(message.CraftingPieceId))
                return;
            lock (gate)
            {
                PendingResearch state = GetPending(
                    peer, message.PlayerHeroId, message.CraftingTemplateId);
                if (state.OpenedPieceIds.Count >= 32 ||
                    state.OpenedPieceIds.Contains(message.CraftingPieceId))
                    return;
                state.OpenedPieceIds.Add(message.CraftingPieceId);
            }
        }

        private void AllowResearch(
            NetPeer peer,
            string playerHeroId,
            string craftingTemplateId,
            int gain)
        {
            if (peer == null || gain < 0 ||
                !TryAuthenticate(peer, playerHeroId))
                return;
            lock (gate)
            {
                permits[peer] = new ResearchPermit(
                    playerHeroId,
                    craftingTemplateId,
                    gain,
                    DateTime.UtcNow.AddSeconds(30));
            }
            TryCommit(peer);
        }

        private void TryCommit(NetPeer peer)
        {
            PendingResearch staged;
            ResearchPermit permit;
            lock (gate)
            {
                if (!pending.TryGetValue(peer, out staged) ||
                    !permits.TryGetValue(peer, out permit) ||
                    !staged.HasXp)
                    return;
                pending.Remove(peer);
                permits.Remove(peer);
            }
            if (DateTime.UtcNow > staged.ExpiresUtc ||
                DateTime.UtcNow > permit.ExpiresUtc ||
                !string.Equals(staged.PlayerHeroId, permit.PlayerHeroId,
                    StringComparison.Ordinal) ||
                !string.Equals(staged.TemplateId, permit.TemplateId,
                    StringComparison.Ordinal) ||
                !TryValidateResearch(staged, permit, out float finalXp))
            {
                Logger.Warning(
                    "Rejected smithing research update for {HeroId}",
                    permit.PlayerHeroId);
                return;
            }

            sessionCraftingPlayerDataInterface.SetCraftingPieceXp(
                permit.PlayerHeroId, permit.TemplateId, finalXp);
            network.SendAll(new NetworkUpdateResearchPoints(
                permit.PlayerHeroId, permit.TemplateId, finalXp));
            foreach (string pieceId in staged.OpenedPieceIds)
            {
                sessionCraftingPlayerDataInterface.UnlockCraftingPiece(
                    permit.PlayerHeroId, permit.TemplateId, pieceId);
                network.SendAll(new NetworkOpenCraftingPart(
                    permit.PlayerHeroId, permit.TemplateId, pieceId));
            }
        }

        private bool TryValidateResearch(
            PendingResearch staged,
            ResearchPermit permit,
            out float finalXp)
        {
            finalXp = 0f;
            if (!objectManager.TryGetObject(
                    permit.TemplateId, out CraftingTemplate template) ||
                template == null)
                return false;

            var opened = new HashSet<string>(
                sessionCraftingPlayerDataInterface.GetOpenedCraftingPieces(
                    permit.PlayerHeroId, permit.TemplateId),
                StringComparer.Ordinal);
            float xp = sessionCraftingPlayerDataInterface.GetCraftingPieceXp(
                permit.PlayerHeroId, permit.TemplateId) + permit.Gain;
            int total = template.Pieces.Count;
            foreach (string pieceId in staged.OpenedPieceIds)
            {
                int openedCount = CountOpened(template, opened);
                float required = Campaign.Current.Models.SmithingModel
                    .ResearchPointsNeedForNewPart(total, openedCount);
                if (!(xp > required) ||
                    !objectManager.TryGetObject(
                        pieceId, out CraftingPiece piece) ||
                    piece == null || piece.IsGivenByDefault ||
                    piece.IsHiddenOnDesigner || opened.Contains(pieceId) ||
                    !template.Pieces.Contains(piece))
                    return false;
                int minimumTier = template.Pieces
                    .Where(candidate =>
                        !candidate.IsGivenByDefault &&
                        !candidate.IsHiddenOnDesigner &&
                        objectManager.TryGetId(candidate, out string id) &&
                        !opened.Contains(id))
                    .Select(candidate => candidate.PieceTier)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
                if (piece.PieceTier != minimumTier)
                    return false;
                xp -= required;
                opened.Add(pieceId);
            }

            int finalOpenedCount = CountOpened(template, opened);
            float nextRequired = Campaign.Current.Models.SmithingModel
                .ResearchPointsNeedForNewPart(total, finalOpenedCount);
            bool hasEligible = template.Pieces.Any(candidate =>
                !candidate.IsGivenByDefault &&
                !candidate.IsHiddenOnDesigner &&
                objectManager.TryGetId(candidate, out string id) &&
                !opened.Contains(id));
            if (hasEligible && xp > nextRequired ||
                Math.Abs(xp - staged.NewXp) > 0.01f)
                return false;
            finalXp = xp;
            return true;
        }

        private int CountOpened(
            CraftingTemplate template,
            ISet<string> opened)
        {
            return template.Pieces.Count(piece =>
                piece.IsGivenByDefault ||
                objectManager.TryGetId(piece, out string id) &&
                opened.Contains(id));
        }

        private bool TryAuthenticate(NetPeer peer, string playerHeroId)
        {
            return peer != null && playerManager.TryGetPlayer(
                peer, out var player) && player != null &&
                string.Equals(player.HeroId, playerHeroId,
                    StringComparison.Ordinal);
        }

        private PendingResearch GetPending(
            NetPeer peer,
            string heroId,
            string templateId)
        {
            if (!pending.TryGetValue(peer, out PendingResearch state) ||
                DateTime.UtcNow > state.ExpiresUtc ||
                !string.Equals(state.PlayerHeroId, heroId,
                    StringComparison.Ordinal) ||
                !string.Equals(state.TemplateId, templateId,
                    StringComparison.Ordinal))
            {
                state = new PendingResearch(
                    heroId, templateId, DateTime.UtcNow.AddSeconds(30));
                pending[peer] = state;
            }
            return state;
        }

        private sealed class PendingResearch
        {
            internal readonly string PlayerHeroId;
            internal readonly string TemplateId;
            internal readonly DateTime ExpiresUtc;
            internal readonly List<string> OpenedPieceIds = new();
            internal bool HasXp;
            internal float NewXp;

            internal PendingResearch(
                string playerHeroId,
                string templateId,
                DateTime expiresUtc)
            {
                PlayerHeroId = playerHeroId;
                TemplateId = templateId;
                ExpiresUtc = expiresUtc;
            }
        }

        private sealed class ResearchPermit
        {
            internal readonly string PlayerHeroId;
            internal readonly string TemplateId;
            internal readonly int Gain;
            internal readonly DateTime ExpiresUtc;

            internal ResearchPermit(
                string playerHeroId,
                string templateId,
                int gain,
                DateTime expiresUtc)
            {
                PlayerHeroId = playerHeroId;
                TemplateId = templateId;
                Gain = gain;
                ExpiresUtc = expiresUtc;
            }
        }
    }
}

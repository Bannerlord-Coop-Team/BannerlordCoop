using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Locations.BoardGames.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.Locations.BoardGames.Handlers;

internal class BoardGameRewardHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BoardGameRewardHandler>();

    // No vanilla constant exists for these: they are literals in the
    // taverngamehost_player_{100,200,300,400,500}_denars dialog lines and the
    // matching SetBetAmount(N) consequences in BoardGameCampaignBehavior.
    private static readonly int[] ValidBets = { 100, 200, 300, 400, 500 };

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public BoardGameRewardHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<BoardGameTavernResult>(Handle_BoardGameTavernResult);
        messageBroker.Subscribe<NetworkBoardGameTavernResult>(Handle_NetworkBoardGameTavernResult);
        messageBroker.Subscribe<BoardGameLordResult>(Handle_BoardGameLordResult);
        messageBroker.Subscribe<NetworkBoardGameLordResult>(Handle_NetworkBoardGameLordResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BoardGameTavernResult>(Handle_BoardGameTavernResult);
        messageBroker.Unsubscribe<NetworkBoardGameTavernResult>(Handle_NetworkBoardGameTavernResult);
        messageBroker.Unsubscribe<BoardGameLordResult>(Handle_BoardGameLordResult);
        messageBroker.Unsubscribe<NetworkBoardGameLordResult>(Handle_NetworkBoardGameLordResult);
    }

    private void Handle_BoardGameTavernResult(MessagePayload<BoardGameTavernResult> obj)
    {
        var data = obj.What;
        GameThread.RunSafe(() =>
        {
            network.SendAll(new NetworkBoardGameTavernResult(
                data.BetAmount, data.GameOver));
            Logger.Information("Forwarding board-game tavern result: bet {Bet} outcome {Outcome}",
                data.BetAmount, data.GameOver);
        }, context: nameof(BoardGameRewardHandler));
    }

    private void Handle_NetworkBoardGameTavernResult(MessagePayload<NetworkBoardGameTavernResult> obj)
    {
        if (ModInformation.IsClient) return;
        var data = obj.What;
        if (obj.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(NetworkBoardGameTavernResult));
            return;
        }

        GameThread.RunSafe(() =>
        {
            // All registry lookups stay inside the game-thread closure so they
            // run ordered behind deferred registrations on the same FIFO queue.
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero)) return;
            if (System.Array.IndexOf(ValidBets, data.BetAmount) < 0)
            {
                Logger.Warning("Dropping board-game tavern result with invalid bet {Bet}", data.BetAmount);
                return;
            }

            if (data.GameOver == BoardGameHelper.BoardGameState.Win)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, playerHero, data.BetAmount, false);
                Logger.Information("Applied board-game tavern win: {Bet} gold for hero {HeroId}", data.BetAmount, player.HeroId);
            }
            else if (data.GameOver == BoardGameHelper.BoardGameState.Loss)
            {
                GiveGoldAction.ApplyBetweenCharacters(playerHero, null, data.BetAmount, false);
                Logger.Information("Applied board-game tavern loss: {Bet} gold from hero {HeroId}", data.BetAmount, player.HeroId);
            }
            else
                Logger.Warning("Dropping board-game tavern result with invalid outcome {Outcome}", data.GameOver);
            // Runs with patches live: Hero.Gold autosync and NotifyGoldChanged
            // replicate to clients. Never wrap in AllowedThread here.
        }, context: nameof(BoardGameRewardHandler));
    }

    private void Handle_BoardGameLordResult(MessagePayload<BoardGameLordResult> obj)
    {
        var data = obj.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.OpposingHero, out var opposingHeroId)) return;
            network.SendAll(new NetworkBoardGameLordResult(
                opposingHeroId, data.Difficulty, data.Reward, data.ExtraXp));
            Logger.Information("Forwarding board-game lord result: reward {Reward} difficulty {Difficulty} against hero {HeroId}",
                data.Reward, data.Difficulty, opposingHeroId);
        }, context: nameof(BoardGameRewardHandler));
    }

    private void Handle_NetworkBoardGameLordResult(MessagePayload<NetworkBoardGameLordResult> obj)
    {
        if (ModInformation.IsClient) return;
        var data = obj.What;
        if (!(obj.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(NetworkBoardGameLordResult));
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OpposingHeroId, out var opposingHero)) return;
            if (!System.Enum.IsDefined(typeof(BoardGameHelper.AIDifficulty), data.Difficulty)
                || !System.Enum.IsDefined(typeof(LordBoardGameReward), data.Reward)
                || data.Difficulty == BoardGameHelper.AIDifficulty.NumTypes)
            {
                Logger.Warning("Dropping board-game lord result with invalid payload");
                return;
            }
            // Mirror vanilla legality (OnPlayerBoardGameOver: flag2 and the
            // influence branch both require Hard; extra XP is only rolled
            // otherwise): influence and renown only exist on Hard, extra XP
            // only on non-Hard difficulties.
            bool isHard = data.Difficulty == BoardGameHelper.AIDifficulty.Hard;
            if ((data.Reward == LordBoardGameReward.Influence || data.Reward == LordBoardGameReward.Renown) && !isHard)
            {
                Logger.Warning("Dropping board-game lord result with {Reward} on non-Hard difficulty", data.Reward);
                return;
            }
            if (data.ExtraXp && isHard)
            {
                Logger.Warning("Dropping board-game lord result with extra XP on Hard difficulty");
                return;
            }

            // Makes vanilla Hero.MainHero derefs resolve to the requesting
            // player and feeds ResolvedMainHeroContext for the relation patch.
            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                SkillLevelingManager.OnBoardGameWonAgainstLord(
                    opposingHero,
                    data.Difficulty,
                    data.ExtraXp);

                switch (data.Reward)
                {
                    case LordBoardGameReward.Relation:
                        ChangeRelationAction.ApplyPlayerRelation(opposingHero, 1, true, true);
                        break;
                    case LordBoardGameReward.Influence:
                        GainKingdomInfluenceAction.ApplyForBoardGameWon(opposingHero, 1f);
                        break;
                    case LordBoardGameReward.Renown:
                        GainRenownAction.Apply(playerHero, 1f, false);
                        break;
                }
                Logger.Information("Applied board-game lord win: reward {Reward} difficulty {Difficulty} for hero {HeroId} against hero {OpposingHeroId}",
                    data.Reward, data.Difficulty, player.HeroId, data.OpposingHeroId);
            }
        }, context: nameof(BoardGameRewardHandler));
    }
}

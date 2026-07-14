using GameInterface.Services.MapEvents.Messages.Start;
using LiteNetLib;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] One battle-resolution mode's start strategy. <see cref="BattleStartDispatcher"/> is the sole subscriber
/// of <see cref="NetworkBattleStartRequest"/>; it resolves the map event on the game thread and hands the request to
/// the matching starter. Each starter owns its mode-specific rejection gates and their verbatim replies, decides
/// when to commit by invoking the dispatcher-supplied claim, and owns the timing of its own success reply.
/// </summary>
internal interface IBattleModeStarter
{
    /// <summary>The mode this starter handles; the dispatcher routes by the request's <see cref="NetworkBattleStartRequest.Mode"/>.</summary>
    BattleStartMode Mode { get; }

    /// <summary>
    /// [Server, game thread] Handle a resolved battle-start request. The starter runs its own pre-claim rejection
    /// gates (replying as it does today on rejection); when it is ready to commit it invokes <paramref name="claim"/>
    /// — which the dispatcher owns — and acts on the tri-state result (e.g. mission proceeds on
    /// <see cref="BattleClaimResult.AlreadyClaimedSameMode"/>; simulation additionally consults its own session
    /// state). <paramref name="mapEvent"/> was re-resolved by the dispatcher at drain time.
    /// </summary>
    void HandleRequest(MapEvent mapEvent, NetworkBattleStartRequest request, NetPeer requester, Func<BattleClaimResult> claim);
}

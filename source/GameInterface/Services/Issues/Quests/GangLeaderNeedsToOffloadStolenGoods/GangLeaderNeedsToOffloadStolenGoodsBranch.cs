namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal enum GangLeaderNeedsToOffloadStolenGoodsBranch : byte
{
    SuccessByKeepingGoods = 1,
    SuccessByGivingGoodsBack = 2,
    BetrayalByKeepingGoods = 3,
    BetrayalByGivingGoodsBack = 4,
    FailByLosingHideoutBattle = 5,
}

#if COOP_NAVAL
using Coop.Naval.Models.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using NavalDLC.GameComponents;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Naval;

public class NavalModelPatchesTests : IDisposable
{
    private static readonly ConditionalWeakTable<object, ControlledObjectInfo> PlayerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools.Field(typeof(PlayerManager), "PlayerObjects").GetValue(null);

    private readonly List<object> playerObjects = new();
    private readonly NavalDLCMapDistanceModel distanceModel = CreateDistanceModel();
    private readonly Settlement from = CreateSettlement(gate: new Vec2(0f, 0f), port: new Vec2(0f, 0f));
    private readonly Settlement to = CreateSettlement(gate: new Vec2(3f, 4f), port: new Vec2(6f, 8f));

    public void Dispose()
    {
        foreach (var obj in playerObjects) PlayerObjects.Remove(obj);
    }

    // A joining client loads the save before the map scene registers its navigation caches
    [Fact]
    public void GetDistance_WithoutNavigationCache_FallsBackToTheGateDistance()
    {
        float landRatio = 0f;
        float result = 0f;

        Assert.False(NavalDLCMapDistanceModelPatches.GetDistancePrefix(distanceModel, from, to, MobileParty.NavigationType.Naval, ref landRatio, ref result));

        Assert.Equal(5f, result);
        Assert.Equal(1f, landRatio);
    }

    [Fact]
    public void GetDistance_WithNavigationCache_RunsVanilla()
    {
        AddNavigationCache(MobileParty.NavigationType.Naval);
        float landRatio = 0f;
        float result = 0f;

        Assert.True(NavalDLCMapDistanceModelPatches.GetDistancePrefix(distanceModel, from, to, MobileParty.NavigationType.Naval, ref landRatio, ref result));
    }

    // Vanilla doesn't read the cache for the same settlement
    [Fact]
    public void GetDistance_SameSettlement_RunsVanilla()
    {
        float landRatio = 0f;
        float result = 0f;

        Assert.True(NavalDLCMapDistanceModelPatches.GetDistancePrefix(distanceModel, from, from, MobileParty.NavigationType.Naval, ref landRatio, ref result));
    }

    [Fact]
    public void GetClosestEntranceToFace_WithoutNavigationCache_ReturnsNoSettlement()
    {
        var result = (to, true);

        Assert.False(NavalDLCMapDistanceModelPatches.GetClosestEntranceToFacePrefix(distanceModel, MobileParty.NavigationType.Naval, ref result));

        Assert.Equal((null, false), result);
    }

    [Fact]
    public void GetPortToGateDistance_WithoutNavigationCache_FallsBackToTheStraightDistance()
    {
        float result = 0f;

        Assert.False(NavalDLCMapDistanceModelPatches.GetPortToGateDistanceForSettlementPrefix(distanceModel, to, ref result));

        Assert.Equal(5f, result);
    }

    // The 99% discount is for AI parties trading among themselves, never a player clan's purchase or repair
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GetShipBaseValue_ClearsTheAiDiscountOnlyForAPlayerClanBuyer(bool buyerIsPlayerClan, bool expectedDiscount)
    {
        var applyAiDiscount = true;

        NavalDLCShipCostModelPatches.GetShipBaseValuePrefix(ref applyAiDiscount, CreateParty(buyerIsPlayerClan));

        Assert.Equal(expectedDiscount, applyAiDiscount);
    }

    [Fact]
    public void GetShipBaseValue_NoBuyer_KeepsTheAiDiscount()
    {
        var applyAiDiscount = true;

        NavalDLCShipCostModelPatches.GetShipBaseValuePrefix(ref applyAiDiscount, null);

        Assert.True(applyAiDiscount);
    }

    [Theory]
    [InlineData(ShipHull.ShipType.Light, false, 2)]
    [InlineData(ShipHull.ShipType.Medium, false, 4)]
    [InlineData(ShipHull.ShipType.Heavy, false, 6)]
    [InlineData(ShipHull.ShipType.Light, true, 200)]
    [InlineData(ShipHull.ShipType.Heavy, true, 600)]
    public void GetShipUpgradePieceValue_PicksTheHullValueAndDiscountsOnlyAi(ShipHull.ShipType hullType, bool buyerIsPlayerClan, int expected)
    {
        var hull = (ShipHull)RuntimeHelpers.GetUninitializedObject(typeof(ShipHull));
        Traverse.Create(hull).Property(nameof(ShipHull.Type)).SetValue(hullType);
        var ship = (Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship));
        AccessTools.Field(typeof(Ship), nameof(Ship.ShipHull)).SetValue(ship, hull);
        var piece = (ShipUpgradePiece)RuntimeHelpers.GetUninitializedObject(typeof(ShipUpgradePiece));
        Traverse.Create(piece).Property(nameof(ShipUpgradePiece.LightValue)).SetValue(200);
        Traverse.Create(piece).Property(nameof(ShipUpgradePiece.MediumValue)).SetValue(400);
        Traverse.Create(piece).Property(nameof(ShipUpgradePiece.HeavyValue)).SetValue(600);
        var result = 0;

        Assert.False(NavalDLCShipCostModelPatches.GetShipUpgradePieceValueInternalPrefix(ref result, ship, piece, null, CreateParty(buyerIsPlayerClan)));

        Assert.Equal(expected, result);
    }

    // A player without ships can't sail on its army's ships, but an AI party still can
    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(false, 0, true)]
    public void HasNavalNavigationCapability_PlayerPartyNeedsItsOwnShips(bool isPlayerParty, int shipCount, bool expected)
    {
        var party = CreateParty(isPlayerClan: false);
        for (int i = 0; i < shipCount; i++)
        {
            party._ships.Add((Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship)));
        }
        if (isPlayerParty) AddPlayerObject(party.MobileParty);
        var result = true;

        NavalPartyNavigationModelPatches.HasNavalNavigationCapabilityPostfix(ref result, party.MobileParty);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void HasNavalNavigationCapability_KeepsVanillaFalse()
    {
        var party = CreateParty(isPlayerClan: false);
        party._ships.Add((Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship)));
        var result = false;

        NavalPartyNavigationModelPatches.HasNavalNavigationCapabilityPostfix(ref result, party.MobileParty);

        Assert.False(result);
    }

    private PartyBase CreateParty(bool isPlayerClan)
    {
        var clan = (Clan)RuntimeHelpers.GetUninitializedObject(typeof(Clan));
        var mobileParty = (MobileParty)RuntimeHelpers.GetUninitializedObject(typeof(MobileParty));
        mobileParty._actualClan = clan;
        var party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        party.MobileParty = mobileParty;
        party._ships = new MBList<Ship>();
        mobileParty.Party = party;

        if (isPlayerClan) AddPlayerObject(clan);

        return party;
    }

    private void AddPlayerObject(object obj)
    {
        PlayerObjects.Add(obj, new ControlledObjectInfo("naval-model-test", null));
        playerObjects.Add(obj);
    }

    private void AddNavigationCache(MobileParty.NavigationType navigationType)
    {
        ((IDictionary)distanceModel._navigationCaches).Add(navigationType, null);
    }

    private static NavalDLCMapDistanceModel CreateDistanceModel()
    {
        var model = (NavalDLCMapDistanceModel)RuntimeHelpers.GetUninitializedObject(typeof(NavalDLCMapDistanceModel));
        var cachesField = AccessTools.Field(typeof(NavalDLCMapDistanceModel), nameof(NavalDLCMapDistanceModel._navigationCaches));
        cachesField.SetValue(model, Activator.CreateInstance(cachesField.FieldType));
        return model;
    }

    private static Settlement CreateSettlement(Vec2 gate, Vec2 port)
    {
        var settlement = (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement));
        settlement.GatePosition = new CampaignVec2(gate, true);
        settlement.PortPosition = new CampaignVec2(port, false);
        return settlement;
    }
}
#endif

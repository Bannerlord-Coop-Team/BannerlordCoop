using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.StanceLinks.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.StanceLinks.Patches;


/// <summary>
/// Patches for managing lifetime of <see cref="StanceLink"/> objects.
/// </summary>
[HarmonyPatch(typeof(StanceLink))]
internal class StanceLinkLifetimePatches
{
	private static readonly ILogger Logger = LogManager.GetLogger<StanceLinkLifetimePatches>
	();

	[HarmonyPatch(typeof(StanceLink))]
	[HarmonyPatch(MethodType.Constructor)]
	[HarmonyPrefix]
	private static bool ConstructorPrefix(ref StanceLink __instance)
	{
	// Call original if we call this function
	if (CallOriginalPolicy.IsOriginalAllowed()) return true;

	if (ModInformation.IsClient)
	{
	Logger.Error("Client created unmanaged {name}\n"
	+ "Callstack: {callstack}", typeof(StanceLink), Environment.StackTrace);
	return true;
	}

	var message = new StanceLinkCreated(__instance);

	MessageBroker.Instance.Publish(__instance, message);

	return true;
	}

	//[HarmonyPatch(typeof(StanceLink), "Remove method name here!")]
	[HarmonyPrefix]
	private static bool RemovePrefix(ref StanceLink __instance)
	{
	// Call original if we call this function
	if (CallOriginalPolicy.IsOriginalAllowed()) return true;

	if (ModInformation.IsClient)
	{
	Logger.Error("Client destroyed unmanaged {name}\n"
	+ "Callstack: {callstack}", typeof(StanceLink), Environment.StackTrace);
	return false;
	}

	MessageBroker.Instance.Publish(__instance, new StanceLinkDestroyed(__instance));

	return true;
	}
	}

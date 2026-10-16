using Common.Logging;
using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Education.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Education.Interfaces;

public interface IEducationCampaignBehaviorInterface : IGameAbstraction
{
    /// <summary>
    /// Server method
    /// Player's client finished loading into the campaign and can now be picked as an educator
    /// </summary>
    void SetPlayerInCampaign(string controllerId);

    /// <summary>
    /// Server method
    /// Drops disconnected players and their pending notifications, their client loses its map notices on disconnect
    /// </summary>
    void RemoveDisconnectedPlayers();

    /// <summary>
    /// Server method
    /// Removes matching pending notifications and closes them on the educator's client
    /// </summary>
    void CancelPendingNotifications(Func<(Hero, int), string, bool> shouldCancel);

    /// <summary>
    /// Client method
    /// Server controls education behavior, clear client copy on connection
    /// </summary>
    void ClearPreviousEducations();

    /// <summary>
    /// Client method
    /// Display notification coming from the server
    /// </summary>
    void ShowEducationNotification(Hero childToEducate, int age, short education);

    /// <summary>
    /// Client method
    /// Remove a notification the server cancelled
    /// </summary>
    void RemoveEducationNotification(Hero childToEducate, int age);

    /// <summary>
    /// Client method
    /// Locks the child's current stage while the education screen is open, so a notification arriving mid-screen doesn't change the stage being finalized
    /// </summary>
    void LockEducationStage(Hero child);

    /// <summary>
    /// Client method
    /// Releases the stage locked when the education screen opened
    /// </summary>
    void UnlockEducationStage();

    /// <summary>
    /// Client method
    /// Gets the stage locked for this child while its education screen is open
    /// </summary>
    bool TryGetLockedStage(Hero child, out EducationCampaignBehavior.EducationStage stage);

    /// <summary>
    /// Server method
    /// Replacement for EducationCampaignBehavior.OnDailyTick to work for multiple player clans
    /// </summary>
    void OnDailyTick();

    /// <summary>
    /// Server method
    /// Replacement for EducationCampaignBehavior.OnHeroComesOfAge to work for any player clan
    /// </summary>
    void OnHeroComesOfAge(Hero hero);

    /// <summary>
    /// Server method
    /// Replacement for EducationCampaignBehavior.OnHeroKilled to work for any player clan
    /// </summary>
    void OnHeroKilled(Hero victim);

    /// <summary>
    /// Server method
    /// Client finalized education, applies the attributes and skills its options granted. Returns false when the stage was no longer pending and nothing was applied
    /// </summary>
    bool UpdateEducationStage(Hero child, short target, List<CharacterAttribute> attributes, List<SkillObject> skills);

    /// <summary>
    /// Server method
    /// Version of EducationCampaignBehavior.OnFinalize that runs from patched method
    /// </summary>
    void OnFinalize(Hero child, short target, EducationCampaignBehavior.EducationStage stage, List<string> chosenOptions);
}

public class EducationCampaignBehaviorInterface : IEducationCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<EducationCampaignBehaviorInterface>();

    // Dictionary<(childToEducate, childStage), controllerId of the player managing the education>
    private readonly Dictionary<(Hero, int), string> pendingNotifications = new();

    // Controller ids whose client has finished loading into the campaign
    private readonly HashSet<string> playersInCampaign = new();

    // Client: the stage the open education screen was built for
    private Hero lockedChild;
    private EducationCampaignBehavior.EducationStage lockedStage;

    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;

    public EducationCampaignBehaviorInterface(
        IPlayerManager playerManager,
        IMessageBroker messageBroker)
    {
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
    }

    public void SetPlayerInCampaign(string controllerId)
    {
        playersInCampaign.Add(controllerId);
    }

    public void RemoveDisconnectedPlayers()
    {
        playersInCampaign.RemoveWhere(controllerId => !IsConnected(controllerId));

        // No cancel is sent, OnDailyTick resends to the next available educator
        foreach (var pending in pendingNotifications.Where(pending => !IsConnected(pending.Value)).ToArray())
        {
            pendingNotifications.Remove(pending.Key);
        }
    }

    private bool IsConnected(string controllerId)
    {
        return playerManager.TryGetPeer(controllerId, out var peer) && peer.ConnectionState == ConnectionState.Connected;
    }

    public void ClearPreviousEducations()
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        educationBehavior._previousEducations = new();
    }

    public void ShowEducationNotification(Hero childToEducate, int age, short education)
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        TextObject textObject = GameTexts.FindText("str_education_notification_right", null);
        textObject.SetCharacterProperties("CHILD", childToEducate.CharacterObject, true);
        Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new EducationMapNotification(childToEducate, age, textObject));

        // Locally set previous education from server for UI
        educationBehavior._previousEducations[childToEducate] = education;
    }

    public void RemoveEducationNotification(Hero childToEducate, int age)
    {
        // EducationNotificationItemVM removes its own notice when this event fires for its child at or above its age
        CampaignEventDispatcher.Instance.OnChildEducationCompleted(childToEducate, age);

        if (!TryGetEducationBehavior(out var educationBehavior)) return;
        educationBehavior._previousEducations.Remove(childToEducate);
    }

    public void LockEducationStage(Hero child)
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        // Unlock first so GetStage resolves the current stage instead of an earlier lock
        UnlockEducationStage();
        lockedStage = educationBehavior.GetStage(child);
        lockedChild = child;
    }

    public void UnlockEducationStage()
    {
        lockedChild = null;
        lockedStage = null;
    }

    public bool TryGetLockedStage(Hero child, out EducationCampaignBehavior.EducationStage stage)
    {
        stage = lockedStage;
        return stage != null && lockedChild == child;
    }

    public void OnDailyTick()
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        foreach (var playerClan in Campaign.Current.CampaignObjectManager.GetPlayerClans())
        {
            foreach (var clanHero in playerClan.Heroes)
            {
                // Vanilla checks to filter alive, non-player children in the clan
                if (!clanHero.IsAlive
                    || clanHero.IsPlayerHero()
                    || clanHero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge) continue;

                EducationCampaignBehavior.ChildAgeState lastDoneStage = educationBehavior.GetLastDoneStage(clanHero);
                if (lastDoneStage == EducationCampaignBehavior.ChildAgeState.Year16) continue;

                EducationCampaignBehavior.ChildAgeState childAgeState = (EducationCampaignBehavior.ChildAgeState)MathF.Max((int)(lastDoneStage + 1), (int)educationBehavior.GetClosestStage(clanHero));
                int age = EducationCampaignBehavior.ChildStateToAge(childAgeState);

                if (!(clanHero.BirthDay + CampaignTime.Years(age)).IsPast || HasNotificationForAge(clanHero, age)) continue;

                // Earlier stages are completed randomly on schedule, only the current stage waits for an educator
                educationBehavior.DoEducationUntil(clanHero, childAgeState);
                if (clanHero.IsDisabled) continue;

                // A stage the educator never finished was just done automatically, close its notice before sending the new one
                CancelPendingNotifications((childKey, _) => childKey.Item1 == clanHero && childKey.Item2 < age);

                var heroEducator = FindFirstSuitableHeroEducator(playerClan, clanHero);
                if (heroEducator == null || !playerManager.TryGetPlayer(heroEducator, out var playerEducator)) continue;

                // Save pending notification on the server. No other client should get the same notification and it shouldn't be re-sent
                pendingNotifications.Add((clanHero, age), playerEducator.ControllerId);

                // Send notification to display on clients, equivalent to calling ShowEducationNotification in vanilla
                var message = new SendEducationNotification(
                    clanHero,
                    age,
                    educationBehavior._previousEducations.TryGetValue(clanHero, out var education) ? education : (short)-1,
                    playerEducator.ControllerId);
                messageBroker.Publish(this, message);
            }
        }
    }

    private Hero FindFirstSuitableHeroEducator(Clan playerClan, Hero childToEducate)
    {
        var leader = playerClan.Leader;
        if (IsAvailableEducator(leader)
            && (leader == childToEducate.Mother || leader == childToEducate.Father)) return leader;

        // Prefer player father as educator if neither player parent is the clan leader
        if (IsAvailablePlayerParentInClan(childToEducate.Father, playerClan)) return childToEducate.Father;
        if (IsAvailablePlayerParentInClan(childToEducate.Mother, playerClan)) return childToEducate.Mother;

        return null;
    }

    private bool IsAvailablePlayerParentInClan(Hero parent, Clan clan)
    {
        return parent != null && parent.Clan == clan && IsAvailableEducator(parent);
    }

    private bool IsAvailableEducator(Hero hero)
    {
        return playerManager.TryGetPlayer(hero, out var player) && playersInCampaign.Contains(player.ControllerId);
    }

    public void OnHeroComesOfAge(Hero hero)
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        // Don't add education for heroes not born to player parents
        if (hero.Mother?.Clan?.IsPlayerClan() != true && hero.Father?.Clan?.IsPlayerClan() != true) return;

        educationBehavior.DoEducationUntil(hero, EducationCampaignBehavior.ChildAgeState.Count);
        educationBehavior._previousEducations.Remove(hero);

        CancelPendingNotifications((childKey, _) => childKey.Item1 == hero);
    }

    public void OnHeroKilled(Hero victim)
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        if (victim.Clan.IsPlayerClan() && educationBehavior._previousEducations.ContainsKey(victim))
        {
            educationBehavior._previousEducations.Remove(victim);
        }

        playerManager.TryGetPlayer(victim, out var victimPlayer);

        CancelPendingNotifications((childKey, controllerId) => childKey.Item1 == victim || controllerId == victimPlayer?.ControllerId);
    }

    public bool UpdateEducationStage(Hero child, short target, List<CharacterAttribute> attributes, List<SkillObject> skills)
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return false;

        var childKey = (child, EducationCampaignBehavior.ChildStateToAge((EducationCampaignBehavior.ChildAgeState)target));
        if (!pendingNotifications.ContainsKey(childKey))
        {
            Logger.Warning($"Education stage for key ({childKey.child.StringId}, {childKey.Item2}) was updated after pending notification was removed. Education changes not applied.");
            return false;
        }

        foreach (var attribute in attributes)
        {
            child.HeroDeveloper.AddAttribute(attribute, 1, false);
        }

        foreach (var skill in skills)
        {
            child.HeroDeveloper.AddFocus(skill, 1, false);
            child.HeroDeveloper.ChangeSkillLevel(skill, 15);
        }

        educationBehavior._previousEducations[child] = target;
        RemovePendingNotification(childKey);
        return true;
    }

    private void RemovePendingNotification((Hero, int) childKey)
    {
        pendingNotifications.Remove(childKey);
    }

    public void OnFinalize(Hero child, short target, EducationCampaignBehavior.EducationStage stage, List<string> chosenOptions)
    {
        if (!TryGetEducationBehavior(out var educationBehavior)) return;

        foreach (string optionKey in chosenOptions ?? new List<string>())
        {
            var option = stage.GetOption(optionKey);
            if (option == null)
            {
                Logger.Warning($"Education option {optionKey} not found on stage {stage.Target} for {child.StringId}, skipped.");
                continue;
            }

            option.OnConsequence(child);
        }

        educationBehavior._previousEducations[child] = target;
    }

    public void CancelPendingNotifications(Func<(Hero, int), string, bool> shouldCancel)
    {
        foreach (var pending in pendingNotifications.ToArray())
        {
            if (!shouldCancel(pending.Key, pending.Value)) continue;

            pendingNotifications.Remove(pending.Key);
            messageBroker.Publish(this, new CancelEducationNotification(pending.Key.Item1, pending.Key.Item2, pending.Value));
        }
    }

    // Vanilla EducationCampaignBehavior.HasNotificationForAge relies on client-side notifications
    // Save pending notifications locally in this interface, updating when players disconnect or complete an education
    private bool HasNotificationForAge(Hero hero, int age)
    {
        return (pendingNotifications.ContainsKey((hero, age)));
    }

    private bool TryGetEducationBehavior(out EducationCampaignBehavior educationBehavior)
    {
        educationBehavior = Campaign.Current?.GetCampaignBehavior<EducationCampaignBehavior>();
        if (educationBehavior != null) return true;

        Logger.Debug("Skipping education update because the campaign behavior is unavailable");
        return false;
    }
}
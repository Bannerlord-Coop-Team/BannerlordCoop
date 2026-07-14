using Coop.Steam;
using System;
using System.Collections.Generic;

namespace Coop.Tests.Steam
{
    /// <summary>
    /// Scriptable <see cref="ISteamLobbyApi"/>: records calls and lets tests decide when and
    /// how the asynchronous lobby operations complete.
    /// </summary>
    public class FakeSteamLobbyApi : ISteamPublicLobbyApi
    {
        public bool OverlayEnabled = true;
        public string PersonaName = "Test Host";
        public ulong NextCreatedLobbyId = 1001;
        public ulong LobbyOwner = 76561198000000001;
        public bool CreateSucceeds = true;
        public bool JoinSucceeds = true;
        public bool ListSucceeds = true;
        public bool SetLobbyDataSucceeds = true;
        public string FailedLobbyDataKey;
        public bool CompleteOperationsImmediately = true;
        public bool CompleteLobbyDataRequestsImmediately = true;
        public string LaunchCommandLine = string.Empty;
        public bool LastCreateWasPublic;
        public bool ThrowOnListRequest;
        public bool ThrowOnFriendLobbyRequest;
        public bool ThrowOnLobbyDataRequest;

        public readonly List<ulong> LeftLobbies = new List<ulong>();
        public readonly List<ulong> InviteDialogsOpened = new List<ulong>();
        public readonly List<string> RichPresenceConnects = new List<string>();
        public readonly List<ulong> ListedLobbyIds = new List<ulong>();
        public readonly List<ulong> FriendLobbyIds = new List<ulong>();
        public readonly List<ulong> RequestedLobbyDataIds = new List<ulong>();
        public readonly HashSet<ulong> FailedLobbyDataRequests = new HashSet<ulong>();
        public readonly Dictionary<ulong, Dictionary<string, string>> LobbyData = new Dictionary<ulong, Dictionary<string, string>>();
        private readonly Dictionary<ulong, Action> pendingLobbyDataCompletions = new Dictionary<ulong, Action>();
        public int ClearRichPresenceCalls;

        public Action PendingCreateCompletion;
        public Action PendingJoinCompletion;
        public Action PendingListCompletion;

        public bool IsOverlayEnabled => OverlayEnabled;
        public string LocalPersonaName => PersonaName;

        public event Action<ulong> LobbyJoinRequested;
        public event Action<string> ConnectStringReceived;

        public void RaiseLobbyJoinRequested(ulong lobbyId) => LobbyJoinRequested?.Invoke(lobbyId);
        public void RaiseConnectStringReceived(string connectString) => ConnectStringReceived?.Invoke(connectString);

        public void CreateFriendsOnlyLobby(int maxMembers, Action<ulong, bool> onCompleted)
        {
            LastCreateWasPublic = false;
            BeginCreate(onCompleted);
        }

        public void CreatePublicLobby(int maxMembers, Action<ulong, bool> onCompleted)
        {
            LastCreateWasPublic = true;
            BeginCreate(onCompleted);
        }

        private void BeginCreate(Action<ulong, bool> onCompleted)
        {
            PendingCreateCompletion = () => onCompleted(NextCreatedLobbyId, CreateSucceeds);

            if (CompleteOperationsImmediately) CompletePendingCreate();
        }

        public void CompletePendingCreate()
        {
            var completion = PendingCreateCompletion;
            PendingCreateCompletion = null;
            completion?.Invoke();
        }

        public void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted)
        {
            PendingJoinCompletion = () => onCompleted(lobbyId, JoinSucceeds);

            if (CompleteOperationsImmediately) CompletePendingJoin();
        }

        public void CompletePendingJoin()
        {
            var completion = PendingJoinCompletion;
            PendingJoinCompletion = null;
            completion?.Invoke();
        }

        public void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted)
        {
            if (ThrowOnListRequest) throw new InvalidOperationException("scripted lobby-list failure");

            PendingListCompletion = () => onCompleted(ListedLobbyIds, ListSucceeds);

            if (CompleteOperationsImmediately) CompletePendingList();
        }

        public void CompletePendingList()
        {
            var completion = PendingListCompletion;
            PendingListCompletion = null;
            completion?.Invoke();
        }

        public IReadOnlyList<ulong> GetFriendLobbyIds()
        {
            if (ThrowOnFriendLobbyRequest) throw new InvalidOperationException("scripted friend-lobby failure");
            return FriendLobbyIds;
        }

        public void RequestLobbyData(ulong lobbyId, Action<bool> onCompleted)
        {
            if (ThrowOnLobbyDataRequest) throw new InvalidOperationException("scripted lobby-data failure");

            RequestedLobbyDataIds.Add(lobbyId);
            pendingLobbyDataCompletions[lobbyId] = () => onCompleted(!FailedLobbyDataRequests.Contains(lobbyId));
            if (CompleteLobbyDataRequestsImmediately) CompletePendingLobbyData(lobbyId);
        }

        public void CompletePendingLobbyData(ulong lobbyId)
        {
            if (!pendingLobbyDataCompletions.TryGetValue(lobbyId, out var completion)) return;
            pendingLobbyDataCompletions.Remove(lobbyId);
            completion();
        }

        public void LeaveLobby(ulong lobbyId) => LeftLobbies.Add(lobbyId);

        public bool SetLobbyData(ulong lobbyId, string key, string value)
        {
            if (!SetLobbyDataSucceeds || key == FailedLobbyDataKey) return false;

            if (!LobbyData.TryGetValue(lobbyId, out var data))
            {
                data = new Dictionary<string, string>();
                LobbyData[lobbyId] = data;
            }

            data[key] = value;
            return true;
        }

        public string GetLobbyData(ulong lobbyId, string key)
        {
            if (LobbyData.TryGetValue(lobbyId, out var data) && data.TryGetValue(key, out var value)) return value;

            return string.Empty;
        }

        // Only valid while a member, like the real API; reading after leaving yields nothing.
        public ulong GetLobbyOwner(ulong lobbyId)
        {
            return LeftLobbies.Contains(lobbyId) ? 0 : LobbyOwner;
        }

        public void OpenInviteDialog(ulong lobbyId) => InviteDialogsOpened.Add(lobbyId);

        public bool SetRichPresenceConnect(string value)
        {
            RichPresenceConnects.Add(value);
            return true;
        }

        public void ClearRichPresenceConnect() => ClearRichPresenceCalls++;

        public string GetLaunchCommandLine() => LaunchCommandLine;

        public void Dispose()
        {
        }
    }
}

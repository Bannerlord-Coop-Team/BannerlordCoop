using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using NLog;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Path = System.IO.Path;

namespace Coop.Mod.DebugUtil
{
    public static class MoveTo
    {
        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

        private const string logsDir = "logs";
        private const string recordExt = ".moveto";
        private const string logFilename = "-verified.csv";
        private static string current;
        private static CampaignTime firstTick;
        private static CampaignTime lastTick;

        private static MoveToState state { get; set; } = MoveToState.Stop;
        private static List<MoveToEvent> RecordingEventList;
        private static List<MoveToEvent> PlaybackEventList;
        private static List<MoveToEvent> PlaybackMainPartyList;

        public static Action<EntityId, MobileParty, MovementData> MoveToRecording { get; private set; }
        public static Action MoveToPlayback { get; private set; }

        private static bool isValid(string fileName) => !string.IsNullOrEmpty(fileName) &&
              fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        public enum MoveToState
        {
            Stop,
            Recording,
            Playback
        }

        internal static string StartRecord(string filename)
        {
            if (state == MoveToState.Playback)
                return "Could not start recording while playback.";

            if (CoopServer.Instance?.Persistence?.Room?.Tick.IsValid != true)
                return "Could not start recording while server not started.";

            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            if (!isValid(filename))
                return $"Filename '{filename}' is not valid file name.";

            string path = Path.Combine(logsDir, filename + recordExt);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                    return $"Could not delete existed file '{path}'";
                }
            }

            RecordingEventList = new List<MoveToEvent>();
            current = filename;
            state = MoveToState.Recording;
            MoveToRecording += OnEventRecording;

            return $"Recording '{filename}' started.";
        }

        internal static string Playback(string filename)
        {
            if (state == MoveToState.Recording)
                return "Could not start playback while recording.";

            if (CoopServer.Instance?.Persistence?.Room?.Tick.IsValid != true)
                return "Could not start playback while server not started.";

            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            if (!isValid(filename))
                return $"Filename '{filename}' is not valid file name.";

            string path = Path.Combine(logsDir, filename + recordExt);
            if (!File.Exists(path))
                return $"Could not find file '{path}'";

            var buffer = new RailBitBuffer();
            buffer.Load(new ArraySegment<byte>(File.ReadAllBytes(path)));
            PlaybackEventList = new List<MoveToEvent>();
            while (!buffer.IsFinished)
            {
                PlaybackEventList.AddRange(buffer.UnpackAll(q => q.ReadMoveToEvent()));
            }

            if (PlaybackEventList.Count == 0)
                return "Record is empty.";

            var now = CampaignTime.Now;
            firstTick = PlaybackEventList.First().time;
            if (now > firstTick)
                return $"Tick : {(long)now.ToMilliseconds} > {(long)firstTick.ToMilliseconds}\n" +
                    "Current campaign time is passed.";

            PlaybackMainPartyList = PlaybackEventList.Where(q => q.party.IsPlayerControlled()).ToList();

            RecordingEventList = new List<MoveToEvent>();
            lastTick = PlaybackEventList.Last().time;
            current = filename;
            state = MoveToState.Playback;
            MoveToPlayback += OnEventPlayback;

            return $"Playback file '{filename}' started.";
        }

        internal static string Stop()
        {
            switch (state)
            {
                case MoveToState.Stop:
                    return "Nothing happend.";

                case MoveToState.Recording:
                    MoveToRecording -= OnEventRecording;
                    state = MoveToState.Stop;

                    var buffer = new RailBitBuffer();
                    int count = RecordingEventList.Count / RailBitBuffer.MAX_LIST_COUNT;
                    for (int i = 0; i < count; i++)
                    {
                        buffer.PackAll(RecordingEventList.GetRange(i * RailBitBuffer.MAX_LIST_COUNT, RailBitBuffer.MAX_LIST_COUNT), q => buffer.WriteMoveToEvent(q));
                    }
                    buffer.PackAll(RecordingEventList.GetRange(count * RailBitBuffer.MAX_LIST_COUNT, RecordingEventList.Count % RailBitBuffer.MAX_LIST_COUNT), q => buffer.WriteMoveToEvent(q));
                    byte[] data = new byte[buffer.ByteSize + 4];
                    Array.Resize(ref data, buffer.Store(data));

                    var path = Path.Combine(logsDir, current + recordExt);
                    File.WriteAllBytes(path, data);

                    RecordingEventList = null;
                    return $"Recording stopped and exported in file '{current}'.";

                case MoveToState.Playback:
                    MoveToPlayback -= OnEventPlayback;
                    MoveToRecording -= OnEventRecording;
                    state = MoveToState.Stop;

                    VerifyEvents();

                    RecordingEventList = null;
                    PlaybackEventList = null;
                    PlaybackMainPartyList = null;
                    return $"Playback file '{current}' stopped.";

                default:
                    return null;
            }
        }

        private static void OnEventRecording(EntityId entityId, MobileParty party, MovementData movement)
        {
            RecordingEventList.Add(new MoveToEvent()
            {
                time = CampaignTime.Now,
                entityId = entityId,
                party = party,
                movement = movement
            });
            if (party.IsPlayerControlled())
                Logger.Warn("[MOVETO] Yet one player's moving recorded.");
        }

        public static void OnEventPlayback()
        {
            var now = CampaignTime.Now;

            if (firstTick <= now)
            {
                MoveToRecording += OnEventRecording;
                firstTick = CampaignTime.Never;
            }

            var moveTo = PlaybackMainPartyList.FirstOrDefault(q => !q.passed && q.time <= now);
            if (moveTo != null)
            {
                if (CoopServer.Instance.Persistence.Room.Entities.FirstOrDefault(q => q.Id == moveTo.entityId) is MobilePartyEntityServer entity)
                    entity.State.Movement = new MovementState()
                    {
                        DefaultBehavior = moveTo.movement.DefaultBehaviour,
                        Position = moveTo.movement.TargetPosition,
                        SettlementIndex = moveTo.movement.TargetSettlement != null ? moveTo.movement.TargetSettlement.Id : MovementState.InvalidIndex,
                        TargetPartyIndex = moveTo.movement.TargetParty != null ? moveTo.movement.TargetParty.Id : MovementState.InvalidIndex
                    };

                moveTo.passed = true;
                Logger.Warn("[MOVETO] Moving to new position.");
            }

            if (now >= lastTick)
            {
                Stop(); // TODO: send message in debug console instead to send on game screen
                Logger.Warn("[MOVETO] Playback has finished.");
            }
        }

        private static void VerifyEvents()
        {
            //TODO: verify events
            var difftime = new List<long?>();

            foreach (var play in PlaybackEventList)
            {
                var rec = RecordingEventList.FirstOrDefault(q => !q.passed && q.Equals(play));
                if (rec != null)
                {
                    difftime.Add((long)(rec.time - play.time).ToMilliseconds);
                    rec.passed = true;
                }
                else
                    difftime.Add(null);
            }

            var csv = difftime.Where(q => q != null).Select(q => new object[6] { q, 0, null, null, null, null }).ToList();
            var skipped = difftime.Count(q => q == null);
            var new_events = RecordingEventList.Count(q => !q.passed);

            int l_minValue = (int)difftime.Min().Value;
            int l_maxValue = (int)difftime.Max().Value;
            int minValue = Math.Sign(l_minValue) * RailUtil.Log2((ulong)Math.Abs(l_minValue));
            int maxValue = Math.Sign(l_maxValue) * RailUtil.Log2((ulong)Math.Abs(l_maxValue)) + 1;

            int colsCount = csv.Count / 5;
            int colWidth = (maxValue - minValue) / colsCount;

            csv[0][2] = skipped;
            csv[0][3] = new_events;
            csv[0][4] = l_minValue;
            csv[0][5] = l_maxValue;

            foreach (var line in csv)
            {
                long diff = ((long?)line[0]).Value;
                int index = (Math.Sign(diff) * RailUtil.Log2((ulong)Math.Abs(diff)) - minValue) / colWidth;
                csv[index][1] = (int)csv[index][1] + 1;
            }

            var report = $"time_diff(ms),cols,skipped,new_events,minValue,maxValue\r\n";
            foreach (var line in csv)
            {
                report += $"{line[0]},{line[1]},{line[2]},{line[3]},{line[4]},{line[5]}\r\n";
            }

            var path = Path.Combine(logsDir, current + logFilename);
            File.WriteAllText(path, report);

            Logger.Warn($"[MOVETO] Verifying has finished and exported into '{path}'.");
            if (skipped > 0)
                Logger.Warn($"[MOVETO] Skipped {skipped} movements.");
            if (new_events > 0)
                Logger.Warn($"[MOVETO] There is {new_events} new movements.");
        }
    }
}

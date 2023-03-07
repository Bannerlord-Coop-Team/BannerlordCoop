using System;
using System.Diagnostics;
using System.Threading;

namespace Common
{
    /// <summary>
    ///     Tries to achieve a minimum given frame time by waiting an appropriate amount of time each frame. Waiting
    ///     is either done through a spin lock or sleep, depending on the amount of time that needs to be spent waiting.
    /// </summary>
    public class FrameLimiter
    {
        /// <summary>
        ///     The elapsed time between the last call to <see cref="Throttle"/> and the call before that.
        /// </summary>
        public TimeSpan LastThrottledFrameTime { get; private set; }

        /// <summary>
        ///     Create a new frame limiter with the given target frame time.
        /// </summary>
        /// <param name="targetFrameTime"></param>
        public FrameLimiter(TimeSpan targetFrameTime)
        {
            m_TargetTicksPerFrame = targetFrameTime.Ticks;
            m_Timer = Stopwatch.StartNew();
            if (targetFrameTime == TimeSpan.Zero)
            {
                m_WaitUntilTick = tick => { };
            }
            else if (targetFrameTime > TimeSpan.FromMilliseconds(1))
            {
                m_WaitUntilTick = tick =>
                {
                    TimeSpan waitTime = TimeSpan.FromTicks(tick) - m_Timer.Elapsed;
                    if (waitTime > TimeSpan.Zero)
                    {
                        Thread.Sleep(waitTime);
                    }
                };
            }
            else
            {
                m_WaitUntilTick = tick =>
                {
                    SpinWait.SpinUntil(() => m_Timer.ElapsedTicks >= tick);
                };
            }

            m_AverageActualFrameTime = new MovingAverage(RollingBufferSize);
            m_AverageThrottledFrameTime = new MovingAverage(RollingBufferSize);
        }

        /// <summary>
        ///     Returns the average frame time measured over the last few <see cref="Throttle"/> calls.
        /// </summary>
        public TimeSpan AverageFrameTime => TimeSpan.FromTicks((long) m_AverageThrottledTicksPerFrame);

        /// <summary>
        ///     Waits until the target frame time has passed since the last call to this method. Does nothing if the
        ///     elapsed time is higher than the target time.
        /// </summary>
        public void Throttle()
        {
            long elapsedTicks = m_Timer.Elapsed.Ticks;
            m_AverageActualTicksPerFrame = m_AverageActualFrameTime.Push(elapsedTicks);
            long ticksAhead = m_TargetTicksPerFrame - (long) m_AverageActualTicksPerFrame;
            if (ticksAhead > 0)
            {
                m_WaitUntilTick(elapsedTicks + ticksAhead);
            }
            LastThrottledFrameTime = m_Timer.Elapsed;
            m_AverageThrottledTicksPerFrame = m_AverageThrottledFrameTime.Push(LastThrottledFrameTime.Ticks);
            m_Timer.Restart();
        }
        
        #region Private
        private const int RollingBufferSize = 32;
        private readonly MovingAverage m_AverageActualFrameTime;
        private readonly MovingAverage m_AverageThrottledFrameTime;
        private readonly long m_TargetTicksPerFrame;
        private readonly Stopwatch m_Timer;
        private readonly Action<long> m_WaitUntilTick;
        private double m_AverageActualTicksPerFrame;
        private double m_AverageThrottledTicksPerFrame;
        #endregion
    }
}

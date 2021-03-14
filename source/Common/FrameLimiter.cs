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
        public TimeSpan LastFrameTime;

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

            m_Avg = new MovingAverage(32);
        }

        /// <summary>
        ///     Returns the average frame time measured over the last few <see cref="Throttle"/> calls.
        /// </summary>
        public TimeSpan AverageFrameTime => TimeSpan.FromTicks((long) m_AverageTicksPerFrame);

        /// <summary>
        ///     Waits until the target frame time has passed since the last call to this method. Does nothing if the
        ///     elapsed time is higher than the target time.
        /// </summary>
        public void Throttle()
        {
            long elapsedTicks = m_Timer.Elapsed.Ticks;
            m_AverageTicksPerFrame = m_Avg.Push(elapsedTicks);
            if (m_AverageTicksPerFrame < m_TargetTicksPerFrame)
            {
                m_WaitUntilTick(
                    elapsedTicks + (m_TargetTicksPerFrame - (long) m_AverageTicksPerFrame));
            }

            LastFrameTime = m_Timer.Elapsed;
            m_Timer.Restart();
        }
        
        #region Private
        private readonly MovingAverage m_Avg;
        private readonly long m_TargetTicksPerFrame;
        private readonly Stopwatch m_Timer;
        private readonly Action<long> m_WaitUntilTick;
        private double m_AverageTicksPerFrame;
        #endregion
    }
}

using System;

namespace Common
{
    /// <summary>
    ///     Something that expects an update call in regular intervals.
    /// </summary>
    public interface IUpdateable
    {
        /// <summary>
        ///     Execute the update.
        /// </summary>
        /// <param name="frameTime">Time elapsed since the last call to this function.</param>
        void Update(TimeSpan frameTime);
        /// <summary>
        ///     The priority of this updateable. Highest priority is executed first. 
        /// </summary>
        int Priority { get; }
    }
}

namespace Common.LogicStates
{
    public interface ILogic
    {
        void Start();
        void Stop();

        bool RunningState { get; }
    }
}

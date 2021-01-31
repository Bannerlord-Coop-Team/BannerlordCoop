namespace Sync.Behaviour
{
    public abstract class ActionBehaviour
    {
        public delegate bool IsApplicableDelegate(EOriginator eOrigin, object instance);
        
        public ActionBehaviour(IsApplicableDelegate decider)
        {
            m_Decider = decider;
        }
        public bool DoesBehaviourApply(EOriginator origin, object instance)
        {
            return m_Decider == null || m_Decider(origin, instance);
        }
        
        #region Private
        private IsApplicableDelegate m_Decider;
        #endregion
    }
}
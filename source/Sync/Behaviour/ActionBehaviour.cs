namespace Sync.Behaviour
{
    public abstract class ActionBehaviour
    {
        public ActionBehaviour(Condition condition)
        {
            m_Condition = condition;
        }
        public bool DoesBehaviourApply(EOriginator origin, object instance)
        {
            return m_Condition == null || m_Condition.Evaluate(origin, instance);
        }
        
        #region Private
        private Condition m_Condition;
        #endregion
    }
}
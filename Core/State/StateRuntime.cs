namespace BBBNexus
{
    /// <summary>
    /// 状态运行时值（数据层）。
    /// </summary>
    public sealed class StateRuntime
    {
        public double Current { get; private set; }
        public bool Dirty { get; private set; }
        public long Version { get; private set; }

        public StateRuntime(double initialValue)
        {
            Current = initialValue;
            Dirty = false;
            Version = 0;
        }

        public void Set(double value)
        {
            if (Current.Equals(value))
            {
                return;
            }

            Current = value;
            Dirty = true;
            Version++;
        }

        public void ClearDirty()
        {
            Dirty = false;
        }
    }
}

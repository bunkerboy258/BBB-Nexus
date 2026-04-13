namespace BBBNexus
{
    /// <summary>
    /// 已停用：BBB 内不再承载血量逻辑。
    /// </summary>
    public class HealthArbiter
    {
        public HealthArbiter(BBBCharacterController player)
        {
        }

        internal void Enqueue(in DamageRequest request)
        {
        }

        public void Arbitrate()
        {
        }

        public bool TryHeal(float amount)
        {
            return false;
        }
    }
}

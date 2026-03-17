using BBBHe.Core.Combat; // 【核心改动】：只引入公共战斗接口，彻底剥离对 Player 命名空间的依赖
using UnityEngine;

// 简单投射物脚本 处理子弹 炮弹等投射物的碰撞反应与销毁 
// 支持碰撞与触发器两种检测方式 生成击中特效与音效 
public class SimpleProjectile : MonoBehaviour
{
    [Header("Hit Effects")]
    // 击中时播放的特效预制体 会在击中点瞄准击中面生成 
    public GameObject hitVFXPrefab;
    // 击中时播放的音效 由开火脚本注入 
    public AudioClip hitSound;

    [Header("Settings")]
    // 投射物的生存时长 防止无限漂流在地图上导致内存泄漏 
    public float lifeTime = 5f;

    // 是否在命中时触发爆炸效果（爆炸会对周围刚体施加冲击力）
    [Tooltip("命中时是否触发爆炸并对周围刚体施加冲击力")]
    public bool ExplodeOnImpact = false;
    [Tooltip("爆炸影响半径（米）")]
    public float ExplosionRadius = 5f;
    [Tooltip("爆炸施加的最大冲击力")]
    public float ExplosionForce = 700f;
    [Tooltip("爆炸向上修正，越大效果越向上抬起")]
    public float ExplosionUpwardsModifier = 0.0f;
    [Tooltip("哪些层会受爆炸影响（LayerMask）")]
    public LayerMask ExplosionAffectLayers = ~0; // 默认影响所有层

    [Header("Damage Settings")]
    // 是否在命中时对爆炸范围内的指定标签角色发送伤害请求
    [Tooltip("命中时是否对爆炸范围内的指定标签角色发送伤害请求")]
    public bool DealDamageOnImpact = false;

    [Tooltip("爆炸造成的伤害量")]
    public float DamageAmount = 10f;

    // 防止重复触发碰撞 确保只处理一次击中 避免多次播放特效与音效 
    private bool _hasImpacted = false;

    // 初始化 设置生存时间 
    private void Start()
    {
        // 使用生命周期销毁 当投射物没有击中任何东西时 经过指定时长后自动销毁 
        // 这防止了无限飘浮的僵尸投射物占用内存 
        Destroy(gameObject, lifeTime);
    }

    // 碰撞检测 用于有刚体的碰撞体 
    private void OnCollisionEnter(Collision collision)
    {
        // 已经击中过则直接返回 
        if (_hasImpacted) return;

        // 获取第一个碰撞接触点 包含击中位置与法线方向 
        ContactPoint contact = collision.GetContact(0);

        // 统一处理击中事件 
        HandleImpact(contact.point, contact.normal, collision.collider);
    }

    // 触发器检测 用于没有刚体但设置了 IsTrigger 的碰撞体 
    private void OnTriggerEnter(Collider other)
    {
        // 已经击中过则直接返回 
        if (_hasImpacted) return;

        // 触发器没有 ContactPoint 所以需要手动计算击中位置与法线 
        // ClosestPoint 返回碰撞体上最接近投射物的点 
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        // 法线是从接触点指向投射物的方向 代表投射物撞向碰撞体的方向 
        Vector3 contactNormal = (transform.position - contactPoint).normalized;

        // 如果法线计算失败 使用投射物的飞行方向作为备选法线 
        if (contactNormal.sqrMagnitude < 1e-6f)
        {
            // 投射物的反向就是击中面的法向 这样特效会正确指向 
            contactNormal = -transform.forward;
        }

        // 统一处理击中事件 
        HandleImpact(contactPoint, contactNormal, other);
    }

    // 统一处理击中逻辑 生成特效与音效 标记已击中 销毁自身 
    private void HandleImpact(Vector3 point, Vector3 normal, Collider other)
    {
        // 双重防护 确保只处理一次 
        if (_hasImpacted) return;
        _hasImpacted = true;

        // 生成击中视觉特效 
        if (hitVFXPrefab != null)
        {
            Instantiate(hitVFXPrefab, point, Quaternion.LookRotation(normal));
        }

        // 生成击中音效 
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, point);
        }

        // 1. 物理层：爆炸对周围刚体施加冲击力
        if (ExplodeOnImpact)
        {
            ApplyExplosionPhysics(point);
        }

        // 2. 逻辑层：处理伤害
        if (DealDamageOnImpact && DamageAmount > 0f)
        {
            // 如果是爆炸范围伤害 (AOE)
            ApplyDamageToTargets(point);
        }
        else if (DamageAmount > 0f && other != null)
        {
            IDamageable directTarget = other.GetComponentInParent<IDamageable>();
            if (directTarget != null)
            {
                var req = new DamageRequest(DamageAmount);
                directTarget.RequestDamage(in req);
            }
        }

        // 销毁投射物实例 
        Destroy(gameObject);
    }

    // 物理力施加提取为一个独立方法，保持代码整洁
    private void ApplyExplosionPhysics(Vector3 explosionCenter)
    {
        Collider[] hits = Physics.OverlapSphere(explosionCenter, ExplosionRadius, ExplosionAffectLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        System.Collections.Generic.HashSet<Rigidbody> affected = new System.Collections.Generic.HashSet<Rigidbody>();

        foreach (var col in hits)
        {
            if (col == null) continue;
            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;
            if (affected.Contains(rb)) continue;

            rb.AddExplosionForce(ExplosionForce, explosionCenter, ExplosionRadius, ExplosionUpwardsModifier, ForceMode.Impulse);
            affected.Add(rb);
        }
    }

    // 【核心重构】：向爆炸范围内的对象发送伤害请求（完全基于接口）
    private void ApplyDamageToTargets(Vector3 explosionCenter)
    {
        // 查询爆炸范围内的所有碰撞体
        Collider[] targetColliders = Physics.OverlapSphere(explosionCenter, ExplosionRadius);

        if (targetColliders == null || targetColliders.Length == 0)
            return;

        System.Collections.Generic.HashSet<IDamageable> damagedTargets = new System.Collections.Generic.HashSet<IDamageable>();

        foreach (var collider in targetColliders)
        {
            if (collider == null) continue;

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            if (damagedTargets.Contains(damageable))
                continue;

            var damageRequest = new DamageRequest(DamageAmount);
            damageable.RequestDamage(in damageRequest);

            damagedTargets.Add(damageable);
        }
    }
}
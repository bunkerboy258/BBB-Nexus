using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    public static class AttackClipGeometryTemplateFactory
    {
        private const int DefaultSampleRate = 30;

#if UNITY_EDITOR
        /// <summary>
        /// 从角色 Prefab + 武器 Hitbox 上的 Collider + AnimationClip 真实采样生成攻击几何定义。
        /// 唯一权威采样入口：策划摆放的 Collider 就是数据源。
        /// </summary>
        public static AttackClipGeometryDefinition CreateFromClipSampling(
            GameObject characterPrefab,
            GameObject weaponPrefab,
            string weaponId,
            string displayName,
            AnimationClip[] comboClips,
            FistsDamageWindowSidecar[] damageWindows,
            int sampleRate = DefaultSampleRate,
            EquipmentSlot mountSlot = EquipmentSlot.MainHand,
            Vector3 holdPositionOffset = default,
            Quaternion holdRotationOffset = default,
            bool applyHoldOffset = false,
            bool useRootTransformForSweep = true)
        {
            if (characterPrefab == null)
                throw new ArgumentNullException(nameof(characterPrefab));
            if (comboClips == null || comboClips.Length == 0)
                throw new ArgumentException("ComboClips is null or empty.", nameof(comboClips));

            // 实例化临时角色
            GameObject tempCharacter = UnityEngine.Object.Instantiate(characterPrefab, Vector3.zero, Quaternion.identity);
            tempCharacter.hideFlags = HideFlags.HideAndDontSave;

            // 找到武器挂点上的 FistHitbox，如果 Prefab 上自带就直接用，否则实例化武器 Prefab
            FistHitbox hitbox = tempCharacter.GetComponentInChildren<FistHitbox>();
            GameObject tempWeapon = null;

            if (hitbox == null && weaponPrefab != null)
            {
                // 尽量模拟运行时挂载链路（容器 -> 手骨），避免扫掠轨迹偏移。
                Transform mount = ResolveWeaponMountTransform(tempCharacter, mountSlot);
                tempWeapon = mount != null
                    ? UnityEngine.Object.Instantiate(weaponPrefab, mount, false)
                    : UnityEngine.Object.Instantiate(weaponPrefab, tempCharacter.transform, false);
                tempWeapon.hideFlags = HideFlags.HideAndDontSave;
                if (applyHoldOffset)
                {
                    tempWeapon.transform.localPosition = holdPositionOffset;
                    tempWeapon.transform.localRotation = IsZeroQuaternion(holdRotationOffset)
                        ? Quaternion.identity
                        : holdRotationOffset;
                }

                hitbox = tempWeapon.GetComponentInChildren<FistHitbox>();
            }

            if (hitbox == null)
            {
                CleanupTemp(tempCharacter, tempWeapon);
                Debug.LogError("[AttackClipGeometryTemplateFactory] No FistHitbox found on character or weapon prefab.");
                return null;
            }

            IReadOnlyList<Collider> colliders = hitbox.GetDetectionColliders();
            if (colliders == null || colliders.Count == 0)
            {
                CleanupTemp(tempCharacter, tempWeapon);
                Debug.LogError("[AttackClipGeometryTemplateFactory] FistHitbox has no detection colliders.");
                return null;
            }

            Animator animator =
                tempCharacter.GetComponent<Animator>() ??
                tempCharacter.GetComponentInChildren<Animator>(true);
            Transform hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform root = tempCharacter.transform;

            var definition = new AttackClipGeometryDefinition
            {
                WeaponId = weaponId,
                DisplayName = displayName ?? characterPrefab.name,
                Clips = new List<AttackClipGeometryClipDefinition>(),
            };

            try
            {
                for (int comboIndex = 0; comboIndex < comboClips.Length; comboIndex++)
                {
                    AnimationClip clip = comboClips[comboIndex];
                    if (clip == null)
                    {
                        definition.Clips.Add(new AttackClipGeometryClipDefinition
                        {
                            ComboIndex = comboIndex,
                            DisplayName = $"Combo {comboIndex + 1} (null clip)",
                            UseRootTransformForSweep = useRootTransformForSweep,
                            Samples = new List<AttackGeometrySampleDefinition>(),
                        });
                        continue;
                    }

                    // 收集所有伤害窗口区间
                    List<(float start, float end)> windowRanges =
                        BuildSamplingRanges(damageWindows, comboIndex);

                    // 计算全局采样范围（用于 SweepProgressNormalized）
                    float globalMinNorm = 1f, globalMaxNorm = 0f;
                    for (int wi = 0; wi < windowRanges.Count; wi++)
                    {
                        globalMinNorm = Mathf.Min(globalMinNorm, windowRanges[wi].start);
                        globalMaxNorm = Mathf.Max(globalMaxNorm, windowRanges[wi].end);
                    }
                    float globalDuration = (globalMaxNorm - globalMinNorm) * clip.length;
                    float referenceTime = Mathf.Clamp(globalMinNorm, 0f, 1f) * clip.length;

                    SampleRootMotionSource(
                        clip,
                        tempCharacter,
                        root,
                        animator,
                        hips,
                        referenceTime,
                        out Vector3 referenceRootPosition,
                        out Quaternion referenceRootRotation,
                        out RootMotionSource rootMotionSource);

                    int effectiveRate = Mathf.Max(1, sampleRate);
                    if (clip.frameRate > 0f)
                        effectiveRate = Mathf.Max(effectiveRate, Mathf.RoundToInt(clip.frameRate));

                    var rootMotionSamples = new List<AttackRootMotionSampleDefinition>();
                    int rootSampleCount = Mathf.Max(2, Mathf.CeilToInt(clip.length * effectiveRate) + 1);
                    for (int sampleIndex = 0; sampleIndex < rootSampleCount; sampleIndex++)
                    {
                        float normalized = rootSampleCount <= 1 ? 0f : sampleIndex / (float)(rootSampleCount - 1);
                        float sampleTime = normalized * clip.length;

                        SampleRootMotionSource(
                            clip,
                            tempCharacter,
                            root,
                            animator,
                            hips,
                            sampleTime,
                            out Vector3 sampledRootPosition,
                            out Quaternion sampledRootRotation,
                            out _);

                        Vector3 rootLocalPosition = Quaternion.Inverse(referenceRootRotation) * (sampledRootPosition - referenceRootPosition);
                        float rootLocalRotationY =
                            Mathf.DeltaAngle(referenceRootRotation.eulerAngles.y, sampledRootRotation.eulerAngles.y);

                        rootMotionSamples.Add(new AttackRootMotionSampleDefinition
                        {
                            ClipProgressNormalized = normalized,
                            RootLocalPosition = rootLocalPosition,
                            RootLocalRotationY = rootLocalRotationY,
                        });
                    }

                    if (rootMotionSamples.Count > 1)
                    {
                        Vector3 totalRootDelta = (Vector3)rootMotionSamples[rootMotionSamples.Count - 1].RootLocalPosition;
                        if (totalRootDelta.sqrMagnitude <= 0.000001f &&
                            Mathf.Abs(rootMotionSamples[rootMotionSamples.Count - 1].RootLocalRotationY) <= 0.001f)
                        {
                            Debug.LogWarning(
                                $"[AttackClipGeometryTemplateFactory] Clip '{clip.name}' baked zero root motion. source={rootMotionSource} humanoid={(animator != null && animator.avatar != null && animator.avatar.isHuman)}",
                                tempCharacter);
                        }
                    }

                    float deltaTime = 1f / effectiveRate;
                    var samples = new List<AttackGeometrySampleDefinition>();

                    // 空间均匀采样：根据 Collider 尺寸决定采样密度
                    // 原理：采样间距应该小于 Collider 尺寸，这样才能保证命中检测连续
                    const float CoverageFactor = 0.5f; // 采样间距 = Collider 半径 × CoverageFactor

                    // 计算最小 Collider 尺寸（取所有碰撞体的最小半径）
                    // 原因：如果武器上有多个 Collider（如大锤 + 小尖刺），必须保证最小的那个也能被覆盖
                    //       否则小碰撞体会从采样点之间漏过去！
                    float minColliderRadius = 0.1f; // 默认值
                    if (colliders.Count > 0)
                    {
                        float minRadius = float.MaxValue;
                        for (int ci = 0; ci < colliders.Count; ci++)
                        {
                            Collider col = colliders[ci];
                            if (col == null) continue;

                            float radius = GetColliderHorizontalRadius(col);
                            if (radius > 0.001f && radius < minRadius)
                            {
                                minRadius = radius;
                            }
                        }
                        if (minRadius < float.MaxValue)
                        {
                            minColliderRadius = minRadius;
                        }
                    }

                    // 采样间距 = 最小 Collider 半径 × CoverageFactor（保证最小的碰撞体也能被覆盖）
                    // 不限制上下限：大锤就大间距，匕首就小间距
                    float targetSampleSpacing = minColliderRadius * CoverageFactor;

                    // 对每个窗口分别采样
                    for (int wi = 0; wi < windowRanges.Count; wi++)
                    {
                        float startTime = windowRanges[wi].start * clip.length;
                        float endTime = windowRanges[wi].end * clip.length;

                        // 第一步：密集预采样，记录轨迹
                        float preSampleDelta = deltaTime / 3f;
                        var preSamples = new List<(float time, Vector3 localPos)>();
                        
                        for (float t = startTime; t <= endTime; t += preSampleDelta)
                        {
                            float clampedT = Mathf.Min(t, endTime);
                            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                            clip.SampleAnimation(tempCharacter, clampedT);

                            // 取第一个碰撞体的中心作为武器位置代表
                            Collider col = colliders[0];
                            if (col != null)
                            {
                                Vector3 worldPos = col.transform.TransformPoint(GetColliderCenter(col));
                                Vector3 localPos = root.InverseTransformPoint(worldPos);
                                preSamples.Add((clampedT, localPos));
                            }
                        }

                        // 确保终点被采样
                        if (preSamples.Count == 0 || preSamples[preSamples.Count - 1].time < endTime - 0.001f)
                        {
                            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                            clip.SampleAnimation(tempCharacter, endTime);
                            
                            Collider col = colliders[0];
                            if (col != null)
                            {
                                Vector3 worldPos = col.transform.TransformPoint(GetColliderCenter(col));
                                Vector3 localPos = root.InverseTransformPoint(worldPos);
                                preSamples.Add((endTime, localPos));
                            }
                        }

                        // 第二步：计算总轨迹长度
                        float totalDistance = 0f;
                        for (int i = 1; i < preSamples.Count; i++)
                        {
                            totalDistance += Vector3.Distance(preSamples[i - 1].localPos, preSamples[i].localPos);
                        }

                        // 第三步：根据空间距离选择采样点
                        var selectedTimes = new List<float>();
                        if (preSamples.Count > 0)
                        {
                            selectedTimes.Add(preSamples[0].time); // 起点必须选

                            if (totalDistance > targetSampleSpacing)
                            {
                                // 按空间距离选择采样点
                                float accumulatedDistance = 0f;
                                float nextThreshold = targetSampleSpacing;

                                for (int i = 1; i < preSamples.Count; i++)
                                {
                                    float segmentDist = Vector3.Distance(preSamples[i - 1].localPos, preSamples[i].localPos);
                                    accumulatedDistance += segmentDist;

                                    if (accumulatedDistance >= nextThreshold - 0.001f)
                                    {
                                        selectedTimes.Add(preSamples[i].time);
                                        nextThreshold += targetSampleSpacing;
                                    }
                                }

                                // 确保终点被选中
                                if (selectedTimes[selectedTimes.Count - 1] < endTime - 0.001f)
                                {
                                    selectedTimes.Add(endTime);
                                }
                            }
                            else
                            {
                                // 轨迹太短，只取起点和终点
                                if (preSamples.Count > 1)
                                {
                                    selectedTimes.Add(preSamples[preSamples.Count - 1].time);
                                }
                            }
                        }

                        // 第四步：用选中的时间点生成最终的采样数据
                        foreach (float selectedTime in selectedTimes)
                        {
                            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                            clip.SampleAnimation(tempCharacter, selectedTime);

                            Vector3 sweepReferencePosition = useRootTransformForSweep ? referenceRootPosition : root.position;
                            Quaternion sweepReferenceRotation = useRootTransformForSweep ? referenceRootRotation : root.rotation;

                            var shapes = new List<AttackGeometryShapeDefinition>();
                            for (int ci = 0; ci < colliders.Count; ci++)
                            {
                                Collider col = colliders[ci];
                                if (col == null) continue;
                                shapes.Add(ColliderToShapeDefinition(col, sweepReferencePosition, sweepReferenceRotation));
                            }

                            float sweepProgress = globalDuration > 0.0001f
                                ? Mathf.Clamp01((selectedTime - globalMinNorm * clip.length) / globalDuration)
                                : 0f;

                            Vector3 rootLocalPosition = Quaternion.Inverse(referenceRootRotation) * (root.position - referenceRootPosition);
                            float rootLocalRotationY =
                                Mathf.DeltaAngle(referenceRootRotation.eulerAngles.y, root.rotation.eulerAngles.y);

                            samples.Add(new AttackGeometrySampleDefinition
                            {
                                SweepProgressNormalized = sweepProgress,
                                RootLocalPosition = useRootTransformForSweep ? rootLocalPosition : Vector3.zero,
                                RootLocalRotationY = useRootTransformForSweep ? rootLocalRotationY : 0f,
                                Shapes = shapes,
                            });
                        }
                    }

                    definition.Clips.Add(new AttackClipGeometryClipDefinition
                    {
                        ComboIndex = comboIndex,
                        DisplayName = $"Combo {comboIndex + 1}",
                        UseRootTransformForSweep = useRootTransformForSweep,
                        Samples = samples,
                        RootMotionSamples = rootMotionSamples,
                    });
                }
            }
            finally
            {
                CleanupTemp(tempCharacter, tempWeapon);
            }

            return definition;
        }

        private static AttackGeometryShapeDefinition ColliderToShapeDefinition(
            Collider collider,
            Vector3 referenceRootPosition,
            Quaternion referenceRootRotation)
        {
            switch (collider)
            {
                case SphereCollider sphere:
                {
                    Vector3 worldCenter = sphere.transform.TransformPoint(sphere.center);
                    Vector3 localPos = Quaternion.Inverse(referenceRootRotation) * (worldCenter - referenceRootPosition);
                    float maxScale = Mathf.Max(
                        Mathf.Abs(sphere.transform.lossyScale.x),
                        Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.y),
                            Mathf.Abs(sphere.transform.lossyScale.z)));

                    return new AttackGeometryShapeDefinition
                    {
                        ShapeType = AttackGeometryShapeType.Sphere,
                        LocalPosition = new SerializableVector3(localPos.x, localPos.y, localPos.z),
                        LocalEulerAngles = new SerializableVector3(0f, 0f, 0f),
                        Radius = sphere.radius * maxScale,
                        Height = 0f,
                        HalfExtents = new SerializableVector3(0f, 0f, 0f),
                    };
                }

                case CapsuleCollider capsule:
                {
                    Vector3 worldCenter = capsule.transform.TransformPoint(capsule.center);
                    Vector3 localPos = Quaternion.Inverse(referenceRootRotation) * (worldCenter - referenceRootPosition);
                    Quaternion localRot = Quaternion.Inverse(referenceRootRotation) * capsule.transform.rotation;
                    Vector3 localEuler = localRot.eulerAngles;

                    Vector3 lossyScale = capsule.transform.lossyScale;
                    int dir = capsule.direction;
                    float axisScale = dir switch
                    {
                        0 => Mathf.Abs(lossyScale.x),
                        1 => Mathf.Abs(lossyScale.y),
                        _ => Mathf.Abs(lossyScale.z),
                    };
                    float radiusScale = dir switch
                    {
                        0 => Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)),
                        1 => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)),
                        _ => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y)),
                    };

                    return new AttackGeometryShapeDefinition
                    {
                        ShapeType = AttackGeometryShapeType.Capsule,
                        LocalPosition = new SerializableVector3(localPos.x, localPos.y, localPos.z),
                        LocalEulerAngles = new SerializableVector3(localEuler.x, localEuler.y, localEuler.z),
                        Radius = capsule.radius * radiusScale,
                        Height = capsule.height * axisScale,
                        HalfExtents = new SerializableVector3(0f, 0f, 0f),
                    };
                }

                case BoxCollider box:
                {
                    Vector3 worldCenter = box.transform.TransformPoint(box.center);
                    Vector3 localPos = Quaternion.Inverse(referenceRootRotation) * (worldCenter - referenceRootPosition);
                    Quaternion localRot = Quaternion.Inverse(referenceRootRotation) * box.transform.rotation;
                    Vector3 localEuler = localRot.eulerAngles;

                    Vector3 lossyScale = box.transform.lossyScale;
                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, lossyScale);
                    halfExtents.x = Mathf.Abs(halfExtents.x);
                    halfExtents.y = Mathf.Abs(halfExtents.y);
                    halfExtents.z = Mathf.Abs(halfExtents.z);

                    return new AttackGeometryShapeDefinition
                    {
                        ShapeType = AttackGeometryShapeType.Box,
                        LocalPosition = new SerializableVector3(localPos.x, localPos.y, localPos.z),
                        LocalEulerAngles = new SerializableVector3(localEuler.x, localEuler.y, localEuler.z),
                        Radius = 0f,
                        Height = 0f,
                        HalfExtents = new SerializableVector3(halfExtents.x, halfExtents.y, halfExtents.z),
                    };
                }

                default:
                {
                    // 回退：用 bounds
                    Bounds bounds = collider.bounds;
                    Vector3 localPos = Quaternion.Inverse(referenceRootRotation) * (bounds.center - referenceRootPosition);

                    return new AttackGeometryShapeDefinition
                    {
                        ShapeType = AttackGeometryShapeType.Box,
                        LocalPosition = new SerializableVector3(localPos.x, localPos.y, localPos.z),
                        LocalEulerAngles = new SerializableVector3(0f, 0f, 0f),
                        Radius = 0f,
                        Height = 0f,
                        HalfExtents = new SerializableVector3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
                    };
                }
            }
        }

        private enum RootMotionSource
        {
            RootTransform,
            Body,
            Hips,
        }

        private static void SampleRootMotionSource(
            AnimationClip clip,
            GameObject target,
            Transform root,
            Animator animator,
            Transform hips,
            float time,
            out Vector3 rootPosition,
            out Quaternion rootRotation,
            out RootMotionSource source)
        {
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clip.SampleAnimation(target, time);

            Vector3 transformPosition = root.position;
            Quaternion transformRotation = root.rotation;

            Vector3 bodyPosition = transformPosition;
            Quaternion bodyRotation = transformRotation;
            bool hasBody = animator != null && animator.avatar != null && animator.avatar.isHuman;
            if (hasBody)
            {
                bodyPosition = animator.bodyPosition;
                bodyRotation = animator.bodyRotation;
            }

            Vector3 hipsPosition = transformPosition;
            Quaternion hipsRotation = transformRotation;
            bool hasHips = hips != null;
            if (hasHips)
            {
                hipsPosition = hips.position;
                hipsRotation = hips.rotation;
            }

            if (hasBody && (bodyPosition.sqrMagnitude > 0.000001f || Quaternion.Angle(bodyRotation, Quaternion.identity) > 0.001f))
            {
                rootPosition = bodyPosition;
                rootRotation = bodyRotation;
                source = RootMotionSource.Body;
                return;
            }

            if (hasHips && (hipsPosition.sqrMagnitude > 0.000001f || Quaternion.Angle(hipsRotation, Quaternion.identity) > 0.001f))
            {
                rootPosition = hipsPosition;
                rootRotation = hipsRotation;
                source = RootMotionSource.Hips;
                return;
            }

            rootPosition = transformPosition;
            rootRotation = transformRotation;
            source = RootMotionSource.RootTransform;
        }

        private static void CleanupTemp(GameObject tempCharacter, GameObject tempWeapon)
        {
            if (tempWeapon != null) UnityEngine.Object.DestroyImmediate(tempWeapon);
            if (tempCharacter != null) UnityEngine.Object.DestroyImmediate(tempCharacter);
        }

        private static List<(float start, float end)> BuildSamplingRanges(
            FistsDamageWindowSidecar[] damageWindows,
            int comboIndex)
        {
            var ranges = new List<(float start, float end)>();

            if (damageWindows != null &&
                comboIndex >= 0 &&
                comboIndex < damageWindows.Length &&
                damageWindows[comboIndex].Enabled)
            {
                var dw = damageWindows[comboIndex];
                for (int wi = 0; wi < dw.WindowCount; wi++)
                {
                    dw.GetWindow(wi, out float ws, out float we);
                    ws = Mathf.Clamp01(ws);
                    we = Mathf.Clamp01(we);
                    if (we < ws)
                        (ws, we) = (we, ws);
                    ranges.Add((ws, we));
                }
            }
            else
            {
                ranges.Add((0f, 1f));
            }

            if (ranges.Count <= 1)
                return ranges;

            ranges.Sort((a, b) => a.start.CompareTo(b.start));
            var merged = new List<(float start, float end)>(ranges.Count);
            (float start, float end) current = ranges[0];

            for (int i = 1; i < ranges.Count; i++)
            {
                (float start, float end) next = ranges[i];
                if (next.start <= current.end + 0.0001f)
                {
                    current.end = Mathf.Max(current.end, next.end);
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }

            merged.Add(current);
            return merged;
        }

        private static Transform ResolveWeaponMountTransform(GameObject tempCharacter, EquipmentSlot mountSlot)
        {
            if (tempCharacter == null)
                return null;

            BBBCharacterController controller =
                tempCharacter.GetComponent<BBBCharacterController>() ??
                tempCharacter.GetComponentInChildren<BBBCharacterController>(true);
            if (controller != null)
            {
                Transform fromContainer = mountSlot == EquipmentSlot.OffHand
                    ? controller.OffhandWeaponContainer
                    : controller.MainhandWeaponContainer;
                if (fromContainer != null)
                    return fromContainer;
            }

            Animator animator =
                tempCharacter.GetComponent<Animator>() ??
                tempCharacter.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                HumanBodyBones handBone = mountSlot == EquipmentSlot.OffHand
                    ? HumanBodyBones.LeftHand
                    : HumanBodyBones.RightHand;
                Transform hand = animator.GetBoneTransform(handBone);
                if (hand != null)
                    return hand;
            }

            return tempCharacter.transform;
        }

        private static bool IsZeroQuaternion(Quaternion q)
        {
            return Mathf.Approximately(q.x, 0f) &&
                   Mathf.Approximately(q.y, 0f) &&
                   Mathf.Approximately(q.z, 0f) &&
                   Mathf.Approximately(q.w, 0f);
        }

        private static Vector3 GetColliderCenter(Collider col)
        {
            switch (col)
            {
                case SphereCollider sphere:
                    return sphere.center;
                case CapsuleCollider capsule:
                    return capsule.center;
                case BoxCollider box:
                    return box.center;
                default:
                    return col.bounds.center - col.transform.position;
            }
        }

        private static float GetColliderHorizontalRadius(Collider col)
        {
            switch (col)
            {
                case SphereCollider sphere:
                    return sphere.radius * Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x), Mathf.Abs(sphere.transform.lossyScale.z));

                case CapsuleCollider capsule:
                    return capsule.radius * Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.z));

                case BoxCollider box:
                    Vector3 lossyScale = box.transform.lossyScale;
                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, lossyScale);
                    return new Vector2(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.z)).magnitude;

                default:
                    return Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }
        }
#endif

        // ─────────────────────────────────────────────────────
        // 旧硬编码模板（保留兼容，标记过时）
        // ─────────────────────────────────────────────────────

        private const float DefaultCoverageFactor = 0.72f;

        private readonly struct SphereAnchor
        {
            public readonly Vector3 Position;
            public readonly float Radius;

            public SphereAnchor(float x, float y, float z, float radius)
            {
                Position = new Vector3(x, y, z);
                Radius = radius;
            }
        }

        [Obsolete("Use CreateFromClipSampling instead. Hardcoded templates do not reflect actual collider/animation data.")]
        public static AttackClipGeometryDefinition CreateForWeapon(WeaponSO weapon)
        {
            var definition = new AttackClipGeometryDefinition
            {
                WeaponId = weapon.ItemID,
                DisplayName = string.IsNullOrWhiteSpace(weapon.DisplayName) ? weapon.name : weapon.DisplayName,
                Clips = new List<AttackClipGeometryClipDefinition>(),
            };

            int comboCount = weapon.ComboSequence != null ? weapon.ComboSequence.Length : 0;
            for (int i = 0; i < comboCount; i++)
            {
                definition.Clips.Add(new AttackClipGeometryClipDefinition
                {
                    ComboIndex = i,
                    DisplayName = $"Combo {i + 1}",
                    UseRootTransformForSweep = true,
                    Samples = BuildDefaultSamplesForClip(i),
                });
            }

            return definition;
        }

        private static List<AttackGeometrySampleDefinition> BuildDefaultSamplesForClip(int comboIndex)
        {
            int patternIndex = comboIndex % 3;
            return patternIndex switch
            {
                0 => BuildForwardJabPattern(),
                1 => BuildCrossBodyPattern(),
                _ => BuildWideSwingPattern(),
            };
        }

        private static List<AttackGeometrySampleDefinition> BuildForwardJabPattern()
        {
            return BuildSphereSweep(
                new SphereAnchor(0.04f, 0.98f, 0.24f, 0.13f),
                new SphereAnchor(0.12f, 1.07f, 0.58f, 0.15f),
                new SphereAnchor(0.10f, 1.10f, 0.88f, 0.17f));
        }

        private static List<AttackGeometrySampleDefinition> BuildCrossBodyPattern()
        {
            return BuildSphereSweep(
                new SphereAnchor(-0.24f, 0.98f, 0.22f, 0.13f),
                new SphereAnchor(-0.04f, 1.10f, 0.60f, 0.15f),
                new SphereAnchor(0.20f, 1.12f, 0.80f, 0.16f));
        }

        private static List<AttackGeometrySampleDefinition> BuildWideSwingPattern()
        {
            return BuildSphereSweep(
                new SphereAnchor(0.30f, 1.00f, 0.18f, 0.13f),
                new SphereAnchor(0.04f, 1.10f, 0.52f, 0.16f),
                new SphereAnchor(-0.30f, 1.06f, 0.78f, 0.18f));
        }

        private static List<AttackGeometrySampleDefinition> BuildSphereSweep(params SphereAnchor[] anchors)
        {
            var samples = new List<AttackGeometrySampleDefinition>();
            if (anchors == null || anchors.Length == 0)
            {
                return samples;
            }

            if (anchors.Length == 1)
            {
                samples.Add(BuildSphereSample(0f, anchors[0].Position, anchors[0].Radius));
                return samples;
            }

            float totalLength = 0f;
            for (int i = 0; i < anchors.Length - 1; i++)
            {
                totalLength += Vector3.Distance(anchors[i].Position, anchors[i + 1].Position);
            }

            if (totalLength <= 0.0001f)
            {
                samples.Add(BuildSphereSample(0f, anchors[0].Position, anchors[0].Radius));
                samples.Add(BuildSphereSample(1f, anchors[^1].Position, anchors[^1].Radius));
                return samples;
            }

            float traversedLength = 0f;
            samples.Add(BuildSphereSample(0f, anchors[0].Position, anchors[0].Radius));

            for (int segmentIndex = 0; segmentIndex < anchors.Length - 1; segmentIndex++)
            {
                SphereAnchor from = anchors[segmentIndex];
                SphereAnchor to = anchors[segmentIndex + 1];
                float segmentLength = Vector3.Distance(from.Position, to.Position);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                float maxSpacing = Mathf.Max(0.01f, (from.Radius + to.Radius) * DefaultCoverageFactor);
                int subdivisions = Mathf.Max(1, Mathf.CeilToInt(segmentLength / maxSpacing));

                for (int step = 1; step <= subdivisions; step++)
                {
                    float localT = step / (float)subdivisions;
                    float progress = (traversedLength + segmentLength * localT) / totalLength;
                    Vector3 position = Vector3.LerpUnclamped(from.Position, to.Position, localT);
                    float radius = Mathf.LerpUnclamped(from.Radius, to.Radius, localT);
                    samples.Add(BuildSphereSample(progress, position, radius));
                }

                traversedLength += segmentLength;
            }

            if (samples.Count > 0)
            {
                samples[^1].SweepProgressNormalized = 1f;
            }

            return samples;
        }

        private static AttackGeometrySampleDefinition BuildSphereSample(float progress, Vector3 position, float radius)
        {
            return new AttackGeometrySampleDefinition
            {
                SweepProgressNormalized = progress,
                Shapes = new List<AttackGeometryShapeDefinition>
                {
                    new AttackGeometryShapeDefinition
                    {
                        ShapeType = AttackGeometryShapeType.Sphere,
                        LocalPosition = new SerializableVector3(position.x, position.y, position.z),
                        LocalEulerAngles = new SerializableVector3(0f, 0f, 0f),
                        Radius = radius,
                        Height = 0f,
                        HalfExtents = new SerializableVector3(0f, 0f, 0f),
                    }
                }
            };
        }
    }
}

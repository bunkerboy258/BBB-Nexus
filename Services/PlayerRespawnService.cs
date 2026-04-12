using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public sealed class PlayerRespawnService : SingletonMono<PlayerRespawnService>
    {
        [Header("开发期调试复活")]
        [SerializeField] private bool _debugAllowRespawn = true;
        [SerializeField] private float _debugRespawnDelay = 0.2f;
        [SerializeField] private bool _refillOnRespawn = true;

        [Header("死亡统计")]
        [SerializeField] private int _deathCount = 0;

        private bool _initialSpawnApplied;
        private bool _respawnInProgress;
        private CheckpointRespawnPoint _activeCheckpoint;
        private SavePointInteractable _activeSavePoint;

        public int DeathCount => _deathCount;

        public bool TryHandleDeath(BBBCharacterController player)
        {
            Debug.Log($"[PlayerRespawnService] TryHandleDeath called. _debugAllowRespawn={_debugAllowRespawn}, _respawnInProgress={_respawnInProgress}, player={player != null}");

            if (!_debugAllowRespawn || player == null || _respawnInProgress)
            {
                Debug.Log($"[PlayerRespawnService] Returning false. allow={_debugAllowRespawn}, inProgress={_respawnInProgress}");
                return false;
            }

            _deathCount++;
            string savePointId = _activeSavePoint?.SavePointId ?? _activeCheckpoint?.CheckpointId ?? "未知位置";
            Debug.Log($"[PlayerRespawnService] 玩家死亡 {_deathCount} 次，将在 '{savePointId}' 复活。", this);

            Transform respawnTransform = ResolveRespawnTransform(player);
            if (respawnTransform == null)
            {
                Debug.Log("[PlayerRespawnService] ResolveRespawnTransform returned null.");
                return false;
            }

            HandlePlayerDeathDrops(player);
            StartCoroutine(RespawnRoutine(player, respawnTransform));
            return true;
        }

        public void SetActiveCheckpoint(CheckpointRespawnPoint point)
        {
            if (point == null)
            {
                return;
            }

            _activeCheckpoint = point;
            Debug.Log($"[PlayerRespawnService] Active checkpoint set to '{point.CheckpointId}'.", point);
        }

        public void SetActiveSavePoint(SavePointInteractable point)
        {
            if (point == null)
            {
                return;
            }

            _activeSavePoint = point;
            Debug.Log($"[PlayerRespawnService] Active save point set to '{point.SavePointId}'.", point);
        }

        public void TryPlaceAtInitialSpawn(BBBCharacterController player)
        {
            if (_initialSpawnApplied || player == null)
            {
                return;
            }

            var spawnPoint = ResolveInitialSpawnPoint();
            if (spawnPoint == null)
            {
                return;
            }

            TeleportPlayer(player, spawnPoint.transform);
            _initialSpawnApplied = true;
        }

        private IEnumerator RespawnRoutine(BBBCharacterController player, Transform respawnTransform)
        {
            _respawnInProgress = true;
            if (_debugRespawnDelay > 0f)
            {
                yield return new WaitForSeconds(_debugRespawnDelay);
            }

            RespawnPlayer(player, respawnTransform);
            _respawnInProgress = false;
        }

        private void RespawnPlayer(BBBCharacterController player, Transform respawnTransform)
        {
            if (player == null || respawnTransform == null)
            {
                return;
            }

            player.StatusEffects?.Clear();
            player.RuntimeData.IsDead = false;
            player.RuntimeData.Arbitration.Clear();
            player.RuntimeData.ActionControl.Clear();
            player.RuntimeData.StatusControl.Clear();
            player.RuntimeData.CharacterControl.Clear();
            player.RuntimeData.Override.Clear();
            player.RuntimeData.CurrentStamina = player.RuntimeData.MaxStamina;
            player.RuntimeData.IsStaminaDepleted = false;
            player.RuntimeData.VerticalVelocity = 0f;
            player.RuntimeData.WantsLookAtIK = false;
            player.RuntimeData.CurrentAimReference = null;
            player.RuntimeData.ResetIntetnt();

            if (_refillOnRespawn)
            {
                if (player.CharStateService != null && player.CharStateService.TryGetMaxCoreState(out var stateData))
                    player.ApplyMaxCoreState(stateData, refillCurrent: true);
                else
                    player.ApplyMaxCoreState(player.CreateDefaultMaxCoreStateData(), refillCurrent: true);
            }
            else
            {
                player.RuntimeData.CurrentHealth = Mathf.Min(player.RuntimeData.CurrentHealth, player.CurrentMaxHealth);
            }

            TeleportPlayer(player, respawnTransform);
            RefreshAllEnemyRespawnPoints();

            var idleState = player.StateRegistry.GetState<PlayerIdleState>();
            if (idleState != null && player.StateMachine.CurrentState != idleState)
            {
                player.StateMachine.ChangeState(idleState);
            }
        }

        private void HandlePlayerDeathDrops(BBBCharacterController player)
        {
            if (player == null)
            {
                return;
            }

            var ammoDrops = new Dictionary<string, int>(System.StringComparer.Ordinal);
            DrainMagazineToDrops(player, player.RuntimeData?.MainhandItem, EquipmentSlot.MainHand, ammoDrops);
            DrainMagazineToDrops(player, player.RuntimeData?.OffhandItem, EquipmentSlot.OffHand, ammoDrops);

            if (ammoDrops.Count == 0)
            {
                return;
            }

            var dropOrigin = player.transform.position + Vector3.up * 0.05f;
            var index = 0;
            foreach (var pair in ammoDrops)
            {
                var offset = new Vector3((index % 2 == 0 ? -0.35f : 0.35f), 0f, index * 0.18f);
                // RuntimePickupDropFactory.SpawnItemPickup(pair.Key, pair.Value, dropOrigin + offset); // TODO: 实现掉落逻辑，需要外部系统支持
                index++;
            }
        }

        private static void DrainMagazineToDrops(BBBCharacterController player, ItemInstance instance, EquipmentSlot slot, Dictionary<string, int> ammoDrops)
        {
            // 弹夹状态不再持久化（AmmoPackVfs 已移除），复活时不掉落弹夹子弹
        }

        private static void RefreshAllEnemyRespawnPoints()
        {
            var allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (var i = 0; i < allMonoBehaviours.Length; i++)
            {
                var behaviour = allMonoBehaviours[i];
                if (behaviour is IEnemyRespawnPoint enemyRespawnPoint)
                {
                    enemyRespawnPoint.RefreshSpawn();
                }
            }
        }

        private static void TeleportPlayer(BBBCharacterController player, Transform anchor)
        {
            var controller = player.CharController;
            bool restoreEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            player.RuntimeData.ViewYaw = anchor.eulerAngles.y;
            player.RuntimeData.CurrentYaw = anchor.eulerAngles.y;
            player.RuntimeData.AuthorityYaw = anchor.eulerAngles.y;
            player.RuntimeData.AuthorityRotation = anchor.rotation;

            if (controller != null)
            {
                controller.enabled = restoreEnabled;
            }
        }

        private Transform ResolveRespawnTransform(BBBCharacterController player)
        {
            if (_activeSavePoint != null)
            {
                return _activeSavePoint.transform;
            }

            if (_activeCheckpoint != null)
            {
                return _activeCheckpoint.transform;
            }

            var checkpoints = FindObjectsOfType<CheckpointRespawnPoint>(true);
            if (checkpoints != null && checkpoints.Length > 0)
            {
                CheckpointRespawnPoint best = null;
                float bestScore = float.MaxValue;
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    var point = checkpoints[i];
                    if (point == null)
                    {
                        continue;
                    }

                    float score = Vector3.SqrMagnitude(point.transform.position - player.transform.position) - point.SelectionBias;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = point;
                    }
                }

                if (best != null)
                {
                    _activeCheckpoint = best;
                    return best.transform;
                }
            }

            var debugRespawn = ResolveNearestDebugRespawnPoint(player.transform.position);
            if (debugRespawn != null)
            {
                return debugRespawn.transform;
            }

            return ResolveInitialSpawnPoint()?.transform;
        }

        private static PlayerSpawnPoint ResolveInitialSpawnPoint()
        {
            var points = FindObjectsOfType<PlayerSpawnPoint>(true);
            if (points == null || points.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].IsDefault)
                {
                    return points[i];
                }
            }

            return points[0];
        }

        private static DebugRespawnPoint ResolveNearestDebugRespawnPoint(Vector3 position)
        {
            var points = FindObjectsOfType<DebugRespawnPoint>(true);
            if (points == null || points.Length == 0)
            {
                return null;
            }

            DebugRespawnPoint best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point == null)
                {
                    continue;
                }

                float score = Vector3.SqrMagnitude(point.transform.position - position) - point.SelectionBias;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = point;
                }
            }

            return best;
        }
    }
}
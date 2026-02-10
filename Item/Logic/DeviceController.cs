using UnityEngine;
using Items.Data;

namespace Items.Logic
{
    /// <summary>
    /// 挂在物品 Prefab 上的逻辑控制器。
    /// 负责处理具体的“使用”逻辑 (发射、挥动、治疗)。
    /// </summary>
    [RequireComponent(typeof(InteractableItem))] // 必须和 IK 脚本在一起
    public abstract class   DeviceController : MonoBehaviour
    {
        protected InteractableItem _itemBase;
        protected DeviceItemSO _config;
        protected GameObject _owner; // 使用者

        public virtual void Initialize(DeviceItemSO config, GameObject owner)
        {
            _itemBase = GetComponent<InteractableItem>();
            _config = config;
            _owner = owner;
        }

        // --- 核心接口 ---

        /// <summary>
        /// 按下扳机/按钮时调用。
        /// </summary>
        public abstract void OnTriggerDown();

        /// <summary>
        /// 松开扳机/按钮时调用。
        /// </summary>
        public abstract void OnTriggerUp();

        /// <summary>
        /// 持续按住时每帧调用 (可选)。
        /// </summary>
        public virtual void OnTriggerHold() { }

        /// <summary>
        /// 换弹/充能逻辑。
        /// </summary>
        public virtual void Reload() { }
    }
}

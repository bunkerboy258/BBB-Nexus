using UnityEngine;
using UnityEngine.InputSystem;

namespace BBBNexus
{
    public class PlayerInputReader : InputSourceBase
    {
        public bool InputTrace = true;

        #region 配置参数
        [Header("视角设置")]
        public float mouseSensitivity = 1f;
        public bool invertMouseX = false;
        public bool invertMouseY = false;
        #endregion

        #region InputAction 引用
        [Header("输入动作引用")]
        public InputActionReference moveAction;
        public InputActionReference lookAction;
        public InputActionReference jumpAction;
        public InputActionReference sprintAction;
        public InputActionReference walkAction;
        public InputActionReference aimAction;
        public InputActionReference dodgeAction;
        public InputActionReference rollAction;
        public InputActionReference interactAction;        // E 键：交互 / 捡取 / 开门
        public InputActionReference primaryAttackAction;   // 左键：主要攻击 / 连招
        public InputActionReference secondaryAttackAction; // 右键：辅助攻击 / 瞄准射击
        public InputActionReference number1Action;
        public InputActionReference number2Action;
        public InputActionReference number3Action;
        public InputActionReference number4Action;
        public InputActionReference number5Action;

        [Header("表情输入引用")]
        public InputActionReference expression1Action;
        public InputActionReference expression2Action;
        public InputActionReference expression3Action;
        public InputActionReference expression4Action;
        public InputActionReference expression5Action;
        public InputActionReference expression6Action;
        public InputActionReference expression7Action;
        public InputActionReference expression8Action;

        [Header("游戏专属动作输入引用")]
        [Tooltip("切换闭眼（tap）→ ExtraAction1JustPressed")]
        public InputActionReference toggleEyesAction;
        [Tooltip("按住闭眼（hold）→ ExtraAction1Held")]
        public InputActionReference holdEyesAction;
        [Tooltip("换弹 R 键 → ExtraAction2JustPressed")]
        public InputActionReference reloadAction;
        [Tooltip("使用道具 F 键 → ExtraAction3JustPressed")]
        public InputActionReference useItemAction;
        [Tooltip("打开背包 Tab 键 → InventoryJustPressed")]
        public InputActionReference inventoryAction;
        [Tooltip("预留 → ExtraAction4JustPressed")]
        public InputActionReference extraAction4Action;
        #endregion

        private InputAction _resolvedReloadFallback;   // 兜底：从 asset 里找 "Reload"
        private InputAction _resolvedUseItemFallback;  // 兜底：从 asset 里找 "UseItem"

        private void OnEnable()
        {
            ResolveFallbackActions();
            ToggleActions(true);
        }

        private void OnDisable() => ToggleActions(false);

        public override void FetchRawInput(ref RawInputData rawData)
        {
            // ============== 轴向输入 ==============
            rawData.MoveAxis = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

            Vector2 rawLook = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
            rawLook.x *= mouseSensitivity * (invertMouseX ? -1f : 1f);
            rawLook.y *= mouseSensitivity * (invertMouseY ? -1f : 1f);
            rawData.LookAxis = rawLook;

            // ============== 持续状态采样 (Held) ==============
            rawData.JumpHeld = jumpAction != null && jumpAction.action.IsPressed();
            rawData.DodgeHeld = dodgeAction != null && dodgeAction.action.IsPressed();
            rawData.RollHeld = rollAction != null && rollAction.action.IsPressed();
            rawData.SprintHeld = sprintAction != null && sprintAction.action.IsPressed();
            rawData.WalkHeld = walkAction != null && walkAction.action.IsPressed();
            rawData.AimHeld = aimAction != null && aimAction.action.IsPressed();
            rawData.InteractHeld = interactAction != null && interactAction.action.IsPressed();
            rawData.PrimaryAttackHeld = primaryAttackAction != null && primaryAttackAction.action.IsPressed();
            rawData.SecondaryAttackHeld = secondaryAttackAction != null && secondaryAttackAction.action.IsPressed();

            rawData.Expression1Held = expression1Action != null && expression1Action.action.IsPressed();
            rawData.Expression2Held = expression2Action != null && expression2Action.action.IsPressed();
            rawData.Expression3Held = expression3Action != null && expression3Action.action.IsPressed();
            rawData.Expression4Held = expression4Action != null && expression4Action.action.IsPressed();
            rawData.Expression5Held = expression5Action != null && expression5Action.action.IsPressed();
            rawData.Expression6Held = expression6Action != null && expression6Action.action.IsPressed();
            rawData.Expression7Held = expression7Action != null && expression7Action.action.IsPressed();
            rawData.Expression8Held = expression8Action != null && expression8Action.action.IsPressed();
            rawData.Number1Held = number1Action != null && number1Action.action.IsPressed();
            rawData.Number2Held = number2Action != null && number2Action.action.IsPressed();
            rawData.Number3Held = number3Action != null && number3Action.action.IsPressed();
            rawData.Number4Held = number4Action != null && number4Action.action.IsPressed();
            rawData.Number5Held = number5Action != null && number5Action.action.IsPressed();

            // ============== 瞬间硬件数据 ==============
            rawData.JumpJustPressed = jumpAction != null && jumpAction.action.WasPressedThisFrame();
            rawData.DodgeJustPressed = dodgeAction != null && dodgeAction.action.WasPressedThisFrame();
            rawData.RollJustPressed = rollAction != null && rollAction.action.WasPressedThisFrame();
            rawData.InteractJustPressed = interactAction != null && interactAction.action.WasPressedThisFrame();
            rawData.PrimaryAttackJustPressed = primaryAttackAction != null && primaryAttackAction.action.WasPressedThisFrame();
            rawData.SecondaryAttackJustPressed = secondaryAttackAction != null && secondaryAttackAction.action.WasPressedThisFrame();

            rawData.Expression1JustPressed = expression1Action != null && expression1Action.action.WasPressedThisFrame();
            rawData.Expression2JustPressed = expression2Action != null && expression2Action.action.WasPressedThisFrame();
            rawData.Expression3JustPressed = expression3Action != null && expression3Action.action.WasPressedThisFrame();
            rawData.Expression4JustPressed = expression4Action != null && expression4Action.action.WasPressedThisFrame();
            rawData.Expression5JustPressed = expression5Action != null && expression5Action.action.WasPressedThisFrame();
            rawData.Expression6JustPressed = expression6Action != null && expression6Action.action.WasPressedThisFrame();
            rawData.Expression7JustPressed = expression7Action != null && expression7Action.action.WasPressedThisFrame();
            rawData.Expression8JustPressed = expression8Action != null && expression8Action.action.WasPressedThisFrame();

            rawData.ToggleEyesJustPressed   = toggleEyesAction  != null && toggleEyesAction.action.WasPressedThisFrame();
            rawData.HoldEyesHeld            = holdEyesAction    != null && holdEyesAction.action.IsPressed();
            rawData.ReloadJustPressed       = WasPressedThisFrame(reloadAction,  _resolvedReloadFallback);
            rawData.UseItemJustPressed      = WasPressedThisFrame(useItemAction, _resolvedUseItemFallback);
            rawData.InventoryJustPressed    = inventoryAction   != null && inventoryAction.action.WasPressedThisFrame();
            rawData.ExtraAction4JustPressed = extraAction4Action != null && extraAction4Action.action.WasPressedThisFrame();

            if (rawData.InventoryJustPressed)
            {
                Debug.Log($"[InventoryTrace] frame={Time.frameCount} InventoryJustPressed=true actionBound={inventoryAction != null}", this);
            }

            rawData.Number1JustPressed = number1Action != null && number1Action.action.WasPressedThisFrame();
            rawData.Number2JustPressed = number2Action != null && number2Action.action.WasPressedThisFrame();
            rawData.Number3JustPressed = number3Action != null && number3Action.action.WasPressedThisFrame();
            rawData.Number4JustPressed = number4Action != null && number4Action.action.WasPressedThisFrame();
            rawData.Number5JustPressed = number5Action != null && number5Action.action.WasPressedThisFrame();

            if (InputTrace && (rawData.PrimaryAttackJustPressed || rawData.PrimaryAttackHeld))
            {
                Debug.Log(
                    $"[InputTrace] frame={Time.frameCount} primaryJustPressed={rawData.PrimaryAttackJustPressed} " +
                    $"primaryHeld={rawData.PrimaryAttackHeld} sprintHeld={rawData.SprintHeld} jumpHeld={rawData.JumpHeld} " +
                    $"move={rawData.MoveAxis}");
            }
        }

        private void ToggleActions(bool enable)
        {
            ResolveFallbackActions();

            InputActionReference[] all = {
                moveAction, lookAction, jumpAction, sprintAction, walkAction,
                aimAction, dodgeAction, rollAction,
                interactAction, primaryAttackAction, secondaryAttackAction,
                number1Action, number2Action, number3Action, number4Action, number5Action,
                expression1Action, expression2Action, expression3Action, expression4Action,
                expression5Action, expression6Action, expression7Action, expression8Action,
                toggleEyesAction, holdEyesAction, reloadAction, useItemAction, inventoryAction, extraAction4Action
            };

            foreach (var ar in all)
            {
                if (ar == null) continue;
                if (enable) ar.action.Enable();
                else ar.action.Disable();
            }

            ToggleResolvedAction(_resolvedReloadFallback,  reloadAction,   enable);
            ToggleResolvedAction(_resolvedUseItemFallback, useItemAction,  enable);
        }

        private void ResolveFallbackActions()
        {
            var asset = ResolveAnyBoundAsset();
            _resolvedReloadFallback  = asset != null ? asset.FindAction("Reload",   throwIfNotFound: false) : null;
            _resolvedUseItemFallback = asset != null ? asset.FindAction("UseItem",  throwIfNotFound: false) : null;
        }

        private InputActionAsset ResolveAnyBoundAsset()
        {
            InputActionReference[] all = {
                moveAction, lookAction, jumpAction, sprintAction, walkAction,
                aimAction, dodgeAction, rollAction, interactAction,
                primaryAttackAction, secondaryAttackAction,
                toggleEyesAction, holdEyesAction, reloadAction, useItemAction, inventoryAction, extraAction4Action
            };

            foreach (var actionRef in all)
            {
                var asset = actionRef?.action?.actionMap?.asset;
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static bool WasPressedThisFrame(InputActionReference actionRef, InputAction fallbackAction)
        {
            if (actionRef != null && actionRef.action != null && actionRef.action.WasPressedThisFrame())
            {
                return true;
            }

            return fallbackAction != null &&
                   fallbackAction != actionRef?.action &&
                   fallbackAction.WasPressedThisFrame();
        }

        private static void ToggleResolvedAction(InputAction action, InputActionReference boundReference, bool enable)
        {
            if (action == null || action == boundReference?.action)
            {
                return;
            }

            if (enable)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }
    }
}

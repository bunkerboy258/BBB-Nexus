using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace Characters.Player.Input
{
    /// <summary>
    /// 玩家输入读取器：封装 Unity InputSystem 的输入监听逻辑，对外暴露输入状态和事件
    /// 核心职责：监听移动、冲刺、挥手、攻击等输入，记录输入状态（按下时长、是否按下），触发外部注册的事件
    /// 修改说明：将攻击输入从单一 performed 回调改为 started（按下）与 canceled（抬起）两个回调，新增 OnAttackDown/OnAttackUp 回调字段。
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        // 移动输入值（-1~1 范围的二维向量）
        public Vector2 MoveInput { get; private set; }
        // 是否处于冲刺状态：通过属性实时判断冲刺按键是否按下
        public bool IsSprinting => sprintAction != null && sprintAction.action.IsPressed();

        // C# 表达式体语法，等价于 get { return ... }
        //移动输入是连续值 需要实时同步输入值的变化 所以用事件绑定
        //跳跃 / 冲刺是布尔值，且通常不是 “持续响应” 用 IsPressed() 实时查询足够，且代码更简洁（不用绑定事件、管理回调）

        // 跳跃输入状态
        public bool IsJumpPressed => jumpAction != null && jumpAction.action.IsPressed();

        // 移动输入的状态记录
        public float MovePressTime { get; private set; } // 移动输入按下的时间戳（Time.time）
        public bool IsMovePressed { get; private set; }  // 移动输入当前是否按下

        // 新增：鼠标/视角输入
        public Vector2 LookInput { get; private set; }

        // 基础动作事件回调（供外部注册）
        public UnityAction OnWavePressed;
        // 改动：将攻击回调拆成按下与抬起两个回调
        public UnityAction OnLeftMouseDown;
        public UnityAction OnLeftMouseUp;
        public UnityAction OnJumpPressed;
        public UnityAction OnAimStarted;
        public UnityAction OnAimCanceled;

        // 数字键1-5的事件回调（供外部注册）
        public UnityAction OnNumber1Pressed;
        public UnityAction OnNumber2Pressed;
        public UnityAction OnNumber3Pressed;
        public UnityAction OnNumber4Pressed;
        public UnityAction OnNumber5Pressed;

        [Header("Input References")]
        [Tooltip("移动动作的引用（InputActionReference）")]
        public InputActionReference moveAction;
        [Tooltip("冲刺动作的引用（InputActionReference）")]
        public InputActionReference sprintAction;
        [Tooltip("跳跃动作的引用（InputActionReference）")]
        public InputActionReference jumpAction;
        [Tooltip("挥手动作的引用（InputActionReference）")]
        public InputActionReference waveAction;
        [Tooltip("攻击动作的引用（InputActionReference）")]
        public InputActionReference LeftMouseAction;
        [Tooltip("瞄准动作的引用（InputActionReference）")]
        public InputActionReference aimAction;

        // 新增：数字键1-5的InputActionReference引用（需在Inspector面板赋值）
        [Header("Number Key References")]
        [Tooltip("数字键1动作的引用（InputActionReference）")]
        public InputActionReference number1Action;
        [Tooltip("数字键2动作的引用（InputActionReference）")]
        public InputActionReference number2Action;
        [Tooltip("数字键3动作的引用（InputActionReference）")]
        public InputActionReference number3Action;
        [Tooltip("数字键4动作的引用（InputActionReference）")]
        public InputActionReference number4Action;
        [Tooltip("数字键5动作的引用（InputActionReference）")]
        public InputActionReference number5Action;

        [Header("Mouse Look")]
        [Tooltip("鼠标移动动作的引用（InputActionReference），通常绑定 Mouse/delta 或 Gamepad/rightStick）")]
        public InputActionReference mouseLookAction;
        [Tooltip("鼠标灵敏度")]
        public float mouseSensitivity = 1f;
        [Tooltip("是否反转 Y 轴")]
        public bool invertMouseY = false;

        #region 生命周期方法
        private void OnEnable()
        {
            InitializeMoveInput();
            InitializeSprintInput();
            InitializeJumpInput();
            InitializeWaveInput();
            InitializeLeftMouseInput();
            InitializeAimInput();
            InitializeMouseLookInput();

            // 新增：初始化数字键1-5的输入监听
            InitializeNumber1Input();
            InitializeNumber2Input();
            InitializeNumber3Input();
            InitializeNumber4Input();
            InitializeNumber5Input();
        }

        private void OnDisable()
        {
            UninitializeMoveInput();
            UninitializeSprintInput();
            UninitializeJumpInput();
            UninitializeWaveInput();
            UninitializeLeftMouseInput();
            UninitializeAimInput();
            UninitializeMouseLookInput();

            // 新增：反初始化数字键1-5的输入监听（防止内存泄漏）
            UninitializeNumber1Input();
            UninitializeNumber2Input();
            UninitializeNumber3Input();
            UninitializeNumber4Input();
            UninitializeNumber5Input();
        }
        #endregion

        #region 输入初始化/反初始化
        // 初始化移动输入监听：启用动作、注册按下/取消/开始/结束回调
        private void InitializeMoveInput()
        {
            if (moveAction == null) return;

            moveAction.action.Enable();
            moveAction.action.performed += OnMove;//当移动输入变化，InputSystem 会高频触发 performed 事件
            moveAction.action.canceled += OnMove;
            moveAction.action.started += OnMoveStarted;
            moveAction.action.canceled += OnMoveCanceled;
        }

        // 反初始化移动输入监听：禁用动作、注销所有回调，防止内存泄漏
        private void UninitializeMoveInput()
        {
            if (moveAction == null) return;

            moveAction.action.Disable();
            moveAction.action.performed -= OnMove;
            moveAction.action.canceled -= OnMove;
            moveAction.action.started -= OnMoveStarted;
            moveAction.action.canceled -= OnMoveCanceled;
        }

        private void InitializeSprintInput()
        {
            if (sprintAction == null) return;

            sprintAction.action.Enable();
        }

        private void UninitializeSprintInput()
        {
            if (sprintAction == null) return;

            sprintAction.action.Disable();
        }

        private void InitializeJumpInput()
        {
            if (jumpAction == null) return; 

            jumpAction.action.Enable();
            jumpAction.action.performed += OnJumpPerformed;
        }

        private void UninitializeJumpInput()
        {
            if (jumpAction == null) return; 

            jumpAction.action.Disable();
            jumpAction.action.performed -= OnJumpPerformed;
        }

        // 修正：原代码错误判断OnJumpPressed，改为判断aimAction
        private void InitializeAimInput()
        {
            if (aimAction == null) return;

            aimAction.action.Enable();
            aimAction.action.performed += OnAimPerformed;
            aimAction.action.canceled += OnaimCancled;
        }

        // 修正：原代码错误判断OnJumpPressed，改为判断aimAction
        private void UninitializeAimInput()
        {
            if (aimAction == null) return;

            aimAction.action.Disable();
            aimAction.action.performed -= OnAimPerformed;
            aimAction.action.canceled -= OnaimCancled;
        }

        private void InitializeWaveInput()
        {
            if (waveAction == null) return;

            waveAction.action.Enable();
            waveAction.action.performed += OnWavePerformed;
        }

        private void UninitializeWaveInput()
        {
            if (waveAction == null) return;

            waveAction.action.Disable();
            waveAction.action.performed -= OnWavePerformed;
        }

        private void InitializeLeftMouseInput()
        {
            if (LeftMouseAction == null) return;

            LeftMouseAction.action.Enable();
            LeftMouseAction.action.started += OnLeftMouseStarted;
            LeftMouseAction.action.canceled += OnLeftMouseCanceled;
        }

        private void UninitializeLeftMouseInput()
        {
            if (LeftMouseAction == null) return;

            LeftMouseAction.action.Disable();
            LeftMouseAction.action.started -= OnLeftMouseStarted;
            LeftMouseAction.action.canceled -= OnLeftMouseCanceled;
        }

        // 新增：初始化鼠标视角输入
        private void InitializeMouseLookInput()
        {
            if (mouseLookAction == null) return;

            mouseLookAction.action.Enable();
            mouseLookAction.action.performed += OnMouseLookPerformed;
            mouseLookAction.action.canceled += OnMouseLookCanceled;
        }

        // 新增：反初始化鼠标视角输入
        private void UninitializeMouseLookInput()
        {
            if (mouseLookAction == null) return;

            mouseLookAction.action.Disable();
            mouseLookAction.action.performed -= OnMouseLookPerformed;
            mouseLookAction.action.canceled -= OnMouseLookCanceled;
        }

        // 新增：数字键1输入初始化
        private void InitializeNumber1Input()
        {
            if (number1Action == null) return;

            number1Action.action.Enable();
            number1Action.action.performed += OnNumber1Performed;
        }

        // 新增：数字键1输入反初始化
        private void UninitializeNumber1Input()
        {
            if (number1Action == null) return;

            number1Action.action.Disable();
            number1Action.action.performed -= OnNumber1Performed;
        }

        // 新增：数字键2输入初始化
        private void InitializeNumber2Input()
        {
            if (number2Action == null) return;

            number2Action.action.Enable();
            number2Action.action.performed += OnNumber2Performed;
        }

        // 新增：数字键2输入反初始化
        private void UninitializeNumber2Input()
        {
            if (number2Action == null) return;

            number2Action.action.Disable();
            number2Action.action.performed -= OnNumber2Performed;
        }

        // 新增：数字键3输入初始化
        private void InitializeNumber3Input()
        {
            if (number3Action == null) return;

            number3Action.action.Enable();
            number3Action.action.performed += OnNumber3Performed;
        }

        // 新增：数字键3输入反初始化
        private void UninitializeNumber3Input()
        {
            if (number3Action == null) return;

            number3Action.action.Disable();
            number3Action.action.performed -= OnNumber3Performed;
        }

        // 新增：数字键4输入初始化
        private void InitializeNumber4Input()
        {
            if (number4Action == null) return;

            number4Action.action.Enable();
            number4Action.action.performed += OnNumber4Performed;
        }

        // 新增：数字键4输入反初始化
        private void UninitializeNumber4Input()
        {
            if (number4Action == null) return;

            number4Action.action.Disable();
            number4Action.action.performed -= OnNumber4Performed;
        }

        // 新增：数字键5输入初始化
        private void InitializeNumber5Input()
        {
            if (number5Action == null) return;

            number5Action.action.Enable();
            number5Action.action.performed += OnNumber5Performed;
        }

        // 新增：数字键5输入反初始化
        private void UninitializeNumber5Input()
        {
            if (number5Action == null) return;

            number5Action.action.Disable();
            number5Action.action.performed -= OnNumber5Performed;
        }
        #endregion

        #region 输入回调方法
        /// <summary>
        /// 移动输入回调：更新移动输入的二维向量值
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// 移动输入开始回调：标记输入按下状态、记录按下时间戳
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnMoveStarted(InputAction.CallbackContext context)
        {
            IsMovePressed = true;
            MovePressTime = Time.time;
        }

        /// <summary>
        /// 移动输入取消回调：重置输入按下状态
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            IsMovePressed = false;
        }

        /// <summary>
        /// 跳跃输入按下回调：执行外部注册的跳跃逻辑（空安全调用）
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            OnJumpPressed?.Invoke();
        }

        /// <summary>
        /// 挥手输入按下回调：执行外部注册的挥手逻辑（空安全调用）
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnWavePerformed(InputAction.CallbackContext context)
        {
            OnWavePressed?.Invoke();
        }

        /// <summary>
        /// 攻击输入按下回调：执行外部注册的攻击逻辑（空安全调用）
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnLeftMouseStarted(InputAction.CallbackContext context)
        {
            OnLeftMouseDown?.Invoke();
        }

        /// <summary>
        /// 攻击输入抬起回调：执行外部注册的攻击逻辑（空安全调用）
        /// </summary>
        /// <param name="context">InputSystem 回调上下文</param>
        private void OnLeftMouseCanceled(InputAction.CallbackContext context)
        {
            OnLeftMouseUp?.Invoke();
        }

        private void OnAimPerformed(InputAction.CallbackContext context)
        {
            OnAimStarted?.Invoke();
        }

        private void OnaimCancled(InputAction.CallbackContext context)
        {
            OnAimCanceled?.Invoke();
        }

        // 新增：鼠标视角回调：将 delta 转换为 LookInput（不乘 deltaTime，这里保留原始增量）
        private void OnMouseLookPerformed(InputAction.CallbackContext context)
        {
            Vector2 raw = context.ReadValue<Vector2>();
            if (invertMouseY) raw.y = -raw.y;
            LookInput = raw * mouseSensitivity;
        }

        private void OnMouseLookCanceled(InputAction.CallbackContext context)
        {
            LookInput = Vector2.zero;
        }

        // 新增：数字键1按下回调
        private void OnNumber1Performed(InputAction.CallbackContext context)
        {
            OnNumber1Pressed?.Invoke();
        }

        // 新增：数字键2按下回调
        private void OnNumber2Performed(InputAction.CallbackContext context)
        {
            OnNumber2Pressed?.Invoke();
        }

        // 新增：数字键3按下回调
        private void OnNumber3Performed(InputAction.CallbackContext context)
        {
            OnNumber3Pressed?.Invoke();
        }

        // 新增：数字键4按下回调
        private void OnNumber4Performed(InputAction.CallbackContext context)
        {
            OnNumber4Pressed?.Invoke();
        }

        // 新增：数字键5按下回调
        private void OnNumber5Performed(InputAction.CallbackContext context)
        {
            OnNumber5Pressed?.Invoke();
        }
        #endregion
    }
}
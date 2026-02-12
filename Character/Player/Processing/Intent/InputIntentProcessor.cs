using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Characters.Player.Parameters
{
    /// <summary>
    /// 输入意图处理器
    /// 职责：将原始输入数据（移动按键按下时长）转换为核心游戏逻辑意图，供状态机决策使用。
    /// </summary>
    public class InputIntentProcessor
    {
        private PlayerController _player;
        private PlayerInputReader _input;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        /// <summary>
        /// 构造函数：注入玩家核心依赖（控制器、输入读取器、运行时数据、配置文件）
        /// </summary>
        /// <param name="player">玩家核心控制器</param>
        public InputIntentProcessor(PlayerController player)
        {
            _player = player;
            _input = player.InputReader;   // 输入读取器：获取原始输入状态
            _data = player.RuntimeData;    // 运行时数据：写入转换后的意图状态
            _config = player.Config;       // 配置文件：读取短按阈值等参数
        }

        /// <summary>
        /// 每帧更新：转换原始输入为游戏意图（长按/短按），并记录有效输入方向
        /// </summary>
        public void Update()
        {
            
        }
    }
}
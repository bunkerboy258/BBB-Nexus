using Characters.Player;
using Characters.Player.Expression;
using Characters.Player.States;
using Items.Core;

public class UpperBodyHoldItemState : UpperBodyBaseState
{
    private IHoldableItem _currentItem;
    private ItemInstance _cachedInstance; // 必须缓存！

    public UpperBodyHoldItemState(PlayerController p) : base(p) { }
    public override void Enter()
    {
        player.AnimFacade.SetLayerWeight(1, 1f, 0.25f);
        SyncEquipmentFromBlackboard(); // 封装成独立方法
    }

    protected override void UpdateStateLogic()
    {
        // 黑板上的物品有没有被换？！
        if (_cachedInstance != player.RuntimeData.CurrentItem)
        {
            SyncEquipmentFromBlackboard(); 
            return; // 换枪这帧跳过物品逻辑更新
        }

        // 2. 正常运行
        _currentItem?.OnUpdateLogic();

        if (player.EquipmentDriver.CurrentItemDirector == null)
        {
            player.UpperBodyCtrl.StateMachine.ChangeState(player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyEmptyState>());
        }
    }

    private void SyncEquipmentFromBlackboard()
    {
        _currentItem?.OnForceUnequip(); // 剥夺旧武器控制权

        _cachedInstance = player.RuntimeData.CurrentItem;

        if (_cachedInstance != null)
        {
            _currentItem = player.EquipmentDriver.CurrentItemDirector;
            _currentItem?.OnEquipEnter(player);
        }
        else
        {
            player.EquipmentDriver.UnequipCurrentItem();
            _currentItem = null;
        }
    }

    public override void Exit()
    {
        
    }
}
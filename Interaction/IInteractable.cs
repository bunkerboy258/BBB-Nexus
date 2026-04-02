using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 交互对象接口。玩家按下 E 时由 ActionController 查询并执行。
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(BBBCharacterController interactor);
        Transform GetInteractionTransform();
        string GetPromptText(BBBCharacterController interactor);
        bool TryGetInteractionRequest(BBBCharacterController interactor, out ActionRequest request);
        void Interact(BBBCharacterController interactor);
    }
}

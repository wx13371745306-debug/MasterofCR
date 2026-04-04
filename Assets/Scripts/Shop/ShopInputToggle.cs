using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 临时入口：按 P 开关商店。与玩家移动脚本解耦；后期可删除本组件，改由其它系统调用 ShopUIController.Open/Close。
/// </summary>
public class ShopInputToggle : MonoBehaviour
{
    public ShopUIController shopUI;

    void Update()
    {
        if (Keyboard.current == null || shopUI == null) return;

        if (Keyboard.current.pKey.wasPressedThisFrame)
            shopUI.Toggle();
    }
}

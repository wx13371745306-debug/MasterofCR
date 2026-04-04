using UnityEngine.UI;

/// <summary>
/// 放在 ScrollRect 内的按钮常因 Navigation 与拖拽焦点冲突导致 onClick 不触发；统一关掉 Navigation 并确保 Target Graphic 参与射线检测。
/// </summary>
public static class ShopUIButtonUtil
{
    public static void FixForScrollView(Button button)
    {
        if (button == null) return;

        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = true;
    }
}

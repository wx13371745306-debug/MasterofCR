using UnityEngine;

/// <summary>
/// 羁绊列表 UI 控制器：读取 BondActivationStateSO，
/// 在 VerticalLayoutGroup 容器中为每条已激活羁绊生成一行 BondLineView。
/// 可同时用于选菜面板与开始界面。
/// </summary>
public class BondListUIController : MonoBehaviour
{
    [Header("数据")]
    public BondActivationStateSO bondState;

    [Header("UI")]
    [Tooltip("行预制体，需挂 BondLineView")]
    public GameObject bondLinePrefab;
    [Tooltip("行的父容器（应有 VerticalLayoutGroup）")]
    public Transform lineContainer;

    void Start()
    {
        Refresh();
    }

    /// <summary>清空并重建已激活羁绊的行。</summary>
    public void Refresh()
    {
        if (lineContainer == null)
        {
            Debug.LogWarning("[BondListUI] lineContainer 未赋值！");
            return;
        }
        if (bondLinePrefab == null)
        {
            Debug.LogWarning("[BondListUI] bondLinePrefab 未赋值！");
            return;
        }
        if (bondState == null)
        {
            Debug.LogWarning("[BondListUI] bondState 未赋值！");
            return;
        }

        for (int i = lineContainer.childCount - 1; i >= 0; i--)
            Destroy(lineContainer.GetChild(i).gameObject);

        int count = 0;
        foreach (var bond in bondState.bonds)
        {
            if (!bond.isActive) continue;

            GameObject go = Instantiate(bondLinePrefab, lineContainer);
            go.transform.localScale = Vector3.one;
            var view = go.GetComponent<BondLineView>();
            if (view != null)
                view.Setup(bond.icon, bond.displayName, bond.description);
            else
                Debug.LogWarning("[BondListUI] bondLinePrefab 上没有找到 BondLineView 脚本！");
            count++;
        }
        Debug.Log($"[BondListUI] Refresh 完成，生成了 {count} 行已激活羁绊（总槽位: {bondState.bonds.Count}）");
    }
}

using UnityEngine;
using Mirror;

/// <summary>
/// 脏盘子堆：单一网络实体，<see cref="plateCount"/> 为权威数量；拾取/放下走标准 <see cref="CarryableItem"/> + 网络 Cmd。
/// 视觉为本地沿 Y 叠放的单盘模型（<see cref="singlePlatePrefab"/> 应为纯表现预制体，勿挂 NetworkIdentity）。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DirtyPlateStack : CarryableItem
{
    [Header("Dirty Plate Stack")]
    [SyncVar(hook = nameof(OnPlateCountChanged))]
    public int plateCount = 1;

    [Tooltip("单盘视觉预制体（建议仅 Mesh/材质；勿挂 NetworkIdentity，否则联机 Instantiate 子物体可能触发 Mirror 报错）")]
    public GameObject singlePlatePrefab;

    [Tooltip("垂直堆叠的 Y 轴间隔")]
    public float stackYOffset = 0.05f;

    [Tooltip("存放所有视觉盘子的父节点")]
    public Transform visualRoot;

    private BoxCollider stackCollider;
    private float baseColliderSizeY = 0.05f;
    private float baseColliderCenterY = 0.025f;

    [SyncVar(hook = nameof(OnForcedHiddenChanged))]
    private bool forcedHidden = false;

    protected override void Awake()
    {
        base.Awake();
        stackCollider = GetComponent<BoxCollider>();
        if (stackCollider != null)
        {
            baseColliderSizeY = stackCollider.size.y;
            baseColliderCenterY = stackCollider.center.y;
        }

        if (visualRoot == null)
        {
            visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(transform);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        categories |= ItemCategory.DirtyPlate;
        // 整堆可走标准 CmdRequestPickUp，勿再禁止拾取
        isPickable = true;
    }

    protected override void Start()
    {
        base.Start();
        UpdateVisuals();

        if (forcedHidden && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
    }

    void OnPlateCountChanged(int oldVal, int newVal)
    {
        UpdateVisuals();
    }

    void OnForcedHiddenChanged(bool oldVal, bool newVal)
    {
        if (newVal && visualRoot != null)
            visualRoot.gameObject.SetActive(false);
    }

    public void SetPlateCount(int count)
    {
        if (NetworkServer.active)
            plateCount = Mathf.Max(1, count);
    }

    /// <summary>由餐桌在吃完饭时调用：在仍有残羹视觉时隐藏通用盘堆模型。</summary>
    public void ForceHideUntilPickedUp()
    {
        if (NetworkServer.active)
            forcedHidden = true;
    }

    void StripNetworkComponentsFromVisualPlate(GameObject plate)
    {
        if (plate == null) return;

        // 防御：策划误拖带 NI 的预制体时，避免 Mirror 在子物体上报错
        NetworkIdentity[] nids = plate.GetComponentsInChildren<NetworkIdentity>(true);
        foreach (var nid in nids)
        {
            if (nid != null) Destroy(nid);
        }

        StackableProp sp = plate.GetComponent<StackableProp>();
        if (sp != null) Destroy(sp);
        CarryableItem ci = plate.GetComponent<CarryableItem>();
        if (ci != null) Destroy(ci);
        Rigidbody rb = plate.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        Collider[] cols = plate.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            if (c != null) Destroy(c);
        }
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null) return;

        foreach (Transform child in visualRoot)
            Destroy(child.gameObject);

        // 无单盘预制体时仍更新碰撞体高度（仅 plateCount）
        bool useGenericStackVisual = singlePlatePrefab != null && !forcedHidden;

        if (useGenericStackVisual)
        {
            visualRoot.gameObject.SetActive(true);
            for (int i = 0; i < plateCount; i++)
            {
                GameObject plate = Instantiate(singlePlatePrefab, visualRoot);
                plate.transform.localPosition = new Vector3(0, i * stackYOffset, 0);
                plate.transform.localRotation = Quaternion.identity;
                StripNetworkComponentsFromVisualPlate(plate);
            }
        }
        else
        {
            visualRoot.gameObject.SetActive(false);
        }

        if (stackCollider != null)
        {
            float newSizeY = baseColliderSizeY + (plateCount - 1) * stackYOffset;
            float newCenterY = baseColliderCenterY + (plateCount - 1) * stackYOffset * 0.5f;

            stackCollider.size = new Vector3(stackCollider.size.x, newSizeY, stackCollider.size.z);
            stackCollider.center = new Vector3(stackCollider.center.x, newCenterY, stackCollider.center.z);
        }
    }
}

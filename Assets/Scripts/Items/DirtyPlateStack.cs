using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DirtyPlateStack : CarryableItem
{
    [Header("Dirty Plate Stack")]
    public int plateCount = 1;
    [Tooltip("代表单个脏盘子的预制体，建议只包含外观模型文件")]
    public GameObject singlePlatePrefab;
    [Tooltip("垂直堆叠的Y轴间隔，决定了垒起来的高度间距")]
    public float stackYOffset = 0.05f;
    [Tooltip("存放所有视觉盘子的父节点")]
    public Transform visualRoot;

    private BoxCollider stackCollider;
    private float baseColliderSizeY = 0.05f;
    private float baseColliderCenterY = 0.025f;

    private bool forcedHidden = false; // 终极隐身锁，只有被玩家确切拿到手里才会解除

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
            visualRoot.SetParent(this.transform);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        categories |= ItemCategory.DirtyPlate;
    }

    protected override void Start()
    {
        base.Start();
        UpdateVisuals();

        // 如果在初始化前就被施加了隐身锁，确保它生效
        if (forcedHidden && visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    public void SetPlateCount(int count)
    {
        plateCount = Mathf.Max(1, count);
        UpdateVisuals();
    }

    // 由餐桌在吃完饭时调用，施加终极隐身锁
    public void ForceHideUntilPickedUp()
    {
        forcedHidden = true;
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }

    // J键拿取时，CarryableItem基类会触发该方法
    public override void BeginHold(Transform holdPoint)
    {
        base.BeginHold(holdPoint);
        
        // 瞬间解除隐身锁
        forcedHidden = false;
        if (visualRoot != null) visualRoot.gameObject.SetActive(true);
    }

    public override void DropToGround()
    {
        base.DropToGround();
        
        // 如果中途掉落，也确保现形
        forcedHidden = false;
        if (visualRoot != null) visualRoot.gameObject.SetActive(true);
    }

    private void UpdateVisuals()
    {
        if (singlePlatePrefab == null || visualRoot == null) return;

        // 清理旧渲染
        foreach (Transform child in visualRoot)
        {
            Destroy(child.gameObject);
        }

        // 创建新网格
        for (int i = 0; i < plateCount; i++)
        {
            GameObject plate = Instantiate(singlePlatePrefab, visualRoot);
            plate.transform.localPosition = new Vector3(0, i * stackYOffset, 0);
            plate.transform.localRotation = Quaternion.identity;
        }

        // 刷新碰撞体
        if (stackCollider != null)
        {
            float newSizeY = baseColliderSizeY + (plateCount - 1) * stackYOffset;
            float newCenterY = baseColliderCenterY + (plateCount - 1) * stackYOffset * 0.5f;

            stackCollider.size = new Vector3(stackCollider.size.x, newSizeY, stackCollider.size.z);
            stackCollider.center = new Vector3(stackCollider.center.x, newCenterY, stackCollider.center.z);
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

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

    private List<GameObject> visualPlates = new List<GameObject>();
    private BoxCollider stackCollider;
    private float baseColliderSizeY = 0.05f;
    private float baseColliderCenterY = 0.025f;

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

        // 初始化自身类别，确保能放到通用的或者专门只收脏盘子的桌面上
        categories |= ItemCategory.DirtyPlate;
    }

    protected override void Start()
    {
        base.Start();
        // 初始渲染一次模型
        UpdateVisuals();
    }

    public void SetPlateCount(int count)
    {
        plateCount = Mathf.Max(1, count);
        UpdateVisuals();
    }



    private bool hiddenByTable = false;

    // 允许外界（如餐桌）强制让它进入隐身高阶状态，直到被拿起
    public void HideVisualsForTable()
    {
        hiddenByTable = true;
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (visualRoot == null) return;
        
        if (hiddenByTable)
        {
            if (State == ItemState.Held || State == ItemState.Free)
            {
                // 一旦被玩家拿走或者脱落，立马现原形
                hiddenByTable = false; 
                if (!visualRoot.gameObject.activeSelf) visualRoot.gameObject.SetActive(true);
            }
            else
            {
                // 只要还放在那儿，就藏着
                if (visualRoot.gameObject.activeSelf) visualRoot.gameObject.SetActive(false);
            }
            return;
        }

        // 普通状态下始终显示
        if (!visualRoot.gameObject.activeSelf) visualRoot.gameObject.SetActive(true);
    }

    private void UpdateVisuals()
    {
        if (singlePlatePrefab == null) return;

        // 清理旧网格
        foreach (var p in visualPlates)
        {
            if (p != null) Destroy(p);
        }
        visualPlates.Clear();

        // 实例化新网格叠放
        for (int i = 0; i < plateCount; i++)
        {
            GameObject plate = Instantiate(singlePlatePrefab, visualRoot);
            plate.transform.localPosition = new Vector3(0, i * stackYOffset, 0);
            plate.transform.localRotation = Quaternion.identity;
            visualPlates.Add(plate);
        }

        // 动态扩大碰撞体的高度和中心，使其包裹住所有垒起来的盘子
        if (stackCollider != null)
        {
            float newSizeY = baseColliderSizeY + (plateCount - 1) * stackYOffset;
            float newCenterY = baseColliderCenterY + (plateCount - 1) * stackYOffset * 0.5f;

            stackCollider.size = new Vector3(stackCollider.size.x, newSizeY, stackCollider.size.z);
            stackCollider.center = new Vector3(stackCollider.center.x, newCenterY, stackCollider.center.z);
        }
    }
}

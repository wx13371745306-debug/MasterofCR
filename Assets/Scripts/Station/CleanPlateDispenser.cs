using UnityEngine;
using System.Collections.Generic;

public class CleanPlateDispenser : MonoBehaviour
{
    [Header("Dispenser Setting")]
    public int availablePlates = 0;
    public ItemPlacePoint dispensePoint;
    public CarryableItem cleanPlatePrefab; 

    [Header("Visual Stack")]
    public GameObject singlePlateVirtualModel;
    public Transform visualRoot;
    public float stackYOffset = 0.05f;

    private List<GameObject> visualPlates = new List<GameObject>();

    void Start()
    {
        UpdateVisuals();
    }

    private float errorSpamTimer = 0f;

    void Update()
    {
        if (errorSpamTimer > 0f) errorSpamTimer -= Time.deltaTime;

        // 如果我们有库存，并且分发点是空的，就自动弹出一个真实的盘子供玩家拿取
        // 联机修改：必须仅在服务端（或者单机模式）才实际生成真正的盘子，避免客户端生成本地幽灵盘子！
        if (availablePlates > 0 && dispensePoint != null && dispensePoint.CurrentItem == null)
        {
            if (cleanPlatePrefab != null && Mirror.NetworkServer.active)
            {
                CarryableItem newPlate = Instantiate(cleanPlatePrefab, dispensePoint.attachPoint.position, Quaternion.identity);
                Mirror.NetworkServer.Spawn(newPlate.gameObject);
                if (dispensePoint.TryAcceptItem(newPlate))
                {
                    availablePlates--;
                    UpdateVisuals();
                }
                else
                {
                    Destroy(newPlate.gameObject);
                    if (errorSpamTimer <= 0f)
                    {
                        Debug.LogError("[CleanPlateDispenser] 生成实体盘子失败！因为下方的 Dispense Point 拒绝接收。请检查它的 AllowedCategories 中是否允许了 CleanPlate！");
                        errorSpamTimer = 3f;
                    }
                }
            }
        }
    }

    public void AddPlate(int count = 1)
    {
        availablePlates += count;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (singlePlateVirtualModel == null || visualRoot == null) return;

        foreach (var p in visualPlates)
        {
            if (p != null) Destroy(p);
        }
        visualPlates.Clear();

        // 根据当前的可用存货数量，渲染那一叠盘子的外观
        for (int i = 0; i < availablePlates; i++)
        {
            GameObject plate = Instantiate(singlePlateVirtualModel, visualRoot);
            plate.transform.localPosition = new Vector3(0, i * stackYOffset, 0);
            plate.transform.localRotation = Quaternion.identity;
            visualPlates.Add(plate);
        }
    }
}

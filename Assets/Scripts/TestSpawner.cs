using UnityEngine;

public class TestSpawner : MonoBehaviour
{
    public CustomerGroup groupPrefab; // 把 CustomerGroup 挂在一个空物体上做成 Prefab 拖进来
    public OrderResponse targetTable; // 拖入场景里的一张桌子
    public Transform spawnPoint;      // 拖入你刚建的出生点

    [ContextMenu("测试：生成一波2人顾客")]
    public void SpawnTest()
    {
        CustomerGroup group = Instantiate(groupPrefab);
        group.InitGroup(2, targetTable, spawnPoint); // 生成2个人
    }
}
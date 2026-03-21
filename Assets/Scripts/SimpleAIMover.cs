using UnityEngine;
using UnityEngine.AI; // 必须引入这个命名空间

public class SimpleAIMover : MonoBehaviour
{
    [Header("目标设置")]
    public Transform targetTransform; // 这里拖入你那个子节点

    private NavMeshAgent agent;

    void Start()
    {
        // 获取身上的 NavMeshAgent 组件
        agent = GetComponent<NavMeshAgent>();

        if (targetTransform != null)
        {
            // 核心指令：设置导航目标点
            // 我们直接取子节点的世界坐标
            agent.SetDestination(targetTransform.position);
        }
    }
}
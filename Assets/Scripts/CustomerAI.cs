using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAI : MonoBehaviour
{
    [Header("Components")]
    public NavMeshAgent agent;
    public Collider aiCollider;

    [Header("Movement Settings")]
    [Tooltip("距离集结点多远就算到达（调大可以防止小人挤在一起）")]
    public float arrivalRadius = 1.0f; // 1米范围内都算到达

    [Header("Debug")]
    public bool debugLog = true;

    private Transform targetChair;
    private Action onSeatedCallback; 
    private bool isSittingDown = false;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (aiCollider == null) aiCollider = GetComponent<Collider>();
    }

    // 【修改】：接收集结点和椅子两个位置
    public void MoveToChair(Transform approachPoint, Transform chair, Action onSeated)
    {
        targetChair = chair;
        onSeatedCallback = onSeated;
        
        // 1. 开启导航，目标设为那片开阔的空地（集结点）！
        agent.enabled = true;
        agent.SetDestination(approachPoint.position); 
        
        if (debugLog) Debug.Log($"<color=#00FF00>[CustomerAI]</color> {gameObject.name} 正在前往桌子集结点: {approachPoint.name}");
    }

    void Update()
    {
        // 【修改点】：将 agent.stoppingDistance 替换成我们自定义的 arrivalRadius
        if (!isSittingDown && !agent.pathPending && agent.remainingDistance <= arrivalRadius)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f || agent.remainingDistance <= arrivalRadius)
            {
                // 到达集结点的宽泛区域了，立刻开始入座流程！
                StartCoroutine(SitDownRoutine());
            }
        }
    }

    private IEnumerator SitDownRoutine()
    {
        isSittingDown = true;

        // 3. 核心精髓：一到集结点，立刻关闭物理碰撞和导航！
        // 这样接下来走向椅子的过程，就算是穿墙、穿桌子，也不会卡住了！
        agent.enabled = false;
        if (aiCollider != null) aiCollider.enabled = false;

        // 【新增】：走向椅子的过渡动画（你可以调整 duration 控制走过去的速度）
        Vector3 currentPos = transform.position;
        float walkToChairDuration = 1.0f; 
        float tWalk = 0;

        while (tWalk < 1f)
        {
            tWalk += Time.deltaTime / walkToChairDuration;
            // 平滑地从集结点飘（走）向椅子的真实位置
            transform.position = Vector3.Lerp(currentPos, targetChair.position, tWalk);
            
            // 顺便把身体转过去面朝椅子
            Vector3 direction = (targetChair.position - currentPos).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), tWalk);
            }
            yield return null;
        }

        // 4. 原来的落座对齐代码保持不变
        transform.SetParent(targetChair);
        
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        float sitDuration = 0.5f; 
        float tSit = 0;

        while (tSit < 1f)
        {
            tSit += Time.deltaTime / sitDuration;
            transform.localPosition = Vector3.Lerp(startPos, Vector3.zero, tSit);
            transform.localRotation = Quaternion.Lerp(startRot, Quaternion.identity, tSit); 
            yield return null;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (debugLog) Debug.Log($"<color=#00FF00>[CustomerAI]</color> {gameObject.name} 完美落座完毕！");

        onSeatedCallback?.Invoke();
    }
}
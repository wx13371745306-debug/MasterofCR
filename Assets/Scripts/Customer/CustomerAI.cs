using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Mirror;

/// <summary>
/// 顾客 AI：服务端驱动寻路与入座；位姿由 NetworkTransform（建议 coordinateSpace=World）全程同步。
/// 入座/离座状态用 SyncVar 驱动各端动画与 Agent/Collider，不再关 NT、不再 Rpc 对齐 Transform。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAI : NetworkBehaviour
{
    [Header("Components")]
    public NavMeshAgent agent;
    public Collider aiCollider;

    [Header("Random Model")]
    [Tooltip("可用的人物模型预制体，生成时随机选一个")]
    public GameObject[] modelPrefabs;

    [Tooltip("顾客专用 Animator Controller（脚本会自动分配给模型）")]
    public RuntimeAnimatorController customerAnimController;

    [Header("Movement Settings")]
    [Tooltip("距离集结点多远就算到达（调大可以防止小人挤在一起）")]
    public float arrivalRadius = 1.0f;

    [Header("Debug")]
    public bool debugLog = false;

    [SyncVar] public int syncTableId = -1;
    [SyncVar] public int syncChairIndex = -1;

    [SyncVar(hook = nameof(OnSyncSeatedChanged))]
    bool syncIsSeated;

    /// <summary>是否已入座（SyncVar）；供外部查询并消除对 backing 字段的未使用警告。</summary>
    public bool IsSeatedSynced => syncIsSeated;

    private Transform targetChair;
    private Action onSeatedCallback;
    private bool isSittingDown = false;
    private bool isLeaving;
    private Animator modelAnimator;
    private bool guestInitialized;
    private Vector3 prevPos;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (aiCollider == null) aiCollider = GetComponent<Collider>();

        SpawnRandomModel();
    }

    private void SpawnRandomModel()
    {
        if (modelPrefabs == null || modelPrefabs.Length == 0) return;

        int idx = UnityEngine.Random.Range(0, modelPrefabs.Length);
        GameObject model = Instantiate(modelPrefabs[idx], transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        modelAnimator = model.GetComponentInChildren<Animator>();
        if (modelAnimator != null)
        {
            if (customerAnimController != null)
                modelAnimator.runtimeAnimatorController = customerAnimController;
            modelAnimator.applyRootMotion = false;
        }

        MeshRenderer placeholder = GetComponent<MeshRenderer>();
        if (placeholder != null) placeholder.enabled = false;
    }

    void OnSyncSeatedChanged(bool oldVal, bool newVal)
    {
        isSittingDown = newVal;
        SetSitting(newVal);
        if (newVal)
        {
            if (agent != null) agent.enabled = false;
            if (aiCollider != null) aiCollider.enabled = false;
        }
    }

    /// <summary>Guest：SyncVar 已同步后缓存椅子引用（动画等）。</summary>
    void TryGuestInit()
    {
        if (guestInitialized || isServer) return;
        if (syncTableId < 0 || syncChairIndex < 0) return;

        guestInitialized = true;

        var tables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
        OrderResponse table = null;
        foreach (var t in tables)
        {
            if (t.tableId == syncTableId) { table = t; break; }
        }

        if (table == null || syncChairIndex >= table.chairs.Count)
        {
            if (debugLog) Debug.LogWarning($"[CustomerAI] Guest 找不到桌子 tableId={syncTableId} 或椅子索引越界");
            return;
        }

        targetChair = table.chairs[syncChairIndex];
        if (agent != null) agent.enabled = false;
        if (debugLog) Debug.Log($"[CustomerAI] Guest 端缓存椅子引用 桌号={syncTableId} 椅子={syncChairIndex}");
    }

    public void MoveToChair(Transform approachPoint, Transform chair, Action onSeated)
    {
        targetChair = chair;
        onSeatedCallback = onSeated;

        agent.enabled = true;
        agent.SetDestination(approachPoint.position);

        if (debugLog) Debug.Log($"<color=#00FF00>[CustomerAI]</color> {gameObject.name} 正在前往桌子集结点: {approachPoint.name}");
    }

    void Update()
    {
        if (!isServer)
        {
            TryGuestInit();
            UpdateAnimation();
            return;
        }

        UpdateAnimation();

        if (isLeaving) return;

        if (!isSittingDown && agent.enabled && !agent.pathPending && agent.remainingDistance <= arrivalRadius)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f || agent.remainingDistance <= arrivalRadius)
            {
                StartCoroutine(SitDownRoutine());
            }
        }
    }

    private void UpdateAnimation()
    {
        if (modelAnimator == null) return;

        bool isWalking;
        if (isServer || (!guestInitialized && agent != null && agent.enabled))
        {
            isWalking = agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.01f;
        }
        else
        {
            float delta = (transform.position - prevPos).sqrMagnitude;
            isWalking = delta > 0.0001f;
            prevPos = transform.position;
        }
        modelAnimator.SetBool("IsWalking", isWalking);
    }

    private void SetWalking(bool walking)
    {
        if (modelAnimator != null)
            modelAnimator.SetBool("IsWalking", walking);
    }

    private void SetSitting(bool sitting)
    {
        if (modelAnimator != null)
            modelAnimator.SetBool("IsSitting", sitting);
    }

    private IEnumerator SitDownRoutine()
    {
        if (targetChair == null) yield break;

        isSittingDown = true;

        agent.enabled = false;
        if (aiCollider != null) aiCollider.enabled = false;

        SetWalking(false);

        Vector3 currentPos = transform.position;
        float walkToChairDuration = 1.0f;
        float tWalk = 0;

        while (tWalk < 1f)
        {
            tWalk += Time.deltaTime / walkToChairDuration;
            if (targetChair == null) yield break;
            transform.position = Vector3.Lerp(currentPos, targetChair.position, tWalk);

            Vector3 direction = (targetChair.position - currentPos).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), tWalk);
            }
            yield return null;
        }

        transform.SetParent(targetChair);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        syncIsSeated = true;

        if (debugLog) Debug.Log($"<color=#00FF00>[CustomerAI]</color> {gameObject.name} 落座完毕（NT 同步位姿）。");

        onSeatedCallback?.Invoke();
    }

    [ClientRpc]
    void RpcBeginLeave()
    {
        if (isServer) return;
        if (aiCollider != null) aiCollider.enabled = true;
    }

    /// <summary>
    /// 离座并走向消失点，到达后销毁自身并回调（用于耐心归零）。
    /// </summary>
    public void BeginLeave(Transform exit, Action onDestroyed)
    {
        if (isLeaving) return;
        isLeaving = true;
        StopAllCoroutines();
        StartCoroutine(LeaveRoutine(exit, onDestroyed));
    }

    private IEnumerator LeaveRoutine(Transform exit, Action onDestroyed)
    {
        syncIsSeated = false;
        transform.SetParent(null);

        if (isServer)
            RpcBeginLeave();

        if (exit == null)
        {
            if (debugLog) Debug.Log($"[PatienceLeave][{name}] 消失点为 NULL → 原地销毁（若需走路，请在 CustomerSpawner 配置 customerExitPoint）");
            onDestroyed?.Invoke();
            if (NetworkServer.active)
                NetworkServer.Destroy(gameObject);
            else
                Destroy(gameObject);
            yield break;
        }

        if (aiCollider != null) aiCollider.enabled = true;

        if (agent != null)
        {
            agent.enabled = true;
            bool sampled = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas);
            if (debugLog)
            {
                Debug.Log($"[PatienceLeave][{name}] 离场目标={exit.name} | NavMesh.SamplePosition={(sampled ? "OK" : "失败")} | isOnNavMesh={agent.isOnNavMesh}");
            }
            if (sampled)
                agent.Warp(hit.position);
            if (!agent.isOnNavMesh && debugLog)
                Debug.LogWarning($"[PatienceLeave][{name}] Agent 不在 NavMesh 上，可能无法走向消失点，请检查椅子旁采样/烘焙。");
            agent.SetDestination(exit.position);
        }
        else if (debugLog)
        {
            Debug.LogWarning($"[PatienceLeave][{name}] NavMeshAgent 为空，无法走路离场");
        }

        const float arriveDist = 0.75f;
        float timeout = 45f;
        while (agent != null && agent.enabled && agent.isOnNavMesh && timeout > 0f)
        {
            if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= arriveDist)
                break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        SetWalking(false);

        if (debugLog && timeout <= 0f && agent != null)
            Debug.LogWarning($"[PatienceLeave][{name}] 离场超时(45s)，仍销毁。remainingDist={agent.remainingDistance:F2} pathPending={agent.pathPending}");

        onDestroyed?.Invoke();
        if (NetworkServer.active)
            NetworkServer.Destroy(gameObject);
        else
            Destroy(gameObject);
    }
}

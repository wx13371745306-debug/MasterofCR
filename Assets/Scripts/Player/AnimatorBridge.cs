using UnityEngine;

public class AnimatorBridge : MonoBehaviour
{
    [Header("Animator 参数名（需与 Controller 中一致）")]
    [SerializeField] private string isWalkingID = "IsWalking";

    [Header("移动判定阈值")]
    [SerializeField] private float moveEpsilon = 0.1f;

    [Header("调试")]
    [SerializeField] private bool debugLog = true;

    private Rigidbody rb;
    private Animator animator;
    private bool lastIsWalking;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        animator = GetComponentInChildren<Animator>();

        if (rb == null)
            Debug.LogError("[AnimatorBridge] 未找到 Rigidbody!");
        if (animator == null)
            Debug.LogError("[AnimatorBridge] 未找到 Animator!");
    }

    private void Update()
    {
        if (rb == null || animator == null) return;

        Vector3 v = rb.linearVelocity;
        float horizontalSqr = v.x * v.x + v.z * v.z;
        bool isWalking = horizontalSqr > moveEpsilon * moveEpsilon;

        if (isWalking != lastIsWalking)
        {
            if (debugLog) Debug.Log($"[AnimatorBridge] {gameObject.name} 的动画状态切换为: {(isWalking ? "走路" : "静止")} (速度大小: {Mathf.Sqrt(horizontalSqr):F2})");
            lastIsWalking = isWalking;
        }

        animator.SetBool(isWalkingID, isWalking);
    }
}
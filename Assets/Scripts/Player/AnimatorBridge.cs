using UnityEngine;

public class AnimatorBridge : MonoBehaviour
{
    [Header("Animator Params (match your controller)")]
    [SerializeField] private string horID = "Hor";
    [SerializeField] private string vertID = "Vert";
    [SerializeField] private string stateID = "State";
    [SerializeField] private string jumpID = "IsJump";

    [Header("Tuning")]
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float moveEpsilon = 0.05f;

    [Header("Ground Check (optional)")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDist = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody rb;
    private Animator animator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if (rb == null)
            Debug.LogError("[AnimatorBridge] No Rigidbody found on Player root.");

        if (animator == null)
            Debug.LogError("[AnimatorBridge] No Animator found in children. Make sure Character is a child of Player.");
    }

    private void Update()
    {
        if (rb == null || animator == null) return;

        Vector3 v = rb.linearVelocity; // 如果这里报错，改成 rb.velocity
        Vector3 vXZ = new Vector3(v.x, 0f, v.z);

        Vector3 local = transform.InverseTransformDirection(vXZ);

        float norm = Mathf.Max(runSpeed, 0.01f);
        float hor = Mathf.Clamp(local.x / norm, -1f, 1f);
        float vert = Mathf.Clamp(local.z / norm, -1f, 1f);

        animator.SetFloat(horID, hor, 0.1f, Time.deltaTime);
        animator.SetFloat(vertID, vert, 0.1f, Time.deltaTime);

        bool isMoving = vXZ.magnitude > moveEpsilon;
        bool isRun = isMoving && vXZ.magnitude > (runSpeed * 0.6f);
        animator.SetFloat(stateID, isRun ? 1f : 0f);

        bool grounded = CheckGrounded();
        animator.SetBool(jumpID, !grounded);
    }

    private bool CheckGrounded()
    {
        Vector3 origin = groundCheck != null ? groundCheck.position : (transform.position + Vector3.up * 0.1f);
        return Physics.Raycast(origin, Vector3.down, groundCheckDist, groundMask, QueryTriggerInteraction.Ignore);
    }
}
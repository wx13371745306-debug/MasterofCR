using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMoveRB : MonoBehaviour
{
    public float moveSpeed = 6f;      // 最大水平速度
    public float acceleration = 25f;  // 加速强度，越大越“跟手”

    private Rigidbody rb;
    private Vector2 moveInput;

    private bool horizontalPositionLocked;
    private Vector2 lockedXZ;

    /// <summary>锁定水平位置（X/Z），Y 仍受重力等影响。用于商店等界面。</summary>
    public void SetHorizontalPositionLocked(bool locked)
    {
        horizontalPositionLocked = locked;
        if (locked && rb != null)
        {
            Vector3 p = rb.position;
            lockedXZ = new Vector2(p.x, p.z);
        }
    }

    public bool IsHorizontalPositionLocked => horizontalPositionLocked;

    private void Start()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>获取实际移动速度（含羁绊加成）。不修改 moveSpeed 字段本身。</summary>
    float GetEffectiveMoveSpeed()
    {
        if (BondRuntimeBridge.Instance != null
            && BondRuntimeBridge.Instance.State != null
            && BondRuntimeBridge.Instance.State.IsActive(RecipeBondTag.Meat))
        {
            return moveSpeed * 1.2f;
        }
        return moveSpeed;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 读取 New Input System 的“当前键盘状态”
        // 这样做不需要 PlayerInput、不需要 OnMove 回调，最不容易出 bug
        var k = Keyboard.current;
        if (k == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float x = (k.dKey.isPressed ? 1f : 0f) + (k.aKey.isPressed ? -1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) + (k.sKey.isPressed ? -1f : 0f);

        Vector2 v = new Vector2(x, y);
        moveInput = v.sqrMagnitude > 1f ? v.normalized : v; // 防止斜向更快
    }

    private void FixedUpdate()
    {
        if (horizontalPositionLocked)
        {
            Vector3 p = rb.position;
            p.x = lockedXZ.x;
            p.z = lockedXZ.y;
            rb.position = p;

            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        float effectiveSpeed = GetEffectiveMoveSpeed();
        Vector3 currentVel = rb.linearVelocity;
        Vector3 targetVel = new Vector3(moveInput.x * effectiveSpeed, currentVel.y, moveInput.y * effectiveSpeed);

        // 用“加速度限制”的方式靠近目标速度：不会直接覆盖外力结果，更适合以后撞飞
        Vector3 newVel = Vector3.MoveTowards(currentVel, targetVel, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = newVel;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(inputDir.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, 15f * Time.fixedDeltaTime));
        }

        rb.angularVelocity = Vector3.zero;
    }
}
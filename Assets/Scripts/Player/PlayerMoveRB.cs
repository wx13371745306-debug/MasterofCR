using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMoveRB : MonoBehaviour
{
    public float moveSpeed = 6f;      // 最大水平速度
    public float acceleration = 25f;  // 加速强度，越大越"跟手"

    [Header("冲刺（由运动鞋饰品启用）")]
    [Tooltip("冲刺速度")]
    public float dashSpeed = 30f;
    [Tooltip("冲刺持续时间（秒）")]
    public float dashDuration = 0.15f;
    [Tooltip("冲刺冷却时间（秒）")]
    public float dashCooldown = 1f;

    private Rigidbody rb;
    private Vector2 moveInput;

    private bool horizontalPositionLocked;
    private Vector2 lockedXZ;

    // 冲刺运行时状态
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;
    private PlayerAttributes playerAttributes;

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
        playerAttributes = GetComponent<PlayerAttributes>();
    }

    private void Update()
    {
        var k = Keyboard.current;
        if (k == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float x = (k.dKey.isPressed ? 1f : 0f) + (k.aKey.isPressed ? -1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) + (k.sKey.isPressed ? -1f : 0f);

        Vector2 v = new Vector2(x, y);
        moveInput = v.sqrMagnitude > 1f ? v.normalized : v;

        // 冲刺冷却计时
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        // 冲刺触发：装备运动鞋 + 按下 Shift + 冷却完毕 + 未在冲刺中
        if (playerAttributes != null && playerAttributes.hasDash
            && k.leftShiftKey.wasPressedThisFrame
            && dashCooldownTimer <= 0f && !isDashing
            && !horizontalPositionLocked)
        {
            isDashing = true;
            dashTimer = dashDuration;
            // 冲刺方向：玩家当前朝向
            dashDirection = transform.forward;
        }
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

        // 冲刺中：高速直线位移，忽略正常移动输入
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector3(
                dashDirection.x * dashSpeed,
                rb.linearVelocity.y,
                dashDirection.z * dashSpeed);

            if (dashTimer <= 0f)
            {
                isDashing = false;
                dashCooldownTimer = dashCooldown;
            }

            rb.angularVelocity = Vector3.zero;
            return;
        }

        float effectiveSpeed = GetEffectiveMoveSpeed();
        Vector3 currentVel = rb.linearVelocity;
        Vector3 targetVel = new Vector3(moveInput.x * effectiveSpeed, currentVel.y, moveInput.y * effectiveSpeed);

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

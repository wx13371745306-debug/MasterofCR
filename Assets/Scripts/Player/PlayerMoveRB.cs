using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMoveRB : MonoBehaviour
{
    public float moveSpeed = 6f;      // 最大水平速度
    public float acceleration = 25f;  // 加速强度，越大越“跟手”

    private Rigidbody rb;
    private Vector2 moveInput; // WASD 输入 (-1..1)

    private void Start()
    {
        rb.linearVelocity = Vector3.zero;   // 如果报错就用 rb.velocity
        rb.angularVelocity = Vector3.zero;
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
        // 目标水平速度（不动 y，保留重力和被撞飞的竖直速度）
        Vector3 currentVel = rb.linearVelocity;
        Vector3 targetVel = new Vector3(moveInput.x * moveSpeed, currentVel.y, moveInput.y * moveSpeed);

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
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dead-zone soft follow on XZ for a camera rig. Hold Space to follow; release to freeze.
/// Movement is smoothed; max speed per second is capped by <see cref="maxFollowSpeed"/>.
/// </summary>
public class CameraFollowRig : MonoBehaviour
{
    public Transform target;
    public float deadZoneRadius = 4f;
    public float smoothTime = 0.25f;
    [Tooltip("Max rig movement speed (units/sec). SmoothDamp caps velocity at this. Use 0 for no cap.")]
    public float maxFollowSpeed = 12f;
    [Tooltip("When enabled, the camera always follows the player without needing to hold Space.")]
    public bool alwaysFollow = false;

    private float fixedHeight;
    private Vector3 smoothVelocity;
    private bool wasFollowing;

    private void Awake()
    {
        fixedHeight = transform.position.y;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        bool following;
        if (alwaysFollow)
        {
            following = true;
        }
        else
        {
            var k = Keyboard.current;
            following = k != null && k.spaceKey.isPressed;
        }

        if (!following)
        {
            wasFollowing = false;
            return;
        }

        Vector3 c = transform.position;
        Vector3 p = target.position;

        if (!wasFollowing)
            smoothVelocity = Vector3.zero;
        wasFollowing = true;

        Vector2 delta = new Vector2(p.x - c.x, p.z - c.z);
        float dist = delta.magnitude;

        Vector3 desired;
        if (dist <= deadZoneRadius)
        {
            desired = c;
        }
        else
        {
            Vector2 dir = delta / dist;
            Vector2 desiredXZ = new Vector2(p.x, p.z) - dir * deadZoneRadius;
            desired = new Vector3(desiredXZ.x, fixedHeight, desiredXZ.y);
        }

        float maxSpeed = maxFollowSpeed > 0f ? maxFollowSpeed : Mathf.Infinity;
        transform.position = Vector3.SmoothDamp(
            c,
            desired,
            ref smoothVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        float y = Application.isPlaying ? fixedHeight : transform.position.y;
        Vector3 center = new Vector3(transform.position.x, y, transform.position.z);
        Gizmos.DrawWireSphere(center, deadZoneRadius);
    }
}

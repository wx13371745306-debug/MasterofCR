using UnityEngine;

public class PlayerHandIK : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("拖入 Player 根物体上的 PlayerItemInteractor")]
    public PlayerItemInteractor interactor;

    [Header("IK 设置")]
    [Tooltip("IK 权重平滑过渡速度")]
    [SerializeField] private float ikSmoothSpeed = 8f;

    [Header("默认偏移（物品没有自定义抓取点时使用）")]
    [SerializeField] private Vector3 leftHandOffset = new Vector3(0.1f, 0f, 0f);
    [SerializeField] private Vector3 rightHandOffset = new Vector3(-0.1f, 0f, 0f);

    private Animator animator;
    private float leftIkWeight;
    private float rightIkWeight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private CarryableItem cachedHeld;

    private void Update()
    {
        if (interactor == null) return;

        CarryableItem held = interactor.GetHeldItem();

        if (held == null)
        {
            Transform hp = interactor.GetHoldPoint();
            if (hp != null)
                held = hp.GetComponentInChildren<CarryableItem>();
        }

        cachedHeld = held;
        bool holding = held != null;

        Transform leftGrip = holding ? held.leftHandGrip : null;
        Transform rightGrip = holding ? held.rightHandGrip : null;
        bool hasLeft = leftGrip != null;
        bool hasRight = rightGrip != null;
        bool hasNeither = !hasLeft && !hasRight;

        float leftTarget = holding && (hasLeft || hasNeither) ? 1f : 0f;
        float rightTarget = holding && (hasRight || hasNeither) ? 1f : 0f;

        leftIkWeight = Mathf.MoveTowards(leftIkWeight, leftTarget, ikSmoothSpeed * Time.deltaTime);
        rightIkWeight = Mathf.MoveTowards(rightIkWeight, rightTarget, ikSmoothSpeed * Time.deltaTime);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || interactor == null) return;

        Transform holdPoint = interactor.GetHoldPoint();
        CarryableItem held = cachedHeld;

        // 左手
        if (leftIkWeight > 0.01f && holdPoint != null)
        {
            Transform leftGrip = held != null ? held.leftHandGrip : null;
            Vector3 leftPos = leftGrip != null
                ? leftGrip.position
                : holdPoint.TransformPoint(leftHandOffset);

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftIkWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftPos);

            if (leftGrip != null)
            {
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftIkWeight);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation);
            }
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        }

        // 右手
        if (rightIkWeight > 0.01f && holdPoint != null)
        {
            Transform rightGrip = held != null ? held.rightHandGrip : null;
            Vector3 rightPos = rightGrip != null
                ? rightGrip.position
                : holdPoint.TransformPoint(rightHandOffset);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightIkWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightPos);

            if (rightGrip != null)
            {
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightIkWeight);
                animator.SetIKRotation(AvatarIKGoal.RightHand, rightGrip.rotation);
            }
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        }
    }
}
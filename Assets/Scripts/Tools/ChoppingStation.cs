using UnityEngine;

public class ChoppingStation : BaseStation
{
    public enum Axis { X, Y, Z }

    [Header("Chopping")]
    public Transform rotatingPart;
    public Axis rotateAxis = Axis.Z;
    public float angleRange = 45f; // 摆动幅度
    public float swingSpeed = 5f;

    private Quaternion initialRotation;
    private float currentPhase = 0f;
    private bool isInteracting = false;

    void Start()
    {
        if (rotatingPart != null)
            initialRotation = rotatingPart.localRotation;
    }

    void Update()
    {
        if (!isInteracting || rotatingPart == null) return;

        currentPhase += Time.deltaTime * swingSpeed;
        
        // 计算当前角度：-angleRange 到 +angleRange
        float angle = Mathf.Sin(currentPhase) * angleRange;

        Vector3 axisVec = Vector3.forward;
        switch (rotateAxis)
        {
            case Axis.X: axisVec = Vector3.right; break;
            case Axis.Y: axisVec = Vector3.up; break;
            case Axis.Z: axisVec = Vector3.forward; break;
        }

        rotatingPart.localRotation = initialRotation * Quaternion.AngleAxis(angle, axisVec);
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return true;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        if (isInteracting) return;

        isInteracting = true;

        if (debugLog)
            Debug.Log($"[ChoppingStation] Begin interact: {name}");
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        if (!isInteracting) return;

        isInteracting = false;

        if (debugLog)
            Debug.Log($"[ChoppingStation] End interact: {name}");
    }
}
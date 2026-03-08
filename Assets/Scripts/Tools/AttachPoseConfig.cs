using UnityEngine;

public class AttachPoseConfig : MonoBehaviour
{
    [Header("Hold Pose")]
    public Vector3 holdLocalPosition = Vector3.zero;
    public Vector3 holdLocalEulerAngles = Vector3.zero;

    [Header("Equip Pose")]
    public Vector3 equipLocalPosition = Vector3.zero;
    public Vector3 equipLocalEulerAngles = Vector3.zero;

    [Header("Place Pose")]
    public Vector3 placeLocalPosition = Vector3.zero;
    public Vector3 placeLocalEulerAngles = Vector3.zero;
}
using UnityEngine;

public class InteractableHighlight : MonoBehaviour
{
    public GameObject highlightObject;
    public bool debugLog = false;

    void Awake()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        if (highlightObject == null)
        {
            if (debugLog)
                Debug.LogWarning($"[InteractableHighlight] highlightObject is null on {name}");
            return;
        }

        highlightObject.SetActive(on);
    }
}

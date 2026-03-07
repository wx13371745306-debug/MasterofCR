using UnityEngine;

public class DishHighlight : MonoBehaviour
{
    public GameObject highlightRing;
    public bool debugLog = true;

    void Awake()
    {
        if (debugLog)
            Debug.Log($"[DishHighlight] Awake on {name}, ring={(highlightRing ? highlightRing.name : "NULL")}");

        SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        if (highlightRing == null)
        {
            if (debugLog)
                Debug.Log($"[DishHighlight] FAIL: highlightRing is NULL on {name}");
            return;
        }

        if (debugLog)
            Debug.Log($"[DishHighlight] SetHighlighted({on}) on {name}. ringWasActive={highlightRing.activeSelf}");

        highlightRing.SetActive(on);
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    [Header("Refs")]
    public InteractSensor sensor;
    public Transform[] holdPoints;     // 现在只放 1 个，以后加第二个

    [Header("Carry Limits")]
    public int capacity = 1;           // 现在=1，以后改成2即可

    [Header("Debug")]
    public bool debugLog = true;

    private readonly List<CarryableDish> held = new List<CarryableDish>();

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            TryPickUp();

        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
            TryDrop();
    }

    public bool IsHoldingSomething()
    {
        return held.Count > 0;
    }

    void TryPickUp()
    {
        if (held.Count >= capacity)
        {
            if (debugLog)
                Debug.Log($"[Interactor] PickUp blocked: held={held.Count}, capacity={capacity}");
            return;
        }

        var dishHighlight = sensor ? sensor.GetCurrentDish() : null;
        if (dishHighlight == null)
        {
            if (debugLog)
                Debug.Log("[Interactor] No current dish to pick up.");
            return;
        }

        var dish = dishHighlight.GetComponentInParent<CarryableDish>();
        if (dish == null)
        {
            if (debugLog)
                Debug.Log($"[Interactor] FAIL: {dishHighlight.name} has no CarryableDish in parent.");
            return;
        }

        int slot = held.Count; // 第0个拿到 HoldPoint_0，第1个拿到 HoldPoint_1...
        if (holdPoints == null || slot >= holdPoints.Length || holdPoints[slot] == null)
        {
            if (debugLog)
                Debug.Log($"[Interactor] FAIL: No hold point for slot {slot}. Check holdPoints array.");
            return;
        }

        dish.PickUp(holdPoints[slot]);
        held.Add(dish);

        if (debugLog)
            Debug.Log($"[Interactor] Picked up: {dish.name}. held={held.Count}/{capacity}, slot={slot}");
    }

    void TryDrop()
    {
        if (held.Count == 0)
        {
            if (debugLog)
                Debug.Log("[Interactor] Drop ignored: nothing held.");
            return;
        }

        var dish = held[held.Count - 1];
        PlaceableSurface surface = sensor ? sensor.GetCurrentSurface() : null;

        held.RemoveAt(held.Count - 1);

        // 优先放桌子
        if (surface != null && surface.CanPlace())
        {
            if (debugLog)
                Debug.Log($"[Interactor] Place on surface: {dish.name} -> {surface.name}");

            dish.PlaceOnSurface(surface);
        }
        else
        {
            if (debugLog)
                Debug.Log($"[Interactor] Drop to ground: {dish.name}");

            dish.Drop();
        }

        if (debugLog)
            Debug.Log($"[Interactor] Held now = {held.Count}/{capacity}");
    }
}
using System.Collections.Generic;
using UnityEngine;

public class InteractSensor : MonoBehaviour
{
    [Header("Dish")]
    public LayerMask dishMask;

    [Header("Surface")]
    public LayerMask surfaceMask;

    [Header("Refs")]
    public Transform playerRoot;
    public Interactor interactor;

    [Header("Debug")]
    public bool debugLog = true;

    private readonly HashSet<DishHighlight> dishCandidates = new HashSet<DishHighlight>();
    private readonly HashSet<PlaceableSurface> surfaceCandidates = new HashSet<PlaceableSurface>();

    private DishHighlight currentDish;
    private PlaceableSurface currentSurface;

    void Update()
    {
        if (playerRoot == null || interactor == null) return;

        if (interactor.IsHoldingSomething())
        {
            // 手里有东西：只处理桌子黄色高亮
            ClearCurrentDish();
            PickNearestSurface();
        }
        else
        {
            // 手里没东西：只处理菜绿色高亮
            ClearCurrentSurface();
            PickNearestDish();
        }
    }

    void PickNearestDish()
    {
        DishHighlight best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = playerRoot.position;

        foreach (var dish in dishCandidates)
        {
            if (dish == null) continue;

            float dist = Vector3.Distance(origin, dish.transform.position);

            if (dist < bestDist - 0.001f)
            {
                bestDist = dist;
                best = dish;
            }
            else if (Mathf.Abs(dist - bestDist) <= 0.001f && best != null)
            {
                if (dish.GetInstanceID() < best.GetInstanceID())
                    best = dish;
            }
        }

        if (best != currentDish)
        {
            if (debugLog)
            {
                string from = currentDish ? currentDish.name : "None";
                string to = best ? best.name : "None";
                Debug.Log($"[Sensor] Dish switch: {from} -> {to}");
            }

            if (currentDish != null)
                currentDish.SetHighlighted(false);

            currentDish = best;

            if (currentDish != null)
            {
                currentDish.SetHighlighted(true);

                if (debugLog)
                    Debug.Log($"[Sensor] Current dish = {currentDish.name}, dist={bestDist:F3}");
            }
        }
    }

    void PickNearestSurface()
    {
        PlaceableSurface best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = playerRoot.position;

        foreach (var surface in surfaceCandidates)
        {
            if (surface == null) continue;
            if (!surface.CanPlace()) continue;

            float dist = Vector3.Distance(origin, surface.transform.position);

            if (dist < bestDist - 0.001f)
            {
                bestDist = dist;
                best = surface;
            }
            else if (Mathf.Abs(dist - bestDist) <= 0.001f && best != null)
            {
                if (surface.GetInstanceID() < best.GetInstanceID())
                    best = surface;
            }
        }

        if (best != currentSurface)
        {
            if (debugLog)
            {
                string from = currentSurface ? currentSurface.name : "None";
                string to = best ? best.name : "None";
                Debug.Log($"[Sensor] Surface switch: {from} -> {to}");
            }

            if (currentSurface != null)
                currentSurface.SetHighlighted(false);

            currentSurface = best;

            if (currentSurface != null)
            {
                currentSurface.SetHighlighted(true);

                if (debugLog)
                    Debug.Log($"[Sensor] Current surface = {currentSurface.name}, dist={bestDist:F3}");
            }
        }
    }

    void ClearCurrentDish()
    {
        if (currentDish != null)
        {
            currentDish.SetHighlighted(false);
            currentDish = null;
        }
    }

    void ClearCurrentSurface()
    {
        if (currentSurface != null)
        {
            currentSurface.SetHighlighted(false);
            currentSurface = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (debugLog)
            Debug.Log($"[Sensor] ENTER: {other.name} (layer={other.gameObject.layer})");

        // Dish
        if (((1 << other.gameObject.layer) & dishMask) != 0)
        {
            DishHighlight dish = other.GetComponentInParent<DishHighlight>();
            if (dish != null)
            {
                bool added = dishCandidates.Add(dish);

                if (debugLog)
                    Debug.Log($"[Sensor] -> Dish {(added ? "ADD" : "ALREADY")} : {dish.name} (count={dishCandidates.Count})");
            }
            else if (debugLog)
            {
                Debug.Log($"[Sensor] -> FAIL: No DishHighlight found in parent chain of {other.name}");
            }
        }

        // Surface
        if (((1 << other.gameObject.layer) & surfaceMask) != 0)
        {
            PlaceableSurface surface = other.GetComponentInParent<PlaceableSurface>();
            if (surface != null)
            {
                bool added = surfaceCandidates.Add(surface);

                if (debugLog)
                    Debug.Log($"[Sensor] -> Surface {(added ? "ADD" : "ALREADY")} : {surface.name} (count={surfaceCandidates.Count})");
            }
            else if (debugLog)
            {
                Debug.Log($"[Sensor] -> FAIL: No PlaceableSurface found in parent chain of {other.name}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (debugLog)
            Debug.Log($"[Sensor] EXIT: {other.name}");

        // Dish
        DishHighlight dish = other.GetComponentInParent<DishHighlight>();
        if (dish != null)
        {
            bool removed = dishCandidates.Remove(dish);

            if (debugLog)
                Debug.Log($"[Sensor] -> Dish {(removed ? "REMOVE" : "NOT FOUND")} : {dish.name} (count={dishCandidates.Count})");

            if (dish == currentDish)
            {
                currentDish.SetHighlighted(false);
                currentDish = null;
            }
        }

        // Surface
        PlaceableSurface surface = other.GetComponentInParent<PlaceableSurface>();
        if (surface != null)
        {
            bool removed = surfaceCandidates.Remove(surface);

            if (debugLog)
                Debug.Log($"[Sensor] -> Surface {(removed ? "REMOVE" : "NOT FOUND")} : {surface.name} (count={surfaceCandidates.Count})");

            if (surface == currentSurface)
            {
                currentSurface.SetHighlighted(false);
                currentSurface = null;
            }
        }
    }

    public DishHighlight GetCurrentDish()
    {
        return currentDish;
    }

    public PlaceableSurface GetCurrentSurface()
    {
        return currentSurface;
    }
}
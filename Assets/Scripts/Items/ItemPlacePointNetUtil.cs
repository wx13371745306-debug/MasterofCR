using UnityEngine;

/// <summary>
/// 无 NetworkIdentity 的 ItemPlacePoint 占位在客户端需靠世界坐标对齐；供 RpcApplyReleaseHeld、成品 Spawn 镜像等共用。
/// </summary>
public static class ItemPlacePointNetUtil
{
    /// <summary>服务端 Cmd 解析槽位时：以 aux 世界坐标搜最近 ItemPlacePoint 的半径。</summary>
    public const float ReleasePlaceSearchRadius = 0.5f;

    /// <summary>纯客户端 Rpc 镜像时：若仅靠 hint 搜不到，用此半径再搜。</summary>
    public const float ReleasePlaceClientSearchRadius = 2.5f;

    /// <summary>与服务端传入的槽位世界坐标对齐时的匹配半径（与 RpcApplyReleaseHeld 一致）。</summary>
    public const float ServerHintMatchRadius = 1.25f;

    /// <summary>联机 ReleasePlace：客户端传入的「锅」netId 解析出 ingredientPlacePoint 后，与 auxWorldPos 的最大允许偏差（防伪造/串槽）。</summary>
    public const float ReleasePlacePotHintMaxDistance = 2.5f;

    /// <summary>按世界坐标在场景中查找最近的指定组件（用于 PlacePoint / FridgeStation 等无 NetworkIdentity 的对象）。</summary>
    public static T FindNearestComponent<T>(Vector3 worldPos, float maxDist) where T : Component
    {
        float bestSqr = maxDist * maxDist;
        T best = null;
        foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            float sqr = (c.transform.position - worldPos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = c; }
        }
        return best;
    }

    /// <summary>客机：用服务端记录的 placePoint 世界坐标找同一场景中的同一槽位（取距离最小且在容差内）。</summary>
    public static ItemPlacePoint FindItemPlacePointNearServerPosition(Vector3 serverPlacePointWorldPos, float maxDist)
    {
        float maxSqr = maxDist * maxDist;
        ItemPlacePoint best = null;
        float bestSqr = float.MaxValue;
        foreach (var p in Object.FindObjectsByType<ItemPlacePoint>(FindObjectsSortMode.None))
        {
            if (p == null) continue;
            float sqr = (p.transform.position - serverPlacePointWorldPos).sqrMagnitude;
            if (sqr <= maxSqr && sqr < bestSqr)
            {
                bestSqr = sqr;
                best = p;
            }
        }
        return best;
    }
}

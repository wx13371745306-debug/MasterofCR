using UnityEngine;

public class BreakOnFloor : MonoBehaviour
{
    [Header("Spawn Prefabs")]
    public GameObject dirtyStainPrefab;
    public GameObject breakEffectPrefab;

    [Header("Spawn Settings")]
    public Vector3 stainOffset = new Vector3(0f, 0.02f, 0f);
    public Vector3 effectOffset = Vector3.zero;

    [Header("Debug")]
    public bool debugLog = true;

    private bool broken = false;

    public void Break()
    {
        if (broken) return;
        broken = true;

        Vector3 basePos = transform.position;

        // 生成污渍（固定贴地，不继承物体旋转）
        if (dirtyStainPrefab != null)
        {
            Vector3 stainPos = basePos + stainOffset;
            Instantiate(dirtyStainPrefab, stainPos, Quaternion.identity);

            if (debugLog)
                Debug.Log($"[BreakOnFloor] Spawn stain for {name}");
        }

        // 生成粒子效果（播放一遍）
        if (breakEffectPrefab != null)
        {
            Vector3 effectPos = basePos + effectOffset;
            GameObject fx = Instantiate(breakEffectPrefab, effectPos, Quaternion.identity);

            // 自动播放粒子系统
            ParticleSystem ps = fx.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }

            if (debugLog)
                Debug.Log($"[BreakOnFloor] Spawn break effect for {name}");
        }

        if (debugLog)
            Debug.Log($"[BreakOnFloor] Destroy broken item: {name}");

        Destroy(gameObject);
    }
}
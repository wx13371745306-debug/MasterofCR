using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class FryPot : MonoBehaviour
{
    [Header("Refs")]
    public FryRecipeDatabase recipeDatabase;
    // 删除：public GameObject failedDishPrefab; (已经移交给 Database 管理)

    [Tooltip("专门用于放入原材料的锅内 PlacePoint。食材放上去后会被锅自动吸收。")]
    public ItemPlacePoint ingredientPlacePoint;

    [Header("Visual Settings")]
    public Transform visualContainer;
    public float spawnRandomRange = 0.08f;

    [Header("State")]
    public float currentProgress;
    public float requiredProgress;
    public bool cookingFinished;

    [Header("糊菜倒计时")]
    [Tooltip("烹饪完成后多少秒菜会糊掉")]
    [SerializeField] private float burnTime = 10f;
    [Tooltip("前多少秒为安全期（进度条不闪烁）")]
    [SerializeField] private float burnSafeTime = 4f;
    private float burnElapsed;
    private bool isBurnCountdown;

    public bool IsBurnCountdown => isBurnCountdown;
    /// <summary>供网络同步读取糊菜倒计时内部进度。</summary>
    public float BurnElapsedNetwork => burnElapsed;
    public float BurnRatio => burnTime > 0f ? Mathf.Clamp01(burnElapsed / burnTime) : 0f;
    public float BurnSafeRatio => burnTime > 0f ? Mathf.Clamp01(burnSafeTime / burnTime) : 0f;

    /// <summary>本帧是否由某个 FryStation 判定为「锅在台上、可加热」。</summary>
    public bool ReceivesStationHeat { get; private set; }

    FryStation _heatFromStation;

    [Header("品质系统")]
    [Tooltip("厨具等级暴击加成 T")]
    [SerializeField] private float kitchenwareCritBonus = 0.1f;
    [Tooltip("羁绊状态资产，用于读取激活羁绊的暴击加成 B")]
    [SerializeField] private BondActivationStateSO bondState;

    public DishQuality LastServedQuality { get; private set; }

    [Header("Debug")]
    public bool debugLog = true;

    [Header("UI")]
    public TableOrderProgressUI progressUI;

    private readonly Dictionary<string, int> materials = new Dictionary<string, int>(System.StringComparer.Ordinal);
    private FryRecipeDatabase.FryRecipe finishedRecipe;

    private readonly List<PlayerAttributes> contributorPlayers = new List<PlayerAttributes>();
    private readonly List<float> ingredientFreshnessValues = new List<float>();
    private int rottenCount;
    private DishQuality resolvedQuality;

    private List<GameObject> spawnedIngredientVisuals = new List<GameObject>();
    private GameObject spawnedFinishedVisual;

    /// <summary>服务端：按下锅顺序记录食材 id，供 <see cref="FryPotNetworkSync"/> 同步到客户端重建散件视觉。</summary>
    readonly List<string> networkIngredientVisualOrder = new List<string>();

    /// <summary>客户端：由联机同步重建的锅内散件（非服务端 Instantiate）。</summary>
    readonly List<GameObject> clientIngredientVisualReplicas = new List<GameObject>();

    void OnEnable()
    {
        // 订阅事件：当有物品放到锅的专属放置点时，自动触发吸收逻辑
        if (ingredientPlacePoint != null)
        {
            ingredientPlacePoint.OnItemPlacedEvent += OnIngredientPlaced;
        }
    }

    void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        if (ingredientPlacePoint != null)
        {
            ingredientPlacePoint.OnItemPlacedEvent -= OnIngredientPlaced;
        }
        ReceivesStationHeat = false;
        _heatFromStation = null;
    }

    void Start()
    {
        // 游戏开始时，检查一下锅里是不是已经有东西了（初始化兜底；联机仅服务端吸收）
        if (ingredientPlacePoint != null && ingredientPlacePoint.CurrentItem != null && NetworkServer.active)
        {
            OnIngredientPlaced(ingredientPlacePoint.CurrentItem);
        }
    }

    /// <summary>由 FryStation 在锅放置于台上时调用；离开台子则不会推进糊菜时间。</summary>
    public void AdvanceBurn(float deltaTime)
    {
        if (!isBurnCountdown || deltaTime <= 0f) return;
        burnElapsed += deltaTime;
        if (burnElapsed >= burnTime)
        {
            if (debugLog) Debug.Log("[FryPot] 糊菜倒计时结束，菜糊了！", this);
            isBurnCountdown = false;
            cookingFinished = true;
            SpoilFinishedDish();
        }
    }

    /// <summary>
    /// 由 FryStation 调用：本站本帧是否架着此锅。
    /// 多口煎炸台并存时，仅「当前绑定本站」的 false 会清除加热状态，避免被别站误清。
    /// </summary>
    public void NotifyStationHeat(FryStation source, bool onThisStation)
    {
        if (onThisStation)
        {
            _heatFromStation = source;
            ReceivesStationHeat = true;
            return;
        }
        if (_heatFromStation == source)
        {
            ReceivesStationHeat = false;
            _heatFromStation = null;
        }
    }

    // 当有食材被放置进来时，由 ItemPlacePoint 的事件主动调用这个方法
    void OnIngredientPlaced(CarryableItem item)
    {
        if (item == null || cookingFinished || isBurnCountdown) return;
        if (!Mirror.NetworkServer.active) return;

        FryIngredientTag tag = item.GetComponent<FryIngredientTag>();
        if (tag == null)
        {
            if (debugLog) Debug.LogWarning($"[FryPot] 物体 '{item.name}' 没有 FryIngredientTag，被忽略", item);
            return;
        }

        string id = tag.NormalizedIngredientId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning(
                $"[FryPot] 物体带有 FryIngredientTag 但 ingredientId 为空或仅空白，无法识别为食材: {item.name}",
                item);
            return;
        }

        FryableItem fry = item.GetComponent<FryableItem>();
        if (fry == null)
        {
            Debug.LogWarning(
                $"[FryPot] 物体带有 FryIngredientTag 但缺少 FryableItem，无法下锅: {item.name} (ingredientId={id})",
                item);
            return;
        }

        if (fry.visualInPotPrefab != null && visualContainer != null)
        {
            GameObject v = Instantiate(fry.visualInPotPrefab, visualContainer);
            v.transform.localPosition = Random.insideUnitSphere * spawnRandomRange;
            v.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            spawnedIngredientVisuals.Add(v);
        }

        PlayerAttributes holder = item.GetComponent<CarryableItem>()?.lastHolderPlayer;
        if (holder != null && !contributorPlayers.Contains(holder))
            contributorPlayers.Add(holder);

        DecayableProp decay = item.GetComponent<DecayableProp>();
        if (decay != null)
        {
            float f = decay.CurrentState switch
            {
                DecayableProp.DecayState.Fresh  => 1.5f,
                DecayableProp.DecayState.Stale  => 1.0f,
                _                               => 0f,
            };
            ingredientFreshnessValues.Add(f);
            if (decay.CurrentState == DecayableProp.DecayState.Rotten)
                rottenCount++;
        }

        int addValue = materials.Count == 0 ? fry.baseRequired : fry.addedRequired;
        requiredProgress += addValue;

        if (!materials.ContainsKey(id)) materials[id] = 0;
        materials[id]++;
        networkIngredientVisualOrder.Add(id);

        ingredientPlacePoint.ClearOccupant();
        if (debugLog)
        {
            Debug.Log($"[FryPot] 吸收食材: '{item.name}' → ingredientId='{id}'  " +
                      $"当前锅内: {DumpMaterials()}  " +
                      $"进度: {currentProgress}/{requiredProgress}  " +
                      $"贡献玩家数: {contributorPlayers.Count}  腐烂数: {rottenCount}", this);
        }
        StartCoroutine(DestroyIngredientDeferred(item.gameObject));
    }

    private System.Collections.IEnumerator DestroyIngredientDeferred(GameObject obj)
    {
        yield return null;
        if (obj == null) yield break;
        obj.SetActive(false);
        if (NetworkServer.active)
            NetworkServer.Destroy(obj);
        else
            Destroy(obj);
    }

    public bool HasAnyIngredient()
    {
        if (materials.Count > 0) return true;
        // 客户端无 materials 字典，用同步后的进度判断锅内是否有菜
        if (NetworkClient.active && !NetworkServer.active && requiredProgress > 0.001f)
            return true;
        return false;
    }
    public bool CanReceiveProgress() => !cookingFinished && !isBurnCountdown && requiredProgress > 0f;

    public void AddProgress(float amount)
    {
        if (!CanReceiveProgress() || amount <= 0f) return;
        currentProgress += amount;
        if (currentProgress >= requiredProgress)
        {
            currentProgress = requiredProgress;
            ResolveRecipe();
        }
    }

    void ResolveRecipe()
    {
        if (cookingFinished || isBurnCountdown) return;

        ClearIngredientVisuals();

        if (recipeDatabase == null)
        {
            cookingFinished = true;
            if (debugLog) Debug.LogWarning("[FryPot] Resolve failed: recipeDatabase is null.");
            return;
        }

        if (debugLog)
            Debug.Log($"[FryPot] === 开始匹配菜谱 ===  锅内材料({materials.Count}种): {DumpMaterials()}", this);

        finishedRecipe = recipeDatabase.FindMatch(materials);

        if (finishedRecipe == null)
        {
            cookingFinished = true;
            Debug.LogWarning("[FryPot] 匹配失败: 无匹配菜谱，且未配置兜底 FailedDish!", this);
            return;
        }

        bool isFailed = (finishedRecipe == recipeDatabase.failedDishRecipe);

        if (debugLog)
            Debug.Log($"[FryPot] 匹配结果: '{finishedRecipe.recipeName}' " +
                      (isFailed ? "(兜底/失败菜，直接完成)" : $"(正常菜谱，进入{burnTime}s糊菜倒计时)"), this);

        if (finishedRecipe.finishedVisualPrefab != null && visualContainer != null)
            spawnedFinishedVisual = Instantiate(finishedRecipe.finishedVisualPrefab, visualContainer);

        if (isFailed)
        {
            cookingFinished = true;
        }
        else
        {
            resolvedQuality = RollDishQuality();
            isBurnCountdown = true;
            burnElapsed = 0f;
        }
    }

    DishQuality RollDishQuality()
    {
        // P：取所有贡献玩家中最高的 baseCritRate
        float P = 0f;
        foreach (var p in contributorPlayers)
            if (p != null && p.baseCritRate > P) P = p.baseCritRate;

        float T = kitchenwareCritBonus;
        float B = bondState != null ? bondState.GetTotalActiveCritBonus() : 0f;

        // 平均新鲜度（无 DecayableProp 的食材不参与计算）
        float avgF = 1f;
        if (ingredientFreshnessValues.Count > 0)
        {
            float sum = 0f;
            foreach (float f in ingredientFreshnessValues) sum += f;
            avgF = sum / ingredientFreshnessValues.Count;
        }

        float rawC = avgF * (P + T + B + 0.10f);

        // 一票否决：有腐烂食材则暴击率归零
        if (rottenCount >= 1) rawC = 0f;

        float critRate = Mathf.Clamp01(rawC);
        float flawRate = Mathf.Clamp01(rottenCount * 0.5f);

        float roll1 = Random.value;
        float roll2 = Random.value;

        DishQuality result;
        if (roll1 < critRate)
            result = DishQuality.Critical;
        else if (roll2 < flawRate)
            result = DishQuality.Flawed;
        else
            result = DishQuality.Normal;

        if (debugLog)
            Debug.Log($"[FryPot] 品质判定: P={P:P0} T={T:P0} B={B:P0} avgF={avgF:F2} " +
                      $"critRate={critRate:P1} flawRate={flawRate:P1} " +
                      $"roll1={roll1:F3} roll2={roll2:F3} → {result}", this);
        return result;
    }

    public bool CanDump() => HasAnyIngredient() || cookingFinished || isBurnCountdown;

    public void ForceClear()
    {
        // 与 Serve() 一致：纯客户端禁止改权威状态，联机须走 PlayerNetworkController.CmdRequestFryPotDump
        if (NetworkClient.active && !NetworkServer.active)
        {
            if (debugLog)
                Debug.LogWarning("[FryPot] ForceClear 仅应在服务端调用；联机请使用 CmdRequestFryPotDump。");
            return;
        }
        if (debugLog) Debug.Log("[FryPot] ForceClear: 玩家清空了锅。");
        ResetPot();
    }

    /// <summary>将锅内成品强制替换为 FailedDish（糊菜 / 隔夜腐烂共用）。</summary>
    public void SpoilFinishedDish()
    {
        if (recipeDatabase == null) return;
        var failed = recipeDatabase.failedDishRecipe;
        if (failed == null || finishedRecipe == failed) return;

        if (debugLog) Debug.Log($"[FryPot] 菜品变质: '{finishedRecipe?.recipeName}' → '{failed.recipeName}'", this);
        finishedRecipe = failed;

        if (spawnedFinishedVisual != null) Destroy(spawnedFinishedVisual);
        if (failed.finishedVisualPrefab != null && visualContainer != null)
            spawnedFinishedVisual = Instantiate(failed.finishedVisualPrefab, visualContainer);
    }

    public bool CanServe() => cookingFinished || isBurnCountdown;

    public GameObject Serve()
    {
        // 纯客户端禁止执行盛菜，否则会 ResetPot 破坏本地镜像且与权威不同步（应走 Command）
        if (NetworkClient.active && !NetworkServer.active)
        {
            if (debugLog)
                Debug.LogWarning("[FryPot] Serve 仅在服务端可调用（联机时请走 Command 盛菜）。");
            return null;
        }

        if (!cookingFinished && !isBurnCountdown) return null;

        GameObject result = finishedRecipe != null ? finishedRecipe.resultPrefab : null;
        LastServedQuality = resolvedQuality;

        if (spawnedFinishedVisual != null) Destroy(spawnedFinishedVisual);

        if (result == null && debugLog) Debug.LogWarning("[FryPot] Serve returned null prefab.");

        ResetPot();
        return result;
    }

    void ResetPot()
    {
        materials.Clear();
        currentProgress = 0f;
        requiredProgress = 0f;
        cookingFinished = false;
        isBurnCountdown = false;
        burnElapsed = 0f;
        ReceivesStationHeat = false;
        _heatFromStation = null;
        finishedRecipe = null;
        contributorPlayers.Clear();
        ingredientFreshnessValues.Clear();
        rottenCount = 0;
        resolvedQuality = DishQuality.Normal;

        ClearIngredientVisuals();
        if (spawnedFinishedVisual != null) Destroy(spawnedFinishedVisual);
    }

    private void ClearIngredientVisuals()
    {
        foreach (var v in spawnedIngredientVisuals)
        {
            if (v != null) Destroy(v);
        }
        spawnedIngredientVisuals.Clear();
        if (NetworkServer.active)
            networkIngredientVisualOrder.Clear();
    }

    /// <summary>供 <see cref="FryPotNetworkSync"/> 序列化当前下锅顺序。</summary>
    public string GetNetworkIngredientVisualLine()
    {
        if (networkIngredientVisualOrder.Count == 0) return string.Empty;
        return string.Join(";", networkIngredientVisualOrder);
    }

    /// <summary>供同步成品锅内模型对应的菜谱名（空则无成品视觉）。</summary>
    public string GetNetworkFinishedRecipeName() => finishedRecipe != null ? finishedRecipe.recipeName : string.Empty;

    /// <summary>仅客户端：应用进度镜像供 UI 使用。</summary>
    public void ApplyClientMirrorProgress(float cur, float req)
    {
        if (NetworkServer.active) return;
        currentProgress = cur;
        requiredProgress = req;
    }

    /// <summary>仅客户端：糊菜计时镜像。</summary>
    public void ApplyClientMirrorBurnElapsed(float el)
    {
        if (NetworkServer.active) return;
        burnElapsed = el;
    }

    /// <summary>仅客户端：状态位镜像。</summary>
    public void ApplyClientMirrorFlags(bool cookingFin, bool burnCd, bool heat)
    {
        if (NetworkServer.active) return;
        cookingFinished = cookingFin;
        isBurnCountdown = burnCd;
        ReceivesStationHeat = heat;
    }

    /// <summary>仅客户端：按同步字符串重建锅内散件视觉。</summary>
    public void RebuildClientIngredientVisualsFromNetwork(string line)
    {
        if (NetworkServer.active) return;
        foreach (var v in clientIngredientVisualReplicas)
        {
            if (v != null) Destroy(v);
        }
        clientIngredientVisualReplicas.Clear();

        if (visualContainer == null || recipeDatabase == null || string.IsNullOrEmpty(line)) return;

        var parts = line.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawId in parts)
        {
            GameObject prefab = recipeDatabase.TryGetVisualInPotPrefab(rawId);
            if (prefab == null) continue;
            GameObject v = Instantiate(prefab, visualContainer);
            v.transform.localPosition = Random.insideUnitSphere * spawnRandomRange;
            v.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            clientIngredientVisualReplicas.Add(v);
        }
    }

    /// <summary>仅客户端：重建成品锅内模型。</summary>
    public void RebuildClientFinishedVisualFromNetwork(string recipeName)
    {
        if (NetworkServer.active) return;
        if (spawnedFinishedVisual != null)
        {
            Destroy(spawnedFinishedVisual);
            spawnedFinishedVisual = null;
        }
        if (visualContainer == null || recipeDatabase == null || string.IsNullOrEmpty(recipeName)) return;

        var recipe = recipeDatabase.FindByName(recipeName);
        if (recipe == null || recipe.finishedVisualPrefab == null) return;
        spawnedFinishedVisual = Instantiate(recipe.finishedVisualPrefab, visualContainer);
    }

    private string DumpMaterials()
    {
        if (materials.Count == 0) return "(空)";
        var sb = new System.Text.StringBuilder();
        foreach (var kv in materials)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"'{kv.Key}'×{kv.Value}");
        }
        return sb.ToString();
    }
}
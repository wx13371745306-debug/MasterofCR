using UnityEngine;
using UnityEngine.UI;

public class DishWashingStationUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;
    public Image fillImage;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    public bool hideWhenEmpty = true;

    private DishWashingStation station;
    private Camera mainCamera;

    void Start()
    {
        station = GetComponentInParent<DishWashingStation>();
        mainCamera = Camera.main;

        if (canvas != null && hideWhenEmpty)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (station == null || canvas == null || fillImage == null) return;

        // 显示逻辑：
        // 1. 水池里有脏盘子
        // 2. 出口没有满（满了就不再洗）
        bool hasDirtyPlates = station.dirtyPlatesInSink > 0;
        bool outputFull = station.IsOutputFull();

        bool shouldShow = hasDirtyPlates && !outputFull;

        if (hideWhenEmpty && canvas.gameObject.activeSelf != shouldShow)
        {
            canvas.gameObject.SetActive(shouldShow);
        }

        if (!canvas.gameObject.activeSelf)
            return;

        // 使用 DishWashingStation 自己的归一化进度
        float progress = station.GetWashProgressNormalized();
        fillImage.fillAmount = Mathf.Clamp01(progress);

        if (alwaysFaceCamera && mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
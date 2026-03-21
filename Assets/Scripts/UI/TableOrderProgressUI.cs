using UnityEngine;
using UnityEngine.UI;

public class TableOrderProgressUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;
    public GameObject callIconObj;     
    public GameObject progressBarObj;  
    public Image fillImage;            

    [Header("Target")]
    public OrderResponse tableResponse; 

    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    
    [Header("Debug")]
    public bool debugLog = true; // 开启 Debug

    private Camera mainCamera;

    void Start()
    {
        if (tableResponse == null)
            tableResponse = transform.parent.GetComponentInChildren<OrderResponse>();

        mainCamera = Camera.main;

        if (canvas != null) canvas.enabled = false;
    }

    void LateUpdate()
    {
        if (tableResponse == null) return;
        if (canvas == null) return;

        bool shouldShowCanvas = tableResponse.currentState == OrderResponse.TableState.WaitingToOrder || 
                                tableResponse.currentState == OrderResponse.TableState.Ordering;

        if (canvas.enabled != shouldShowCanvas)
        {
            canvas.enabled = shouldShowCanvas;
            if (debugLog) Debug.Log($"[TableUI Debug] Canvas 状态切换为: {canvas.enabled}，当前桌子状态: {tableResponse.currentState}");
        }

        if (canvas.enabled)
        {
            if (tableResponse.currentState == OrderResponse.TableState.WaitingToOrder)
            {
                if (callIconObj != null && !callIconObj.activeSelf) callIconObj.SetActive(true);
                if (progressBarObj != null && progressBarObj.activeSelf) progressBarObj.SetActive(false);
            }
            else if (tableResponse.currentState == OrderResponse.TableState.Ordering)
            {
                if (callIconObj != null && callIconObj.activeSelf) callIconObj.SetActive(false);
                if (progressBarObj != null && !progressBarObj.activeSelf) progressBarObj.SetActive(true);

                // 【核心 Debug 区域】：检查数值传递
                if (fillImage != null)
                {
                    float progress = tableResponse.GetOrderProgressNormalized();
                    fillImage.fillAmount = progress;
                    
                    if (debugLog) 
                    {
                        // 每帧打印当前的原始数值和转换后的比例
                        Debug.Log($"[TableUI Debug] 正在读条! 原始进度: {tableResponse.currentOrderProgress:F2} / {tableResponse.requiredOrderTime:F2} | UI FillAmount: {progress:F2}");
                    }
                }
                else
                {
                    if (debugLog) Debug.LogError("[TableUI Debug] 严重错误：fillImage 引用为空！");
                }
            }

            if (alwaysFaceCamera && mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}
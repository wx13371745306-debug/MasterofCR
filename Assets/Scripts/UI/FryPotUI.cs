using UnityEngine;
using UnityEngine.UI;

public class FryPotUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;
    public Image fillImage;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    public bool hideWhenEmpty = true;

    private FryPot pot;
    private Camera mainCamera;

    void Start()
    {
        pot = GetComponentInParent<FryPot>();
        mainCamera = Camera.main;

        if (canvas != null && hideWhenEmpty)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (pot == null || canvas == null || fillImage == null) return;

        // 判断显隐逻辑：锅里有东西且没做完时显示
        bool hasIngredient = pot.HasAnyIngredient();
        bool isCooking = hasIngredient && !pot.cookingFinished;
        
        // 如果想要做完后还显示一会儿直到被取走，可以只用 hasIngredient
        // 但根据之前的逻辑，做完变成成品后进度条应该消失（或者显示满格）
        // 这里我们就设定：只要锅里有食材，且还没有被清空重置，就显示
        // 如果你希望做完后立刻消失，就用 !pot.cookingFinished
        bool shouldShow = hasIngredient; 
        
        // 特殊处理：如果已经做完了，可能我们希望它显示满格或者变成成品图标
        // 这里暂时只显示烹饪中的进度条，做完即消失（或者你可以改为一直显示直到取走）
        // 既然是进度条，做完那一刻通常是满的。
        if (pot.cookingFinished) 
        {
            // 做完了，可以选择隐藏进度条（因为模型已经变了）
            shouldShow = false; 
        }

        if (hideWhenEmpty && canvas.gameObject.activeSelf != shouldShow)
        {
            canvas.gameObject.SetActive(shouldShow);
        }

        if (canvas.gameObject.activeSelf)
        {
            float progress = 0f;
            if (pot.requiredProgress > 0.001f)
            {
                progress = Mathf.Clamp01(pot.currentProgress / pot.requiredProgress);
            }

            fillImage.fillAmount = progress;

            if (alwaysFaceCamera && mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UnityEngine.EventSystems;
using Game.Runtime.AOT;
using UnityEditor.SceneManagement; // 引入场景管理命名空间

/// <summary>
/// 负责为 Logo 场景一键创建基础 UI 结构的编辑器脚本
/// </summary>
public class LogoSceneSetup
{
    [MenuItem("FreamWork/Setup/Setup Logo Scene UI")]
    public static void SetupLogoScene()
    {
        // --- 查找或创建 Canvas 和 EventSystem ---
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas"); // 支持撤销并标记脏
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Debug.Log("Created Canvas.");
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            Debug.Log("Created EventSystem.");
        }

        // --- 创建 UI 元素 ---
        CreateBackgroundImage(canvas.transform);
        CreateProgressSlider(canvas.transform);
        CreateLoadingInfoText(canvas.transform);
        CreateVersionText(canvas.transform);

        // --- 创建 GameBoot 节点并挂载 HotUpdateManager ---
        CreateGameBoot(canvas.transform);
        
        // 强制标记场景为脏，确保必须保存
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        EditorUtility.DisplayDialog("Scene Setup", "Logo 场景完全初始化成功！\n(已标记为脏，请按 Ctrl+S 保存)", "好的");
    }

    private static void CreateGameBoot(Transform canvasTransform)
    {
        // 查找是否已存在
        GameObject bootGO = GameObject.Find("GameBoot");
        if (bootGO == null)
        {
            bootGO = new GameObject("GameBoot");
            Undo.RegisterCreatedObjectUndo(bootGO, "Create GameBoot");
            bootGO.transform.position = Vector3.zero;
            Debug.Log("Created GameBoot GameObject.");
        }

        // 确保挂载 HotUpdateManager
        HotUpdateMgr hotUpdateMgr = bootGO.GetComponent<HotUpdateMgr>();
        if (hotUpdateMgr == null)
        {
            hotUpdateMgr = Undo.AddComponent<HotUpdateMgr>(bootGO); // 使用 Undo 添加组件
            Debug.Log("Attached HotUpdateManager component.");
        }

        // --- 自动绑定 UI 引用 ---
        // 记录修改，以便支持 Undo 和保存
        Undo.RecordObject(hotUpdateMgr, "Bind UI References");

        Transform sliderTrans = canvasTransform.Find("Progress_Slider");
        if (sliderTrans != null) 
            hotUpdateMgr.progressSlider = sliderTrans.GetComponent<Slider>();

        Transform infoTrans = canvasTransform.Find("Loading_Info_Text");
        if (infoTrans != null) 
            hotUpdateMgr.infoText = infoTrans.GetComponent<TextMeshProUGUI>();

        Transform versionTrans = canvasTransform.Find("Version_Text");
        if (versionTrans != null) 
            hotUpdateMgr.versionText = versionTrans.GetComponent<TextMeshProUGUI>();

        Debug.Log("Auto-assigned UI references to HotUpdateManager.");
    }

    private static void CreateBackgroundImage(Transform parent)
    {
        if (parent.Find("BG_Image")) return;

        GameObject bgGO = new GameObject("BG_Image", typeof(Image));
        Undo.RegisterCreatedObjectUndo(bgGO, "Create BG_Image");
        bgGO.transform.SetParent(parent, false);
        Image bgImage = bgGO.GetComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        RectTransform rect = bgGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
    }

    private static void CreateProgressSlider(Transform parent)
    {
        if (parent.Find("Progress_Slider")) return;

        GameObject sliderGO = new GameObject("Progress_Slider", typeof(Slider));
        Undo.RegisterCreatedObjectUndo(sliderGO, "Create Slider");
        sliderGO.transform.SetParent(parent, false);
        Slider slider = sliderGO.GetComponent<Slider>();

        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.1f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.1f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = new Vector2(800, 30);

        GameObject backgroundArea = new GameObject("Background", typeof(Image));
        backgroundArea.transform.SetParent(sliderRect, false);
        backgroundArea.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
        RectTransform backgroundRect = backgroundArea.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = new Vector2(0, -5);
        backgroundRect.offsetMax = new Vector2(0, 5);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderRect, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(0, -5);
        fillAreaRect.offsetMax = new Vector2(0, 5);

        GameObject fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = Color.white;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        
        slider.fillRect = fillRect;
        slider.targetGraphic = fill.GetComponent<Image>();
        slider.interactable = false;
        slider.value = 0f;
    }

    private static void CreateLoadingInfoText(Transform parent)
    {
        if (parent.Find("Loading_Info_Text")) return;

        GameObject textGO = new GameObject("Loading_Info_Text", typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textGO, "Create Info Text");
        textGO.transform.SetParent(parent, false);
        
        TextMeshProUGUI loadingText = textGO.GetComponent<TextMeshProUGUI>();
        loadingText.text = "正在加载资源...";
        loadingText.fontSize = 24;
        loadingText.color = Color.white;
        loadingText.alignment = TextAlignmentOptions.Center;

        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.1f);
        rect.anchorMax = new Vector2(0.5f, 0.1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 50);
        rect.sizeDelta = new Vector2(800, 50);
    }

    private static void CreateVersionText(Transform parent)
    {
        if (parent.Find("Version_Text")) return;

        GameObject textGO = new GameObject("Version_Text", typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textGO, "Create Version Text");
        textGO.transform.SetParent(parent, false);
        
        TextMeshProUGUI versionText = textGO.GetComponent<TextMeshProUGUI>();
        versionText.text = "Version 1.0.0";
        versionText.fontSize = 28;
        versionText.color = Color.white;
        versionText.alignment = TextAlignmentOptions.BottomRight;

        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-30, 30);
        rect.sizeDelta = new Vector2(500, 60);
    }
}

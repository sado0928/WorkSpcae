using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UnityEngine.EventSystems;
using UnityEditor.SceneManagement;

/// <summary>
/// 快速生成标准 UI 界面结构的编辑器工具
/// </summary>
public class LoginSceneSetup
{
    [MenuItem("FreamWork/Setup/Setup Login Scene UI")]
    public static void SetupLoginScene()
    {
        // 1. 确保 Canvas 环境
        Canvas canvas = EnsureCanvasEnvironment();

        // 2. 创建登录面板容器
        GameObject panelGO = CreatePanel(canvas.transform, "Login_Panel");
        
        // 3. 创建背景
        CreateImage(panelGO.transform, "BG", new Color(0.2f, 0.2f, 0.25f, 1f), true);

        // 4. 创建标题
        CreateText(panelGO.transform, "Title_Text", "GAME LOGIN", 60, new Vector2(0, 200));

        // 5. 创建输入框
        CreateInputField(panelGO.transform, "Account_Input", "Enter Username...", new Vector2(0, 50));
        CreateInputField(panelGO.transform, "Password_Input", "Enter Password...", new Vector2(0, -50), true);

        // 6. 创建按钮
        CreateButton(panelGO.transform, "Login_Btn", "Start Game", new Vector2(0, -180));

        // 7. 创建提示文本
        CreateText(panelGO.transform, "Info_Text", "", 24, new Vector2(0, -250), Color.red);

        // 标记脏以便保存
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Success", "登录 UI 已生成！", "OK");
    }

    private static Canvas EnsureCanvasEnvironment()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        }
        return canvas;
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Panel");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    private static void CreateImage(Transform parent, string name, Color color, bool fullScreen = false)
    {
        GameObject go = new GameObject(name, typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create Image");
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        if (fullScreen)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    private static void CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Color? color = null)
    {
        GameObject go = new GameObject(name, typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create Text");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color ?? Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(600, 100);
    }

    private static void CreateInputField(Transform parent, string name, string placeholderText, Vector2 pos, bool isPassword = false)
    {
        // 简化版 InputField 创建，实际需要更复杂的子节点结构
        // 这里为了简单，我们直接创建 Image + TMP_InputField
        GameObject bg = new GameObject(name, typeof(Image), typeof(TMP_InputField));
        Undo.RegisterCreatedObjectUndo(bg, "Create InputField");
        bg.transform.SetParent(parent, false);
        
        RectTransform rect = bg.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 60);
        rect.anchoredPosition = pos;

        Image img = bg.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0.8f);

        TMP_InputField input = bg.GetComponent<TMP_InputField>();
        input.targetGraphic = img;

        // Text Area
        GameObject textArea = new GameObject("TextArea", typeof(RectTransform));
        textArea.transform.SetParent(bg.transform, false);
        RectTransform areaRect = textArea.GetComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(10, 0);
        areaRect.offsetMax = new Vector2(-10, 0);

        // Placeholder
        GameObject placeholder = new GameObject("Placeholder", typeof(TextMeshProUGUI));
        placeholder.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI phTmp = placeholder.GetComponent<TextMeshProUGUI>();
        phTmp.text = placeholderText;
        phTmp.fontSize = 24;
        phTmp.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        phTmp.alignment = TextAlignmentOptions.Left; // 垂直居中需设置
        RectTransform phRect = placeholder.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        input.placeholder = phTmp;

        // Text
        GameObject text = new GameObject("Text", typeof(TextMeshProUGUI));
        text.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI tTmp = text.GetComponent<TextMeshProUGUI>();
        tTmp.fontSize = 24;
        tTmp.color = Color.black;
        tTmp.alignment = TextAlignmentOptions.Left;
        RectTransform tRect = text.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        input.textComponent = tTmp;

        input.textViewport = areaRect;
        
        if (isPassword)
            input.contentType = TMP_InputField.ContentType.Password;
    }

    private static void CreateButton(Transform parent, string name, string btnText, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create Button");
        go.transform.SetParent(parent, false);
        
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 80);
        rect.anchoredPosition = pos;

        go.GetComponent<Image>().color = new Color(0.2f, 0.6f, 1f);

        GameObject textGO = new GameObject("Text", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = btnText;
        tmp.fontSize = 32;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
    }
}
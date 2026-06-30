using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    [Header("Color Presets")]
    public Color[] presetColors = new Color[]
    {
        new Color(0.91f, 0.30f, 0.24f),  // Red
        new Color(0.24f, 0.60f, 0.91f),  // Blue
        new Color(0.30f, 0.69f, 0.31f),  // Green
        new Color(1.00f, 0.92f, 0.23f),  // Yellow
        new Color(1.00f, 0.55f, 0.00f),  // Orange
        new Color(0.61f, 0.15f, 0.69f),  // Purple
        new Color(1.00f, 0.41f, 0.71f),  // Pink
        Color.white,                      // White
        new Color(0.25f, 0.25f, 0.25f),  // Dark Gray
        new Color(0.40f, 0.26f, 0.13f),  // Brown
        Color.black,                      // Black
        new Color(0.50f, 0.78f, 0.92f),  // Sky Blue
    };

    private Canvas canvas;
    private GameObject panel;
    private Image selectedSwatch;
    private Font uiFont;

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager", typeof(GameManager));

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        uiFont = GetFont();

        CreateCanvas();
        CreateUI();

        if (presetColors.Length > 0)
            SelectColor(presetColors[0]);
    }

    static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Lucida Console.ttf");
        return font;
    }

    void CreateCanvas()
    {
        GameObject go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
    }

    void CreateUI()
    {
        panel = new GameObject("MenuPanel", typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

        VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 20;
        vlg.padding = new RectOffset(80, 80, 60, 60);

        AddTitle(panel.transform);
        AddColorPicker(panel.transform);
        AddStartButton(panel.transform);
    }

    void AddTitle(Transform parent)
    {
        GameObject titleGO = new GameObject("Title", typeof(Text));
        titleGO.transform.SetParent(parent, false);

        Text title = titleGO.GetComponent<Text>();
        title.text = "SWINGING BUCKET PAINTER";
        title.fontSize = 48;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(0.9f, 0.9f, 0.95f);
        title.font = uiFont;

        LayoutElement le = titleGO.AddComponent<LayoutElement>();
        le.preferredHeight = 80;
        le.flexibleHeight = 0;
    }

    void AddColorPicker(Transform parent)
    {
        GameObject section = new GameObject("ColorSection", typeof(VerticalLayoutGroup));
        section.transform.SetParent(parent, false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.MiddleCenter;
        slg.childControlWidth = true;
        slg.childControlHeight = false;
        slg.spacing = 16;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.flexibleHeight = 1;

        // Label
        GameObject labelGO = new GameObject("ColorLabel", typeof(Text));
        labelGO.transform.SetParent(section.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "Select Paint Color";
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.8f, 0.8f, 0.85f);
        label.font = uiFont;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredHeight = 40;
        labelLE.flexibleHeight = 0;

        // Swatch grid
        int cols = 6;
        GameObject gridGO = new GameObject("ColorGrid", typeof(GridLayoutGroup));
        gridGO.transform.SetParent(section.transform, false);

        GridLayoutGroup glg = gridGO.GetComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = cols;
        glg.childAlignment = TextAnchor.MiddleCenter;
        glg.cellSize = new Vector2(80, 80);
        glg.spacing = new Vector2(12, 12);
        glg.padding = new RectOffset(10, 10, 10, 10);

        int rows = Mathf.CeilToInt((float)presetColors.Length / cols);
        float gridHeight = rows * 80 + (rows - 1) * 12 + 20;

        LayoutElement gridLE = gridGO.AddComponent<LayoutElement>();
        gridLE.preferredHeight = gridHeight;
        gridLE.flexibleHeight = 0;

        for (int i = 0; i < presetColors.Length; i++)
            CreateSwatch(gridGO.transform, presetColors[i], i);
    }

    void CreateSwatch(Transform parent, Color color, int index)
    {
        GameObject swatchGO = new GameObject("Swatch_" + index, typeof(Image));
        swatchGO.transform.SetParent(parent, false);

        Image swatch = swatchGO.GetComponent<Image>();
        swatch.color = color;

        Button btn = swatchGO.AddComponent<Button>();
        btn.targetGraphic = swatch;

        ColorBlock cb = btn.colors;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
        cb.selectedColor = Color.Lerp(color, Color.white, 0.5f);
        btn.colors = cb;

        Color captured = color;
        btn.onClick.AddListener(() => SelectColor(captured));
    }

    void SelectColor(Color color)
    {
        GameManager.Instance.SetColor(color);

        if (selectedSwatch != null)
            selectedSwatch.rectTransform.localScale = Vector3.one;

        foreach (Transform child in panel.transform.Find("ColorSection/ColorGrid"))
        {
            Image img = child.GetComponent<Image>();
            if (Mathf.Approximately(img.color.r, color.r) &&
                Mathf.Approximately(img.color.g, color.g) &&
                Mathf.Approximately(img.color.b, color.b))
            {
                selectedSwatch = img;
                selectedSwatch.rectTransform.localScale = Vector3.one * 1.18f;
                break;
            }
        }
    }

    void AddStartButton(Transform parent)
    {
        GameObject btnGO = new GameObject("StartButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.color = new Color(0.22f, 0.56f, 0.95f);

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 70;
        btnLE.flexibleHeight = 0;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.35f, 0.65f, 1.0f);
        cb.normalColor = new Color(0.22f, 0.56f, 0.95f);
        cb.selectedColor = new Color(0.22f, 0.56f, 0.95f);
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "START PAINTING";
        label.fontSize = 28;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.font = uiFont;

        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() =>
        {
            GameManager.Instance.StartGame();
            Destroy(panel);
            Destroy(canvas.gameObject);
            Destroy(gameObject);
        });
    }
}

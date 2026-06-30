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
    private GameObject menuPanel;
    private GameObject hudPanel;
    private Image selectedSwatch;
    private Font uiFont;

    private GameObject hudContent;
    private Text thetaText, phiText, paintText, massText;
    private Rope rope;
    private BucketBody bucket;

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager", typeof(GameManager));

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        uiFont = GetFont();

        CreateCanvas();
        CreateHUD();
        CreateMenu();

        if (presetColors.Length > 0)
            SelectColor(presetColors[0]);
    }

    void Start()
    {
        rope = FindFirstObjectByType<Rope>();
        bucket = FindFirstObjectByType<BucketBody>();
    }

    void Update()
    {
        if (hudPanel != null && Input.GetKeyDown(KeyCode.Tab))
        {
            hudContent.SetActive(!hudContent.activeSelf);
        }

        if (hudPanel == null || rope == null || bucket == null) return;

        float thetaDeg = rope.theta * Mathf.Rad2Deg;
        float phiDeg = rope.phi * Mathf.Rad2Deg;
        thetaText.text = string.Format("\u03B8: {0:F1}\u00B0", thetaDeg);
        phiText.text = string.Format("\u03C6: {0:F1}\u00B0", phiDeg);
        paintText.text = string.Format("Paint: {0:F1} kg", bucket.paintMass);
        massText.text = string.Format("Mass: {0:F1} kg", bucket.mass);
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
        GameObject go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
    }

    void CreateHUD()
    {
        hudPanel = new GameObject("HUD", typeof(Image), typeof(VerticalLayoutGroup));
        hudPanel.transform.SetParent(canvas.transform, false);

        RectTransform hrt = hudPanel.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(0, 1);
        hrt.pivot = new Vector2(0, 1);
        hrt.anchoredPosition = new Vector2(20, -20);
        hrt.sizeDelta = new Vector2(260, 220);

        Image hbg = hudPanel.GetComponent<Image>();
        hbg.color = new Color(0, 0, 0, 0.55f);

        VerticalLayoutGroup hlg = hudPanel.GetComponent<VerticalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;
        hlg.spacing = 0;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        // Header row with title and toggle button
        GameObject header = new GameObject("Header", typeof(Image), typeof(HorizontalLayoutGroup));
        header.transform.SetParent(hudPanel.transform, false);

        Image hdrBg = header.GetComponent<Image>();
        hdrBg.color = new Color(0, 0, 0, 0.25f);

        HorizontalLayoutGroup hlgH = header.GetComponent<HorizontalLayoutGroup>();
        hlgH.childAlignment = TextAnchor.MiddleLeft;
        hlgH.childControlWidth = true;
        hlgH.childControlHeight = true;
        hlgH.spacing = 0;
        hlgH.padding = new RectOffset(10, 4, 0, 0);

        LayoutElement hdrLE = header.AddComponent<LayoutElement>();
        hdrLE.preferredHeight = 34;
        hdrLE.flexibleHeight = 0;

        GameObject headerLabel = new GameObject("HeaderLabel", typeof(Text));
        headerLabel.transform.SetParent(header.transform, false);
        Text hl = headerLabel.GetComponent<Text>();
        hl.text = "INFO";
        hl.fontSize = 20;
        hl.fontStyle = FontStyle.Bold;
        hl.alignment = TextAnchor.MiddleLeft;
        hl.color = new Color(0.9f, 0.9f, 0.95f, 0.9f);
        hl.font = uiFont;

        LayoutElement hlLE = headerLabel.AddComponent<LayoutElement>();
        hlLE.flexibleWidth = 1;

        // Toggle button
        GameObject toggleBtn = new GameObject("ToggleBtn", typeof(Image), typeof(Button));
        toggleBtn.transform.SetParent(header.transform, false);

        Image tglBg = toggleBtn.GetComponent<Image>();
        tglBg.color = new Color(1, 1, 1, 0.2f);

        LayoutElement tglLE = toggleBtn.AddComponent<LayoutElement>();
        tglLE.preferredWidth = 34;
        tglLE.flexibleWidth = 0;

        GameObject tglLabel = new GameObject("TglLabel", typeof(Text));
        tglLabel.transform.SetParent(toggleBtn.transform, false);
        Text tglText = tglLabel.GetComponent<Text>();
        tglText.text = "\u2212";
        tglText.fontSize = 22;
        tglText.fontStyle = FontStyle.Bold;
        tglText.alignment = TextAnchor.MiddleCenter;
        tglText.color = Color.white;
        tglText.font = uiFont;

        RectTransform tlr = tglLabel.GetComponent<RectTransform>();
        tlr.anchorMin = Vector2.zero;
        tlr.anchorMax = Vector2.one;
        tlr.offsetMin = Vector2.zero;
        tlr.offsetMax = Vector2.zero;

        Button tglBtn = toggleBtn.GetComponent<Button>();
        tglBtn.targetGraphic = tglBg;
        tglBtn.transition = Selectable.Transition.ColorTint;
        ColorBlock tcb = tglBtn.colors;
        tcb.highlightedColor = new Color(1, 1, 1, 0.4f);
        tglBtn.colors = tcb;

        // Content container (toggled)
        hudContent = new GameObject("Content", typeof(VerticalLayoutGroup));
        hudContent.transform.SetParent(hudPanel.transform, false);

        VerticalLayoutGroup clg = hudContent.GetComponent<VerticalLayoutGroup>();
        clg.childAlignment = TextAnchor.UpperLeft;
        clg.childControlWidth = true;
        clg.childControlHeight = false;
        clg.spacing = 4;
        clg.padding = new RectOffset(12, 12, 6, 8);

        thetaText = AddHUDLine("hudTheta", "\u03B8: 0.0\u00B0");
        phiText = AddHUDLine("hudPhi", "\u03C6: 0.0\u00B0");
        paintText = AddHUDLine("hudPaint", "Paint: 0.0 kg");
        massText = AddHUDLine("hudMass", "Mass: 0.0 kg");

        tglBtn.onClick.AddListener(() =>
        {
            bool show = !hudContent.activeSelf;
            hudContent.SetActive(show);
            tglText.text = show ? "\u2212" : "+";
        });
    }

    Text AddHUDLine(string name, string initialText)
    {
        GameObject go = new GameObject(name, typeof(Text));
        go.transform.SetParent(hudContent.transform, false);

        Text t = go.GetComponent<Text>();
        t.text = initialText;
        t.fontSize = 22;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
        t.font = uiFont;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 32;
        le.flexibleHeight = 0;
        return t;
    }

    void CreateMenu()
    {
        menuPanel = new GameObject("MenuPanel", typeof(Image), typeof(VerticalLayoutGroup));
        menuPanel.transform.SetParent(canvas.transform, false);
        RectTransform prt = menuPanel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        Image bg = menuPanel.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

        VerticalLayoutGroup vlg = menuPanel.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 20;
        vlg.padding = new RectOffset(80, 80, 60, 60);

        AddTitle(menuPanel.transform);
        AddColorPicker(menuPanel.transform);
        AddStartButton(menuPanel.transform);
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

        if (menuPanel == null) return;
        Transform grid = menuPanel.transform.Find("ColorSection/ColorGrid");
        if (grid == null) return;

        foreach (Transform child in grid)
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
            Destroy(menuPanel);
            menuPanel = null;
        });
    }
}

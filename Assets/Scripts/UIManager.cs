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
    private GameObject controlPanel;
    private Image selectedSwatch;
    private Font uiFont;

    private GameObject hudContent;
    private Text thetaText, phiText, paintText, massText;
    private Slider thetaSlider, phiSlider;
    private Text thetaSliderValue, phiSliderValue;
    private Rope rope;
    private BucketBody bucket;

    private const float ThetaMin = 5f;
    private const float ThetaMax = 85f;
    private const float PhiMin = -180f;
    private const float PhiMax = 180f;

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager", typeof(GameManager));

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        uiFont = GetFont();

        CreateCanvas();
        CreateHUD();
        CreateControlPanel();
        CreateMenu();

        if (presetColors.Length > 0)
            SelectColor(presetColors[0]);
    }

    void Start()
    {
        rope = FindFirstObjectByType<Rope>();
        bucket = FindFirstObjectByType<BucketBody>();
        SyncAngleControlsFromRope();
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
        thetaText.text = string.Format("{0:F1}\u00B0", thetaDeg);
        phiText.text = string.Format("{0:F1}\u00B0", phiDeg);
        paintText.text = string.Format("{0:F1} kg", bucket.paintMass);
        massText.text = string.Format("{0:F1} kg", bucket.mass);
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
        hrt.anchoredPosition = new Vector2(30, -30);
        hrt.sizeDelta = new Vector2(280, 230);

        Image hbg = hudPanel.GetComponent<Image>();
        hbg.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);

        VerticalLayoutGroup hlg = hudPanel.GetComponent<VerticalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;
        hlg.spacing = 0;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        // Accent bar at top
        GameObject accent = new GameObject("Accent", typeof(Image));
        accent.transform.SetParent(hudPanel.transform, false);
        Image accentImg = accent.GetComponent<Image>();
        accentImg.color = new Color(0.22f, 0.56f, 0.95f);
        LayoutElement accentLE = accent.AddComponent<LayoutElement>();
        accentLE.preferredHeight = 3;
        accentLE.flexibleHeight = 0;

        // Header row with title and toggle button
        GameObject header = new GameObject("Header", typeof(Image), typeof(HorizontalLayoutGroup));
        header.transform.SetParent(hudPanel.transform, false);

        Image hdrBg = header.GetComponent<Image>();
        hdrBg.color = new Color(0, 0, 0, 0.15f);

        HorizontalLayoutGroup hlgH = header.GetComponent<HorizontalLayoutGroup>();
        hlgH.childAlignment = TextAnchor.MiddleLeft;
        hlgH.childControlWidth = true;
        hlgH.childControlHeight = true;
        hlgH.spacing = 0;
        hlgH.padding = new RectOffset(14, 4, 0, 0);

        LayoutElement hdrLE = header.AddComponent<LayoutElement>();
        hdrLE.preferredHeight = 38;
        hdrLE.flexibleHeight = 0;

        GameObject headerLabel = new GameObject("HeaderLabel", typeof(Text));
        headerLabel.transform.SetParent(header.transform, false);
        Text hl = headerLabel.GetComponent<Text>();
        hl.text = "SIMULATION DATA";
        hl.fontSize = 18;
        hl.fontStyle = FontStyle.Bold;
        hl.alignment = TextAnchor.MiddleLeft;
        hl.color = new Color(0.7f, 0.75f, 0.85f, 0.9f);
        hl.font = uiFont;

        LayoutElement hlLE = headerLabel.AddComponent<LayoutElement>();
        hlLE.flexibleWidth = 1;

        // Toggle button
        GameObject toggleBtn = new GameObject("ToggleBtn", typeof(Image), typeof(Button));
        toggleBtn.transform.SetParent(header.transform, false);

        Image tglBg = toggleBtn.GetComponent<Image>();
        tglBg.color = new Color(1, 1, 1, 0.15f);

        LayoutElement tglLE = toggleBtn.AddComponent<LayoutElement>();
        tglLE.preferredWidth = 36;
        tglLE.flexibleWidth = 0;

        GameObject tglLabel = new GameObject("TglLabel", typeof(Text));
        tglLabel.transform.SetParent(toggleBtn.transform, false);
        Text tglText = tglLabel.GetComponent<Text>();
        tglText.text = "\u2212";
        tglText.fontSize = 20;
        tglText.fontStyle = FontStyle.Bold;
        tglText.alignment = TextAnchor.MiddleCenter;
        tglText.color = new Color(0.8f, 0.8f, 0.9f);
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
        tcb.highlightedColor = new Color(1, 1, 1, 0.35f);
        tglBtn.colors = tcb;

        // Content container (toggled)
        hudContent = new GameObject("Content", typeof(VerticalLayoutGroup));
        hudContent.transform.SetParent(hudPanel.transform, false);

        VerticalLayoutGroup clg = hudContent.GetComponent<VerticalLayoutGroup>();
        clg.childAlignment = TextAnchor.UpperLeft;
        clg.childControlWidth = true;
        clg.childControlHeight = false;
        clg.spacing = 2;
        clg.padding = new RectOffset(14, 14, 8, 10);

        AddSeparator();
        thetaText = AddHUDLine("hudTheta", "\u03B8", "0.0\u00B0");
        phiText = AddHUDLine("hudPhi", "\u03C6", "0.0\u00B0");
        AddSeparator();
        paintText = AddHUDLine("hudPaint", "Paint", "0.0 kg");
        massText = AddHUDLine("hudMass", "Mass", "0.0 kg");

        tglBtn.onClick.AddListener(() =>
        {
            bool show = !hudContent.activeSelf;
            hudContent.SetActive(show);
            tglText.text = show ? "\u2212" : "+";
        });
    }


    void CreateControlPanel()
    {
        controlPanel = new GameObject("ControlPanel", typeof(Image), typeof(VerticalLayoutGroup));
        controlPanel.transform.SetParent(canvas.transform, false);

        RectTransform prt = controlPanel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1, 1);
        prt.anchorMax = new Vector2(1, 1);
        prt.pivot = new Vector2(1, 1);
        prt.anchoredPosition = new Vector2(-30, -30);
        prt.sizeDelta = new Vector2(340, 230);

        Image bg = controlPanel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);

        VerticalLayoutGroup vlg = controlPanel.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 10;
        vlg.padding = new RectOffset(14, 14, 12, 12);

        GameObject titleGO = new GameObject("Title", typeof(Text));
        titleGO.transform.SetParent(controlPanel.transform, false);
        Text title = titleGO.GetComponent<Text>();
        title.text = "BUCKET ANGLES";
        title.fontSize = 20;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(0.7f, 0.75f, 0.85f, 0.95f);
        title.font = uiFont;

        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 28;

        thetaSlider = AddAngleSlider(controlPanel.transform, "Theta", ThetaMin, ThetaMax, rope != null ? rope.startTheta : 20f, value =>
        {
            if (rope != null) rope.SetThetaPhiDegrees(value, phiSlider != null ? phiSlider.value : rope.startPhi);
        }, out thetaSliderValue);

        phiSlider = AddAngleSlider(controlPanel.transform, "Phi", PhiMin, PhiMax, rope != null ? rope.startPhi : 0f, value =>
        {
            if (rope != null) rope.SetThetaPhiDegrees(thetaSlider != null ? thetaSlider.value : rope.startTheta, value);
        }, out phiSliderValue);
    }

    Slider AddAngleSlider(Transform parent, string label, float min, float max, float initialValue, System.Action<float> onChanged, out Text valueText)
    {
        Text valueTextLocal = null;

        GameObject row = new GameObject(label + "Row", typeof(VerticalLayoutGroup));
        row.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = row.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.spacing = 4;

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 72;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(row.transform, false);
        Text labelText = labelGO.GetComponent<Text>();
        labelText.text = label;
        labelText.fontSize = 16;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = new Color(0.85f, 0.86f, 0.92f);
        labelText.font = uiFont;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredHeight = 20;

        GameObject sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(row.transform, false);
        Slider slider = sliderGO.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;

        RectTransform srt = sliderGO.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(0, 24);

        GameObject bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        Image bg = bgGO.GetComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.35f);
        RectTransform bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1, 0.75f);
        fillAreaRt.offsetMin = new Vector2(10, 0);
        fillAreaRt.offsetMax = new Vector2(-10, 0);

        GameObject fillGO = new GameObject("Fill", typeof(Image));
        fillGO.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.color = new Color(0.22f, 0.56f, 0.95f);
        RectTransform fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10, 0);
        handleAreaRt.offsetMax = new Vector2(-10, 0);

        GameObject handleGO = new GameObject("Handle", typeof(Image));
        handleGO.transform.SetParent(handleArea.transform, false);
        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.color = Color.white;
        RectTransform handleRt = handleGO.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(16, 16);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;

        GameObject valueGO = new GameObject("Value", typeof(Text));
        valueGO.transform.SetParent(row.transform, false);
        valueTextLocal = valueGO.GetComponent<Text>();
        valueTextLocal.fontSize = 14;
        valueTextLocal.alignment = TextAnchor.MiddleRight;
        valueTextLocal.color = new Color(0.85f, 0.86f, 0.92f);
        valueTextLocal.font = uiFont;

        valueText = valueTextLocal;

        LayoutElement valueLE = valueGO.AddComponent<LayoutElement>();
        valueLE.preferredHeight = 18;

        slider.onValueChanged.AddListener(v =>
        {
            valueTextLocal.text = string.Format("{0:F1}\u00B0", v);
            if (onChanged != null)
                onChanged(v);
        });

        slider.SetValueWithoutNotify(initialValue);
        valueTextLocal.text = string.Format("{0:F1}\u00B0", initialValue);

        return slider;
    }

    void SyncAngleControlsFromRope()
    {
        if (rope == null)
            return;

        if (thetaSlider != null)
        {
            thetaSlider.SetValueWithoutNotify(rope.startTheta);
            if (thetaSliderValue != null)
                thetaSliderValue.text = string.Format("{0:F1}\u00B0", rope.startTheta);
        }

        if (phiSlider != null)
        {
            phiSlider.SetValueWithoutNotify(rope.startPhi);
            if (phiSliderValue != null)
                phiSliderValue.text = string.Format("{0:F1}\u00B0", rope.startPhi);
        }
    }

    void AddSeparator()
    {
        GameObject sep = new GameObject("Separator", typeof(Image));
        sep.transform.SetParent(hudContent.transform, false);
        Image sepImg = sep.GetComponent<Image>();
        sepImg.color = new Color(1, 1, 1, 0.08f);
        LayoutElement sepLE = sep.AddComponent<LayoutElement>();
        sepLE.preferredHeight = 1;
        sepLE.flexibleHeight = 0;
    }

    Text AddHUDLine(string name, string label, string initialValue)
    {
        GameObject row = new GameObject(name, typeof(HorizontalLayoutGroup));
        row.transform.SetParent(hudContent.transform, false);

        HorizontalLayoutGroup rlg = row.GetComponent<HorizontalLayoutGroup>();
        rlg.childAlignment = TextAnchor.MiddleLeft;
        rlg.childControlWidth = true;
        rlg.childControlHeight = true;
        rlg.spacing = 8;
        rlg.padding = new RectOffset(0, 0, 2, 2);

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 32;
        rowLE.flexibleHeight = 0;

        // Indicator dot
        GameObject dot = new GameObject("Dot", typeof(Image));
        dot.transform.SetParent(row.transform, false);
        Image dotImg = dot.GetComponent<Image>();
        dotImg.color = new Color(0.22f, 0.56f, 0.95f);
        LayoutElement dotLE = dot.AddComponent<LayoutElement>();
        dotLE.preferredWidth = 8;
        dotLE.preferredHeight = 8;
        dotLE.flexibleWidth = 0;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(row.transform, false);
        Text labelText = labelGO.GetComponent<Text>();
        labelText.text = label;
        labelText.fontSize = 18;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.black;
        labelText.font = uiFont;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 60;
        labelLE.flexibleWidth = 0;

        // Value
        GameObject valGO = new GameObject("Value", typeof(Text));
        valGO.transform.SetParent(row.transform, false);
        Text valText = valGO.GetComponent<Text>();
        valText.text = initialValue;
        valText.fontSize = 20;
        valText.fontStyle = FontStyle.Bold;
        valText.alignment = TextAnchor.MiddleRight;
        valText.color = Color.black;
        valText.font = uiFont;

        LayoutElement valLE = valGO.AddComponent<LayoutElement>();
        valLE.flexibleWidth = 1;

        return valText;
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

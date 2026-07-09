using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Seb.Fluid.Simulation;
using Seb.Fluid.Rendering;

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
    private GameObject hudPanel;
    private GameObject sidePanel;
    private Image selectedSwatch;
    private Font uiFont;

    // private GameObject hudContent;
    private float fpsSmoothed;
    private Text thetaText, phiText, paintText, massText;
    private Text windText, dragText, frictionText;
    private Text frameText;
    private Slider thetaSlider, phiSlider;
    private Text thetaSliderValue, phiSliderValue;
    private Rope rope;
    private BucketBody bucket;
    private FluidSim fluidSim;
    private DrawingBoard drawingBoard;
    private Button spawnButton;
    private SimulationController simController;
    private bool particlesSpawned;
    private Button startButton;
    private ParticleDisplay3D particleDisplay;
    private Button displayModeButton;
    private FluidRenderTest fluidRender;

    private Button screenSpaceButton;

    private InputField gravityInput, smoothingRadiusInput, viscosityInput;
    private Slider dampingSlider;
    private Text dampingValueText, particleCountText;

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
        AddSaveBoardButton(canvas.transform);
        CreateSidePanel();

        if (presetColors.Length > 0)
            SelectColor(presetColors[0]);
    }

    void Start()
    {
        rope = FindFirstObjectByType<Rope>();
        bucket = FindFirstObjectByType<BucketBody>();
        fluidSim = FindFirstObjectByType<FluidSim>();
        drawingBoard = FindFirstObjectByType<DrawingBoard>();
        simController = FindFirstObjectByType<SimulationController>();
        particleDisplay = FindFirstObjectByType<ParticleDisplay3D>();
        fluidRender = FindFirstObjectByType<FluidRenderTest>();
        SyncAngleControlsFromRope();
        SyncFluidSettings();
    }

    void Update()
    {
        if (hudPanel == null || rope == null || bucket == null) return;

        float thetaDeg = rope.theta * Mathf.Rad2Deg;
        float phiDeg = rope.phi * Mathf.Rad2Deg;
        thetaText.text = string.Format("{0:F1}\u00B0", thetaDeg);
        phiText.text = string.Format("{0:F1}\u00B0", phiDeg);
        paintText.text = string.Format("{0:F1} kg", bucket.paintMass);
        massText.text = string.Format("{0:F1} kg", bucket.mass);

        float currentFps = 1f / Time.unscaledDeltaTime;
        fpsSmoothed = Mathf.Lerp(fpsSmoothed, currentFps, Time.unscaledDeltaTime * 5f);
        if (frameText != null)
            frameText.text = string.Format("{0:F0}", fpsSmoothed);

        windText.text = string.Format("{0:F1} m/s", rope.windStrength);
        dragText.text = string.Format("{0:F3}", rope.Cd);
        frictionText.text = string.Format("{0:F4}", rope.pivotFriction);

        if (particleCountText != null && fluidSim != null)
            particleCountText.text = fluidSim.NumParticles.ToString();
    }

    static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Lucida Console.ttf");
        Debug.Log(font);
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
        int radius = 8;
        int borderW = 2;
        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 0.94f);
        Sprite roundedSprite = BuildRoundedSprite(radius, borderW, bgColor, Color.black);

        hudPanel = new GameObject("HUD", typeof(Image), typeof(VerticalLayoutGroup));
        hudPanel.transform.SetParent(canvas.transform, false);

        RectTransform hrt = hudPanel.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(0, 1);
        hrt.pivot = new Vector2(0, 1);
        hrt.anchoredPosition = new Vector2(30, -30);
        hrt.sizeDelta = new Vector2(260, 400);

        Image hbg = hudPanel.GetComponent<Image>();
        hbg.type = Image.Type.Sliced;
        hbg.sprite = roundedSprite;
        hbg.pixelsPerUnitMultiplier = 1;

        VerticalLayoutGroup hlg = hudPanel.GetComponent<VerticalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 0;
        hlg.padding = new RectOffset(borderW + 12, borderW + 12, borderW + 12, borderW + 12);

        // Header
        GameObject titleGO = new GameObject("Title", typeof(Text));
        titleGO.transform.SetParent(hudPanel.transform, false);

        Text title = titleGO.GetComponent<Text>();
        title.text = "SIMULATION DATA";
        title.font = uiFont;
        title.fontSize = 14;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(0.85f, 0.85f, 0.9f);

        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 22;

        thetaText = AddHUDLine("hudTheta", "θ", "\u03B8", "0.0\u00B0");
        phiText = AddHUDLine("hudPhi", "φ", "\u03C6", "0.0\u00B0");
        paintText = AddHUDLine("hudPaint", "💧", "Paint", "0.0 kg");
        massText = AddHUDLine("hudMass", "🎒", "Mass", "0.0 kg");
        windText = AddHUDLine("hudWind", "W", "Wind", "0.0");
        dragText = AddHUDLine("hudDrag", "D", "Drag", "0.0");
        frictionText = AddHUDLine("hudFriction", "F", "Friction", "0.0000");
        frameText = AddHUDLine("hudFrames", "⏱", "FPS", "0");

        AddRenderingSettings(hudPanel.transform);
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

    Text AddHUDLine(string name, string icon, string label, string initialValue)
    {
        GameObject row = new GameObject(name, typeof(HorizontalLayoutGroup));
        row.transform.SetParent(hudPanel.transform, false);

        HorizontalLayoutGroup rlg = row.GetComponent<HorizontalLayoutGroup>();
        rlg.childAlignment = TextAnchor.MiddleLeft;
        rlg.childControlWidth = true;
        rlg.childControlHeight = true;
        rlg.spacing = 8;
        rlg.padding = new RectOffset(0, 0, 1, 1);

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 16;
        rowLE.flexibleHeight = 0;

        // Icon
        GameObject iconBox = new GameObject("IconBox", typeof(Image));
        iconBox.transform.SetParent(row.transform, false);
        Image iconBg = iconBox.GetComponent<Image>();
        iconBg.color = new Color(0.22f, 0.56f, 0.95f);
        LayoutElement iconLE = iconBox.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 16;
        iconLE.preferredHeight = 16;
        iconLE.flexibleWidth = 0;

        GameObject iconTxtGO = new GameObject("IconGlyph", typeof(Text));
        iconTxtGO.transform.SetParent(iconBox.transform, false);
        Text iconTxt = iconTxtGO.GetComponent<Text>();
        iconTxt.text = icon; // "θ", "φ", "💧", "🎒" etc
        iconTxt.font = uiFont;
        iconTxt.fontSize = 12;
        iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.color = Color.white;
        RectTransform itr = iconTxtGO.GetComponent<RectTransform>();
        itr.anchorMin = Vector2.zero; itr.anchorMax = Vector2.one;
        itr.offsetMin = Vector2.zero; itr.offsetMax = Vector2.zero;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(row.transform, false);
        Text labelText = labelGO.GetComponent<Text>();
        labelText.text = label;
        labelText.fontSize = 14;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = new Color(0.9f, 0.9f, 0.95f, 1.0f);
        labelText.font = uiFont;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 60;
        labelLE.flexibleWidth = 0;

        // Value
        GameObject valGO = new GameObject("Value", typeof(Text));
        valGO.transform.SetParent(row.transform, false);
        Text valText = valGO.GetComponent<Text>();
        valText.text = initialValue;
        valText.fontSize = 15;
        valText.fontStyle = FontStyle.Bold;
        valText.alignment = TextAnchor.MiddleRight;
        valText.color = new Color(0.9f, 0.9f, 0.95f, 1.0f); ;
        valText.font = uiFont;

        LayoutElement valLE = valGO.AddComponent<LayoutElement>();
        valLE.flexibleWidth = 1;

        return valText;
    }

    void CreateSidePanel()
    {
        int margin = 10;
        int width = 350;
        int radius = 8;
        int borderW = 2;
        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 0.94f);
        Sprite roundedSprite = BuildRoundedSprite(radius, borderW, bgColor, Color.black);

        GameObject borderGO = new GameObject("SidePanel", typeof(Image));
        borderGO.transform.SetParent(canvas.transform, false);

        RectTransform brt = borderGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(1, 0.5f);
        float rightOffset = margin;
        brt.offsetMin = new Vector2(-width - rightOffset, margin);
        brt.offsetMax = new Vector2(-rightOffset, -margin);

        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.type = Image.Type.Sliced;
        borderImg.sprite = roundedSprite;
        borderImg.pixelsPerUnitMultiplier = 1;

        // Viewport (clips content so it doesn't overflow the side panel)
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(borderGO.transform, false);
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(borderW, borderW);
        vpRt.offsetMax = new Vector2(-borderW, -borderW);

        // Content (grows to fit all cards)
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        crt.pivot = new Vector2(0, 1);

        VerticalLayoutGroup cvlg = content.GetComponent<VerticalLayoutGroup>();
        cvlg.childAlignment = TextAnchor.UpperCenter;
        cvlg.childControlWidth = true;
        cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;
        cvlg.spacing = 10;
        cvlg.padding = new RectOffset(borderW + 12, borderW + 12, borderW + 12, borderW + 12);

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = borderGO.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 20;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // Scrollbar
        GameObject scrollbarGO = new GameObject("Scrollbar", typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(borderGO.transform, false);
        RectTransform sbRt = scrollbarGO.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(1, 0);
        sbRt.anchorMax = new Vector2(1, 1);
        sbRt.pivot = new Vector2(1, 0.5f);
        sbRt.offsetMin = new Vector2(-14, borderW);
        sbRt.offsetMax = new Vector2(-borderW, -borderW);
        Image sbImg = scrollbarGO.GetComponent<Image>();
        sbImg.color = new Color(0.2f, 0.2f, 0.22f, 0.5f);
        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarGO.transform, false);
        RectTransform saRt = slidingArea.GetComponent<RectTransform>();
        saRt.anchorMin = Vector2.zero;
        saRt.anchorMax = Vector2.one;
        saRt.offsetMin = new Vector2(2, 2);
        saRt.offsetMax = new Vector2(-2, -2);

        GameObject handleGO = new GameObject("Handle", typeof(Image));
        handleGO.transform.SetParent(slidingArea.transform, false);
        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.color = new Color(0.4f, 0.4f, 0.45f, 0.8f);
        RectTransform handleRt = handleGO.GetComponent<RectTransform>();
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.offsetMin = Vector2.zero;
        handleRt.offsetMax = Vector2.zero;
        scrollbar.targetGraphic = handleImg;
        scrollbar.handleRect = handleRt;

        scrollRect.verticalScrollbar = scrollbar;

        sidePanel = content;

        AddColorPicker(sidePanel.transform);
        AddActionButtons(sidePanel.transform);
        AddHoleSettings(sidePanel.transform);
        AddFluidSettings(sidePanel.transform);
        AddRopeSettings(sidePanel.transform);
    }

    static Sprite BuildRoundedSprite(int r, int border, Color fill, Color outline)
    {
        int outerR = r + border;
        int size = outerR * 2 + 1;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        int left = outerR;
        int right = size - 1 - outerR;
        int bottom = outerR;
        int top = size - 1 - outerR;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool insideInner = IsInsideRoundedRect(x, y, size, r);
                bool insideOuter = IsInsideRoundedRect(x, y, size, outerR);

                Color pixel;
                if (insideInner)
                    pixel = fill;
                else if (insideOuter)
                    pixel = outline;
                else
                    pixel = Color.clear;

                tex.SetPixel(x, y, pixel);
            }
        }
        tex.Apply();

        Vector4 sprBorder = new Vector4(outerR, outerR, outerR, outerR);
        Rect rect = new Rect(0, 0, size, size);
        return Sprite.Create(tex, rect, Vector2.one * 0.5f, 100, 0, SpriteMeshType.Tight, sprBorder);
    }

    static bool IsInsideRoundedRect(int x, int y, int size, int r)
    {
        int left = r;
        int right = size - 1 - r;
        int bottom = r;
        int top = size - 1 - r;

        if (x >= left && x <= right && y >= bottom && y <= top)
            return true;

        if (x < left && y >= bottom && y <= top)
            return true;
        if (x > right && y >= bottom && y <= top)
            return true;
        if (y < bottom && x >= left && x <= right)
            return true;
        if (y > top && x >= left && x <= right)
            return true;

        if (x < left && y < bottom)
            return (x - left) * (x - left) + (y - bottom) * (y - bottom) <= r * r;
        if (x > right && y < bottom)
            return (x - right) * (x - right) + (y - bottom) * (y - bottom) <= r * r;
        if (x < left && y > top)
            return (x - left) * (x - left) + (y - top) * (y - top) <= r * r;
        if (x > right && y > top)
            return (x - right) * (x - right) + (y - top) * (y - top) <= r * r;

        return false;
    }

    void AddColorPicker(Transform parent)
    {
        GameObject section = new GameObject(
            "ColorSection",
            typeof(VerticalLayoutGroup));
        section.transform.SetParent(parent, false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.MiddleCenter;
        slg.childControlWidth = true;
        slg.childControlHeight = true;
        slg.spacing = 0;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.preferredHeight = 160f;
        sectionLE.minHeight = 160f;
        sectionLE.flexibleHeight = 0;

        GameObject labelGO = new GameObject("ColorLabel", typeof(Text));
        labelGO.transform.SetParent(section.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "🎨  SELECT PAINT COLOR";
        label.fontSize = 13;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.85f, 0.85f, 0.9f);
        label.font = uiFont;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredHeight = 3;
        labelLE.flexibleHeight = 0;

        int cols = 6;
        GameObject gridGO = new GameObject("ColorGrid", typeof(GridLayoutGroup));
        gridGO.transform.SetParent(section.transform, false);

        GridLayoutGroup glg = gridGO.GetComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = cols;
        glg.childAlignment = TextAnchor.MiddleCenter;
        glg.cellSize = new Vector2(32, 32);
        glg.spacing = new Vector2(6, 6);
        glg.padding = new RectOffset(4, 4, 4, 4);

        int rows = Mathf.CeilToInt((float)presetColors.Length / cols);
        float gridHeight = rows * glg.cellSize.y + (rows - 1) * glg.spacing.y + glg.padding.top + glg.padding.bottom;

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
        swatch.sprite = BuildRoundedSprite(6, 0, Color.white, Color.clear);
        swatch.type = Image.Type.Sliced;
        swatch.preserveAspect = true;
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

        if (sidePanel == null) return;
        Transform grid = sidePanel.transform.Find("ColorSection/ColorGrid");
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

    void AddHoleSettings(Transform parent)
    {
        GameObject section = CreateCard(parent, "HOLE SETTINGS", "");
        section.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        Text sectionTitle = section.transform.Find("Header/Title").GetComponent<Text>();
        sectionTitle.text = "HOLE SETTINGS";
        sectionTitle.fontSize = 13;
        sectionTitle.fontStyle = FontStyle.Bold;
        sectionTitle.alignment = TextAnchor.MiddleLeft;
        sectionTitle.color = new Color(0.85f, 0.85f, 0.9f);

        // Hide the separate icon column since the emoji is now baked into the title text
        Transform iconT = section.transform.Find("Header/Icon");
        if (iconT != null) iconT.gameObject.SetActive(false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.UpperLeft;
        slg.childControlWidth = true;
        slg.childControlHeight = true;
        slg.spacing = 8;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.flexibleHeight = 0;

        // Toggle row
        GameObject toggleRow = new GameObject("ToggleRow", typeof(HorizontalLayoutGroup));
        toggleRow.transform.SetParent(section.transform, false);

        HorizontalLayoutGroup tlg = toggleRow.GetComponent<HorizontalLayoutGroup>();
        tlg.childAlignment = TextAnchor.MiddleLeft;
        tlg.childControlWidth = true;
        tlg.childControlHeight = true;
        tlg.childForceExpandWidth = false;
        tlg.spacing = 4;

        GameObject toggleGO = new GameObject("Toggle", typeof(Toggle), typeof(Image));
        toggleGO.transform.SetParent(toggleRow.transform, false);
        Toggle toggle = toggleGO.GetComponent<Toggle>();
        Image toggleBg = toggleGO.GetComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.2f, 0.22f);
        LayoutElement toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 18;
        toggleLE.preferredHeight = 18;
        toggleLE.flexibleWidth = 0;
        toggleLE.flexibleHeight = 0;

        // Checkmark
        GameObject checkGO = new GameObject("Checkmark", typeof(Image));
        checkGO.transform.SetParent(toggleGO.transform, false);
        Image checkImg = checkGO.GetComponent<Image>();
        checkImg.color = new Color(0.22f, 0.56f, 0.95f);
        RectTransform checkRt = checkGO.GetComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(2, 2);
        checkRt.offsetMax = new Vector2(-2, -2);
        toggle.graphic = checkImg;
        toggle.targetGraphic = toggleBg;
        toggle.transition = Selectable.Transition.ColorTint;
        ColorBlock tcb = toggle.colors;
        tcb.highlightedColor = new Color(0.3f, 0.3f, 0.35f);
        toggle.colors = tcb;

        if (bucket != null) toggle.isOn = bucket.holeEnabled;

        // Toggle label
        GameObject toggleLabelGO = new GameObject("Label", typeof(Text));
        toggleLabelGO.transform.SetParent(toggleRow.transform, false);
        Text toggleLabel = toggleLabelGO.GetComponent<Text>();
        toggleLabel.text = "Hole Open";
        toggleLabel.fontSize = 14;
        toggleLabel.alignment = TextAnchor.MiddleLeft;
        toggleLabel.color = new Color(0.85f, 0.86f, 0.92f);
        toggleLabel.font = uiFont;
        LayoutElement toggleLabelLE = toggleLabelGO.AddComponent<LayoutElement>();
        toggleLabelLE.preferredWidth = 120;
        toggleLabelLE.flexibleWidth = 1;

        toggle.onValueChanged.AddListener(v =>
        {
            if (bucket != null) bucket.holeEnabled = v;
        });

        // Radius slider
        GameObject sliderRow = new GameObject("RadiusRow", typeof(VerticalLayoutGroup));
        sliderRow.transform.SetParent(section.transform, false);

        VerticalLayoutGroup slgRow = sliderRow.GetComponent<VerticalLayoutGroup>();
        slgRow.childControlWidth = true;
        slgRow.childControlHeight = true;
        slgRow.spacing = 2;

        LayoutElement sliderRowLE = sliderRow.AddComponent<LayoutElement>();
        sliderRowLE.preferredHeight = 46;

        GameObject sliderLabelGO = new GameObject("Label", typeof(Text));
        sliderLabelGO.transform.SetParent(sliderRow.transform, false);
        Text sliderLabel = sliderLabelGO.GetComponent<Text>();
        sliderLabel.text = "Radius";
        sliderLabel.fontSize = 16;
        sliderLabel.fontStyle = FontStyle.Bold;
        sliderLabel.alignment = TextAnchor.MiddleLeft;
        sliderLabel.color = new Color(0.85f, 0.86f, 0.92f);
        sliderLabel.font = uiFont;
        LayoutElement sliderLabelLE = sliderLabelGO.AddComponent<LayoutElement>();
        sliderLabelLE.preferredHeight = 16;

        GameObject sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(sliderRow.transform, false);
        Slider slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0.10f;
        slider.maxValue = 1.0f;
        slider.wholeNumbers = false;

        RectTransform srt = sliderGO.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(0, 24);

        GameObject bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        Image bg = bgGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(8, 0, new Color(0, 0, 0, 0.35f), Color.clear);   // ✅ rounded track
        RectTransform bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0, 0);
        fillAreaRt.anchorMax = new Vector2(1, 1);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill", typeof(Image));
        fillGO.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.type = Image.Type.Sliced;
        fillImg.sprite = BuildRoundedSprite(8, 0, new Color(0.22f, 0.56f, 0.95f), Color.clear);   // ✅ rounded fill, same radius as track
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
        handleImg.color = new Color(1, 1, 1, 0f);
        RectTransform handleRt = handleGO.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(16, 16);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;

        GameObject valueGO = new GameObject("Value", typeof(Text));
        valueGO.transform.SetParent(sliderRow.transform, false);
        Text valueText = valueGO.GetComponent<Text>();
        valueText.fontSize = 14;
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.color = new Color(0.85f, 0.86f, 0.92f);
        valueText.font = uiFont;
        LayoutElement valueLE = valueGO.AddComponent<LayoutElement>();
        valueLE.preferredHeight = 14;

        float initialVal = bucket != null ? bucket.holeRadius : 0.3f;
        slider.SetValueWithoutNotify(initialVal);
        valueText.text = string.Format("{0:F2}m", initialVal);

        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = string.Format("{0:F2}m", v);
            if (bucket != null) bucket.holeRadius = v;
        });
    }

    void AddFluidSettings(Transform parent)
    {
        GameObject section = CreateCard(parent, "FLUID SETTINGS", "");
        section.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        Text sectionTitle = section.transform.Find("Header/Title").GetComponent<Text>();
        sectionTitle.text = "FLUID SETTINGS";
        sectionTitle.fontSize = 13;
        sectionTitle.fontStyle = FontStyle.Bold;
        sectionTitle.alignment = TextAnchor.MiddleLeft;
        sectionTitle.color = new Color(0.85f, 0.85f, 0.9f);

        Transform iconT = section.transform.Find("Header/Icon");
        if (iconT != null) iconT.gameObject.SetActive(false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.UpperLeft;
        slg.childControlWidth = true;
        slg.childControlHeight = true;
        slg.spacing = 8;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.flexibleHeight = 0;

        // Gravity input
        gravityInput = AddInputFieldRow(section.transform, "GravityRow", "Gravity", "-10", v =>
        {
            if (fluidSim != null && float.TryParse(v, out float val)) fluidSim.gravity = val;
        });

        // Smoothing Radius input
        smoothingRadiusInput = AddInputFieldRow(section.transform, "SmoothingRadiusRow", "Smoothing Radius", "0.200", v =>
        {
            if (fluidSim != null && float.TryParse(v, out float val)) fluidSim.smoothingRadius = val;
        });

        // Viscosity input
        viscosityInput = AddInputFieldRow(section.transform, "ViscosityRow", "Viscosity", "0.00", v =>
        {
            if (fluidSim != null && float.TryParse(v, out float val)) fluidSim.viscosityStrength = val;
        });

        // Damping slider
        GameObject dampingRow = new GameObject("DampingRow", typeof(VerticalLayoutGroup));
        dampingRow.transform.SetParent(section.transform, false);

        VerticalLayoutGroup dlg = dampingRow.GetComponent<VerticalLayoutGroup>();
        dlg.childControlWidth = true;
        dlg.childControlHeight = true;
        dlg.spacing = 2;

        LayoutElement dlgLE = dampingRow.AddComponent<LayoutElement>();
        dlgLE.preferredHeight = 46;

        GameObject dampingLabelGO = new GameObject("Label", typeof(Text));
        dampingLabelGO.transform.SetParent(dampingRow.transform, false);
        Text dampingLabel = dampingLabelGO.GetComponent<Text>();
        dampingLabel.text = "Damping";
        dampingLabel.fontSize = 16;
        dampingLabel.fontStyle = FontStyle.Bold;
        dampingLabel.alignment = TextAnchor.MiddleLeft;
        dampingLabel.color = new Color(0.85f, 0.86f, 0.92f);
        dampingLabel.font = uiFont;
        LayoutElement dampingLabelLE = dampingLabelGO.AddComponent<LayoutElement>();
        dampingLabelLE.preferredHeight = 16;

        GameObject dampingSliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        dampingSliderGO.transform.SetParent(dampingRow.transform, false);
        dampingSlider = dampingSliderGO.GetComponent<Slider>();
        dampingSlider.minValue = 0;
        dampingSlider.maxValue = 1;
        dampingSlider.wholeNumbers = false;

        RectTransform dsrt = dampingSliderGO.GetComponent<RectTransform>();
        dsrt.sizeDelta = new Vector2(0, 24);

        GameObject dbgGO = new GameObject("Background", typeof(Image));
        dbgGO.transform.SetParent(dampingSliderGO.transform, false);
        Image dbg = dbgGO.GetComponent<Image>();
        dbg.type = Image.Type.Sliced;
        dbg.sprite = BuildRoundedSprite(8, 0, new Color(0, 0, 0, 0.35f), Color.clear);
        RectTransform dbgRt = dbgGO.GetComponent<RectTransform>();
        dbgRt.anchorMin = Vector2.zero;
        dbgRt.anchorMax = Vector2.one;
        dbgRt.offsetMin = Vector2.zero;
        dbgRt.offsetMax = Vector2.zero;

        GameObject dfillArea = new GameObject("Fill Area", typeof(RectTransform));
        dfillArea.transform.SetParent(dampingSliderGO.transform, false);
        RectTransform dfillAreaRt = dfillArea.GetComponent<RectTransform>();
        dfillAreaRt.anchorMin = new Vector2(0, 0);
        dfillAreaRt.anchorMax = new Vector2(1, 1);
        dfillAreaRt.offsetMin = Vector2.zero;
        dfillAreaRt.offsetMax = Vector2.zero;

        GameObject dfillGO = new GameObject("Fill", typeof(Image));
        dfillGO.transform.SetParent(dfillArea.transform, false);
        Image dfillImg = dfillGO.GetComponent<Image>();
        dfillImg.type = Image.Type.Sliced;
        dfillImg.sprite = BuildRoundedSprite(8, 0, new Color(0.22f, 0.56f, 0.95f), Color.clear);
        RectTransform dfillRt = dfillGO.GetComponent<RectTransform>();
        dfillRt.anchorMin = Vector2.zero;
        dfillRt.anchorMax = Vector2.one;
        dfillRt.offsetMin = Vector2.zero;
        dfillRt.offsetMax = Vector2.zero;

        GameObject dhandleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        dhandleArea.transform.SetParent(dampingSliderGO.transform, false);
        RectTransform dhandleAreaRt = dhandleArea.GetComponent<RectTransform>();
        dhandleAreaRt.anchorMin = Vector2.zero;
        dhandleAreaRt.anchorMax = Vector2.one;
        dhandleAreaRt.offsetMin = new Vector2(10, 0);
        dhandleAreaRt.offsetMax = new Vector2(-10, 0);

        GameObject dhandleGO = new GameObject("Handle", typeof(Image));
        dhandleGO.transform.SetParent(dhandleArea.transform, false);
        Image dhandleImg = dhandleGO.GetComponent<Image>();
        dhandleImg.color = new Color(1, 1, 1, 0f);
        RectTransform dhandleRt = dhandleGO.GetComponent<RectTransform>();
        dhandleRt.sizeDelta = new Vector2(16, 16);

        dampingSlider.fillRect = dfillRt;
        dampingSlider.handleRect = dhandleRt;
        dampingSlider.targetGraphic = dhandleImg;
        dampingSlider.direction = Slider.Direction.LeftToRight;

        GameObject dvalueGO = new GameObject("Value", typeof(Text));
        dvalueGO.transform.SetParent(dampingRow.transform, false);
        dampingValueText = dvalueGO.GetComponent<Text>();
        dampingValueText.fontSize = 14;
        dampingValueText.alignment = TextAnchor.MiddleRight;
        dampingValueText.color = new Color(0.85f, 0.86f, 0.92f);
        dampingValueText.font = uiFont;
        LayoutElement dvalueLE = dvalueGO.AddComponent<LayoutElement>();
        dvalueLE.preferredHeight = 14;

        float initialDamping = fluidSim != null ? fluidSim.collisionDamping : 0.95f;
        dampingSlider.SetValueWithoutNotify(initialDamping);
        dampingValueText.text = string.Format("{0:F2}", initialDamping);

        dampingSlider.onValueChanged.AddListener(v =>
        {
            dampingValueText.text = string.Format("{0:F2}", v);
            if (fluidSim != null) fluidSim.collisionDamping = v;
        });

        // Particle count (read-only)
        GameObject particleRow = new GameObject("ParticleCountRow", typeof(HorizontalLayoutGroup));
        particleRow.transform.SetParent(section.transform, false);

        HorizontalLayoutGroup prlg = particleRow.GetComponent<HorizontalLayoutGroup>();
        prlg.childAlignment = TextAnchor.MiddleLeft;
        prlg.childControlWidth = true;
        prlg.childControlHeight = true;
        prlg.spacing = 4;

        LayoutElement particleRowLE = particleRow.AddComponent<LayoutElement>();
        particleRowLE.preferredHeight = 24;

        GameObject particleLabelGO = new GameObject("Label", typeof(Text));
        particleLabelGO.transform.SetParent(particleRow.transform, false);
        Text particleLabel = particleLabelGO.GetComponent<Text>();
        particleLabel.text = "Particle Count";
        particleLabel.fontSize = 16;
        particleLabel.fontStyle = FontStyle.Bold;
        particleLabel.alignment = TextAnchor.MiddleLeft;
        particleLabel.color = new Color(0.85f, 0.86f, 0.92f);
        particleLabel.font = uiFont;
        LayoutElement particleLabelLE = particleLabelGO.AddComponent<LayoutElement>();
        particleLabelLE.flexibleWidth = 1;

        GameObject particleValueGO = new GameObject("Value", typeof(Text));
        particleValueGO.transform.SetParent(particleRow.transform, false);
        particleCountText = particleValueGO.GetComponent<Text>();
        particleCountText.text = fluidSim != null ? fluidSim.NumParticles.ToString() : "0";
        particleCountText.fontSize = 16;
        particleCountText.fontStyle = FontStyle.Bold;
        particleCountText.alignment = TextAnchor.MiddleRight;
        particleCountText.color = new Color(0.22f, 0.56f, 0.95f);
        particleCountText.font = uiFont;
        LayoutElement particleValueLE = particleValueGO.AddComponent<LayoutElement>();
        particleValueLE.preferredWidth = 60;
    }

    void AddToggleRow(Transform parent, string name, string label, bool initialValue, System.Action<bool> onChanged)
    {
        GameObject row = new GameObject(name, typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup tlg = row.GetComponent<HorizontalLayoutGroup>();
        tlg.childAlignment = TextAnchor.MiddleLeft;
        tlg.childControlWidth = true;
        tlg.childControlHeight = true;
        tlg.childForceExpandWidth = false;
        tlg.spacing = 4;

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 24;

        GameObject toggleGO = new GameObject("Toggle", typeof(Toggle), typeof(Image));
        toggleGO.transform.SetParent(row.transform, false);
        Toggle toggle = toggleGO.GetComponent<Toggle>();
        Image toggleBg = toggleGO.GetComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.2f, 0.22f);
        LayoutElement toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 18;
        toggleLE.preferredHeight = 18;
        toggleLE.flexibleWidth = 0;
        toggleLE.flexibleHeight = 0;

        GameObject checkGO = new GameObject("Checkmark", typeof(Image));
        checkGO.transform.SetParent(toggleGO.transform, false);
        Image checkImg = checkGO.GetComponent<Image>();
        checkImg.color = new Color(0.22f, 0.56f, 0.95f);
        RectTransform checkRt = checkGO.GetComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(2, 2);
        checkRt.offsetMax = new Vector2(-2, -2);
        toggle.graphic = checkImg;
        toggle.targetGraphic = toggleBg;
        toggle.transition = Selectable.Transition.ColorTint;
        ColorBlock tcb = toggle.colors;
        tcb.highlightedColor = new Color(0.3f, 0.3f, 0.35f);
        toggle.colors = tcb;

        toggle.isOn = initialValue;

        GameObject toggleLabelGO = new GameObject("Label", typeof(Text));
        toggleLabelGO.transform.SetParent(row.transform, false);
        Text toggleLabel = toggleLabelGO.GetComponent<Text>();
        toggleLabel.text = label;
        toggleLabel.fontSize = 18;
        toggleLabel.alignment = TextAnchor.MiddleLeft;
        toggleLabel.color = new Color(0.85f, 0.86f, 0.92f);
        toggleLabel.font = uiFont;
        LayoutElement toggleLabelLE = toggleLabelGO.AddComponent<LayoutElement>();
        toggleLabelLE.flexibleWidth = 1;

        toggle.onValueChanged.AddListener(v => onChanged(v));
    }

    InputField AddInputFieldRow(Transform parent, string name, string label, string initialValue, System.Action<string> onValueChanged)
    {
        GameObject row = new GameObject(name, typeof(VerticalLayoutGroup));
        row.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = row.GetComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 2;

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 46;

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
        labelLE.preferredHeight = 16;

        GameObject inputGO = new GameObject("InputField", typeof(Image), typeof(InputField));
        inputGO.transform.SetParent(row.transform, false);

        Image inputBg = inputGO.GetComponent<Image>();
        inputBg.type = Image.Type.Sliced;
        inputBg.sprite = BuildRoundedSprite(6, 0, new Color(0.15f, 0.16f, 0.18f), Color.clear);
        inputBg.color = Color.white;

        InputField inputField = inputGO.GetComponent<InputField>();

        GameObject textGO = new GameObject("Text", typeof(Text));
        textGO.transform.SetParent(inputGO.transform, false);
        Text text = textGO.GetComponent<Text>();
        text.text = initialValue;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.85f, 0.86f, 0.92f);
        text.font = uiFont;

        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8, 0);
        textRt.offsetMax = new Vector2(-8, 0);

        inputField.textComponent = text;
        inputField.text = initialValue;

        LayoutElement inputLE = inputGO.AddComponent<LayoutElement>();
        inputLE.preferredHeight = 24;

        inputField.onValueChanged.AddListener(v => onValueChanged(v));

        return inputField;
    }

    void SyncFluidSettings()
    {
        if (fluidSim == null) return;

        if (gravityInput != null)
            gravityInput.text = fluidSim.gravity.ToString("F1");
        if (smoothingRadiusInput != null)
            smoothingRadiusInput.text = fluidSim.smoothingRadius.ToString("F3");
        if (viscosityInput != null)
            viscosityInput.text = fluidSim.viscosityStrength.ToString("F2");
        if (dampingSlider != null)
        {
            dampingSlider.SetValueWithoutNotify(fluidSim.collisionDamping);
            if (dampingValueText != null)
                dampingValueText.text = string.Format("{0:F2}", fluidSim.collisionDamping);
        }
    }

    void AddRopeSettings(Transform parent)
    {
        GameObject section = CreateCard(parent, "ROPE SETTINGS", "");
        section.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        Text sectionTitle = section.transform.Find("Header/Title").GetComponent<Text>();
        sectionTitle.text = "ROPE SETTINGS";
        sectionTitle.fontSize = 13;
        sectionTitle.fontStyle = FontStyle.Bold;
        sectionTitle.alignment = TextAnchor.MiddleLeft;
        sectionTitle.color = new Color(0.85f, 0.85f, 0.9f);

        Transform iconT = section.transform.Find("Header/Icon");
        if (iconT != null) iconT.gameObject.SetActive(false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.UpperLeft;
        slg.childControlWidth = true;
        slg.childControlHeight = true;
        slg.spacing = 8;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.flexibleHeight = 0;

        AddInputFieldRow(section.transform, "StartThetaRow", "Start Theta", rope != null ? rope.startTheta.ToString("F1") : "20", v =>
        {
            if (rope != null && float.TryParse(v, out float val)) rope.SetThetaPhiDegrees(val, rope.startPhi);
        });

        AddInputFieldRow(section.transform, "StartPhiRow", "Start Phi", rope != null ? rope.startPhi.ToString("F1") : "0", v =>
        {
            if (rope != null && float.TryParse(v, out float val)) rope.SetThetaPhiDegrees(rope.startTheta, val);
        });

        AddInputFieldRow(section.transform, "RopeLengthRow", "Rope Length", rope != null ? rope.ropeLength.ToString("F1") : "5", v =>
        {
            if (rope != null && float.TryParse(v, out float val)) rope.SetRopeLength(val);
        });

        AddToggleRow(section.transform, "TwistEnableRow", "Twist Enable", rope != null ? rope.twistEnabled : false, v =>
        {
            if (rope != null) rope.twistEnabled = v;
        });

        AddInputFieldRow(section.transform, "PaintMassRow", "Paint Mass", rope != null && rope.bucketBody != null ? rope.bucketBody.paintMass.ToString("F1") : "10", v =>
        {
            if (rope != null && rope.bucketBody != null && float.TryParse(v, out float val)) rope.bucketBody.SetPaintMass(val);
        });

        AddInputFieldRow(section.transform, "WindStrengthRow", "Wind Strength", rope != null ? rope.windStrength.ToString("F1") : "0", v =>
        {
            if (rope != null && float.TryParse(v, out float val)) rope.windStrength = val;
        });

        AddToggleRow(section.transform, "WindDirXRow", "Wind → X", rope != null && rope.windDirection.x != 0, v =>
        {
            if (rope != null) rope.windDirection = new Vector3(v ? 1 : 0, 0, rope.windDirection.z);
        });

        AddToggleRow(section.transform, "WindDirZRow", "Wind → Z", rope != null && rope.windDirection.z != 0, v =>
        {
            if (rope != null) rope.windDirection = new Vector3(rope.windDirection.x, 0, v ? 1 : 0);
        });

        AddInputFieldRow(section.transform, "PivotFrictionRow", "Pivot Friction", rope != null ? rope.pivotFriction.ToString("F4") : "0.01", v =>
        {
            if (rope != null && float.TryParse(v, out float val)) rope.pivotFriction = Mathf.Max(0, val);
        });

        AddInputFieldRow(section.transform, "AirDensityRow", "Air Density", rope != null ? rope.rho_air.ToString("F3") : "1.225", v =>
        {
            if (rope != null && float.TryParse(v, out float val)) rope.rho_air = Mathf.Max(0, val);
        });
    }

    void AddRenderingSettings(Transform parent)
    {
        GameObject section = CreateCard(parent, "RENDERING SETTINGS", "");
        section.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        Text sectionTitle = section.transform.Find("Header/Title").GetComponent<Text>();
        sectionTitle.text = "RENDERING SETTINGS";
        sectionTitle.fontSize = 13;
        sectionTitle.fontStyle = FontStyle.Bold;
        sectionTitle.alignment = TextAnchor.MiddleLeft;
        sectionTitle.color = new Color(0.85f, 0.85f, 0.9f);

        Transform iconT = section.transform.Find("Header/Icon");
        if (iconT != null) iconT.gameObject.SetActive(false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.UpperLeft;
        slg.childControlWidth = true;
        slg.childControlHeight = true;
        slg.spacing = 6;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.flexibleHeight = 0;

        AddDisplayModeButton(section.transform);
        AddScreenSpaceButton(section.transform);
    }

    void AddActionButtons(Transform parent)
    {
        GameObject section = CreateCard(parent, "ACTIONS", "");
        section.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        Text sectionTitle = section.transform.Find("Header/Title").GetComponent<Text>();
        sectionTitle.text = "ACTIONS";
        sectionTitle.fontSize = 13;
        sectionTitle.fontStyle = FontStyle.Bold;
        sectionTitle.alignment = TextAnchor.MiddleLeft;
        sectionTitle.color = new Color(0.85f, 0.85f, 0.9f);

        Transform iconT = section.transform.Find("Header/Icon");
        if (iconT != null) iconT.gameObject.SetActive(false);

        VerticalLayoutGroup slg = section.GetComponent<VerticalLayoutGroup>();
        slg.childAlignment = TextAnchor.UpperLeft;
        slg.childControlWidth = true;
        slg.childControlHeight = true;
        slg.spacing = 6;

        LayoutElement sectionLE = section.AddComponent<LayoutElement>();
        sectionLE.flexibleHeight = 0;

        AddStartButton(section.transform);
        AddSpawnButton(section.transform);
        AddRestartButton(section.transform);
    }

    void AddSpawnButton(Transform parent)
    {
        GameObject btnGO = new GameObject("SpawnButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(10, 0, new Color(0.28f, 0.29f, 0.32f), Color.clear);  // ✅ same grey, radius 10
        bg.color = Color.white;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 30f;   // ✅ was 42 — smaller
        btnLE.flexibleHeight = 0;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "⬤ SPAWN PARTICLES";
        label.fontSize = 13;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.font = uiFont;

        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        spawnButton = btn;

        btn.onClick.AddListener(() =>
        {
            if (fluidSim != null) fluidSim.Spawn();
            particlesSpawned = true;
            btn.interactable = false;
        });
        spawnButton = btn;
    }

    void AddRestartButton(Transform parent)
    {
        GameObject btnGO = new GameObject("RestartButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(10, 0, new Color(0.35f, 0.25f, 0.20f), Color.clear);
        bg.color = Color.white;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 30f;
        btnLE.flexibleHeight = 0;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "↺ RESTART";
        label.fontSize = 13;
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
            if (bucket != null) bucket.ResetState();
            if (rope != null)
            {
                rope.windStrength = 0;
                rope.windDirection = Vector3.right;
                rope.Restart();
            }
            if (fluidSim != null) fluidSim.FullReset();


            particlesSpawned = false;
            if (spawnButton != null) spawnButton.interactable = true;
            if (startButton != null) startButton.interactable = true;
        });
    }

    void AddDisplayModeButton(Transform parent)
    {
        GameObject btnGO = new GameObject("DisplayModeButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(10, 0, new Color(0.20f, 0.25f, 0.35f), Color.clear);
        bg.color = Color.white;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 30f;
        btnLE.flexibleHeight = 0;

        displayModeButton = btnGO.GetComponent<Button>();
        Button btn = displayModeButton;
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "◉ PAR: OFF";
        label.fontSize = 13;
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
            if (particleDisplay == null) particleDisplay = FindFirstObjectByType<ParticleDisplay3D>();
            if (particleDisplay == null) return;

            particleDisplay.mode = particleDisplay.mode switch
            {
                ParticleDisplay3D.DisplayMode.None => ParticleDisplay3D.DisplayMode.Shaded3D,
                ParticleDisplay3D.DisplayMode.Shaded3D => ParticleDisplay3D.DisplayMode.Billboard,
                ParticleDisplay3D.DisplayMode.Billboard => ParticleDisplay3D.DisplayMode.None,
                _ => ParticleDisplay3D.DisplayMode.None
            };

            label.text = particleDisplay.mode switch
            {
                ParticleDisplay3D.DisplayMode.None => "◉ PAR: OFF",
                ParticleDisplay3D.DisplayMode.Shaded3D => "◉ PAR: 3D",
                ParticleDisplay3D.DisplayMode.Billboard => "◉ PAR: BILLBOARD",
                _ => "◉ PAR: OFF"
            };
        });
    }

    void AddScreenSpaceButton(Transform parent)
    {
        GameObject btnGO = new GameObject("ScreenSpaceButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(10, 0, new Color(0.25f, 0.20f, 0.35f), Color.clear);
        bg.color = Color.white;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 30f;
        btnLE.flexibleHeight = 0;

        screenSpaceButton = btnGO.GetComponent<Button>();
        Button btn = screenSpaceButton;
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "⏺ LIQ: ON";
        label.fontSize = 13;
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
            if (fluidRender == null) fluidRender = FindFirstObjectByType<FluidRenderTest>();
            if (fluidRender == null) return;

            fluidRender.renderScreenSpace = !fluidRender.renderScreenSpace;
            label.text = fluidRender.renderScreenSpace ? "⏺ LIQ: ON" : "⏺ LIQ: OFF";
        });
    }

    void AddStartButton(Transform parent)
    {
        GameObject btnGO = new GameObject("StartButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(10, 0, new Color(0.28f, 0.29f, 0.32f), Color.clear);
        bg.color = Color.white;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 30f;
        btnLE.flexibleHeight = 0;

        startButton = btnGO.GetComponent<Button>();
        Button btn = startButton;
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "▶ START SIMULATION";
        label.fontSize = 14;
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
            btn.interactable = false;
        });
        startButton = btn;
    }

    //void AddRestartButton(Transform parent)
    //{
    //    GameObject btnGO = new GameObject("RestartButton", typeof(Image), typeof(Button));
    //    btnGO.transform.SetParent(parent, false);

    //    Image bg = btnGO.GetComponent<Image>();
    //    bg.type = Image.Type.Sliced;
    //    bg.sprite = BuildRoundedSprite(10, 0, new Color(0.38f, 0.29f, 0.22f), Color.clear);
    //    bg.color = Color.white;

    //    LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
    //    btnLE.preferredHeight = 30f;
    //    btnLE.flexibleHeight = 0;

    //    Button btn = btnGO.GetComponent<Button>();
    //    btn.targetGraphic = bg;
    //    ColorBlock cb = btn.colors;
    //    cb.normalColor = Color.white;
    //    cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
    //    cb.selectedColor = Color.white;
    //    btn.colors = cb;

    //    GameObject labelGO = new GameObject("Label", typeof(Text));
    //    labelGO.transform.SetParent(btnGO.transform, false);
    //    Text label = labelGO.GetComponent<Text>();
    //    label.text = "↺ RESTART";
    //    label.fontSize = 13;
    //    label.fontStyle = FontStyle.Bold;
    //    label.alignment = TextAnchor.MiddleCenter;
    //    label.color = Color.white;
    //    label.font = uiFont;

    //    RectTransform lrt = labelGO.GetComponent<RectTransform>();
    //    lrt.anchorMin = Vector2.zero;
    //    lrt.anchorMax = Vector2.one;
    //    lrt.offsetMin = Vector2.zero;
    //    lrt.offsetMax = Vector2.zero;

    //    btn.onClick.AddListener(() =>
    //    {
    //        if (simController != null)
    //            simController.RestartSimulation();

    //        if (startButton != null)
    //        {
    //            startButton.interactable = true;
    //            particlesSpawned = false;
    //        }

    //        if (spawnButton != null)
    //            spawnButton.interactable = true;
    //    });
    //}

    void AddSaveBoardButton(Transform parent)
    {
        GameObject btnGO = new GameObject("SaveBoardButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.sprite = BuildRoundedSprite(10, 0, new Color(0.08f, 0.09f, 0.11f, 0.94f), Color.clear);
        bg.color = Color.white;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 30f;
        btnLE.flexibleHeight = 0;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = cb;

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(30, -440);
        rt.sizeDelta = new Vector2(260, 30);

        GameObject labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);
        Text label = labelGO.GetComponent<Text>();
        label.text = "SAVE BOARD IMAGE";
        label.fontSize = 13;
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
            if (drawingBoard == null)
                drawingBoard = FindFirstObjectByType<DrawingBoard>();

            if (drawingBoard != null)
                drawingBoard.SaveBoardImage();
        });
    }

    GameObject CreateCard(Transform parent, string title, string icon = "")
    {
        // Card
        GameObject card = new GameObject(
            title + "_Card",
            typeof(Image),
            typeof(VerticalLayoutGroup));

        card.transform.SetParent(parent, false);

        Image bg = card.GetComponent<Image>();
        bg.color = new Color(0.10f, 0.11f, 0.13f, 0.95f);

        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 4;
        layout.padding = new RectOffset(12, 12, 12, 12);

        LayoutElement cardLE = card.AddComponent<LayoutElement>();
        cardLE.flexibleHeight = 0;

        // Header
        GameObject header = new GameObject(
            "Header",
            typeof(HorizontalLayoutGroup));

        header.transform.SetParent(card.transform, false);

        HorizontalLayoutGroup hlg = header.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 4;

        LayoutElement headerLE = header.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 20;

        // Optional icon
        if (!string.IsNullOrEmpty(icon))
        {
            GameObject iconGO = new GameObject("Icon", typeof(Text));
            iconGO.transform.SetParent(header.transform, false);

            Text iconText = iconGO.GetComponent<Text>();
            iconText.font = uiFont;
            iconText.fontSize = 14;
            iconText.alignment = TextAnchor.MiddleLeft;
            iconText.color = new Color(0.30f, 0.60f, 1f);
            iconText.text = icon;

            LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 20;
        }

        // Title
        GameObject titleGO = new GameObject("Title", typeof(Text));
        titleGO.transform.SetParent(header.transform, false);

        Text titleText = titleGO.GetComponent<Text>();
        titleText.font = uiFont;
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = new Color(0.85f, 0.87f, 0.92f);
        titleText.text = title.ToUpper();

        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1;

        return card;
    }
}
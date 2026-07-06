using System.Linq;
using UnityEngine;

public enum GameState { Menu, Playing }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; set; } = GameState.Menu;
    public Color selectedColor = Color.blue;

    public static event System.Action<Color> OnColorChanged;
    public static event System.Action OnGameStarted;

    Camera[] cameras;
    int currentCameraIndex;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        cameras = FindObjectsByType<Camera>(FindObjectsSortMode.InstanceID)
            .Where(c => c.gameObject.scene.IsValid() && c.gameObject.hideFlags == HideFlags.None)
            .OrderBy(c => c.depth)
            .ToArray();

        if (cameras.Length == 0) return;

        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].gameObject.SetActive(i == 0);
        }
        currentCameraIndex = 0;
    }

    void Update()
    {
        if (cameras == null || cameras.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.C))
        {
            cameras[currentCameraIndex].gameObject.SetActive(false);
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
            cameras[currentCameraIndex].gameObject.SetActive(true);
        }
    }

    public void SetColor(Color color)
    {
        selectedColor = color;
        OnColorChanged?.Invoke(color);
    }

    public void StartGame()
    {
        State = GameState.Playing;
        OnGameStarted?.Invoke();
    }
}
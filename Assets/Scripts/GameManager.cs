using UnityEngine;

public enum GameState { Menu, Playing }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Menu;
    public Color selectedColor = Color.blue;

    public static event System.Action<Color> OnColorChanged;
    public static event System.Action OnGameStarted;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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

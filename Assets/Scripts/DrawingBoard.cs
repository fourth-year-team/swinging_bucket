using UnityEngine;

public class DrawingBoard : MonoBehaviour
{
    [Header("Board Texture")]
    public int textureResolution = 1024;

    [HideInInspector]
    public RenderTexture boardTexture;

    [HideInInspector]
    public RenderTexture paintAmountTexture;

    void Awake()
    {
        // Create a writable texture the compute shader can paint onto
        boardTexture = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGBFloat);
        boardTexture.enableRandomWrite = true;
        boardTexture.filterMode = FilterMode.Bilinear;
        boardTexture.wrapMode = TextureWrapMode.Clamp;
        boardTexture.Create();

        paintAmountTexture = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RFloat);
        paintAmountTexture.enableRandomWrite = true;
        paintAmountTexture.filterMode = FilterMode.Bilinear;
        paintAmountTexture.wrapMode = TextureWrapMode.Clamp;
        paintAmountTexture.Create();

        // Clear to white (clean board)
        ClearBoard();

        // Show the texture on the board's surface
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.mainTexture = boardTexture;
        }
    }

    /// <summary>
    /// Returns the world-space Y of the board's top surface.
    /// The default Unity Cube mesh spans -0.5 to 0.5, so top = position.y + scale.y * 0.5
    /// </summary>
    public float GetTopSurfaceY()
    {
        return transform.position.y + transform.localScale.y * 0.5f;
    }

    /// <summary>
    /// Returns the half-extents of the board in XZ (world space).
    /// </summary>
    public Vector2 GetHalfSizeXZ()
    {
        return new Vector2(
            transform.localScale.x * 0.5f,
            transform.localScale.z * 0.5f
        );
    }

    public void ClearBoard()
    {
        if (boardTexture == null) return;
        RenderTexture prev = RenderTexture.active;

        RenderTexture.active = boardTexture;
        GL.Clear(true, true, Color.white);

        if (paintAmountTexture != null)
        {
            RenderTexture.active = paintAmountTexture;
            GL.Clear(true, true, Color.clear);
        }

        RenderTexture.active = prev;
    }

    void OnDestroy()
    {
        if (boardTexture != null)
        {
            boardTexture.Release();
        }

        if (paintAmountTexture != null)
        {
            paintAmountTexture.Release();
        }
    }
}

using UnityEngine;
using System.Collections;

public class BoxMovement : MonoBehaviour
{
    [Header("Delay Settings")]
    public float startDelay = 3f;     // seconds to wait before moving

    [Header("Movement Settings")]
    public float speed = 2f;          // units per second after delay
    public float minX = -7f;
    public float maxX = 7f;
    public bool moveOnZ = false;
    public float minZ = -7f;
    public float maxZ = 7f;

    // Internal state
    private float directionX = 1f;
    private float directionZ = 1f;
    private bool canMove = false;

    void Start()
    {
        // Start the delay coroutine
        StartCoroutine(StartDelay());
    }

    IEnumerator StartDelay()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(startDelay);
        canMove = true;
    }

    void Update()
    {
        // Don't move until the delay has finished
        if (!canMove) return;

        Vector3 pos = transform.position;

        // Move X back and forth between minX and maxX
        pos.x += speed * Time.deltaTime * directionX;
        if (pos.x >= maxX) { pos.x = maxX; directionX = -1f; }
        if (pos.x <= minX) { pos.x = minX; directionX = 1f; }

        // Move Z back and forth if enabled
        if (moveOnZ)
        {
            pos.z += speed * Time.deltaTime * directionZ;
            if (pos.z >= maxZ) { pos.z = maxZ; directionZ = -1f; }
            if (pos.z <= minZ) { pos.z = minZ; directionZ = 1f; }
        }

        transform.position = pos;
    }
}

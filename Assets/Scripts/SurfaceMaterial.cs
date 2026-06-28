using UnityEngine;

public class SurfaceMaterial : MonoBehaviour
{
    public enum MaterialType
    {
        Paper,
        Metal,
        Wood,
        Fabric
    }

    [Header("Surface Type")]
    public MaterialType surfaceType = MaterialType.Paper;

    [Header("Generated Properties (Read Only)")]
    public float friction = 0.98f;   // how fast particles slow down
    public float stickiness = 0.95f;   // how quickly they stop completely
    public float absorption = 0.8f;    // how much velocity is killed on impact

    void OnValidate()
    {
        // Auto-set properties when you change the type in Inspector
        ApplyMaterialProperties();
    }

    void Start()
    {
        ApplyMaterialProperties();
    }

    void ApplyMaterialProperties()
    {
        switch (surfaceType)
        {
            case MaterialType.Paper:
                friction = 0.55f;   // high friction — slows fast
                stickiness = 0.92f;   // sticks quickly
                absorption = 0.95f;   // absorbs most impact
                break;

            case MaterialType.Metal:
                friction = 0.97f;   // low friction — slides
                stickiness = 0.20f;   // barely sticks
                absorption = 0.30f;   // bouncy
                break;

            case MaterialType.Wood:
                friction = 0.75f;   // medium friction
                stickiness = 0.60f;   // medium stick
                absorption = 0.70f;   // medium absorption
                break;

            case MaterialType.Fabric:
                friction = 0.30f;   // very high friction
                stickiness = 0.98f;   // sticks almost immediately
                absorption = 0.99f;   // absorbs almost all impact
                break;
        }
    }

    public bool ShouldStick(Vector3 velocity)
    {
        // Particle sticks if it's moving slowly enough
        return velocity.magnitude < (1f - stickiness) * 2f;
    }
}

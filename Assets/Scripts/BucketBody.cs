using System.Runtime.InteropServices;
using UnityEngine;

public class BucketBody
{
    public Vector3 position;
    public Vector3 previousPosition;
    public float mass;
    public float theta;
    public float phi;
    public float paintMass = 10f;

    public BucketBody(Vector3 startPos, float mass, [Optional] Vector3 initialVel, [Optional] float dt, [Optional]float theta, [Optional]float phi)
    {
        position = startPos;
        previousPosition = startPos;
        this.mass = mass;
        this.theta = theta;
        this.phi = phi;
    }

    public void Integrate(Vector3 gravity, float dt, float damping) { 
    
        Vector3 velocity = (position - previousPosition);


        Vector3 current = position;
        position = current + velocity * (1f - damping) + gravity * dt * dt;

        previousPosition = current;

        paintMass -= 10f * dt * dt;

    }
}


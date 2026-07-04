using UnityEngine;

public class RespawnButton : MonoBehaviour
{
	public bool hasSpawned { get; private set; }

	public void Spawn()
	{
		if (hasSpawned) return;
		hasSpawned = true;
		FindFirstObjectByType<Seb.Fluid.Simulation.FluidSim>()?.Spawn();
	}
}

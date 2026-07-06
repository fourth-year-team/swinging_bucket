using UnityEngine;
using Seb.Fluid.Simulation;

public class SimulationController : MonoBehaviour
{
    public Rope rope;
    public FluidSim fluidSim;
    public DrawingBoard drawingBoard;
    public RespawnButton respawnButton;

    public void RestartSimulation()
    {
        if (fluidSim != null)
            fluidSim.FullReset();

        if (rope != null)
            rope.ResetSimulation();

        if (drawingBoard != null)
            drawingBoard.ClearBoard();

        if (respawnButton != null)
            respawnButton.hasSpawned = false;
    }
}

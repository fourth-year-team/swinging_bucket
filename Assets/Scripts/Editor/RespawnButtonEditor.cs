using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RespawnButton))]
public class RespawnButtonEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		RespawnButton spawner = (RespawnButton)target;
		bool disabled = spawner.hasSpawned;

		GUI.enabled = !disabled;
		if (GUILayout.Button(disabled ? "Spawned" : "Spawn Particles"))
		{
			spawner.Spawn();
		}
		GUI.enabled = true;
	}
}

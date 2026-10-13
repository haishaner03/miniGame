using UnityEngine;

[DisallowMultipleComponent]
public sealed class CurseAltar : MonoBehaviour
{
    private RunState run;
    private bool used;

    public void Initialize(RunState state) { run = state; }

    private void Update()
    {
        if (run == null || used || run.Phase != RunState.RunPhase.Playing || run.Player == null) return;
        if (((Vector2)transform.position - (Vector2)run.Player.transform.position).sqrMagnitude > 1.35f * 1.35f) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;
        used = run.UseCurseAltar();
    }
}

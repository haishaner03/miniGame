using UnityEngine;

[DisallowMultipleComponent]
public sealed class HealthPickup : MonoBehaviour
{
    public int Healing { get; private set; }
    private float age;
    private Vector3 landing;
    public void ResetDrop(Vector3 position, int healing) { landing = position; landing.z = 0; transform.position = landing; Healing = healing; age = 0; }
    public bool Tick(SurvivorHealth player, float delta)
    {
        age += delta;
        transform.position = landing + Vector3.up * (.04f * Mathf.Sin(age * 3));
        if (age < .3f || player.CurrentHealth >= player.MaxHealth || ((Vector2)player.transform.position - (Vector2)landing).sqrMagnitude > .45f * .45f) return false;
        player.Heal(Healing);
        return true;
    }
}

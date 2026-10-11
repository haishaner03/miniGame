using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScrapPickup : MonoBehaviour
{
    public int Value { get; private set; }
    private Vector3 landing;
    private float age;

    public void ResetDrop(Vector3 position, int value)
    {
        landing = position; landing.z = 0f;
        transform.position = landing;
        Value = Mathf.Max(1, value);
        age = 0f;
    }

    public void AddValue(int amount) { Value += Mathf.Max(0, amount); }

    public bool Tick(Vector3 playerPosition, float deltaTime)
    {
        age += deltaTime;
        transform.position = landing + Vector3.up * (.06f * Mathf.Sin(age * 3.2f));
        if (age < .25f) return false;
        return ((Vector2)transform.position - (Vector2)playerPosition).sqrMagnitude <= .42f * .42f;
    }
}

using UnityEngine;

/// <summary>A pooled ground collectible; the pool ticks motion and pickup checks centrally.</summary>
[DisallowMultipleComponent]
public sealed class ExperiencePickup : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private float scatterDuration = 0.25f;
    [SerializeField] private float pickupRadius = 0.28f;
    public int Value { get; private set; }
    private Vector3 origin, landing;
    private float age;

    public void ResetDrop(Vector3 position, int value)
    {
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        Value = value;
        age = 0f;
        origin = landing = position;
        origin.z = landing.z = 0f;
        // A short hop makes the drop readable; landing stays on the kill's walkable cell.
        transform.position = origin;
        transform.localScale = Vector3.one;
        if (visual != null) visual.color = value > 10 ? new Color(0.8f, 1f, 0.55f) : Color.white;
    }

    public void AddValue(int amount) { Value += amount; }

    public bool Tick(Vector3 playerPosition, float deltaTime)
    {
        age += deltaTime;
        if (age < scatterDuration)
        {
            float phase = age / scatterDuration;
            transform.position = origin + Vector3.up * (Mathf.Sin(phase * Mathf.PI) * 0.32f);
            return false;
        }
        transform.position = landing + Vector3.up * (0.045f * Mathf.Sin(age * 3f));
        // XP stays on the ground. It is collected only when the player walks onto it.
        return ((Vector2)transform.position - (Vector2)playerPosition).sqrMagnitude < pickupRadius * pickupRadius;
    }
}

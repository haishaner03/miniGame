using UnityEngine;

/// <summary>
/// 一次性区域波次触发器。玩家首次进入触发范围后，向所属 ZombieSpawner 请求一大波丧尸。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class ZombieWaveTrigger : MonoBehaviour
{
    [SerializeField] private string waveId = "Wave_01";
    [SerializeField] private int waveCount = 8;
    [SerializeField] private bool triggerOnEnter = true;
    [SerializeField] private bool destroyAfterTriggered;
    [Tooltip("可选：本波次专属刷怪点。为空时使用 ZombieSpawner 的全局刷怪点。")]
    [SerializeField] private Transform[] spawnPointsOverride;

    private bool triggered;
    private ZombieSpawner spawner;

    public string WaveId => waveId;
    public bool HasTriggered => triggered;
    public int Remaining => triggered && spawner != null ? spawner.WaveRemaining(waveId) : 0;
    public bool IsCleared => triggered && spawner != null && spawner.WaveCleared(waveId);

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnEnter || triggered || !IsPlayer(other))
            return;

        Trigger();
    }

    public void Trigger()
    {
        if (triggered)
            return;

        spawner = FindFirstObjectByType<ZombieSpawner>();
        if (spawner == null || !spawner.TriggerWaveOnce(waveId, waveCount, spawnPointsOverride))
            return;

        triggered = true;
        if (destroyAfterTriggered)
            gameObject.SetActive(false);
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;
        if (other.CompareTag("Player"))
            return true;
        return other.GetComponentInParent<SurvivorHealth>() != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
        Collider2D collider = GetComponent<Collider2D>();
        if (collider is CircleCollider2D circle)
            Gizmos.DrawWireSphere(transform.position + (Vector3)circle.offset, circle.radius);
        else if (collider is BoxCollider2D box)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
    }
}

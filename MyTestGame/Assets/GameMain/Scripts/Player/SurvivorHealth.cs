using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家生命与当前小关卡死亡循环。
/// 丧尸通过 SendMessage("TakeDamage", damage) 调用此组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class SurvivorHealth : MonoBehaviour
{
    [Header("生命")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invulnerabilityDuration = 0.75f;
    [SerializeField] private float hitFlashDuration = 0.12f;

    [Header("死亡循环")]
    [SerializeField] private int maxDeathsPerLevel = 3;
    [SerializeField] private float respawnDelay = 0.8f;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool showGameOverOverlay = true;

    [Header("战斗飘字")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Color damageTextColor = new Color(1f, 0.35f, 0.28f, 1f);

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public int DeathCount { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsGameOver { get; private set; }
    public Vector3 LevelSpawnPosition { get; private set; }

    public event Action<int, int> HealthChanged;
    public event Action<int> PlayerDied;
    public event Action GameOver;

    private SurvivorMovement movement;
    private Rigidbody2D body;
    private Collider2D hitCollider;
    private SpriteRenderer spriteRenderer;
    private ZombieSpawner zombieSpawner;
    private Color baseColor;
    private float invulnerableUntil;
    private Coroutine deathRoutine;
    private bool singleLifeRun;

    public void ConfigureForRun(int maximum, int current)
    {
        singleLifeRun = true;
        showGameOverOverlay = false;
        maxHealth = Mathf.Max(1, maximum);
        int previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Clamp(current, 1, maxHealth);
        // Upgrade healing counts; initial room binding is excluded by the run phase.
        if (RunState.Instance != null && RunState.Instance.Phase == RunState.RunPhase.ChoosingUpgrade)
            RunState.Instance.RecordHealing(CurrentHealth - previousHealth);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead || IsGameOver) return;
        int restored = Mathf.Min(amount, maxHealth - CurrentHealth);
        CurrentHealth += restored;
        if (RunState.Instance != null) RunState.Instance.RecordHealing(restored);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        maxDeathsPerLevel = Mathf.Max(1, maxDeathsPerLevel);
        CurrentHealth = maxHealth;

        movement = GetComponent<SurvivorMovement>();
        body = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;

        LevelSpawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        zombieSpawner = FindFirstObjectByType<ZombieSpawner>();
    }

    private void Start()
    {
        // 场景中的出生点可能在 Awake 之后由其他对象绑定，启动时再解析一次。
        if (spawnPoint == null)
        {
            GameObject point = GameObject.Find("PlayerSpawnPoint");
            if (point != null)
            {
                spawnPoint = point.transform;
                LevelSpawnPosition = spawnPoint.position;
            }
        }

        if (zombieSpawner == null)
            zombieSpawner = FindFirstObjectByType<ZombieSpawner>();

        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// 供丧尸、陷阱和后续武器系统调用。
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector2.zero, 0f);
    }

    public void TakeDamage(int damage, Vector2 hitDirection, float knockbackForce)
    {
        if (damage <= 0 || IsDead || IsGameOver || Time.timeScale <= 0f || Time.time < invulnerableUntil)
            return;

        // 应用减伤
        var combatEffects = RunState.Instance != null ? RunState.Instance.GetComponent<RunCombatEffects>() : null;
        if (combatEffects != null)
        {
            float reduction = combatEffects.GetDamageReduction();
            damage = Mathf.RoundToInt(damage * (1f - reduction));
        }

        if (knockbackForce > 0.01f)
        {
            Vector2 direction = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector2.down;
            if (movement != null)
                movement.ApplyKnockback(direction * knockbackForce);
            else if (body != null)
                body.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
        }

        int actualDamage = Mathf.Min(CurrentHealth, damage);
        CurrentHealth -= actualDamage;
        if (RunState.Instance != null) RunState.Instance.RecordDamageTaken(actualDamage);
        FloatingCombatText.Spawn(damageTextPrefab, transform.position + Vector3.up * 0.58f, "-" + damage.ToString(), damageTextColor);
        invulnerableUntil = Time.time + Mathf.Max(0f, invulnerabilityDuration);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (hitFlashDuration > 0f)
            StartCoroutine(HitFlash());

        if (CurrentHealth == 0 && deathRoutine == null)
            deathRoutine = StartCoroutine(HandleDeath());
    }

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        if (spawnPoint != null)
            LevelSpawnPosition = spawnPoint.position;
    }

    public void SetSpawnPosition(Vector3 position)
    {
        LevelSpawnPosition = position;
        spawnPoint = null;
    }

    private IEnumerator HandleDeath()
    {
        IsDead = true;
        DeathCount++;
        PlayerDied?.Invoke(DeathCount);

        // 炼狱步伐：死亡时点燃周围敌人
        var combatEffects = RunState.Instance != null ? RunState.Instance.CombatEffects : null;
        if (combatEffects != null)
            combatEffects.OnPlayerDeath(transform.position);

        if (movement != null)
            movement.enabled = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
        if (hitCollider != null)
            hitCollider.enabled = false;
        if (zombieSpawner != null)
            zombieSpawner.SetSpawningEnabled(false);

        if (spriteRenderer != null)
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);

        if (singleLifeRun)
        {
            EnterGameOver();
            deathRoutine = null;
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));

        if (DeathCount >= maxDeathsPerLevel)
        {
            EnterGameOver();
            deathRoutine = null;
            yield break;
        }

        RestartCurrentLevelAttempt();
        deathRoutine = null;
    }

    private void RestartCurrentLevelAttempt()
    {
        transform.position = LevelSpawnPosition;
        if (body != null)
        {
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
        }
        if (movement != null)
            movement.ClearKnockback();
        if (hitCollider != null)
            hitCollider.enabled = true;

        CurrentHealth = maxHealth;
        IsDead = false;
        invulnerableUntil = Time.time + Mathf.Max(0f, invulnerabilityDuration);
        if (spriteRenderer != null)
            spriteRenderer.color = baseColor;
        if (movement != null)
            movement.enabled = true;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (zombieSpawner != null)
        {
            zombieSpawner.ResetForLevelRestart();
            zombieSpawner.SetSpawningEnabled(true);
        }
    }

    private void EnterGameOver()
    {
        IsGameOver = true;
        IsDead = true;
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.18f);
        GameOver?.Invoke();
        // 让场景级控制器或 UI 可以选择接管 Game Over 表现。
        SendMessage("OnGameOver", SendMessageOptions.DontRequireReceiver);
    }

    private IEnumerator HitFlash()
    {
        if (spriteRenderer == null)
            yield break;

        spriteRenderer.color = new Color(1f, 0.38f, 0.38f, baseColor.a);
        yield return new WaitForSeconds(hitFlashDuration);
        if (!IsGameOver && !IsDead)
            spriteRenderer.color = baseColor;
    }

    private void OnGUI()
    {
        if (!showGameOverOverlay || !IsGameOver)
            return;

        const float width = 360f;
        const float height = 130f;
        Rect box = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(box, string.Empty);
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle detail = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            normal = { textColor = new Color(1f, 0.75f, 0.75f) }
        };
        GUI.Label(new Rect(box.x, box.y + 12f, box.width, 48f), "GAME OVER", title);
        GUI.Label(new Rect(box.x, box.y + 66f, box.width, 30f), "本小关卡已失败  ·  死亡 3 次", detail);
    }
}

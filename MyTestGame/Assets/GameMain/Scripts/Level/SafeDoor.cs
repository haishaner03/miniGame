using UnityEngine;

/// <summary>
/// 关卡安全门。出生门负责记录重生位置，出口门只有在当前关卡条件完成后才允许进入。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SafeDoor : MonoBehaviour
{
    public enum DoorRole
    {
        Start,
        Exit
    }

    [SerializeField] private DoorRole role = DoorRole.Exit;
    [SerializeField] private LevelFlowController flowController;
    [SerializeField] private bool locked = true;
    [SerializeField] private Color lockedColor = new Color(1f, 0.35f, 0.25f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.35f, 1f, 0.55f, 1f);

    private Collider2D doorCollider;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;

    public DoorRole Role => role;
    public bool IsLocked => locked;

    private void Awake()
    {
        // 兼容场景中原有的门标记：未手动配置枚举时按名称推断出生门。
        if (role == DoorRole.Exit && name.IndexOf("PlayerSpawn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            role = DoorRole.Start;

        doorCollider = GetComponent<Collider2D>();
        doorCollider.isTrigger = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;
    }

    private void Start()
    {
        if (flowController == null)
            flowController = FindFirstObjectByType<LevelFlowController>();

        if (role == DoorRole.Start)
            SetLocked(false);
        else
            SetLocked(locked);
    }

    public void Initialize(LevelFlowController controller, DoorRole doorRole)
    {
        flowController = controller;
        role = doorRole;
        SetLocked(doorRole == DoorRole.Exit && locked);
    }

    public void SetLocked(bool value)
    {
        locked = value;
        if (spriteRenderer == null)
            return;

        Color stateColor = locked ? lockedColor : unlockedColor;
        stateColor.a = baseColor.a;
        spriteRenderer.color = Color.Lerp(baseColor, stateColor, 0.6f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        SurvivorHealth player = other.GetComponentInParent<SurvivorHealth>();
        if (player == null || flowController == null)
            return;

        if (role == DoorRole.Start)
            flowController.HandleStartDoorReached(this, player);
        else
            flowController.HandleExitDoorReached(this, player);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (role != DoorRole.Exit || locked || flowController == null) return;
        SurvivorHealth player = other.GetComponentInParent<SurvivorHealth>();
        if (player != null && !player.IsDead)
            flowController.HandleExitDoorReached(this, player);
    }
}

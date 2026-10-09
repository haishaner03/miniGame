using UnityEngine;

public static class ZombieHudTheme
{
    public static readonly Vector2 ReferenceSize = new Vector2(1280f, 720f);
    public static readonly Color Panel = new Color(0.06f, 0.085f, 0.075f, 0.88f);
    public static readonly Color Border = new Color(0.35f, 0.39f, 0.31f, 0.85f);
    public static readonly Color Text = new Color(0.95f, 0.91f, 0.79f);
    public static readonly Color Muted = new Color(0.62f, 0.69f, 0.62f);
    public static readonly Color Accent = new Color(0.53f, 0.83f, 0.43f);
    public static readonly Color Critical = new Color(1f, 0.28f, 0.24f);
    public const float Inset = 32f;
    public const float Gap = 8f;
    public const float UpgradeSize = 56f;
    public const float SkillSize = 76f;
    public const int Small = 15;
    public const int Body = 18;
    public const int Heading = 22;
    public const int Title = 30;
}

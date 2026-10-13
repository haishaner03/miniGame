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

    // Battle-only metrics use the existing 1280x720 reference canvas.
    public static class Battle
    {
        public const float Edge = 16f, Padding = 12f, Gap = 8f;
        public static readonly Vector2 PlayerSize = new Vector2(268f, 124f);
        public static readonly Vector2 ObjectiveSize = new Vector2(300f, 92f);
        public static readonly Vector2 StatisticsSize = new Vector2(186f, 82f);
        public static readonly Vector2 EncounterSize = new Vector2(480f, 76f);
        public static readonly Vector2 SkillSize = new Vector2(72f, 90f);
        public const float SkillIcon = 48f, SkillRing = 60f, SkillCaption = 20f;
        public const float Slot = 46f, SlotStep = Slot + Gap;
        public const float UpgradeHeight = 88f, UpgradeMinWidth = 330f, UpgradeMaxWidth = 500f;
        public const float UpgradeHeader = 28f;
        public const int Small = 13, Body = 16, Heading = 20;
        public static readonly Color Brass = new Color(.72f, .59f, .35f);
        public static readonly Color Muted = new Color(.76f, .75f, .64f);
        public static readonly Color Health = new Color(.86f, .30f, .22f);
        public static readonly Color Experience = new Color(.26f, .73f, .51f);
        public static readonly Color Ready = new Color(.70f, .83f, .34f);
        public static readonly Color Track = new Color(.18f, .12f, .11f);
    }

    public static class UpgradeDialog
    {
        public static readonly Vector2 Size = new Vector2(760f, 600f);
        public static readonly Vector2 CardSize = new Vector2(696f, 86f);
        public static readonly Vector2 FirstCardPosition = new Vector2(32f, -154f);
        public const float CardStep = 102f;
        public const int ShopProductCount = 3;
    }
}

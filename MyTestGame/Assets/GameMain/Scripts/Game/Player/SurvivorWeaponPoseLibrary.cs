using System;
using UnityEngine;

public enum SurvivorView { Down, Up, Right, Left }

/// <summary>Pixel-space hand anchors for the actual body sprite, including mirrored frames.</summary>
[CreateAssetMenu(menuName = "Zombie/Survivor Weapon Poses")]
public sealed class SurvivorWeaponPoseLibrary : ScriptableObject
{
    [Serializable]
    public struct Pose
    {
        public Sprite sprite;
        public SurvivorView view;
        public Vector2 gripPixel;
        public float weaponAngle;
    }

    public Pose[] poses = new Pose[0];
    public Pose[] attackDown = new Pose[0];
    public Pose[] attackUp = new Pose[0];
    public Pose[] attackRight = new Pose[0];
    public Pose[] attackLeft = new Pose[0];
    public Sprite[] bowAttackDown = new Sprite[0];
    public Sprite[] bowAttackUp = new Sprite[0];
    public Sprite[] bowAttackRight = new Sprite[0];
    public Sprite[] bowAttackLeft = new Sprite[0];

    public Sprite[] BowAttack(SurvivorView view)
    {
        switch (view)
        {
            case SurvivorView.Up: return bowAttackUp;
            case SurvivorView.Right: return bowAttackRight;
            case SurvivorView.Left: return bowAttackLeft;
            default: return bowAttackDown;
        }
    }

    public Pose[] Attack(SurvivorView view)
    {
        switch (view)
        {
            case SurvivorView.Up: return attackUp;
            case SurvivorView.Right: return attackRight;
            case SurvivorView.Left: return attackLeft;
            default: return attackDown;
        }
    }

    public static SurvivorView View(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x < 0f ? SurvivorView.Left : SurvivorView.Right;
        return direction.y > 0f ? SurvivorView.Up : SurvivorView.Down;
    }
}

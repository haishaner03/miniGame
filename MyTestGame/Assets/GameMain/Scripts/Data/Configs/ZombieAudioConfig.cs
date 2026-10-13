using System;
using UnityEngine;

public enum ZombieAudioCue
{
    Button, MeleeSwing, HeavySwing, BowShot, ZombieHit, ChampionHit,
    EliteDeath, BossDeath, PlayerHit,
    IronBarSwing, HammerSwing, HitImpact
}

[CreateAssetMenu(menuName = "Zombie Survivor/Audio Config")]
public sealed class ZombieAudioConfig : ScriptableObject
{
    [Serializable]
    public sealed class Sound
    {
        public ZombieAudioCue cue;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = .6f;
        public Vector2 pitch = new Vector2(.94f, 1.06f);
        [Min(0f)] public float minimumInterval = .05f;
        [Range(1, 16)] public int maximumVoices = 3;
        [Range(0, 256)] public int priority = 100;
    }

    [Header("分类音量")]
    [Range(0f, 1f)] public float masterVolume = .8f;
    [Range(0f, 1f)] public float musicVolume = .32f;
    [Range(0f, 1f)] public float effectsVolume = .8f;
    [Range(0f, 1f)] public float uiVolume = .65f;
    [Header("声道与听觉范围")]
    [Range(8, 32)] public int effectVoiceCount = 16;
    [Min(1f)] public float audibleDistance = 16f;
    [Min(.05f)] public float musicFadeSeconds = .8f;
    [Header("循环音乐")]
    public AudioClip menuMusic;
    public AudioClip battleMusic;
    public AudioClip bossMusic;
    public Sound[] sounds;
}

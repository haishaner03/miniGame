using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent audio service. Fixed voices, shared hit limits and no per-hit objects.</summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class ZombieAudioController : MonoBehaviour
{
    public static ZombieAudioController Instance { get; private set; }
    [SerializeField] private ZombieAudioConfig config;
    public ZombieAudioConfig Config => config;
    public int VoiceCapacity => voices != null ? voices.Length : 0;
    public int ActiveVoices { get; private set; }
    public int PlayedEffects { get; private set; }
    public int DroppedEffects { get; private set; }
    public AudioClip CurrentMusic => musicTarget != null ? musicTarget.clip : null;
    public bool Muted { get; private set; }
    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float EffectsVolume { get; private set; }
    public float UiVolume { get; private set; }

    private sealed class Voice
    {
        public AudioSource source;
        public ZombieAudioCue cue;
        public int priority;
        public float gain;
        public float endTime;
        public bool occupied;
    }

    private Voice[] voices;
    private ZombieAudioConfig.Sound[] sounds;
    private float[] nextCueTime;
    private int[] lastVariant;
    private readonly HashSet<ZombieChampion> bosses = new HashSet<ZombieChampion>();
    private readonly System.Random variation = new System.Random();
    private readonly List<GameObject> roots = new List<GameObject>(64);
    private readonly List<Button> buttons = new List<Button>(128);
    private AudioSource musicA, musicB, musicTarget;
    private AudioListener fallbackListener;
    private Coroutine listenerCheck;
    private bool battleScene, combatPaused;
    private const string SettingsKey = "ZombieSurvivor.Audio.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { Ensure(); }

    public static ZombieAudioController Ensure()
    {
        if (Instance == null && Application.isPlaying)
            new GameObject("ZombieAudioController").AddComponent<ZombieAudioController>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (config == null) config = Resources.Load<ZombieAudioConfig>("ZombieAudioConfig");
        if (config == null) { Debug.LogError("Missing Resources/ZombieAudioConfig."); enabled = false; return; }
        LoadVolumes();
        sounds = new ZombieAudioConfig.Sound[System.Enum.GetValues(typeof(ZombieAudioCue)).Length];
        nextCueTime = new float[sounds.Length];
        lastVariant = new int[sounds.Length];
        for (int i = 0; i < lastVariant.Length; i++) lastVariant[i] = -1;
        if (config.sounds != null)
            foreach (var sound in config.sounds)
                if (sound != null && (int)sound.cue >= 0 && (int)sound.cue < sounds.Length)
                    sounds[(int)sound.cue] = sound;
        voices = new Voice[Mathf.Clamp(config.effectVoiceCount, 8, 32)];
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice { source = MakeSource("SFX " + i) };
        musicA = MakeSource("Music A"); musicB = MakeSource("Music B");
        musicA.loop = musicB.loop = true;
        musicA.priority = musicB.priority = 0;
        musicA.ignoreListenerPause = musicB.ignoreListenerPause = true;
        musicA.volume = musicB.volume = 0f;
        // The framework can disable its listener when a camera listener appears.
        // Keep our fallback available across loading gaps, but enable only one listener.
        fallbackListener = gameObject.AddComponent<AudioListener>();
        EnsureAudioListener();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Start()
    {
        EnsureAudioListener();
    }

    private AudioSource MakeSource(string label)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0f; source.dopplerLevel = 0f;
        return source;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (config == null) return;
        battleScene = scene.name.StartsWith("ZombieLevel", System.StringComparison.Ordinal);
        if (mode == LoadSceneMode.Single)
        {
            bosses.Clear();
            StopCombatSounds();
        }
        EnsureAudioListener();
        SetMusic(battleScene ? config.battleMusic : config.menuMusic);
        BindButtons();
        if (listenerCheck != null) StopCoroutine(listenerCheck);
        listenerCheck = StartCoroutine(EnsureAudioListenerAfterSceneLoad());
    }

    private IEnumerator EnsureAudioListenerAfterSceneLoad()
    {
        yield return null;
        EnsureAudioListener();
        BindButtons();
        listenerCheck = null;
    }

    private void EnsureAudioListener()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool sceneListenerExists = false;
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i] != fallbackListener && listeners[i].isActiveAndEnabled)
            {
                sceneListenerExists = true;
                break;
            }
        }

        fallbackListener.enabled = !sceneListenerExists;
    }

    private void Update()
    {
        bool paused = battleScene && Time.timeScale <= 0f;
        if (paused != combatPaused)
        {
            combatPaused = paused;
            foreach (var voice in voices)
            {
                if (!voice.occupied || voice.cue == ZombieAudioCue.Button) continue;
                if (paused) voice.source.Pause(); else voice.source.UnPause();
            }
        }
        ActiveVoices = 0;
        foreach (var voice in voices)
        {
            if (!voice.occupied) continue;
            bool ui = voice.cue == ZombieAudioCue.Button;
            float clock = ui ? Time.unscaledTime : Time.time;
            if (clock >= voice.endTime) { voice.source.Stop(); voice.occupied = false; continue; }
            voice.source.volume = EffectiveMaster * (ui ? UiVolume : EffectsVolume) * voice.gain;
            ActiveVoices++;
        }
        float duck = paused ? .4f : 1f;
        var run = RunState.Instance;
        if (battleScene && run != null && (run.Phase == RunState.RunPhase.Lost || run.Phase == RunState.RunPhase.Won)) duck = .3f;
        float targetVolume = EffectiveMaster * MusicVolume * duck;
        FadeMusic(musicA, musicA == musicTarget ? targetVolume : 0f);
        FadeMusic(musicB, musicB == musicTarget ? targetVolume : 0f);
    }

    private float EffectiveMaster => Muted ? 0f : MasterVolume;

    private void FadeMusic(AudioSource source, float target)
    {
        source.volume = Mathf.MoveTowards(source.volume, target,
            Time.unscaledDeltaTime / Mathf.Max(.05f, config.musicFadeSeconds));
        if (source == musicTarget) { if (!source.isPlaying && source.clip != null) source.Play(); }
        else if (source.volume <= 0f && source.isPlaying) source.Stop();
    }

    public void SetMusic(AudioClip clip)
    {
        if (musicTarget != null && musicTarget.clip == clip) return;
        AudioSource next = musicTarget == musicA ? musicB : musicA;
        next.Stop(); next.clip = clip; next.volume = 0f;
        musicTarget = next;
        if (clip != null) next.Play();
    }

    public static void SetBossActive(ZombieChampion boss, bool active)
    {
        var audio = Instance;
        if (audio == null || audio.config == null || boss == null || !boss.isBoss) return;
        if (active) audio.bosses.Add(boss); else audio.bosses.Remove(boss);
        if (audio.battleScene)
            audio.SetMusic(audio.bosses.Count > 0 && audio.config.bossMusic != null
                ? audio.config.bossMusic : audio.config.battleMusic);
    }

    public static void Play(ZombieAudioCue cue, Vector3 position = default(Vector3))
    {
        var audio = Ensure();
        if (audio != null) audio.PlayInternal(cue, position);
    }

    private void PlayInternal(ZombieAudioCue cue, Vector3 position)
    {
        int index = (int)cue;
        if (sounds == null || index < 0 || index >= sounds.Length) return;
        var sound = sounds[index];
        bool ui = cue == ZombieAudioCue.Button;
        float clock = ui ? Time.unscaledTime : Time.time;
        if (sound == null || sound.clips == null || sound.clips.Length == 0) return;
        if ((!ui && Time.timeScale <= 0f) || clock < nextCueTime[index]) { DroppedEffects++; return; }
        float distanceGain = 1f, pan = 0f;
        var player = RunState.Instance != null ? RunState.Instance.Player : null;
        if (!ui && player != null)
        {
            Vector2 offset = position - player.transform.position;
            float distance = offset.magnitude;
            if (distance > config.audibleDistance) { DroppedEffects++; return; }
            distanceGain = 1f - Mathf.InverseLerp(3f, config.audibleDistance, distance) * .85f;
            pan = Mathf.Clamp(offset.x / 12f, -.55f, .55f);
        }
        int sameCue = 0;
        Voice free = null, replace = null;
        foreach (var voice in voices)
        {
            // Honor expiry even before this controller's next Update.
            if (voice.occupied && (voice.cue == ZombieAudioCue.Button ? Time.unscaledTime : Time.time) >= voice.endTime)
                voice.occupied = false;
            if (!voice.occupied) { if (free == null) free = voice; continue; }
            if (voice.cue == cue) sameCue++;
            // Make room for an important cue by replacing the lowest-priority voice,
            // never by cutting off a more important sound.
            if (voice.priority < sound.priority && (replace == null || voice.priority < replace.priority)) replace = voice;
        }
        if (sameCue >= sound.maximumVoices || (free == null && replace == null)) { DroppedEffects++; return; }
        var selected = free ?? replace;
        int variant = variation.Next(sound.clips.Length);
        if (sound.clips.Length > 1 && variant == lastVariant[index]) variant = (variant + 1) % sound.clips.Length;
        AudioClip clip = sound.clips[variant];
        if (clip == null) return;
        lastVariant[index] = variant;
        nextCueTime[index] = clock + sound.minimumInterval;
        selected.source.Stop(); selected.source.clip = clip;
        selected.source.pitch = Mathf.Lerp(Mathf.Max(.1f, sound.pitch.x), Mathf.Max(.1f, sound.pitch.y),
            (float)variation.NextDouble());
        selected.source.panStereo = pan; selected.source.priority = sound.priority;
        selected.source.ignoreListenerPause = ui;
        selected.cue = cue; selected.priority = sound.priority;
        selected.gain = sound.volume * distanceGain;
        selected.endTime = clock + clip.length / selected.source.pitch;
        selected.occupied = true;
        selected.source.volume = EffectiveMaster * (ui ? UiVolume : EffectsVolume) * selected.gain;
        selected.source.Play(); PlayedEffects++;
    }

    private void StopCombatSounds()
    {
        if (voices == null) return;
        foreach (var voice in voices)
            if (voice.cue != ZombieAudioCue.Button) { voice.source.Stop(); voice.occupied = false; }
        for (int i = 1; i < nextCueTime.Length; i++) nextCueTime[i] = 0f;
        combatPaused = false;
    }

    // Scenes bind once; dynamically instantiated screens bind in their own initialization.
    private void BindButtons()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) BindScene(SceneManager.GetSceneAt(i));
        BindScene(gameObject.scene);
    }

    private void BindScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        roots.Clear(); scene.GetRootGameObjects(roots);
        foreach (var root in roots)
        {
            if (root == null) continue;
            buttons.Clear(); root.GetComponentsInChildren(true, buttons);
            foreach (var button in buttons) UiButtonSound.Ensure(button);
        }
    }

    public void SetVolumes(float master, float music, float effects, float ui, bool mute = false, bool persist = true)
    {
        MasterVolume = Mathf.Clamp01(master); MusicVolume = Mathf.Clamp01(music);
        EffectsVolume = Mathf.Clamp01(effects); UiVolume = Mathf.Clamp01(ui); Muted = mute;
        if (persist) SaveVolumes();
    }

    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat(SettingsKey + "Master", MasterVolume);
        PlayerPrefs.SetFloat(SettingsKey + "Music", MusicVolume);
        PlayerPrefs.SetFloat(SettingsKey + "Effects", EffectsVolume);
        PlayerPrefs.SetFloat(SettingsKey + "UI", UiVolume);
        PlayerPrefs.SetInt(SettingsKey + "Mute", Muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadVolumes()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsKey + "Master", config.masterVolume));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsKey + "Music", config.musicVolume));
        EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsKey + "Effects", config.effectsVolume));
        UiVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsKey + "UI", config.uiVolume));
        Muted = PlayerPrefs.GetInt(SettingsKey + "Mute", 0) != 0;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
}

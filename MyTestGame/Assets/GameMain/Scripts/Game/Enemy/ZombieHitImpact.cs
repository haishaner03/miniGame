using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ZombieHitImpact : MonoBehaviour
{
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    [SerializeField] private Sprite[] variant01;
    [SerializeField] private Sprite[] variant02;
    [SerializeField] private Sprite[] variant03;
    [SerializeField] private Sprite[] variant04;

    private SpriteRenderer spriteRenderer;
    private Sprite[][] variants;
    private Action<ZombieHitImpact> release;
    private Sprite[] frames;
    private int frameIndex;
    private float nextFrameTime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        variants = new[] { variant01, variant02, variant03, variant04 };
    }

    public void SetReleaseCallback(Action<ZombieHitImpact> callback)
    {
        release = callback;
    }

    public void Play(Vector3 position, Vector2 direction, int variant, float scale, float rotationOffset)
    {
        if (variants == null) variants = new[] { variant01, variant02, variant03, variant04 };
        frames = variants[Mathf.Clamp(variant, 0, variants.Length - 1)];
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
        {
            Release();
            return;
        }

        float angle = direction.sqrMagnitude > 0.001f ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle + rotationOffset));
        transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        frameIndex = 0;
        spriteRenderer.sprite = frames[frameIndex];
        nextFrameTime = Time.time + 1f / Mathf.Max(1f, framesPerSecond);
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (Time.time < nextFrameTime) return;
        frameIndex++;
        if (frames == null || frameIndex >= frames.Length)
        {
            Release();
            return;
        }

        spriteRenderer.sprite = frames[frameIndex];
        nextFrameTime = Time.time + 1f / Mathf.Max(1f, framesPerSecond);
    }

    public void StopAndRelease()
    {
        Release();
    }

    private void Release()
    {
        frames = null;
        if (release != null) release(this);
        else gameObject.SetActive(false);
    }
}

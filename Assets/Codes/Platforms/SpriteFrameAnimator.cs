using UnityEngine;

/// <summary>
/// Plays sprite frames on a SpriteRenderer without an Animator: a looping idle,
/// plus one-shot animations (e.g. lava flaring up) that return to the idle when done.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private Sprite[] loopFrames;
    private float loopFps;

    private Sprite[] frames;
    private float fps;
    private bool looping;
    private float time;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void PlayLoop(Sprite[] newFrames, float framesPerSecond)
    {
        loopFrames = newFrames;
        loopFps = framesPerSecond;
        Play(newFrames, framesPerSecond, true);
    }

    /// <summary>Plays once, then goes back to the loop set with PlayLoop.</summary>
    public void PlayOnce(Sprite[] newFrames, float framesPerSecond)
    {
        if (newFrames == null || newFrames.Length == 0) return;
        Play(newFrames, framesPerSecond, false);
    }

    private void Play(Sprite[] newFrames, float framesPerSecond, bool loop)
    {
        frames = newFrames;
        fps = Mathf.Max(0.01f, framesPerSecond);
        looping = loop;
        time = 0f;
        ShowFrame(0);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        time += Time.deltaTime;
        int index = Mathf.FloorToInt(time * fps);

        if (index >= frames.Length)
        {
            if (looping)
            {
                index %= frames.Length;
            }
            else
            {
                // One-shot finished: back to the idle loop
                if (loopFrames != null && loopFrames.Length > 0) Play(loopFrames, loopFps, true);
                else frames = null;
                return;
            }
        }

        ShowFrame(index);
    }

    private void ShowFrame(int index)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames != null && index < frames.Length && frames[index] != null)
            spriteRenderer.sprite = frames[index];
    }
}

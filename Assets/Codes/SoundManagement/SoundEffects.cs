using UnityEngine;

[CreateAssetMenu(fileName = "SoundEffects", menuName = "Audio/Sound Effects")]
public class SoundEffects : ScriptableObject
{
    [Header("Combat Sounds")]
    public AudioClip[] metalHit;
    public AudioClip[] whoosh;
    public AudioClip[] punch;
    
    [Header("Enemy Sounds")]
    public AudioClip[] devilHit;
    
    [Header("Environment Sounds")]
    public AudioClip[] wallBreak;
    public AudioClip[] rotation;
    public AudioClip[] damageTaken;
    public AudioClip[] groundHit;
    public AudioClip[] superJumpCharge;
    public AudioClip[] superJump;
    
    // Convenience methods
    public void PlayMetalHit() => AudioManager.Instance?.PlayRandomClip(metalHit);
    public void PlayWhoosh() => AudioManager.Instance?.PlayRandomClip(whoosh);
    public void PlayPunch() => AudioManager.Instance?.PlayRandomClip(punch);
    public void PlayDevilHit() => AudioManager.Instance?.PlayRandomClip(devilHit);
    public void PlayWallBreak() => AudioManager.Instance?.PlayRandomClip(wallBreak);
    public void PlayRotation() => AudioManager.Instance?.PlayRandomClip(rotation);
    public void PlayDamageTaken() => AudioManager.Instance?.PlayRandomClip(damageTaken);
    public void PlayGroundHit() => AudioManager.Instance?.PlayRandomClip(groundHit);
    public void PlaySuperJumpCharge() => AudioManager.Instance?.PlayRandomClip(superJumpCharge);
    public void PlaySuperJump() => AudioManager.Instance?.PlayRandomClip(superJump);
}
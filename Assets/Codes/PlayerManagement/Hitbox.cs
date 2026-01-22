using UnityEngine;

public class Hitbox : MonoBehaviour
{
    private Movement playerMovement;
    private ParticleSystem punchParticles;

    void Start()
    {
        playerMovement = GetComponentInParent<Movement>();

        // ✅ Get punch particles from player
        if (playerMovement != null)
            punchParticles = playerMovement.punchParticles;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Wall") || collision.CompareTag("Enemy"))
        {
            // Notify player movement
            if (playerMovement != null)
            {
                if (gameObject.name.Contains("Punch"))
                    playerMovement.OnPunchHit();
                else if (gameObject.name.Contains("Uppercut"))
                    playerMovement.OnUppercutHit();
            }

            // ✅ Play punch particles at hit point
            if (punchParticles != null)
            {
                punchParticles.transform.position = collision.bounds.center;
                punchParticles.Play();
            }

            Debug.Log($"{collision.tag} hit by {gameObject.name}!");
        }
        else if (collision.CompareTag("Sign"))
        {
            Debug.Log($"Sign hit by {gameObject.name}!");
            if (punchParticles != null)
            {
                punchParticles.transform.position = collision.bounds.center;
                punchParticles.Play();
            }
        }
    }
}
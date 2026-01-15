using UnityEngine;

public class Hitbox : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the collided object has the "Wall" or "Enemy" tag
        if (collision.CompareTag("Wall") || collision.CompareTag("Enemy"))
        {
            Debug.Log($"{collision.tag} destroyed by hitbox!");
        }
    }
}

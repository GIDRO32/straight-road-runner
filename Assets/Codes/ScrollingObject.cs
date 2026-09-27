// ScrollingObject.cs – NEW VERSION (physics-based)
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ScrollingObject : MonoBehaviour
{
    private Rigidbody2D rb;
    private float speed;
    private float destroyX;

    public void Init(float scrollSpeed, float destroyPositionX)
    {
        speed = scrollSpeed;
        destroyX = destroyPositionX;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void FixedUpdate()
    {
        rb.velocity = Vector2.right * speed;

        // Destroy when past threshold (direction-aware)
        bool shouldDestroy = (speed > 0 && transform.position.x >= destroyX) ||
                             (speed < 0 && transform.position.x <= destroyX);

        if (shouldDestroy)
            Destroy(gameObject);
    }
}
using UnityEngine;

public class BallDevil : MonoBehaviour
{
    public Transform player;
    public float accelX = 5f;
    public float accelY = 3f;
    public float maxSpeedX = 10f;
    public float maxSpeedY = 5f;
    public float dashInterval = 3f;
    public float dashSpeed = 20f;

    private Rigidbody2D rb;
    private float dashTimer = 0f;
    private bool isFacingRight = true;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isDamaged = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>(); // Add this line
        player = GameObject.FindWithTag("Player").transform;
    }

    void FixedUpdate()
    {
        if (player == null) return;


        // Update animator parameters
        animator.SetFloat("SpeedX", Mathf.Abs(rb.velocity.x));
        animator.SetFloat("SpeedY", rb.velocity.y);
        animator.SetBool("IsDashing", false); // Reset each frame

        if (dashTimer >= dashInterval)
        {
            Dash();
            dashTimer = 0f;
            animator.SetBool("IsDashing", true); // Trigger dash animation
        }
        else
        {
            SeekPlayer();
        }
    }

    private void SeekPlayer()
    {
        if (player == null) return;

        Vector2 dir = (player.position - transform.position).normalized;
        float signX = Mathf.Sign(dir.x);
        float signY = Mathf.Sign(dir.y);

        // Flip sprite based on X direction
        if ((signX > 0 && !isFacingRight) || (signX < 0 && isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 ls = transform.localScale;
            ls.x *= -1f;
            transform.localScale = ls;
        }

        // Accelerate X
        float targetVx = signX * maxSpeedX;
        float deltaVx = targetVx - rb.velocity.x;
        float accelThisX = Mathf.Clamp(deltaVx, -accelX * Time.fixedDeltaTime, accelX * Time.fixedDeltaTime);
        rb.velocity = new Vector2(rb.velocity.x + accelThisX, rb.velocity.y);

        // Accelerate Y
        float targetVy = signY * maxSpeedY;
        float deltaVy = targetVy - rb.velocity.y;
        float accelThisY = Mathf.Clamp(deltaVy, -accelY * Time.fixedDeltaTime, accelY * Time.fixedDeltaTime);
        rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y + accelThisY);
    }
    // === NEW METHOD ===
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isDamaged && collision.CompareTag("Hits"))
        {
            isDamaged = true;
            // animator.SetTrigger("Damaged");  // Play damaged animation
            float punchForce = 5f;
            Vector2 force = new Vector2(
                Random.Range(-punchForce, punchForce),
                Random.Range(0f, punchForce * 1.5f)
            );
            rb.AddForce(force, ForceMode2D.Impulse);
            // Start fade coroutine
            StartCoroutine(FadeAndDestroy());
        }
    }
    // === NEW COROUTINE ===
    private System.Collections.IEnumerator FadeAndDestroy()
    {
        // Fade out over 1 second
        float fadeDuration = 1f;
        float timer = 0f;
        Color originalColor = spriteRenderer.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        // Ensure fully invisible
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        // Physics still work during fade, then destroy
        Destroy(gameObject);
    }
    private void Dash()
    {
        if (player == null) return;

        Vector2 dir = (player.position - transform.position).normalized;
        rb.velocity = dir * dashSpeed;
    }
}
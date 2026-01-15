using UnityEngine;

public class BallDevil : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    
    [Header("Seek Settings")]
    public float accelX = 5f;
    public float accelY = 3f;
    public float maxSpeedX = 10f;
    public float maxSpeedY = 5f;
    
    [Header("Dash Settings")]
    public float dashInterval = 3f;
    public float dashSpeed = 20f;
    public float dashDuration = 0.5f; // ← NEW: How long dash lasts

    private Rigidbody2D rb;
    private float dashTimer = 0f;
    private float dashDurationTimer = 0f; // ← NEW
    private bool isFacingRight = true;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isDamaged = false;
    private bool isDashing = false; // ← NEW: Track dash state

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        
        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    void FixedUpdate()
    {
        if (player == null || isDamaged) return;

        // ✅ INCREMENT TIMER!
        dashTimer += Time.fixedDeltaTime;

        // Handle dash state
        if (isDashing)
        {
            dashDurationTimer -= Time.fixedDeltaTime;
            
            if (dashDurationTimer <= 0f)
            {
                // Dash finished
                isDashing = false;
                animator.SetBool("IsDashing", false);
            }
            // Don't seek while dashing - let dash velocity continue
            return;
        }

        // Check if it's time to dash
        if (dashTimer >= dashInterval)
        {
            StartDash();
            dashTimer = 0f; // Reset interval timer
        }
        else
        {
            // Normal seeking behavior
            SeekPlayer();
        }

        // Update animator parameters
        animator.SetFloat("SpeedX", Mathf.Abs(rb.velocity.x));
        animator.SetFloat("SpeedY", rb.velocity.y);
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

    private void StartDash()
    {
        if (player == null) return;

        // Calculate dash direction
        Vector2 dir = (player.position - transform.position).normalized;
        
        // Apply dash velocity
        rb.velocity = dir * dashSpeed;
        
        // Set dash state
        isDashing = true;
        dashDurationTimer = dashDuration;
        animator.SetBool("IsDashing", true);
        
        Debug.Log("BallDevil dashing!");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isDamaged && collision.CompareTag("Hits"))
        {
            isDamaged = true;
            animator.SetBool("IsDamaged", true);
            
            // Apply knockback force
            float punchForce = 5f;
            Vector2 force = new Vector2(
                Random.Range(-punchForce, punchForce),
                Random.Range(0f, punchForce * 1.5f)
            );
            rb.AddForce(force, ForceMode2D.Impulse);
            
            // Award score for killing enemy
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddEnemyKillScore();
            }
            
            // Start fade coroutine
            StartCoroutine(FadeAndDestroy());
        }
    }

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

        // Destroy
        Destroy(gameObject);
    }
}
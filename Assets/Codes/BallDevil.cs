using UnityEngine;

public class BallDevil : EnemyBase
{
    [Header("Seek Settings")]
    public float accelX = 5f;
    public float accelY = 3f;
    public float maxSpeedX = 10f;
    public float maxSpeedY = 5f;

    [Header("Dash Settings")]
    public float dashInterval = 3f;
    public float dashSpeed = 20f;
    public float dashDuration = 0.5f; // How long dash lasts

    private float dashTimer = 0f;
    private float dashDurationTimer = 0f;
    private bool isFacingRight = true;
    private bool isDashing = false;

    void FixedUpdate()
    {
        if (player == null || isDefeated) return;

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
        PlayAttackSound();
    }
}

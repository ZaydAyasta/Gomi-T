using UnityEngine;
using System.Collections;

public class ArmLauncher : MonoBehaviour
{
    [Header("Proyectil")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 23f;
    public float maxLifetime = 0.5f;

    [Header("Brazo visual")]
    public GameObject armStretchPrefab;
    private GameObject currentArmStretch;

    private GameObject currentProjectile;
    private bool lanzandoBr = false;
    private float timer = 0f;

    [Header("Cooldown")]
    public float cooldownTime = 0.07f;
    private bool canShoot = true;

    private PlayerMovement player;
    private SpringJoint2D springJoint;
    private Vector2 lastFireDirection;

    public bool IsFiring => lanzandoBr;
    public bool IsHoldingBack { get; private set; } = false;

    public float maxGrappleDistance = 10f;
    public float minDistanceFactor = 0.2f;

    void Start()
    {
        player = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && !lanzandoBr && canShoot)
        {
            Fire();
        }

        if (springJoint != null && currentProjectile)
        {
            float currentDist = Vector2.Distance(transform.position, currentProjectile.transform.position);
            springJoint.distance = Mathf.Min(currentDist, maxGrappleDistance);
        }

        if (lanzandoBr)
        {
            timer += Time.deltaTime;

            if (currentProjectile && currentArmStretch)
            {
                Vector3 start = transform.position;
                Vector3 end = currentProjectile.transform.position;

                if (player != null && player.anim.GetBool("Down"))
                {
                    start += Vector3.down * 0.5f;
                }

                Vector3 mid = (start + end) / 2f;
                currentArmStretch.transform.position = mid;

                Vector3 dir = end - start;
                float dist = dir.magnitude;

                currentArmStretch.transform.right = dir.normalized;
                currentArmStretch.transform.localScale = new Vector3(dist, 0.2f, 1f);
            }

            if (timer >= maxLifetime && currentProjectile && springJoint == null)
            {
                StartCoroutine(ReturnProjectile());
            }
        }

        // Verificar si el jugador está tirando hacia atras, pa q se impulse
        if (springJoint != null && currentProjectile != null)
        {
            Vector2 anchorDir = (currentProjectile.transform.position - transform.position).normalized;
            Vector2 playerVel = player.Rb.linearVelocity;

            float dot = Vector2.Dot(playerVel.normalized, anchorDir);
            IsHoldingBack = dot < -0.2f;
        }
        else
        {
            IsHoldingBack = false;
        }

        if (Input.GetKeyUp(KeyCode.Q) && lanzandoBr)
        {
            StartCoroutine(ReturnProjectile());
        }
    }

    public Transform CurrentAnchor
    {
        get
        {
            if (currentProjectile != null)
                return currentProjectile.transform;
            return null;
        }
    }

    public void AttachAnchor(Rigidbody2D anchorRb)
    {
        if (springJoint == null)
        {
            springJoint = gameObject.AddComponent<SpringJoint2D>();
            springJoint.autoConfigureDistance = false;
            springJoint.connectedBody = anchorRb;

            float initialDist = Vector2.Distance(transform.position, anchorRb.position);
            springJoint.distance = Mathf.Min(initialDist, maxGrappleDistance);

            springJoint.frequency = 2.5f;
            springJoint.dampingRatio = 0.3f;
        }
    }

    void Fire()
    {
        lanzandoBr = true;
        timer = 0f;

        currentProjectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);

        Rigidbody2D rb = currentProjectile.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Vector2 dir;
        if (player.anim.GetBool("Down"))
        {
            dir = player.sr.flipX ? new Vector2(-1, 1) : new Vector2(1, 1);
            dir.Normalize();
        }
        else
        {
            dir = player.sr.flipX ? Vector2.left : Vector2.right;
        }

        lastFireDirection = dir.normalized;
        rb.linearVelocity = dir * projectileSpeed;
        currentArmStretch = Instantiate(armStretchPrefab);
    }

    private IEnumerator ReturnProjectile()
    {
        lanzandoBr = false;
        canShoot = false;

        Vector3 anchorPos = (currentProjectile != null) ? currentProjectile.transform.position : transform.position;

        // Usa la dirección de disparo original para mantener la inclinación
        Vector2 impulseDir = lastFireDirection != Vector2.zero ? lastFireDirection : (anchorPos - transform.position).normalized;

        if (springJoint != null)
        {
            Destroy(springJoint);
            springJoint = null;
        }

        Rigidbody2D playerRb = player.Rb;
        if (playerRb != null && impulseDir != Vector2.zero)
        {
            bool isMovingAway = false;
            Vector2 playerVel = playerRb.linearVelocity;

            if (playerVel.magnitude > 0.1f)
            {
                float dot = Vector2.Dot(playerVel.normalized, impulseDir);
                if (dot < -0.2f) isMovingAway = true;
            }
            else
            {
                float moveInput = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(moveInput) > 0.1f)
                {
                    if (impulseDir.x * moveInput < -0.1f) isMovingAway = true;
                }
            }

            if (isMovingAway)
            {
                float distance = Vector2.Distance(transform.position, anchorPos);

                float baseForce = 3f;
                float hookImpulseForce = baseForce + distance * 2.5f;

                float backSpeed = Mathf.Max(0f, -Vector2.Dot(playerVel, impulseDir));
                hookImpulseForce += backSpeed * 12f;

                hookImpulseForce = Mathf.Clamp(hookImpulseForce, 0f, 40f); // para limitar hasta cuant ose puede impulsar, para q no vuele

                Vector2 impulseDirNormalized = impulseDir.normalized;
                Vector2 finalImpulse = impulseDirNormalized * hookImpulseForce;

                if (impulseDirNormalized.y > 0.05f)
                {
                    finalImpulse.y += impulseDirNormalized.y * 3.5f;
                }

                playerRb.AddForce(finalImpulse, ForceMode2D.Impulse);
            }
        }

        if (currentProjectile != null)
        {
            Vector3 start = anchorPos;
            Vector3 end = transform.position;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float progress = t / 0.2f;
                if (currentProjectile) currentProjectile.transform.position = Vector3.Lerp(start, end, progress);
                yield return null;
            }
        }

        if (currentProjectile) Destroy(currentProjectile);
        if (currentArmStretch) Destroy(currentArmStretch);

        currentProjectile = null;
        currentArmStretch = null;

        yield return new WaitForSeconds(cooldownTime);
        canShoot = true;
    }
}

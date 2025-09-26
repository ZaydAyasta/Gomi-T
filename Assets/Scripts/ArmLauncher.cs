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

    //public bool IsHoldingBack => retrocediandoAtras;

    public bool IsFiring => lanzandoBr; //honestamente ni siquiera se que es esto

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

    public bool IsHoldingBack { get; private set; } = false;

    public Transform CurrentAnchor
    {
        get
        {
            if (currentProjectile != null)
                return currentProjectile.transform;
            return null;
        }
    }


    public float maxGrappleDistance = 10f;
    public float minDistanceFactor = 0.2f;

    public void AttachAnchor(Rigidbody2D anchorRb)
    {
        if (springJoint == null)
        {
            springJoint = gameObject.AddComponent<SpringJoint2D>();
            springJoint.autoConfigureDistance = false;

            springJoint.connectedBody = anchorRb;

            float initialDist = Vector2.Distance(transform.position, anchorRb.position);
            springJoint.distance = Mathf.Min(initialDist, maxGrappleDistance);

            // Fuerza del resorte
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

        //direccion del resorte, tmb si te agachas
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

        rb.linearVelocity = dir * projectileSpeed;

        currentArmStretch = Instantiate(armStretchPrefab);
    }

    private System.Collections.IEnumerator ReturnProjectile()
    {
        lanzandoBr = false;
        canShoot = false;

        Vector3 anchorPos = (currentProjectile != null) ? currentProjectile.transform.position : transform.position;
        Vector2 impulseDir = Vector2.zero;
        if ((anchorPos - transform.position).sqrMagnitude > 0.0001f)
            impulseDir = (anchorPos - transform.position).normalized;

        if (springJoint != null)
        {
            Destroy(springJoint);
            springJoint = null;
        }



        // olo si el jugador se estaba MOVIENDO EN CONTRA
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

                float baseForce = 8f; // más alto para que se note
                float hookImpulseForce = baseForce + distance * 2.5f;

                float backSpeed = Mathf.Max(0f, -Vector2.Dot(playerVel, impulseDir));
                hookImpulseForce += backSpeed * 12f;

                Vector2 finalImpulse = impulseDir * hookImpulseForce;
                finalImpulse.y *= 1.35f; // buff pa q se mueva mas arriba 

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

        yield return new WaitForSeconds(cooldownTime); // pa q no se spamee
        canShoot = true;
    }
}

using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float walkSpeed = 3f;
    public float jumpForce = 17f;

    private float currentSpeed;

    public float acceleration = 40f;

    private Rigidbody2D rb;
    public Rigidbody2D Rb => rb;

    [HideInInspector] public Animator anim;
    public bool isGrounded;

    [Header("Referencias")]
    public SpriteRenderer sr;

    [Header("Aire")]
    public float airAcceleration = 20f;   // menor que acceleration
    public bool preserveVXInAir = true;   // mantiene vel.x si no hay input
    public ElasticGrapple grapple;        // arrástralo en el inspector


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        if (!grapple) grapple = GetComponent<ElasticGrapple>();
        currentSpeed = walkSpeed;
    }

    void Update()
    {
        float move = Input.GetAxisRaw("Horizontal");

        bool airLocked = grapple != null && grapple.IsLaunched;   // breve lock tras disparo

        // Si estamos en aire o en lock de lanzamiento:
        if (airLocked || !isGrounded)
        {
            if (isGrounded)
            {
                
            }
            if (preserveVXInAir && Mathf.Abs(move) < 0.01f)
            {
                // deja rb.linearVelocity.x tal cual
            }
            else
            {
                float targetVelX = move * currentSpeed;
                float newVelX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelX, airAcceleration * Time.deltaTime);
                rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);
            }
        }
        else
        {
            // En suelo: suavizado normal
            float targetVelX = move * currentSpeed;
            float newVelX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelX, acceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);
        }
        ArmLauncher launcher = FindFirstObjectByType<ArmLauncher>();
        if (launcher != null && launcher.IsHoldingBack)
        {
            anim.SetBool("Hold", true);
            anim.SetBool("Running", false);

            // 👉 Flip hacia el proyectil
            if (launcher.CurrentAnchor != null)
            {
                Vector2 dir = launcher.CurrentAnchor.position - transform.position;
                if (dir.x > 0) sr.flipX = false;
                else if (dir.x < 0) sr.flipX = true;
            }
        }
        else
        {
            anim.SetBool("Hold", false);
            anim.SetFloat("Speed", Mathf.Abs(move));

            //Flip normal con input
            if (move > 0) sr.flipX = false;
            else if (move < 0) sr.flipX = true;
        }

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
            anim.SetBool("Jumping", true);
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            anim.SetBool("Running", true);
            currentSpeed = speed;
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            anim.SetBool("Running", false);
            currentSpeed = walkSpeed;
        }

        // CAÍDA
        if (rb.linearVelocity.y < -0.1f && !isGrounded)
            anim.SetBool("Falling", true);
        else
            anim.SetBool("Falling", false);

        // AGACHARSE
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            anim.SetBool("Down", true);
        else
            anim.SetBool("Down", false);
    }


    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        isGrounded = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            // Aceptamos como "suelo" superficies cuyo normal tenga un ángulo < 45° con Vector2.up
            if (Vector2.Angle(contact.normal, Vector2.up) < 45f)
            {
                isGrounded = true;
                anim.SetBool("Jumping", false);
                anim.SetBool("Falling", false);
                break;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }


}

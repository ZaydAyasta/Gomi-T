using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float jumpForce = 17f;

    public float acceleration = 40f;

    private Rigidbody2D rb;
    public Rigidbody2D Rb => rb;

    [HideInInspector] public Animator anim;
    private bool isGrounded;

    [Header("Referencias")]
    public SpriteRenderer sr;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float move = Input.GetAxisRaw("Horizontal");

        // Suavizamos la velocidad horizontal
        float targetVelX = move * speed;
        float newVelX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelX, acceleration * Time.deltaTime);
        rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);

        ArmLauncher launcher = FindFirstObjectByType<ArmLauncher>();
        if (launcher != null && launcher.IsHoldingBack)
        {
            anim.SetBool("Hold", true);
            anim.SetFloat("Speed", 0f);

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

            // 👉 Flip normal con input
            if (move > 0) sr.flipX = false;
            else if (move < 0) sr.flipX = true;
        }

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            StartCoroutine(JumpWithDelay());
            anim.SetBool("Jumping", true);
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


    private IEnumerator JumpWithDelay()
    {
        anim.SetBool("Jumping", true);
        yield return new WaitForSeconds(2f / 24f);

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

using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float jumpForce = 17f;
    private Rigidbody2D rb;

    private Animator anim;
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
        rb.linearVelocity = new Vector2(move * speed, rb.linearVelocity.y);

        anim.SetFloat("Speed", Mathf.Abs(move));

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            StartCoroutine(JumpWithDelay());
            anim.SetBool("Jumping", true);
        }

        //CAIDA?
        if (rb.linearVelocity.y < -0.1f && !isGrounded)
        {
            anim.SetBool("Falling", true);
             
        }
        else
        {
            anim.SetBool("Falling", false);
        }

        //AGACHARSE: NOTA, LUEGO QUITAR EL HARDCODE DE LA TECLA X'd
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            anim.SetBool("Down", true);
        }
        else
        {
            anim.SetBool("Down", false);
        }

        if (move > 0)
            sr.flipX = false; // mira a la derecha
        else if (move < 0)
            sr.flipX = true;  // mira a la izquierda


    }

    private IEnumerator JumpWithDelay()
    {
        // Opcional: activar animación de "pre-salto"
        anim.SetBool("Jumping", true);

        // Esperar 1/12 de segundo (~0.083s)
        yield return new WaitForSeconds(2f / 24f);

        // Ejecutar el salto
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.contacts[0].normal.y > 0.5f)
        {
            isGrounded = true;
            anim.SetBool("Jumping", false);
            anim.SetBool("Falling", false);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}

using UnityEngine;

public class Projectile : MonoBehaviour
{
    private ArmLauncher launcher;
    private Rigidbody2D rb;

    void Start()
    {
        launcher = FindFirstObjectByType<ArmLauncher>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (launcher && launcher.IsFiring)
        {
            if (collision.gameObject.CompareTag("Block")) // por cierto, todo lo q tenga tag "Block" se puede anclar
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                launcher.AttachAnchor(rb);
            }
        }
    }
}

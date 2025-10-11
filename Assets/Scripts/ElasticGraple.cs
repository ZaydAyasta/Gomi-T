using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ElasticGrapple : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMovement player;      // opcional (para flipX)
    public Transform shoulder;         // punto del hombro (si null, usa transform.position)

    [Header("Input")]
    public KeyCode aimKey = KeyCode.Mouse0;   // mantener para apuntar/enganchar; soltar = disparar
    public KeyCode altAimKey = KeyCode.Q;

    [Header("Hook")]
    public LayerMask hookMask;               // capas agarrables (NO Player)
    public float maxGrappleDistance = 12f;   // alcance

    [Header("Elastic / Launch")]
    [Range(0.2f, 1.2f)] public float restLenFactor = 1.00f; // 1 = sin tensión al enganchar
    [Range(0.0f, 1.0f)] public float minRestLenFactor = 0.25f;
    public float baseImpulse = 3f;
    public float stretchScale = 7f;          // cuánto suma por unidad de estiramiento
    public float liftIfFlat = 1.0f;          // lift extra si está casi horizontal

    [Header("Cartoon Boost")]
    public float launchMultiplier = 3.5f;   // multiplica TODO el impulso
    public float minLaunchImpulse = 12f;    // mínimo al soltar
    public float stretchPower = 1.8f;       // >1 = más exagerado
    public float stretchDeadZone = 0.02f;   // ignora estiramientos ínfimos
    public float maxImpulse = 140f;         // sube el límite

    [Header("Visual")]
    public LineRenderer ropeLR;              // cuerda
    public LineRenderer trajectoryLR;        // trayectoria
    public float ropeWidth = 0.06f;
    public int trajectoryPoints = 32;
    public float trajectoryDt = 0.05f;

    // --- estado ---
    Rigidbody2D rb;
    Camera cam;

    bool attached = false;
    Vector2 anchorWorld;   // punto fijo
    float restLen = 0f;    // largo “neutro” al enganchar
    float maxDist = 0f;    // mayor distancia alcanzada desde que me enganch
    private Vector2 _cachedLaunchDir;
    private float _cachedForceMag;

    public bool IsLaunched { get; private set; }
    public float launchLockTime = 0.15f;
    float _unlockAt = 0f;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!player) player = GetComponent<PlayerMovement>();
        cam = Camera.main;

        // autocrear LRs si faltan
        if (!ropeLR)
        {
            ropeLR = gameObject.AddComponent<LineRenderer>();
            ropeLR.material = new Material(Shader.Find("Sprites/Default"));
        }
        if (!trajectoryLR)
        {
            trajectoryLR = gameObject.AddComponent<LineRenderer>();
            trajectoryLR.material = new Material(Shader.Find("Sprites/Default"));
        }
        SetupLR(ropeLR, ropeWidth);
        SetupLR(trajectoryLR, ropeWidth * 0.8f);
        HideRope();
        HideTrajectory();
    }

    void Update()
    {
        bool hold = Input.GetKey(aimKey) || Input.GetKey(altAimKey);
        bool down = Input.GetKeyDown(aimKey) || Input.GetKeyDown(altAimKey);
        bool up = Input.GetKeyUp(aimKey) || Input.GetKeyUp(altAimKey);

        Vector2 sh = ShoulderWorld();
        Vector2 fwd = Forward();

        if (!attached)
        {
            // apunto siempre "al frente" (según flip) y engancho lo primero
            RaycastHit2D hit = Physics2D.Raycast(sh, fwd, maxGrappleDistance, hookMask);

            if (hold && hit.collider != null && down)
                AttachAt(hit.point);

            // puntero visual
            if (hold) DrawRope(sh, hit.collider ? (Vector3)hit.point : (Vector3)(sh + fwd * maxGrappleDistance));
            else HideRope();

            HideTrajectory();
        }
        else
        {
            // distancia actual y ACUMULAR la máxima
            float currentDist = Vector2.Distance(sh, anchorWorld);
            maxDist = Mathf.Max(maxDist, currentDist);

            // dirección de lanzamiento = hombro -> ancla
            Vector2 launchDir = (anchorWorld - sh).sqrMagnitude > 1e-6f ?
                                (anchorWorld - sh).normalized : Forward();

            // --- PREVIEW (usa la misma fórmula que al soltar, pero con la dist ACTUAL) ---
            float stretchNow = Mathf.Max(0f, currentDist - restLen);
            float sPrev = Mathf.Max(0f, stretchNow - stretchDeadZone);

            float forceMagPrev = Mathf.Clamp(
                Mathf.Max(minLaunchImpulse, baseImpulse)
                + Mathf.Pow(sPrev, stretchPower) * stretchScale * launchMultiplier,
                0f, maxImpulse
            );

            Vector2 impulsePrev = launchDir * forceMagPrev;
            if (Mathf.Abs(launchDir.y) < 0.12f) impulsePrev.y += liftIfFlat;

            _cachedLaunchDir = (anchorWorld - sh).sqrMagnitude > 1e-6f ? (anchorWorld - sh).normalized : Forward();
            _cachedForceMag = forceMagPrev;

            Vector2 v0 = rb.linearVelocity + impulsePrev / rb.mass;
            DrawTrajectory(rb.position, v0);

            // al soltar:
            if (up) LaunchAndDetach(launchDir);
        }
    }

    // ---------- core ----------
    Vector2 ShoulderWorld() => shoulder ? (Vector2)shoulder.position : (Vector2)transform.position;

    Vector2 Forward()
    {
        bool left = (player != null && player.sr != null && player.sr.flipX);
        return left ? Vector2.left : Vector2.right;
    }

    void AttachAt(Vector2 point)
    {
        attached = true;
        anchorWorld = point;

        float initialDist = Vector2.Distance(ShoulderWorld(), anchorWorld);
        float minLen = maxGrappleDistance * Mathf.Clamp01(minRestLenFactor);
        restLen = Mathf.Clamp(initialDist * restLenFactor, minLen, maxGrappleDistance);

        // resetea carga
        maxDist = initialDist;
    }

    void LaunchAndDetach(Vector2 _ignore)
    {
        attached = false;

        // Usa exactamente la fuerza y dirección que acabas de previsualizar
        Vector2 launchDir = _cachedLaunchDir.sqrMagnitude > 1e-8f ? _cachedLaunchDir : Forward();
        float forceMag = _cachedForceMag;

        Vector2 impulse = launchDir * forceMag;
        if (Mathf.Abs(launchDir.y) < 0.12f) impulse.y += liftIfFlat;

        rb.AddForce(impulse, ForceMode2D.Impulse);
        IsLaunched = true;
        _unlockAt = Time.time + launchLockTime;

        // reset
        maxDist = 0f;
        HideRope();
        HideTrajectory();
    }

    // ---------- visuals ----------
    void SetupLR(LineRenderer lr, float width)
    {
        if (!lr) return;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.positionCount = 0;
        lr.sortingOrder = 10;
    }

    void DrawRope(Vector3 a, Vector3 b)
    {
        if (!ropeLR) return;
        ropeLR.enabled = true;
        ropeLR.positionCount = 2;
        ropeLR.SetPosition(0, a);
        ropeLR.SetPosition(1, b);
    }

    void HideRope() { if (ropeLR) ropeLR.enabled = false; }

    void DrawTrajectory(Vector2 p0, Vector2 v0)
    {
        if (!trajectoryLR) return;
        trajectoryLR.enabled = true;
        trajectoryLR.positionCount = trajectoryPoints;

        Vector2 g = Physics2D.gravity * rb.gravityScale;
        for (int i = 0; i < trajectoryPoints; i++)
        {
            float t = i * trajectoryDt;
            Vector2 p = p0 + v0 * t + 0.5f * g * t * t;
            trajectoryLR.SetPosition(i, p);
        }
    }
    void LateUpdate()
    {
        if (IsLaunched && Time.time >= _unlockAt) IsLaunched = false;
    }

    void HideTrajectory() { if (trajectoryLR) trajectoryLR.enabled = false; }

    // --- API para anims (si te sirve) ---
    public bool IsHooked => attached;
}

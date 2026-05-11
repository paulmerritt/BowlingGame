using UnityEngine;

/// Attached to the ball GameObject at spawn time by BowlingGame.
/// Handles aiming display and launch; after launch it's pure PhysX.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BallController : MonoBehaviour
{
    // ── set by BowlingGame before the ball is active ──────────────────────────
    public Vector3  LaneForward;   // unit vector pointing down the lane toward pins
    public Vector3  LaneRight;     // unit vector pointing lane-right
    public float    MaxSpeed = 25f;

    // ── read by BowlingGame ───────────────────────────────────────────────────
    public bool Launched  { get; private set; }
    public bool Settled   { get; private set; }

    Rigidbody   rb;
    SphereCollider sc;

    // Lateral offset from spawn position (A/D to move left/right before launch)
    Vector3 lateralOffset;
    const float LateralSpeed = 2f;   // units/s
    const float LateralMax   = 1.0f; // max units from lane centre

    // Angle offset from lane forward (arrows to aim, -45..+45 deg)
    float   angleOffset;
    const float AngleSpeed  = 60f;
    const float AngleMax    = 45f;

    // Power slider  0..1
    float   sliderValue;
    float   sliderDir = 1f;
    const float SliderSpeed = 1.1f;   // full bar in ~0.9 s
    bool    charging;

    // Spawn position — saved so lateral movement is relative to it
    Vector3 spawnPosition;

    // Stop detection
    float   settleTimer;
    const float SettleDelay    = 1.5f;
    const float SettleSpeedSq  = 0.08f * 0.08f;

    public float  SliderValue => sliderValue;
    public float  AngleDeg    => angleOffset;

    // Aim line
    LineRenderer aimLine;
    const int   AimSegments = 30;
    const float AimLength   = 20f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sc = GetComponent<SphereCollider>();
        BuildAimLine();
    }

    void BuildAimLine()
    {
        var go = new GameObject("AimLine");
        go.transform.SetParent(transform);
        aimLine = go.AddComponent<LineRenderer>();
        aimLine.positionCount = AimSegments;
        aimLine.startWidth    = 0.06f;
        aimLine.endWidth      = 0.01f;
        aimLine.useWorldSpace = true;
        aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimLine.receiveShadows    = false;

        // Simple unlit gradient: white at ball → transparent at far end
        var mat = new Material(Shader.Find("Sprites/Default"));
        aimLine.material   = mat;
        aimLine.startColor = new Color(1f, 1f, 1f, 0.85f);
        aimLine.endColor   = new Color(1f, 1f, 0f, 0f);
    }

    void UpdateAimLine()
    {
        if (aimLine == null) return;
        Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * LaneForward;
        dir.y = 0f;
        dir.Normalize();

        Vector3 origin = transform.position + Vector3.up * 0.05f; // just above floor
        for (int i = 0; i < AimSegments; i++)
        {
            float t = i / (float)(AimSegments - 1);
            aimLine.SetPosition(i, origin + dir * (AimLength * t));
        }
    }

    // Called once by BowlingGame right after Instantiate
    public void Init(Vector3 laneForward, Vector3 laneRight, float maxSpeed)
    {
        LaneForward    = laneForward;
        LaneRight      = laneRight;
        MaxSpeed       = maxSpeed;
        spawnPosition  = transform.position;
        lateralOffset  = Vector3.zero;

        rb.isKinematic = false;
        rb.useGravity  = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.linearDamping          = 0.02f;
        rb.angularDamping         = 0.15f;
        rb.mass                   = 6f;
        rb.maxAngularVelocity     = 80f;
        rb.constraints            = RigidbodyConstraints.None;
    }

    void Update()
    {
        if (Launched) { CheckSettle(); return; }

        // A/D — lateral position before launch (kinematic move, not physics)
        float lateral = 0f;
        if (Input.GetKey(KeyCode.A)) lateral -= 1f;
        if (Input.GetKey(KeyCode.D)) lateral += 1f;
        if (lateral != 0f)
        {
            lateralOffset += LaneRight * lateral * LateralSpeed * Time.deltaTime;
            float mag = Vector3.Dot(lateralOffset, LaneRight);
            if (Mathf.Abs(mag) > LateralMax)
                lateralOffset = LaneRight * Mathf.Sign(mag) * LateralMax;
            transform.position = spawnPosition + lateralOffset;
        }

        // Arrows — aim angle
        float h = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
        angleOffset = Mathf.Clamp(angleOffset + h * AngleSpeed * Time.deltaTime, -AngleMax, AngleMax);

        UpdateAimLine();

        // Space — power charge and release
        if (Input.GetKeyDown(KeyCode.Space)) { charging = true; sliderValue = 0f; sliderDir = 1f; }
        if (charging)
        {
            sliderValue += sliderDir * SliderSpeed * Time.deltaTime;
            if (sliderValue >= 1f) { sliderValue = 1f; sliderDir = -1f; }
            if (sliderValue <= 0f) { sliderValue = 0f; sliderDir =  1f; }
        }
        if (Input.GetKeyUp(KeyCode.Space) && charging) Launch();
    }

    void Launch()
    {
        charging = false;
        Launched = true;
        if (aimLine != null) aimLine.enabled = false;

        Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * LaneForward;
        dir.y = 0f;
        dir.Normalize();

        float speed = Mathf.Lerp(8f, MaxSpeed, sliderValue);
        rb.linearVelocity  = dir * speed;
        rb.angularVelocity = Vector3.Cross(Vector3.up, dir) * (speed / sc.radius);
    }

    void CheckSettle()
    {
        if (rb.linearVelocity.sqrMagnitude < SettleSpeedSq)
            settleTimer += Time.deltaTime;
        else
            settleTimer = 0f;

        if (settleTimer >= SettleDelay) Settled = true;
    }

    // ── Powerup hooks ─────────────────────────────────────────────────────────
    public void ApplyLateralImpulse(float sign)
    {
        if (!Launched) return;
        rb.AddForce(LaneRight * sign * 120f, ForceMode.Impulse);
    }
}

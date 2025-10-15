using UnityEngine;

public class BallScript : MonoBehaviour {
    public Rigidbody Rb { get; private set; }
    public bool HasLaunched { get; private set; }
    public float maxCharge = 40f;
    public float spinTorque = 20f;
    public Transform aimArrow; // optional visual

    float charge;
    Vector3 launchDir;

    void Awake() { Rb = GetComponent<Rigidbody>(); }

    public void BeginAiming(Vector3 dir) {
        HasLaunched = false;
        charge = 0f;
        launchDir = dir.normalized;
        Rb.isKinematic = true;
        Rb.linearVelocity = Vector3.zero;
        Rb.angularVelocity = Vector3.zero;
    }

    public void UpdateAiming(float dt) {
        // Hold space to charge, A/D to curve pre-release
        if (Input.GetKey(KeyCode.Space)) charge = Mathf.Min(maxCharge, charge + dt * 25f);
        float yaw = Input.GetAxis("Horizontal") * 60f * dt;
        launchDir = Quaternion.Euler(0f, yaw, 0f) * launchDir;
        if (aimArrow) aimArrow.forward = launchDir;
        if (Input.GetKeyUp(KeyCode.Space)) Launch();
    }

    void Launch() {
        HasLaunched = true;
        Rb.isKinematic = false;
        Rb.AddForce(launchDir * (charge * 50f), ForceMode.Impulse);
        // optional spin: use vertical input at release
        float spin = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(spin) > 0.01f) Rb.AddTorque(Vector3.up * spin * spinTorque, ForceMode.Impulse);
    }

    public bool HasStopped(float speedEps = 0.1f) => HasLaunched && Rb.linearVelocity.sqrMagnitude < speedEps * speedEps;
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pin : MonoBehaviour
{
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision col)
    {
        if (!col.gameObject.CompareTag("Ball") && !col.gameObject.CompareTag("Pin")) return;
        if (rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping  = 0.02f;
        rb.angularDamping = 0.02f;
    }
}

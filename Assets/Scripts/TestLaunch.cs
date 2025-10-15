using UnityEngine;

public class TestLaunch : MonoBehaviour
{
    public bool launched = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!launched)
        {
            launched = true;
            //chargingPower = false;
            //Player currentPlayer = players[currentPlayerIndex];

            // Calculate launch position with player offset
            //Vector3 spawnPos = currentPlayer.spawnPoint.position + currentPlayerOffset;
            //spawnPos.y = 0.2f; // Lower spawn height

            // Calculate direction with angle offset
            Vector3 pinZoneCenter = new Vector3(0, 0, 0);
            Vector3 baseDirection = (pinZoneCenter - transform.position).normalized;
            Quaternion angleRotation = Quaternion.Euler(0, 0, 0);
            Vector3 direction = angleRotation * baseDirection;
            direction.y = 0;
            direction.Normalize();
            
            // currentBall = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
            // currentBall.tag = "Ball";
            
            // // Ball collider setup
            SphereCollider ballCollider = transform.GetComponent<SphereCollider>();
            if (ballCollider == null)
            {
                ballCollider = this.gameObject.AddComponent<SphereCollider>();
            }
            ballCollider.radius = 0.108f; // Standard bowling ball radius (10.85cm)
            
            // Ball physics material
            PhysicsMaterial ballMat = new PhysicsMaterial("BallPhysics");
            ballMat.dynamicFriction = 0.3f;
            ballMat.staticFriction = 0.3f;
            ballMat.bounciness = 0.3f;
            ballMat.frictionCombine = PhysicsMaterialCombine.Average;
            ballMat.bounceCombine = PhysicsMaterialCombine.Average;
            ballCollider.material = ballMat;
            

            // Ball rigidbody
            Rigidbody rb = transform.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = this.gameObject.AddComponent<Rigidbody>();
            }
            
            rb.mass = 7.26f; // Standard 16lb bowling ball in kg
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.3f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxAngularVelocity = 50f;
            
            float power = Mathf.Max(1.0f, 0.3f) * 15.0f; // Minimum power
            
            // Apply velocity
            rb.linearVelocity = direction * power;
            
            // Add topspin for forward roll
            Vector3 spinAxis = Vector3.Cross(direction, Vector3.up);
            rb.angularVelocity = spinAxis * power * 0.4f;
            
            //ballInMotion = true;
        }
        
    }
}

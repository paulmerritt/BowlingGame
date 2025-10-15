using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BowlingGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int numPlayers = 2;
    public float laneWidth = 1.5f;
    public float laneLength = 18f;
    public float powerMultiplier = 90f;
    
    [Header("References")]
    public GameObject ballPrefab;
    public GameObject pinPrefab;
    public GameObject powerUpPrefab;
    public Camera downLaneCamera;
    public Camera birdsEyeCamera;
    public Transform[] playerSpawnPoints;
    public Transform pinZoneCenter;
    
    [Header("UI")]
    public UnityEngine.UI.Text statusText;
    public UnityEngine.UI.Text[] playerScoreTexts;
    public UnityEngine.UI.Slider powerSlider;
    public UnityEngine.UI.Text powerText;
    
    [Header("Player Movement")]
    public float playerMoveSpeed = 3f;
    public float maxLateralMovement = 2f;
    public float angleAdjustSpeed = 30f;
    public float maxAngleAdjustment = 45f;
    
    [Header("Aim Assist")]
    public LineRenderer aimLine;
    public float aimLineLength = 10f;
    public int aimLineSegments = 20;
    
    private List<Player> players = new List<Player>();
    private int currentPlayerIndex = 0;
    private List<GameObject> activePins = new List<GameObject>();
    private List<PowerUp> activePowerUps = new List<PowerUp>();
    private GameObject currentBall;
    private GameObject playerIndicator;
    private bool ballInMotion = false;
    private float ballStopTimer = 0f;
    private int currentFrame = 1;
    private int throwInFrame = 1;
    
    private Vector3 currentPlayerOffset = Vector3.zero;
    private float currentAngleOffset = 0f;
    private float launchPower = 0.5f;
    private bool chargingPower = false;
    private float powerChargeDirection = 1f;
    
    private enum CameraMode { DownLane, BirdsEye }
    private CameraMode currentCamera = CameraMode.DownLane;

    void Start()
    {
        InitializePlayers();
        SetupPins();
        SpawnPowerUps();
        SwitchCamera(CameraMode.DownLane);
        CreatePlayerIndicator();
        SetupAimLine();
        UpdateUI();
        UpdatePowerSlider();
    }

    void Update()
    {
        HandleInput();
        CheckBallMotion();
        UpdatePowerUpTimers();
        UpdatePlayerIndicatorPosition();
        UpdateAimLine();
        UpdateDownLaneCamera();
        
        if (chargingPower)
        {
            UpdatePowerCharge();
        }
    }

    void InitializePlayers()
    {
        for (int i = 0; i < Mathf.Clamp(numPlayers, 2, 4); i++)
        {
            Player p = new Player();
            p.id = i;
            p.name = "Player " + (i + 1);
            p.score = 0;
            p.spawnPoint = playerSpawnPoints[i];
            p.activePowerUp = PowerUpType.None;
            players.Add(p);
        }
    }

    void SetupPins()
    {
        ClearPins();
        
        // Standard 10-pin triangle formation at center
        float pinSpacing = 0.5f;
        Vector3 basePos = pinZoneCenter.position;
        
        int[] pinsPerRow = { 1, 2, 3, 4 };
        int pinIndex = 0;
        
        for (int row = 0; row < pinsPerRow.Length; row++)
        {
            for (int col = 0; col < pinsPerRow[row]; col++)
            {
                float xOffset = (col - (pinsPerRow[row] - 1) * 0.5f) * pinSpacing;
                float zOffset = row * pinSpacing * 0.866f;
                
                Vector3 pos = basePos + new Vector3(xOffset, 0.24f, zOffset);
                GameObject pin = Instantiate(pinPrefab, pos, Quaternion.identity);
                pin.name = "Pin_" + (pinIndex + 1);
                pin.tag = "Pin";
                
                // Ensure pin has proper collider
                CapsuleCollider pinCollider = pin.GetComponent<CapsuleCollider>();
                if (pinCollider == null)
                {
                    pinCollider = pin.AddComponent<CapsuleCollider>();
                }
                // pinCollider.height = 0.48f;
                // pinCollider.radius = 0.06f;
                // pinCollider.center = Vector3.zero;
                // pinCollider.direction = 1; // Y-axis capsule (standing upright)
                
                // // Physics material with good friction
                // PhysicsMaterial pinMat = new PhysicsMaterial("PinPhysics");
                // pinMat.dynamicFriction = 0.4f;
                // pinMat.staticFriction = 0.8f; // High static friction to prevent wobble
                // pinMat.bounciness = 0.2f;
                // pinMat.frictionCombine = PhysicsMaterialCombine.Maximum;
                // pinMat.bounceCombine = PhysicsMaterialCombine.Average;
                // pinCollider.material = pinMat;
                
                // // Configure rigidbody - NO CONSTRAINTS, just let physics handle it
                // Rigidbody pinRb = pin.GetComponent<Rigidbody>();
                // if (pinRb == null)
                // {
                //     pinRb = pin.AddComponent<Rigidbody>();
                // }
                
                // pinRb.mass = 1.6f;
                // pinRb.linearDamping = 0.5f; // Higher drag for stability when standing
                // pinRb.angularDamping = 2.0f; // Much higher angular drag to dampen wobble
                // pinRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // pinRb.interpolation = RigidbodyInterpolation.Interpolate;
                // pinRb.useGravity = true;
                // pinRb.centerOfMass = new Vector3(0, -0.15f, 0); // Much lower center of mass
                // pinRb.maxAngularVelocity = 20f; // Limit spinning
                
                // // NO CONSTRAINTS - pins stand naturally via physics
                // pinRb.constraints = RigidbodyConstraints.None;
                
                // // Let pin settle for a moment
                // pinRb.sleepThreshold = 0.1f;
                
                activePins.Add(pin);
                pinIndex++;
            }
        }
        
        // Let pins settle into place
        Physics.SyncTransforms();
    }

    void ClearPins()
    {
        foreach (var pin in activePins)
        {
            if (pin != null) Destroy(pin);
        }
        activePins.Clear();
    }

    void SpawnPowerUps()
    {
        // Spawn power-ups along each lane
        for (int i = 0; i < numPlayers; i++)
        {
            float angle = (360f / numPlayers) * i;
            float rad = Mathf.Deg2Rad * angle;
            
            // Spawn 2-3 power-ups per lane
            for (int j = 0; j < 2; j++)
            {
                float distance = Random.Range(5f, 12f);
                Vector3 pos = pinZoneCenter.position + new Vector3(
                    Mathf.Sin(rad) * distance,
                    10.2f,
                    -Mathf.Cos(rad) * distance
                );
                
                GameObject powerUpObj = Instantiate(powerUpPrefab, pos, Quaternion.identity);
                PowerUp pu = powerUpObj.AddComponent<PowerUp>();
                pu.type = (PowerUpType)Random.Range(1, 4);
                pu.laneIndex = i;
                activePowerUps.Add(pu);
            }
        }
    }

    void HandleInput()
    {
        if (ballInMotion)
        {
            // Use power-ups during ball motion
            if (currentBall != null && players[currentPlayerIndex].activePowerUp != PowerUpType.None)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    UsePowerUp(Vector3.left);
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    UsePowerUp(Vector3.right);
                }
            }
            return;
        }
        
        // Camera switch
        if (Input.GetKeyDown(KeyCode.C))
        {
            SwitchCamera(currentCamera == CameraMode.DownLane ? 
                CameraMode.BirdsEye : CameraMode.DownLane);
        }
        
        // Player lateral movement
        if (Input.GetKey(KeyCode.A))
        {
            MovePlayer(-1);
        }
        if (Input.GetKey(KeyCode.D))
        {
            MovePlayer(1);
        }
        
        // Angle adjustment
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            AdjustAngle(-1);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            AdjustAngle(1);
        }
        
        // Power charge system
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartPowerCharge();
        }
        if (Input.GetKeyUp(KeyCode.Space) && chargingPower)
        {
            LaunchBall();
        }
    }

    void LaunchBall()
    {
        chargingPower = false;
        Player currentPlayer = players[currentPlayerIndex];
        
        // Calculate launch position with player offset
        Vector3 spawnPos = currentPlayer.spawnPoint.position + currentPlayerOffset;
        spawnPos.y = 0.2f; // Lower spawn height

        
        // Calculate direction with angle offset
        Vector3 baseDirection = (pinZoneCenter.position - spawnPos).normalized;
        Quaternion angleRotation = Quaternion.Euler(0, currentAngleOffset, 0);
        Vector3 direction = angleRotation * baseDirection;
        direction.y = 0;
        direction.Normalize();
        
        currentBall = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        currentBall.tag = "Ball";
        
        // Ball collider setup
        SphereCollider ballCollider = currentBall.GetComponent<SphereCollider>();
        if (ballCollider == null)
        {
            ballCollider = currentBall.AddComponent<SphereCollider>();
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
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = currentBall.AddComponent<Rigidbody>();
        }
        
        rb.mass = 7.26f; // Standard 16lb bowling ball in kg
        rb.linearDamping = 0.02f;
        rb.angularDamping = 0.3f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxAngularVelocity = 100f;
        
        float power = Mathf.Max(launchPower, 0.3f) * powerMultiplier; // Minimum power
        
        // Apply velocity
        rb.linearVelocity = direction * power;
        
        // Add topspin for forward roll
        Vector3 spinAxis = Vector3.Cross(direction, Vector3.up);
        rb.angularVelocity = spinAxis * power * 0.4f;
        
        ballInMotion = true;
        statusText.text = currentPlayer.name + " is bowling... (Power: " + (launchPower * 100f).ToString("F0") + "%)";
        
        if (playerIndicator != null) playerIndicator.SetActive(false);
        if (aimLine != null) aimLine.enabled = false;
    }

    PhysicsMaterial CreateBouncyPhysicsMaterial(string name, float bounciness, float dynamicFriction, float staticFriction)
    {
        PhysicsMaterial mat = new PhysicsMaterial(name);
        mat.bounciness = bounciness;
        mat.dynamicFriction = dynamicFriction;
        mat.staticFriction = staticFriction;
        mat.bounceCombine = PhysicsMaterialCombine.Average;
        mat.frictionCombine = PhysicsMaterialCombine.Average;
        return mat;
    }

    void CreatePlayerIndicator()
    {
        // Create a visual indicator for player position
        playerIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        playerIndicator.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
        
        // Make it visible with a material
        Renderer rend = playerIndicator.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.yellow;
        }
        
        // Remove collider so it doesn't interfere
        Collider col = playerIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void UpdatePlayerIndicatorPosition()
    {
        if (playerIndicator == null || ballInMotion) return;
        
        Player currentPlayer = players[currentPlayerIndex];
        Vector3 targetPos = currentPlayer.spawnPoint.position + currentPlayerOffset;
        targetPos.y = 0.05f; // Keep it on the ground
        
        playerIndicator.transform.position = targetPos;
        playerIndicator.SetActive(true);
    }

    void MovePlayer(float direction)
    {
        // Calculate perpendicular direction to lane
        Player currentPlayer = players[currentPlayerIndex];
        Vector3 laneDir = (pinZoneCenter.position - currentPlayer.spawnPoint.position).normalized;
        Vector3 lateralDir = Vector3.Cross(laneDir, Vector3.up).normalized;
        
        // Update offset
        Vector3 movement = lateralDir * direction * playerMoveSpeed * Time.deltaTime;
        currentPlayerOffset += movement;
        
        // Clamp lateral movement
        float currentLateralDist = currentPlayerOffset.magnitude;
        if (currentLateralDist > maxLateralMovement)
        {
            currentPlayerOffset = currentPlayerOffset.normalized * maxLateralMovement;
        }
    }

    void AdjustAngle(float direction)
    {
        currentAngleOffset += direction * angleAdjustSpeed * Time.deltaTime;
        currentAngleOffset = Mathf.Clamp(currentAngleOffset, -maxAngleAdjustment, maxAngleAdjustment);
    }

    void SetupAimLine()
    {
        if (aimLine == null)
        {
            // Create a new GameObject with LineRenderer if not assigned
            GameObject aimLineObj = new GameObject("AimLine");
            aimLine = aimLineObj.AddComponent<LineRenderer>();
        }
        
        // Configure the line renderer
        aimLine.positionCount = aimLineSegments;
        aimLine.startWidth = 0.05f;
        aimLine.endWidth = 0.05f;
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor = new Color(1f, 1f, 0f, 0.8f); // Yellow
        aimLine.endColor = new Color(1f, 0f, 0f, 0.8f); // Red at end
        
        // Make it dotted by using texture tiling
        aimLine.textureMode = LineTextureMode.Tile;
        aimLine.material.mainTextureScale = new Vector2(5f, 1f);
    }

    void UpdateAimLine()
    {
        if (aimLine == null || ballInMotion)
        {
            if (aimLine != null) aimLine.enabled = false;
            return;
        }
        
        aimLine.enabled = true;
        
        Player currentPlayer = players[currentPlayerIndex];
        Vector3 startPos = currentPlayer.spawnPoint.position + currentPlayerOffset;
        startPos.y = 0.2f; // Slightly above ground
        
        // Calculate direction with angle offset
        Vector3 baseDirection = (pinZoneCenter.position - currentPlayer.spawnPoint.position).normalized;
        Quaternion angleRotation = Quaternion.Euler(0, currentAngleOffset, 0);
        Vector3 direction = angleRotation * baseDirection;
        
        // Draw the aim line
        for (int i = 0; i < aimLineSegments; i++)
        {
            float t = i / (float)(aimLineSegments - 1);
            Vector3 point = startPos + direction * aimLineLength * t;
            point.y = 0.2f; // Keep line on ground level
            aimLine.SetPosition(i, point);
        }
    }

    void StartPowerCharge()
    {
        chargingPower = true;
        launchPower = 0f;
        powerChargeDirection = 1f;
    }

    void UpdatePowerCharge()
    {
        // Oscillating power bar
        launchPower += powerChargeDirection * Time.deltaTime * 3.0f;
        
        if (launchPower >= 1f)
        {
            launchPower = 1f;
            powerChargeDirection = -1f;
        }
        else if (launchPower <= 0f)
        {
            launchPower = 0f;
            powerChargeDirection = 1f;
        }
        
        UpdatePowerSlider();
    }

    void UpdatePowerSlider()
    {
        if (powerSlider != null)
        {
            powerSlider.value = launchPower;
            
            // Color code the slider
            UnityEngine.UI.Image fillImage = powerSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
            if (fillImage != null)
            {
                if (launchPower < 0.3f)
                    fillImage.color = Color.red;
                else if (launchPower < 0.7f)
                    fillImage.color = Color.yellow;
                else
                    fillImage.color = Color.green;
            }
        }
        
        if (powerText != null)
        {
            powerText.text = "Power: " + (launchPower * 100f).ToString("F0") + "%";
        }
    }

    void UsePowerUp(Vector3 direction)
    {
        Player p = players[currentPlayerIndex];
        
        if (currentBall == null) return;
        
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;
        
        switch (p.activePowerUp)
        {
            case PowerUpType.Slam:
                rb.AddForce(direction * 10f, ForceMode.Impulse);
                statusText.text = p.name + " used SLAM!";
                break;
                
            case PowerUpType.Curve:
                rb.AddForce(direction * 5f, ForceMode.Impulse);
                break;
                
            case PowerUpType.Interference:
                // Affect other players' lanes (spawn obstacles)
                InterferWithOtherLanes();
                break;
        }
        
        p.activePowerUp = PowerUpType.None;
    }

    void InterferWithOtherLanes()
    {
        // Create temporary bumpers on other lanes
        for (int i = 0; i < numPlayers; i++)
        {
            if (i == currentPlayerIndex) continue;
            
            float angle = (360f / numPlayers) * i;
            float rad = Mathf.Deg2Rad * angle;
            Vector3 obstaclePos = pinZoneCenter.position + new Vector3(
                Mathf.Sin(rad) * 8f,
                0.5f,
                -Mathf.Cos(rad) * 8f
            );
            
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = obstaclePos;
            obstacle.transform.localScale = new Vector3(1f, 1f, 0.3f);
            Destroy(obstacle, 3f);
        }
        
        statusText.text = players[currentPlayerIndex].name + " used INTERFERENCE!";
    }

    void CheckBallMotion()
    {
        if (!ballInMotion || currentBall == null) return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Check if ball stopped

        if (rb.position.y <= -2.5f)
        {
            EndThrow();
        }

        if (rb.linearVelocity.magnitude < 0.1f)
        {
            ballStopTimer += Time.deltaTime;

            if (ballStopTimer > 2f)
            {
                EndThrow();
            }
        }
        else
        {
            ballStopTimer = 0f;
            CheckPowerUpCollision();
        }
    }

    void CheckPowerUpCollision()
    {
        if (currentBall == null) return;
        
        for (int i = activePowerUps.Count - 1; i >= 0; i--)
        {
            PowerUp pu = activePowerUps[i];
            if (pu == null) continue;
            
            float dist = Vector3.Distance(currentBall.transform.position, pu.transform.position);
            if (dist < 0.5f)
            {
                players[currentPlayerIndex].activePowerUp = pu.type;
                statusText.text = players[currentPlayerIndex].name + " collected " + pu.type + "!";
                
                Destroy(pu.gameObject);
                activePowerUps.RemoveAt(i);
            }
        }
    }

    void EndThrow()
    {
        ballInMotion = false;
        ballStopTimer = 0f;
        
        // Count knocked pins
        int pinsDown = CountKnockedPins();
        
        // Update score
        players[currentPlayerIndex].score += pinsDown;
        
        // Clean up ball
        if (currentBall != null) Destroy(currentBall);
        
        // Reset player position and angle for next throw
        currentPlayerOffset = Vector3.zero;
        currentAngleOffset = 0f;
        
        // Determine next action
        throwInFrame++;
        
        if (throwInFrame > 2 || pinsDown == 10)
        {
            // Frame complete, next player
            throwInFrame = 1;
            currentPlayerIndex = (currentPlayerIndex + 1) % numPlayers;
            
            // Update camera for new player
            if (currentCamera == CameraMode.DownLane)
            {
                UpdateDownLaneCamera();
            }
            
            if (currentPlayerIndex == 0)
            {
                currentFrame++;
                if (currentFrame > 10)
                {
                    EndGame();
                    return;
                }
            }
            
            SetupPins();
            SpawnPowerUps();
        }
        
        UpdateUI();
        UpdatePowerSlider();
    }

    int CountKnockedPins()
    {
        int count = 0;
        foreach (var pin in activePins)
        {
            if (pin != null)
            {
                // Check if pin is knocked (tilted or moved)
                if (Vector3.Angle(pin.transform.up, Vector3.up) > 30f)
                {
                    count++;
                }
            }
        }
        return count;
    }

    void UpdateUI()
    {
        for (int i = 0; i < players.Count && i < playerScoreTexts.Length; i++)
        {
            if (playerScoreTexts[i] != null)
            {
                string indicator = (i == currentPlayerIndex) ? ">>> " : "";
                playerScoreTexts[i].text = indicator + players[i].name + ": " + players[i].score;
            }
        }
        
        string angleText = currentAngleOffset != 0 ? " | Angle: " + currentAngleOffset.ToString("F0") + "°" : "";
        statusText.text = "Frame " + currentFrame + " - " + players[currentPlayerIndex].name + "'s turn" + angleText;
    }

    void UpdatePowerUpTimers()
    {
        foreach (var pu in activePowerUps)
        {
            if (pu != null)
            {
                pu.transform.Rotate(Vector3.up, 50f * Time.deltaTime);
            }
        }
    }

    void SwitchCamera(CameraMode mode)
    {
        currentCamera = mode;
        
        downLaneCamera.gameObject.SetActive(mode == CameraMode.DownLane);
        birdsEyeCamera.gameObject.SetActive(mode == CameraMode.BirdsEye);
        
        if (mode == CameraMode.DownLane)
        {
            UpdateDownLaneCamera();
        }
    }

    void UpdateDownLaneCamera()
    {
        if (downLaneCamera == null || currentCamera != CameraMode.DownLane) return;
        
        Player currentPlayer = players[currentPlayerIndex];
        if (currentPlayer.spawnPoint == null || pinZoneCenter == null) return;
        
        // Position camera behind and above the player spawn point
        Vector3 spawnPos = currentPlayer.spawnPoint.position;
        Vector3 directionToPins = (pinZoneCenter.position - spawnPos).normalized;
        
        // Place camera behind player at a good viewing height and distance
        Vector3 cameraOffset = -directionToPins * 3f + Vector3.up * 2f;
        downLaneCamera.transform.position = spawnPos + cameraOffset;
        
        // Make camera look at pin zone center
        downLaneCamera.transform.LookAt(pinZoneCenter.position);
    }

    void EndGame()
    {
        Player winner = players.OrderByDescending(p => p.score).First();
        statusText.text = "Game Over! " + winner.name + " wins with " + winner.score + " points!";
        enabled = false;
    }
}

[System.Serializable]
public class Player
{
    public int id;
    public string name;
    public int score;
    public Transform spawnPoint;
    public PowerUpType activePowerUp;
}

public enum PowerUpType
{
    None,
    Slam,
    Curve,
    Interference
}

public class PowerUp : MonoBehaviour
{
    public PowerUpType type;
    public int laneIndex;
}

public class PinBehavior : MonoBehaviour
{
    private Rigidbody rb;

    public void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Apply extra force when hit by ball for dramatic effect
        if (collision.gameObject.CompareTag("Ball"))
        {
            Vector3 impactDirection = collision.impulse.normalized;
            float impactMagnitude = collision.impulse.magnitude;

            if (rb != null)
            {
                // Reduce drag when hit so pins tumble more freely
                rb.linearDamping = 0.2f;
                rb.angularDamping = 0.3f;

                // Add impact force
                rb.AddForce(impactDirection * impactMagnitude * 0.3f, ForceMode.Impulse);
            }
        }
    }
}



/*

I commented out some lines and I'm much closer to getting the desired behavior. However, the new problem is my bowling ball hit the front-most pin and then every pin mimic'd the reaction and fell over. But it should be more realistic, where the ball bonks one pin into another and into another, slower and slower as speed is transferred between objects
*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
//  BOWLING GAME  –  2-4 players, shared pin deck, lane powerups
// ═══════════════════════════════════════════════════════════════════
public class BowlingGame : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Players")]
    [Range(2, 4)] public int numPlayers = 2;
    public string[] playerNames = { "Player 1", "Player 2", "Player 3", "Player 4" };

    [Header("Prefabs")]
    public GameObject ballPrefab;
    public GameObject pinPrefab;
    public GameObject powerupBoxPrefab;   // trigger box on the lane

    [Header("Lane Layout")]
    public Transform   pinDeckCenter;        // legacy single-deck (used as fallback)
    public Transform[] pinDeckCenters;       // one per player — preferred
    public Vector3[]   pinDeckFacings;       // direction FROM centre TOWARD each player's spawn
    public Transform[] ballSpawnPoints;      // one per player (2-4)
    public float       ballMaxSpeed = 25f;

    [Header("Cameras")]
    public Camera downLaneCam;
    public Camera birdsEyeCam;

    [Header("UI")]
    public Slider       powerSlider;
    public TMP_Text     statusText;
    public TMP_Text     turnText;
    public RectTransform scoreboardRoot; // parent for score rows
    public GameObject   scoreRowPrefab;  // has TMP_Text[] (name + 10 frames + total)
    public Image        powerupIcon;     // shows held powerup icon (alpha 0 when none)
    public TMP_Text     powerupLabel;

    // ── Runtime state ────────────────────────────────────────────────────────
    class PlayerState
    {
        public string   name;
        public int[]    frameRolls  = new int[21]; // enough for 10th-frame bonuses
        public int      rollCount;
        public int      heldPowerup = 0; // 0=none, 1=SlamL, 2=SlamR, 3=Scatter, 4=Guard
        public bool     gutterGuardActive;
        // cached UI row
        public TMP_Text[]  frameCells;
        public TMP_Text    totalCell;
        public Image       rowBg;
    }

    PlayerState[]  players;
    int            currentPlayer;
    int            currentFrame;       // 0-9
    // each player bowls once per "half-frame"; both halves = one frame
    // rollInFrame tracks which bowl of the frame we're on for currentPlayer
    int            rollInFrame;        // 0 or 1 (1 = second bowl of their frame)

    // Pin tracking
    List<GameObject> activePins   = new();
    HashSet<int>     knockedThisFrame = new();  // instance IDs knocked before this roll
    List<GameObject> activePickups = new();
    List<GameObject> gutterGuards  = new();

    BallController  activeBall;
    bool            waitingForBall;

    // ── colours ──────────────────────────────────────────────────────────────
    static readonly Color ColActive   = new Color(1f, 1f, 0.55f, 1f);
    static readonly Color ColInactive = new Color(1f, 1f, 1f,    1f);

    Transform PinDeck(int playerIdx)
    {
        if (pinDeckCenters != null && playerIdx < pinDeckCenters.Length && pinDeckCenters[playerIdx] != null)
            return pinDeckCenters[playerIdx];
        return pinDeckCenter;
    }

    // Direction the pin triangle opens toward for a given player (points at their spawn)
    Vector3 PinFacing(int playerIdx)
    {
        if (pinDeckFacings != null && playerIdx < pinDeckFacings.Length && pinDeckFacings[playerIdx] != Vector3.zero)
            return pinDeckFacings[playerIdx].normalized;
        // fallback: derive from spawn position
        Transform deck  = PinDeck(playerIdx);
        Transform spawn = ballSpawnPoints[playerIdx];
        Vector3 v = spawn.position - deck.position;
        v.y = 0;
        return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward;
    }

    // ══════════════════════════════════════════════════════════════════════════
    void Start()
    {
        // clamp to available spawn points
        numPlayers = Mathf.Clamp(numPlayers, 2, Mathf.Min(4, ballSpawnPoints.Length));

        players = new PlayerState[numPlayers];
        for (int i = 0; i < numPlayers; i++)
        {
            players[i] = new PlayerState();
            players[i].name = i < playerNames.Length ? playerNames[i] : $"P{i+1}";
        }

        BuildScoreboard();
        SetupFullPinRack();
        SpawnPickup();

        birdsEyeCam.gameObject.SetActive(false);
        downLaneCam.gameObject.SetActive(true);

        currentPlayer = 0;
        currentFrame  = 0;
        rollInFrame   = 0;

        StartCoroutine(BeginTurn());
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TURN LOOP
    // ══════════════════════════════════════════════════════════════════════════
    IEnumerator BeginTurn()
    {
        // Snapshot which pins are down before this roll
        knockedThisFrame.Clear();
        foreach (var pin in activePins)
            if (pin != null && IsPinDown(pin)) knockedThisFrame.Add(pin.GetInstanceID());

        PlayerState p = players[currentPlayer];
        SetStatus($"{p.name} – Frame {currentFrame + 1}  |  Bowl {rollInFrame + 1}");
        SetTurnText(p.name, rollInFrame, p.heldPowerup);

        // Camera: point down the lane from this player's spawn
        PositionDownLaneCamera(currentPlayer);

        // Spawn ball
        Transform spawn = ballSpawnPoints[currentPlayer];
        GameObject ballGO = Instantiate(ballPrefab, spawn.position, spawn.rotation);
        ballGO.tag = "Ball";

        activeBall = ballGO.GetComponent<BallController>();
        if (activeBall == null) activeBall = ballGO.AddComponent<BallController>();

        // Lane direction: from spawn toward this player's pin deck, flattened
        Transform deck = PinDeck(currentPlayer);
        Vector3 toPin  = (deck.position - spawn.position);
        toPin.y = 0f;
        toPin.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toPin) * -1f; // lane-right from player's POV

        // Seat ball on floor
        SphereCollider sc = ballGO.GetComponent<SphereCollider>();
        float ballR = sc != null ? sc.radius * ballGO.transform.localScale.x : 0.5f;
        Vector3 pos = spawn.position;
        pos.y = deck.position.y + ballR;
        ballGO.transform.position = pos;

        activeBall.Init(toPin, right, ballMaxSpeed);

        // Gutter guards
        if (p.gutterGuardActive) SpawnGutterGuards(spawn, toPin, ballR);
        p.gutterGuardActive = false;

        // Wait for ball to settle
        waitingForBall = true;
        yield return WaitForBallAndHandle();
        waitingForBall = false;

        // Clean up guards
        foreach (var g in gutterGuards) if (g) Destroy(g);
        gutterGuards.Clear();

        if (activeBall != null) { Destroy(activeBall.gameObject); activeBall = null; }

        yield return new WaitForSeconds(0.4f);
        AdvanceTurn();
    }

    IEnumerator WaitForBallAndHandle()
    {
        // Wait until ball is launched (slider is updated by Update() throughout)
        yield return new WaitUntil(() => activeBall == null || activeBall.Launched);

        // While rolling: watch for all end-of-turn conditions
        while (activeBall != null && !BallDone())
        {
            HandlePowerupInput();
            CheckPickupCollision();
            yield return null;
        }
    }

    // Returns true when the turn should end
    bool BallDone()
    {
        if (activeBall == null) return true;
        if (activeBall.Settled)  return true;

        // All pins down → strike or spare, end immediately
        if (CountStanding() == 0) return true;

        // Ball travelled past the pin deck (overshot) — past the deck by more than 3 units
        Transform deck = PinDeck(currentPlayer);
        Vector3 toBall = activeBall.transform.position - deck.position;
        toBall.y = 0f;
        Vector3 toSpawn = ballSpawnPoints[currentPlayer].position - deck.position;
        toSpawn.y = 0f;
        // If ball is on the far side of the deck from the spawn, it has passed through
        if (Vector3.Dot(toBall, toSpawn) < -3f) return true;

        // Ball out of bounds laterally (fell off the side of the lane)
        float laneHalfWidth = 4f;
        if (Mathf.Abs(Vector3.Dot(toBall, activeBall.LaneRight)) > laneHalfWidth) return true;

        return false;
    }

    void HandlePowerupInput()
    {
        if (activeBall == null || !activeBall.Launched) return;
        PlayerState p = players[currentPlayer];
        if (p.heldPowerup == 0) return;

        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
        {
            UsePowerup(p, activeBall);
        }
    }

    void UsePowerup(PlayerState p, BallController ball)
    {
        switch (p.heldPowerup)
        {
            case 1: ball.ApplyLateralImpulse(-1f); break; // Slam Left
            case 2: ball.ApplyLateralImpulse( 1f); break; // Slam Right
            case 3: ScatterPins();                  break; // Pin Scatter
            case 4: p.gutterGuardActive = true;     break; // Guard (queued for next bowl)
        }
        p.heldPowerup = 0;
        UpdatePowerupUI(p);
    }

    void ScatterPins()
    {
        foreach (var pin in activePins)
        {
            if (pin == null || IsPinDown(pin)) continue;
            Rigidbody rb = pin.GetComponent<Rigidbody>();
            if (rb == null) continue;
            Vector3 impulse = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
            rb.AddForce(impulse, ForceMode.Impulse);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TURN ADVANCE
    // ══════════════════════════════════════════════════════════════════════════
    void AdvanceTurn()
    {
        // Count newly knocked pins this roll
        int newlyKnocked = 0;
        foreach (var pin in activePins)
        {
            if (pin == null) continue;
            if (!IsPinDown(pin)) continue;
            if (!knockedThisFrame.Contains(pin.GetInstanceID())) newlyKnocked++;
        }

        int pinsStanding = CountStanding();
        bool allDown     = pinsStanding == 0;

        // Record roll
        PlayerState p = players[currentPlayer];
        if (p.rollCount < p.frameRolls.Length)
            p.frameRolls[p.rollCount++] = newlyKnocked;

        RefreshScoreRow(currentPlayer);

        // Decide next state
        bool frameComplete = false;

        if (rollInFrame == 0)
        {
            if (allDown) // strike — frame done for this player
                frameComplete = true;
            else
                rollInFrame = 1; // second bowl
        }
        else // rollInFrame == 1
        {
            frameComplete = true;
        }

        if (frameComplete)
        {
            rollInFrame = 0;
            // Move to next player
            int nextPlayer = (currentPlayer + 1) % numPlayers;
            bool fullRoundDone = nextPlayer == 0; // wrapped back to first player

            if (fullRoundDone) currentFrame++;

            currentPlayer = nextPlayer;

            if (currentFrame >= 10) { EndGame(); return; }

            // Reset pins at start of each new frame (after all players bowl twice)
            if (fullRoundDone)
            {
                SetupFullPinRack();
                SpawnPickup();
            }
            else if (allDown)
            {
                // Strike mid-round: reset for next player
                SetupFullPinRack();
                SpawnPickup();
            }
        }

        StartCoroutine(BeginTurn());
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PIN SETUP
    // ══════════════════════════════════════════════════════════════════════════
    void SetupFullPinRack()
    {
        foreach (var p in activePins) if (p) Destroy(p);
        activePins.Clear();

        // Measure pin from prefab
        var probe = Instantiate(pinPrefab);
        probe.SetActive(false);
        var probeCol = probe.GetComponent<CapsuleCollider>();
        float pinR = probeCol != null ? probeCol.radius * probe.transform.localScale.x : 0.25f;
        float pinH = probeCol != null ? probeCol.height * probe.transform.localScale.y : 0.8f;
        Destroy(probe);

        // Pin spacing: real bowling is ~3.2× pin diameter centre-to-centre
        float spacing = pinR * 2f * 1.5f;

        Vector3 deckPos = PinDeck(currentPlayer).position;
        float   groundY = deckPos.y;
        float   spawnY  = groundY + pinH * 0.5f;

        // Facing direction: FROM the deck TOWARD the current player's spawn.
        // Row 0 (1 pin) is closest to the bowler; row 3 (4 pins) is furthest.
        // So we step each row AWAY from the player along (-facing).
        Vector3 toPlayer  = PinFacing(currentPlayer);          // outward = toward player
        Vector3 intoLane  = -toPlayer;                         // step rows away from player
        Vector3 laneRight = Vector3.Cross(Vector3.up, toPlayer).normalized; // lateral

        int[] rowCount = { 1, 2, 3, 4 };
        int idx = 0;
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < rowCount[row]; col++)
            {
                float lateral = (col - (rowCount[row] - 1) * 0.5f) * spacing;
                float depth   = row * spacing * 0.866f - 1.25f; // equilateral triangle row offset

                Vector3 pos = deckPos
                            + laneRight * lateral
                            + intoLane  * depth
                            + Vector3.up * (spawnY - groundY);

                GameObject pin = Instantiate(pinPrefab, pos, Quaternion.identity);
                pin.name = $"Pin_{++idx}";
                pin.tag  = "Pin";
                ConfigurePin(pin, pinR, pinH);
                activePins.Add(pin);
            }
        }
    }

    void ConfigurePin(GameObject pin, float worldRadius, float worldHeight)
    {
        CapsuleCollider cc = pin.GetComponent<CapsuleCollider>();
        if (cc == null) cc = pin.AddComponent<CapsuleCollider>();
        cc.direction = 1; // Y-axis
        // Keep prefab's local dimensions — don't override radius/height here
        // because the probe already measured them correctly.

        PhysicsMaterial pm = new PhysicsMaterial("Pin");
        pm.dynamicFriction   = 0.4f;
        pm.staticFriction    = 0.4f;
        pm.bounciness        = 0.1f;
        pm.frictionCombine   = PhysicsMaterialCombine.Average;
        pm.bounceCombine     = PhysicsMaterialCombine.Minimum;
        cc.material = pm;

        Rigidbody rb = pin.GetComponent<Rigidbody>();
        if (rb == null) rb = pin.AddComponent<Rigidbody>();
        rb.mass                   = 1.5f;
        rb.linearDamping          = 0.05f;
        rb.angularDamping         = 0.05f;  // low — don't fight the fall
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.useGravity             = true;
        rb.maxAngularVelocity     = 20f;
        rb.constraints            = RigidbodyConstraints.None;
        rb.sleepThreshold         = 0.005f;
        // CoM slightly above centre so gravity commits the fall once tipped past vertical
        float localHalfH = cc.height * 0.5f;
        rb.centerOfMass = new Vector3(0, localHalfH * 0.1f, 0);

        Pin pinComp = pin.GetComponent<Pin>();
        if (pinComp == null) pin.AddComponent<Pin>();
    }

    bool IsPinDown(GameObject pin)
    {
        if (pin == null) return true;
        return Vector3.Dot(pin.transform.up, Vector3.up) < 0.7f;
    }

    int CountStanding()
    {
        int n = 0;
        foreach (var pin in activePins)
            if (pin != null && !IsPinDown(pin)) n++;
        return n;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PICKUPS
    // ══════════════════════════════════════════════════════════════════════════
    void SpawnPickup()
    {
        foreach (var pu in activePickups) if (pu) Destroy(pu);
        activePickups.Clear();

        if (powerupBoxPrefab == null) return;

        // Random spot on the lane between spawn and pin deck
        Transform spawn = ballSpawnPoints[currentPlayer];
        Vector3 sp  = spawn.position;
        Vector3 pd  = PinDeck(currentPlayer).position;
        float t     = Random.Range(0.25f, 0.75f);
        Vector3 mid = Vector3.Lerp(sp, pd, t);
        float laneHalfWidth = 0.8f;
        mid += Vector3.Cross((pd - sp).normalized, Vector3.up) * Random.Range(-laneHalfWidth, laneHalfWidth);
        mid.y = sp.y + 0.3f;

        GameObject box = Instantiate(powerupBoxPrefab, mid, Quaternion.identity);
        box.tag = "Powerup";
        // Make it a trigger so it doesn't stop the ball
        Collider col = box.GetComponent<Collider>();
        if (col == null) col = box.AddComponent<BoxCollider>();
        col.isTrigger = true;
        activePickups.Add(box);
    }

    void CheckPickupCollision()
    {
        if (activeBall == null) return;
        Vector3 ballPos = activeBall.transform.position;
        SphereCollider sc = activeBall.GetComponent<SphereCollider>();
        float r = sc != null ? sc.radius * activeBall.transform.localScale.x : 0.5f;

        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            GameObject pu = activePickups[i];
            if (pu == null) { activePickups.RemoveAt(i); continue; }

            if (Vector3.Distance(ballPos, pu.transform.position) < r + 0.5f)
            {
                PlayerState p = players[currentPlayer];
                if (p.heldPowerup == 0)
                {
                    p.heldPowerup = Random.Range(1, 5); // 1-4
                    UpdatePowerupUI(p);
                    SetStatus($"{p.name} picked up: {PowerupName(p.heldPowerup)}!");
                }
                Destroy(pu);
                activePickups.RemoveAt(i);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GUTTER GUARDS
    // ══════════════════════════════════════════════════════════════════════════
    void SpawnGutterGuards(Transform spawn, Vector3 laneForward, float ballR)
    {
        Vector3 right = Vector3.Cross(Vector3.up, laneForward);
        float   laneLen  = Vector3.Distance(spawn.position, PinDeck(currentPlayer).position);
        float   guardW   = 0.3f;
        float   guardH   = ballR * 2.5f;
        float   lateralOffset = 1.1f;

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.position    = spawn.position + laneForward * (laneLen * 0.5f) + right * side * lateralOffset;
            g.transform.localScale  = new Vector3(guardW, guardH, laneLen);
            g.transform.forward     = laneForward;
            Renderer rend = g.GetComponent<Renderer>();
            rend.material.color     = new Color(0.4f, 0.8f, 1f, 0.6f);
            gutterGuards.Add(g);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCORING  (standard 10-pin)
    // ══════════════════════════════════════════════════════════════════════════
    int ComputeScore(PlayerState p, out int[] frameTotals)
    {
        frameTotals = new int[10];
        int r = 0, total = 0;
        for (int f = 0; f < 10; f++)
        {
            if (r >= p.rollCount) break;
            int score;
            if (p.frameRolls[r] == 10) // strike
            {
                score = 10 + Safe(p, r+1) + Safe(p, r+2);
                r += 1;
            }
            else if (Safe(p, r) + Safe(p, r+1) == 10) // spare
            {
                score = 10 + Safe(p, r+2);
                r += 2;
            }
            else
            {
                score = Safe(p, r) + Safe(p, r+1);
                r += 2;
            }
            total += score;
            frameTotals[f] = total;
        }
        return total;
    }

    int Safe(PlayerState p, int idx) => idx < p.rollCount ? p.frameRolls[idx] : 0;

    // ══════════════════════════════════════════════════════════════════════════
    //  UI
    // ══════════════════════════════════════════════════════════════════════════
    void BuildScoreboard()
    {
        foreach (Transform c in scoreboardRoot) Destroy(c.gameObject);

        for (int i = 0; i < numPlayers; i++)
        {
            GameObject row = Instantiate(scoreRowPrefab, scoreboardRoot);
            TMP_Text[] cells = row.GetComponentsInChildren<TMP_Text>();
            // cells[0]=name, cells[1..10]=frames, cells[11]=total
            players[i].frameCells = cells;
            players[i].totalCell  = cells.Length > 11 ? cells[11] : null;
            players[i].rowBg      = row.GetComponent<Image>();
            if (cells.Length > 0) cells[0].text = players[i].name;
        }
    }

    void RefreshScoreRow(int idx)
    {
        PlayerState p = players[idx];
        int total = ComputeScore(p, out int[] ft);

        for (int f = 0; f < 10; f++)
        {
            if (p.frameCells == null || f + 1 >= p.frameCells.Length) break;
            p.frameCells[f + 1].text = ft[f] > 0 ? ft[f].ToString() : "";
        }
        if (p.totalCell != null) p.totalCell.text = total.ToString();

        // Highlight active row
        for (int i = 0; i < numPlayers; i++)
        {
            if (players[i].rowBg != null)
                players[i].rowBg.color = (i == currentPlayer) ? ColActive : ColInactive;
        }
    }

    void UpdateSliderUI()
    {
        if (powerSlider == null || activeBall == null) return;
        powerSlider.value = activeBall.SliderValue;
    }

    void SetStatus(string msg) { if (statusText) statusText.text = msg; }

    void SetTurnText(string pName, int bowl, int powerup)
    {
        if (turnText == null) return;
        string bowlStr = bowl == 0 ? "1st bowl" : "2nd bowl";
        string puStr   = powerup > 0 ? $"  [Q/E: {PowerupName(powerup)}]" : "";
        turnText.text  = $"{pName}  –  {bowlStr}{puStr}";
    }

    void UpdatePowerupUI(PlayerState p)
    {
        if (powerupIcon == null) return;
        powerupIcon.color  = p.heldPowerup > 0 ? Color.white : new Color(1,1,1,0);
        if (powerupLabel) powerupLabel.text = p.heldPowerup > 0 ? PowerupName(p.heldPowerup) : "";
    }

    string PowerupName(int id) => id switch
    {
        1 => "Slam Left",
        2 => "Slam Right",
        3 => "Pin Scatter",
        4 => "Gutter Guard",
        _ => ""
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  CAMERA
    // ══════════════════════════════════════════════════════════════════════════
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleCamera();
        if (activeBall != null) UpdateSliderUI();
    }

    void ToggleCamera()
    {
        bool dl = !downLaneCam.gameObject.activeSelf;
        downLaneCam.gameObject.SetActive(dl);
        birdsEyeCam.gameObject.SetActive(!dl);
    }

    void PositionDownLaneCamera(int playerIdx)
    {
        if (downLaneCam == null) return;
        Transform spawn   = ballSpawnPoints[playerIdx];
        Transform deck    = PinDeck(playerIdx);
        Vector3   inward  = (deck.position - spawn.position).normalized; // spawn → pins
        // Sit behind spawn (further out from centre), slightly elevated
        Vector3 camPos = spawn.position - inward * 3.5f + Vector3.up * 2.5f;
        downLaneCam.transform.position = camPos;
        downLaneCam.transform.LookAt(deck.position + Vector3.up * 0.3f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  END GAME
    // ══════════════════════════════════════════════════════════════════════════
    void EndGame()
    {
        int    bestScore = -1;
        string winner   = "";
        for (int i = 0; i < numPlayers; i++)
        {
            int s = ComputeScore(players[i], out _);
            if (s > bestScore) { bestScore = s; winner = players[i].name; }
        }
        SetStatus($"GAME OVER  –  {winner} wins with {bestScore}!");
        SetTurnText("", 0, 0);
        enabled = false;
    }
}

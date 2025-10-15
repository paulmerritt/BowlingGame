using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour {
    [System.Serializable]
    public class PlayerData {
        public string name;
        public Transform ballSpawn;
        public List<Powerup> inventory = new();
        public int[] frameScores = new int[10];
        public List<int> rolls = new(); // for strike/spare calculation
    }

    public PlayerData[] players; // 2–4
    public BallScript ballPrefab;
    public PinManager pinManager;
    public CameraRig camRig;

    public int currentPlayer;
    public int frameIndex; // 0..9
    int rollInFrame; // 0 or 1
    BallScript activeBall;
    public BallScript ActiveOpponentBall => FindObjectsByType<BallScript>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .FirstOrDefault(b => b != activeBall && b.HasLaunched);

    void Start() { StartFrame(); }

    void StartFrame() {
        rollInFrame = 0;
        SpawnBallForCurrent();
        pinManager.SetupFullRack();
        camRig.Follow(activeBall.transform);
    }

    void SpawnBallForCurrent() {
        var p = players[currentPlayer];
        activeBall = Instantiate(ballPrefab, p.ballSpawn.position, p.ballSpawn.rotation);
        activeBall.BeginAiming(p.ballSpawn.forward);
    }

    void Update() {
        // Aiming & launch
        if (activeBall && !activeBall.HasLaunched) activeBall.UpdateAiming(Time.deltaTime);

        // Use power during roll (if any)
        if (activeBall && activeBall.HasLaunched && Input.GetKeyDown(KeyCode.E)) {
            var p = players[currentPlayer];
            var power = p.inventory.FirstOrDefault(pow => pow && pow.CanUse(activeBall, this));
            if (power) {
                power.Activate(activeBall, this);
                p.inventory.Remove(power);
            }
        }

        // End roll when ball stops or falls off deck
        if (activeBall && activeBall.HasStopped()) OnRollComplete();
    }

    void OnRollComplete() {
        int pinsDown = pinManager.CountKnockedThisFrame();
        RecordScore(pinsDown);

        Destroy(activeBall.gameObject);

        bool strike = (rollInFrame == 0 && pinsDown == 10);
        if (strike || rollInFrame == 1) {
            // End player frame
            NextPlayerOrNextFrame();
        } else {
            // Second roll with standing pins only
            rollInFrame = 1;
            SpawnBallForCurrent();
            pinManager.MarkStandingAsThisFrameBaseline(); // so second roll counts only newly-fallen pins
            camRig.Follow(activeBall.transform);
        }
    }

    void NextPlayerOrNextFrame() {
        // Reset pins for next player
        pinManager.SetupFullRack();
        currentPlayer = (currentPlayer + 1) % players.Length;
        if (currentPlayer == 0) {
            frameIndex++;
            if (frameIndex >= 10) { EndGame(); return; }
        }
        rollInFrame = 0;
        SpawnBallForCurrent();
        camRig.Follow(activeBall.transform);
    }

    void EndGame() {
        // Tally and show results
        enabled = false;
        Debug.Log("Game Over");
        for (int i = 0; i < players.Length; i++) {
            Debug.Log($"{players[i].name} total: {players[i].frameScores.Sum()}");
        }
    }

    public void GivePowerupToCurrent(Powerup p) {
        if (!p) return;
        players[currentPlayer].inventory.Add(p);
        // TODO: UI feedback
    }

    // --- Scoring (simple, strike/spare bonuses supported via roll list) ---
    void RecordScore(int pins) {
        var P = players[currentPlayer];
        P.rolls.Add(pins);
        // Recompute frames up to current
        int roll = 0;
        for (int f = 0; f < 10; f++) {
            if (roll >= P.rolls.Count) break;
            if (IsStrike(P, roll)) {
                P.frameScores[f] = 10 + Bonus(P, roll+1, 2);
                roll += 1;
            } else if (IsSpare(P, roll)) {
                P.frameScores[f] = 10 + Bonus(P, roll+2, 1);
                roll += 2;
            } else {
                P.frameScores[f] = Sum(P, roll, 2);
                roll += 2;
            }
        }
    }
    bool IsStrike(PlayerData p, int r) => p.rolls[r] == 10;
    bool IsSpare(PlayerData p, int r) => r+1 < p.rolls.Count && p.rolls[r] + p.rolls[r+1] == 10;
    int Bonus(PlayerData p, int start, int count) {
        int s = 0; for (int i = 0; i < count; i++) if (start+i < p.rolls.Count) s += p.rolls[start+i]; return s;
    }
    int Sum(PlayerData p, int start, int count) {
        int s = 0; for (int i = 0; i < count; i++) if (start+i < p.rolls.Count) s += p.rolls[start+i]; return s;
    }
}

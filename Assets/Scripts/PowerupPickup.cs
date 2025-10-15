using UnityEngine;

public class PowerupPickup : MonoBehaviour {
    public Powerup power;
    private void OnTriggerEnter(Collider other) {
        var ball = other.GetComponent<BallScript>();
        if (!ball) return;
        var gm = FindAnyObjectByType<GameManager>();
        if (!gm) return;
        // Award to the current player
        gm.GivePowerupToCurrent(power);
        // Despawn this pickup
        Destroy(gameObject);
    }
}

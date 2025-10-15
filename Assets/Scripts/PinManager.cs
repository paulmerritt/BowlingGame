using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PinManager : MonoBehaviour {
    public Transform spawnRoot; // 10 child transforms in standard triangle layout
    public GameObject pinPrefab;

    List<Rigidbody> pins = new();
    HashSet<Rigidbody> baselineStanding = new();

    public void SetupFullRack() {
        ClearPins();
        pins.Clear();
        foreach (Transform t in spawnRoot) {
            var pin = Instantiate(pinPrefab, t.position, t.rotation, transform);
            var rb = pin.GetComponent<Rigidbody>();
            rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            pins.Add(rb);
        }
        MarkStandingAsThisFrameBaseline();
    }

    public void MarkStandingAsThisFrameBaseline() {
        baselineStanding = new HashSet<Rigidbody>(StandingPins());
    }

    IEnumerable<Rigidbody> StandingPins(float tippedDot = 0.9f) {
        foreach (var rb in pins) {
            if (!rb) continue;
            // Consider "standing" if up-vector roughly vertical and not fallen off deck
            if (Vector3.Dot(rb.transform.up, Vector3.up) > tippedDot) yield return rb;
        }
    }

    public int CountKnockedThisFrame() {
        var nowStanding = new HashSet<Rigidbody>(StandingPins());
        int knocked = baselineStanding.Count - nowStanding.Intersect(baselineStanding).Count();
        if (knocked < 0) knocked = 0;
        // For next roll we only care about newly knocked
        baselineStanding = nowStanding;
        return knocked;
    }

    void ClearPins() {
        foreach (Transform c in transform) Destroy(c.gameObject);
    }
}

using UnityEngine;

public abstract class Powerup : ScriptableObject {
    public string displayName;
    public Sprite icon;
    public abstract bool CanUse(BallScript ball, GameManager gm);
    public abstract void Activate(BallScript ball, GameManager gm);
}

[CreateAssetMenu(menuName="Powerups/Lateral Slam")]
public class PU_LateralSlam : Powerup {
    public float lateralImpulse = 1200f;
    public override bool CanUse(BallScript ball, GameManager gm) => ball && ball.HasLaunched;
    public override void Activate(BallScript ball, GameManager gm) {
        // Slam toward ball's local right or left depending on input sign
        float dir = Mathf.Sign(Input.GetAxisRaw("Horizontal")); // A/D or ←/→
        if (dir == 0) dir = 1f; // default right
        ball.Rb.AddForce(ball.transform.right * dir * lateralImpulse, ForceMode.Impulse);
    }
}

[CreateAssetMenu(menuName="Powerups/Gust Opponent")]
public class PU_GustOpponent : Powerup {
    public float gustForce = 600f;
    public float radius = 3f;
    public override bool CanUse(BallScript ball, GameManager gm) => gm && gm.ActiveOpponentBall != null;
    public override void Activate(BallScript ball, GameManager gm) {
        var target = gm.ActiveOpponentBall;
        if (!target) return;
        Vector3 sideways = Vector3.Cross(Vector3.up, target.Rb.linearVelocity.normalized);
        target.Rb.AddForce(sideways * gustForce, ForceMode.Impulse);
    }
}

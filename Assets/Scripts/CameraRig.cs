using UnityEngine;

public class CameraRig : MonoBehaviour {
    public Camera downlaneCam;
    public Camera birdsEyeCam;

    public void Follow(Transform target) {
        // parent the downlane cam to the ball with an offset
        downlaneCam.transform.SetParent(target);
        downlaneCam.transform.localPosition = new Vector3(0, 1.2f, -3.5f);
        downlaneCam.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        SetMode(downlane:true);
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Tab)) Toggle();
    }

    void Toggle() => SetMode(!downlaneCam.enabled);
    void SetMode(bool downlane) {
        downlaneCam.enabled = downlane;
        birdsEyeCam.enabled = !downlane;
    }
}

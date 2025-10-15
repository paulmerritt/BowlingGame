using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerRowUI : MonoBehaviour {
    public TMP_Text nameText;
    public TMP_Text[] frameTexts; // 10
    public TMP_Text totalText;
    public Image background;

    public void Init(string playerName) {
        nameText.text = playerName;
        for (int i = 0; i < frameTexts.Length; i++) frameTexts[i].text = "";
        totalText.text = "0";
    }

    public void UpdateRow(GameManager.PlayerData data, int currentFrame, bool isActive) {
        for (int i = 0; i < data.frameScores.Length; i++) {
            frameTexts[i].text = data.frameScores[i] > 0 ? data.frameScores[i].ToString() : "";
        }
        int sum = 0;
        foreach (var f in data.frameScores) sum += f;
        totalText.text = sum.ToString();

        background.color = isActive ? new Color(1f, 1f, 0.6f) : Color.white;
    }
}

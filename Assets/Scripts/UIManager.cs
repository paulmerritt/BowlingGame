using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour {
    [Header("References")]
    public GameManager gameManager;
    public Transform scoreboardParent;
    public GameObject playerRowPrefab;
    public Transform inventoryParent;
    public GameObject inventorySlotPrefab;
    public TMP_Text promptText;

    private List<PlayerRowUI> playerRows = new();

    void Start() {
        BuildScoreboard();
        UpdateUI();
    }

    void Update() {
        UpdateUI();
    }

    void BuildScoreboard() {
        foreach (Transform c in scoreboardParent) Destroy(c.gameObject);
        playerRows.Clear();

        foreach (var player in gameManager.players) {
            var rowObj = Instantiate(playerRowPrefab, scoreboardParent);
            var rowUI = rowObj.GetComponent<PlayerRowUI>();
            rowUI.Init(player.name);
            playerRows.Add(rowUI);
        }
    }

    void UpdateUI() {
        for (int i = 0; i < gameManager.players.Length; i++) {
            var p = gameManager.players[i];
            playerRows[i].UpdateRow(p, gameManager.frameIndex, i == gameManager.currentPlayer);
        }

        // Inventory for current player
        foreach (Transform c in inventoryParent) Destroy(c.gameObject);
        foreach (var pow in gameManager.players[gameManager.currentPlayer].inventory) {
            var slot = Instantiate(inventorySlotPrefab, inventoryParent);
            var img = slot.GetComponentInChildren<Image>();
            if (pow.icon) img.sprite = pow.icon;
            img.enabled = pow.icon;
        }

        // Prompt
        promptText.text = $"Player {gameManager.currentPlayer + 1}'s Turn - Frame {gameManager.frameIndex + 1}";
    }
}

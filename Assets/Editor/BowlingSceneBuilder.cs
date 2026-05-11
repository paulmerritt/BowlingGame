// ─────────────────────────────────────────────────────────────────────────────
//  BowlingSceneBuilder  –  Menu: Bowling ▶ Build Scene
//  Y-formation: 3 lanes at 120° apart, pins shared at the centre junction.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class BowlingSceneBuilder
{
    // ── layout ────────────────────────────────────────────────────────────────
    const int   NUM_PLAYERS  = 3;
    const float LANE_LENGTH  = 22f;   // centre to each bowler spawn
    const float LANE_WIDTH   = 3f;    // visual width of each lane arm
    const float FLOOR_Y      = 0f;
    const float FLOOR_THICK  = 0.15f;
    // The three lane angles (degrees from +Z).  120° apart = perfect Y.
    static readonly float[] LANE_ANGLES = { 180f, 300f, 60f }; // P1 top, P2 bottom-right, P3 bottom-left

    [MenuItem("Bowling/Build Scene")]
    static void BuildScene()
    {
        if (!EditorUtility.DisplayDialog("Build Bowling Scene",
            "This will DELETE everything in the current scene and rebuild it.\nContinue?",
            "Yes, rebuild", "Cancel")) return;

        // 1. Wipe scene
        foreach (var go in UnityEngine.SceneManagement.SceneManager
                               .GetActiveScene().GetRootGameObjects())
            Object.DestroyImmediate(go);

        // 2. Load prefabs
        var ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BallPrefab.prefab");
        var pinPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pin.prefab");
        if (!ballPrefab) { Debug.LogError("BallPrefab missing"); return; }
        if (!pinPrefab)  { Debug.LogError("Pin prefab missing");  return; }

        // 3. Light
        var sun = new GameObject("Sun");
        var l = sun.AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 4. Floor — large disc-ish square covering the whole Y, plus a central hub
        //    We'll make one big floor quad under everything.
        float floorSpan = LANE_LENGTH + 4f;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position   = new Vector3(0, FLOOR_Y - FLOOR_THICK * 0.5f, 0);
        floor.transform.localScale = new Vector3(floorSpan * 2f, FLOOR_THICK, floorSpan * 2f);
        SetColor(floor, new Color(0.82f, 0.74f, 0.55f));

        // 5. Lane arm visuals — one coloured rectangle per player, rotated along its angle
        var laneColors = new Color[]
        {
            new Color(0.95f, 0.82f, 0.55f),   // P1 – amber
            new Color(0.55f, 0.82f, 0.95f),   // P2 – sky blue
            new Color(0.75f, 0.95f, 0.55f),   // P3 – lime
        };

        for (int i = 0; i < NUM_PLAYERS; i++)
        {
            float   ang = LANE_ANGLES[i] * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)); // outward direction
            Vector3 mid = dir * (LANE_LENGTH * 0.5f);

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = $"LaneArm_{i+1}";
            arm.transform.position   = new Vector3(mid.x, FLOOR_Y - FLOOR_THICK * 0.5f + 0.01f, mid.z);
            arm.transform.localScale = new Vector3(LANE_WIDTH, FLOOR_THICK, LANE_LENGTH);
            arm.transform.rotation   = Quaternion.Euler(0, LANE_ANGLES[i], 0);
            Object.DestroyImmediate(arm.GetComponent<BoxCollider>()); // floor handles physics
            SetColor(arm, laneColors[i]);
        }

        // 6. Centre hub (hexagonal-ish — just a slightly elevated disc for the pin area)
        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "PinHub";
        hub.transform.position   = new Vector3(0, FLOOR_Y - FLOOR_THICK * 0.5f + 0.005f, 0);
        hub.transform.localScale = new Vector3(6f, FLOOR_THICK * 0.5f, 6f);
        Object.DestroyImmediate(hub.GetComponent<Collider>());
        SetColor(hub, new Color(0.88f, 0.88f, 0.88f));

        // 7. Gutters — thin walls along each lane edge
        for (int i = 0; i < NUM_PLAYERS; i++)
        {
            float   ang = LANE_ANGLES[i] * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            for (int side = -1; side <= 1; side += 2)
            {
                float   gutterX = LANE_WIDTH * 0.5f + 0.1f;
                Vector3 gutterMid = fwd * (LANE_LENGTH * 0.5f) + right * side * gutterX;

                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = $"Gutter_P{i+1}_{(side < 0 ? "L" : "R")}";
                g.transform.position   = new Vector3(gutterMid.x, FLOOR_Y + 0.06f, gutterMid.z);
                g.transform.localScale = new Vector3(0.2f, 0.12f, LANE_LENGTH);
                g.transform.rotation   = Quaternion.Euler(0, LANE_ANGLES[i], 0);
                SetColor(g, new Color(0.25f, 0.25f, 0.25f));
            }
        }

        // 8. Spawn points & pin decks
        var spawnsRoot = new GameObject("SpawnPoints");
        var decksRoot  = new GameObject("PinDecks");
        var spawnPoints  = new Transform[NUM_PLAYERS];
        var pinDecks     = new Transform[NUM_PLAYERS];
        var pinFacings   = new Vector3[NUM_PLAYERS]; // direction each player's pins face (toward their spawn)

        for (int i = 0; i < NUM_PLAYERS; i++)
        {
            float   ang = LANE_ANGLES[i] * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)); // centre → spawn direction

            // Spawn is LANE_LENGTH out from centre
            Vector3 spawnPos = outward * LANE_LENGTH;
            spawnPos.y = FLOOR_Y;

            var sp = new GameObject($"Spawn_P{i+1}");
            sp.transform.parent   = spawnsRoot.transform;
            sp.transform.position = spawnPos;
            // Spawn faces inward (toward centre/pins)
            sp.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            spawnPoints[i] = sp.transform;

            // Pin deck is at the centre (shared)
            var pd = new GameObject($"PinDeck_P{i+1}");
            pd.transform.parent   = decksRoot.transform;
            pd.transform.position = Vector3.zero; // ALL at centre
            pd.transform.rotation = Quaternion.LookRotation(outward, Vector3.up); // faces out toward this player's spawn
            pinDecks[i]   = pd.transform;
            pinFacings[i] = outward; // the direction the triangle "opens" toward this player
        }

        // 9. Cameras
        // Down-lane cam starts positioned for P1
        var dlGO = new GameObject("DownLaneCam");
        var dlCam = dlGO.AddComponent<Camera>();
        dlCam.fieldOfView = 55f;
        dlGO.AddComponent<AudioListener>();
        // positioned behind P1 spawn, looking inward
        {
            float ang = LANE_ANGLES[0] * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
            Vector3 spawnPos = outward * LANE_LENGTH;
            dlGO.transform.position = spawnPos + outward * 3f + Vector3.up * 2.5f;
            dlGO.transform.LookAt(Vector3.up * 0.5f);
        }

        // Birds-eye — high above centre
        var beGO = new GameObject("BirdsEyeCam");
        var beCam = beGO.AddComponent<Camera>();
        beCam.fieldOfView = 80f;
        beGO.transform.position = new Vector3(0, 35f, 0);
        beGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        beGO.SetActive(false);

        // 10. Powerup box prefab
        var powerupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PowerupBox.prefab");
        if (!powerupPrefab)
        {
            var puGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            puGO.name = "PowerupBox";
            puGO.transform.localScale = Vector3.one * 0.5f;
            SetColor(puGO, new Color(1f, 0.85f, 0f));
            puGO.GetComponent<BoxCollider>().isTrigger = true;
            var puRb = puGO.GetComponent<Rigidbody>();
            if (puRb) Object.DestroyImmediate(puRb);
            powerupPrefab = PrefabUtility.SaveAsPrefabAsset(puGO, "Assets/Prefabs/PowerupBox.prefab");
            Object.DestroyImmediate(puGO);
        }

        // 11. Score row prefab
        var scoreRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ScoreRow.prefab");
        if (!scoreRowPrefab) scoreRowPrefab = BuildScoreRowPrefab();

        // 12. Canvas & UI
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var cScaler = canvasGO.AddComponent<CanvasScaler>();
        cScaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cScaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Status bar
        var statusGO  = MakeTMP(canvasGO, "StatusText", "A/D move  ◄► aim  SPACE bowl");
        var statusTMP = statusGO.GetComponent<TMP_Text>();
        Anchor(statusGO.GetComponent<RectTransform>(), new Vector2(0.1f,0f), new Vector2(0.9f,0.07f));
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.fontSize  = 26;

        // Turn text
        var turnGO  = MakeTMP(canvasGO, "TurnText", "Player 1 – 1st bowl");
        var turnTMP = turnGO.GetComponent<TMP_Text>();
        Anchor(turnGO.GetComponent<RectTransform>(), new Vector2(0.25f,0.93f), new Vector2(0.75f,1f));
        turnTMP.alignment = TextAlignmentOptions.Center;
        turnTMP.fontSize  = 32;
        turnTMP.fontStyle = FontStyles.Bold;

        // Power slider
        var sliderGO = new GameObject("PowerSlider");
        sliderGO.transform.SetParent(canvasGO.transform, false);
        Anchor(sliderGO.AddComponent<RectTransform>(), new Vector2(0.25f,0.07f), new Vector2(0.75f,0.13f));
        var slider = BuildSlider(sliderGO);

        // Powerup icon + label
        var puIconGO = new GameObject("PowerupIcon");
        puIconGO.transform.SetParent(canvasGO.transform, false);
        Anchor(puIconGO.AddComponent<RectTransform>(), new Vector2(0.86f,0.88f), new Vector2(0.96f,0.98f));
        var puIconImg = puIconGO.AddComponent<Image>();
        puIconImg.color = new Color(1,1,1,0);

        var puLabelGO  = MakeTMP(canvasGO, "PowerupLabel", "");
        var puLabelTMP = puLabelGO.GetComponent<TMP_Text>();
        Anchor(puLabelGO.GetComponent<RectTransform>(), new Vector2(0.7f,0.88f), new Vector2(0.86f,0.98f));
        puLabelTMP.alignment = TextAlignmentOptions.Right;
        puLabelTMP.fontSize  = 20;

        // Scoreboard panel across the top
        var sbPanel = new GameObject("ScoreboardPanel");
        sbPanel.transform.SetParent(canvasGO.transform, false);
        sbPanel.AddComponent<Image>().color = new Color(0,0,0,0.6f);
        Anchor(sbPanel.GetComponent<RectTransform>(), new Vector2(0f,0.85f), new Vector2(1f,0.93f));

        var sbRoot = new GameObject("ScoreboardRoot");
        sbRoot.transform.SetParent(sbPanel.transform, false);
        var sbRootRT = sbRoot.AddComponent<RectTransform>();
        sbRootRT.anchorMin = Vector2.zero; sbRootRT.anchorMax = Vector2.one;
        sbRootRT.offsetMin = new Vector2(6,2); sbRootRT.offsetMax = new Vector2(-6,-2);
        var vlg = sbRoot.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = vlg.childControlWidth = true;
        vlg.childForceExpandHeight = vlg.childForceExpandWidth = true;
        vlg.spacing = 2;

        // 13. EventSystem
        var evSys = new GameObject("EventSystem");
        evSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
        evSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // 14. BowlingGame manager — wire everything
        var mgGO   = new GameObject("BowlingGame");
        var mgComp = mgGO.AddComponent<BowlingGame>();
        var so     = new SerializedObject(mgComp);

        so.FindProperty("numPlayers").intValue = NUM_PLAYERS;
        var namesArr = so.FindProperty("playerNames");
        namesArr.arraySize = 4;
        namesArr.GetArrayElementAtIndex(0).stringValue = "Player 1";
        namesArr.GetArrayElementAtIndex(1).stringValue = "Player 2";
        namesArr.GetArrayElementAtIndex(2).stringValue = "Player 3";
        namesArr.GetArrayElementAtIndex(3).stringValue = "Player 4";

        so.FindProperty("ballPrefab")      .objectReferenceValue = ballPrefab;
        so.FindProperty("pinPrefab")       .objectReferenceValue = pinPrefab;
        so.FindProperty("powerupBoxPrefab").objectReferenceValue = powerupPrefab;
        so.FindProperty("downLaneCam")     .objectReferenceValue = dlCam;
        so.FindProperty("birdsEyeCam")     .objectReferenceValue = beCam;
        so.FindProperty("powerSlider")     .objectReferenceValue = slider;
        so.FindProperty("statusText")      .objectReferenceValue = statusTMP;
        so.FindProperty("turnText")        .objectReferenceValue = turnTMP;
        so.FindProperty("scoreboardRoot")  .objectReferenceValue = sbRootRT;
        so.FindProperty("scoreRowPrefab")  .objectReferenceValue = scoreRowPrefab;
        so.FindProperty("powerupIcon")     .objectReferenceValue = puIconImg;
        so.FindProperty("powerupLabel")    .objectReferenceValue = puLabelTMP;
        so.FindProperty("ballMaxSpeed")    .floatValue           = 25f;
        so.FindProperty("pinDeckCenter")   .objectReferenceValue = pinDecks[0]; // legacy fallback

        var spawnArr = so.FindProperty("ballSpawnPoints");
        spawnArr.arraySize = NUM_PLAYERS;
        for (int i = 0; i < NUM_PLAYERS; i++)
            spawnArr.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];

        var deckArr = so.FindProperty("pinDeckCenters");
        deckArr.arraySize = NUM_PLAYERS;
        for (int i = 0; i < NUM_PLAYERS; i++)
            deckArr.GetArrayElementAtIndex(i).objectReferenceValue = pinDecks[i];

        // Pin facing directions — one per player, pointing FROM centre TOWARD that player's spawn
        var facingArr = so.FindProperty("pinDeckFacings");
        facingArr.arraySize = NUM_PLAYERS;
        for (int i = 0; i < NUM_PLAYERS; i++)
            facingArr.GetArrayElementAtIndex(i).vector3Value = pinFacings[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("✓ Y-formation bowling scene built. Ctrl+S to save, then Play.");
        EditorUtility.DisplayDialog("Done!",
            "Y-formation scene built!\n\n3 lanes at 120° apart, shared pin deck at centre.\n\nCtrl+S to save, then hit Play.",
            "OK");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static void SetColor(GameObject go, Color c)
    {
        var rend = go.GetComponent<Renderer>();
        if (!rend) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = c;
        rend.sharedMaterial = mat;
    }

    static GameObject MakeTMP(GameObject parent, string name, string text)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = 24; t.color = Color.white;
        t.enableWordWrapping = false;
        return go;
    }

    static Slider BuildSlider(GameObject parent)
    {
        var bg = new GameObject("BG"); bg.transform.SetParent(parent.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        var fa = new GameObject("FillArea"); fa.transform.SetParent(parent.transform, false);
        var faRT = fa.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(4,4); faRT.offsetMax = new Vector2(-4,-4);

        var fill = new GameObject("Fill"); fill.transform.SetParent(fa.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = new Color(0.2f, 0.9f, 0.3f);

        var s = parent.AddComponent<Slider>();
        s.fillRect = fillRT; s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0; s.maxValue = 1; s.value = 0; s.interactable = false;
        return s;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static GameObject BuildScoreRowPrefab()
    {
        var row = new GameObject("ScoreRow");
        row.AddComponent<RectTransform>();
        row.AddComponent<Image>().color = Color.white;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = hlg.childControlWidth = true;
        hlg.childForceExpandHeight = hlg.childForceExpandWidth = true;
        hlg.spacing = 1;

        string[] labels = { "Name","1","2","3","4","5","6","7","8","9","10","Total" };
        foreach (var lbl in labels)
        {
            var cell = new GameObject(lbl);
            cell.transform.SetParent(row.transform, false);
            cell.AddComponent<RectTransform>();
            var t = cell.AddComponent<TextMeshProUGUI>();
            t.text = lbl == "Name" ? "Player" : (lbl == "Total" ? "0" : "");
            t.fontSize = 18; t.color = Color.black;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            var le = cell.AddComponent<LayoutElement>();
            le.flexibleWidth = lbl == "Name" ? 2.5f : 1f;
        }

        var saved = PrefabUtility.SaveAsPrefabAsset(row, "Assets/Prefabs/ScoreRow.prefab");
        Object.DestroyImmediate(row);
        return saved;
    }
}
#endif

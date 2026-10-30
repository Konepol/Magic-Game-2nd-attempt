using System.Collections.Generic;
using System.Text.RegularExpressions;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs one stage: keeps the statistics (time, damage dealt and taken, kills), and when the last wave is beaten shows the results
// with a choice between the next stage and the main menu. Put one in every stage scene.
public class StageRunner : MonoBehaviour
{
    [SerializeField] WaveSpawner spawner;
    [SerializeField, Tooltip("Boss stages: the stage is cleared when this boss is defeated (instead of the waves).")] BossEnemy boss;
    [SerializeField] string enemyTag = "Enemy";
    [SerializeField] float resultsDelay = 1.5f;           // a moment to see the last enemy fall before the results appear

    GameCatalog catalog;
    Health playerHealth;
    int stage = -1;

    float damageDealt, damageTaken;
    readonly Dictionary<string, int> kills = new Dictionary<string, int>();
    int totalKills;
    float clearTime, clearedAt;
    bool cleared, shown;
    float showAt;

    // read by the pause menu
    public float Elapsed => cleared ? clearTime
        : boss != null ? (boss.FightStartedAt >= 0f ? Time.time - boss.FightStartedAt : 0f)
        : (spawner != null && spawner.StartedAt >= 0f ? Time.time - spawner.StartedAt : 0f);
    public float DamageDealt => damageDealt;
    public float DamageTaken => damageTaken;
    public int TotalKills => totalKills;
    public IReadOnlyDictionary<string, int> Kills => kills;
    public bool ResultsShown => shown;
    public bool PlayerDead => playerHealth != null && playerHealth.IsDead;

    void Awake()
    {
        if (GetComponent<PauseMenu>() == null) gameObject.AddComponent<PauseMenu>();
        catalog = GameCatalog.Load();
        if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();
        if (boss == null) boss = FindAnyObjectByType<BossEnemy>();
        GroundingAura.Clear();

        // the stage may have been opened straight from the editor instead of through the menu
        string scene = SceneManager.GetActiveScene().name;
        if (PlayerProfile.CurrentStage < 0 || catalog == null || PlayerProfile.CurrentStage >= catalog.stages.Length
            || catalog.stages[PlayerProfile.CurrentStage].sceneName != scene)
            PlayerProfile.CurrentStage = Mathf.Max(PlayerProfile.StageOfScene(catalog, scene), 0);
        stage = PlayerProfile.CurrentStage;
    }

    void OnEnable()
    {
        Health.AnyDamageApplied += OnDamage;
        Health.AnyDied += OnDied;
        if (spawner != null) spawner.Cleared += OnCleared;
        if (boss != null) boss.Defeated += OnCleared;
    }

    void OnDisable()
    {
        Health.AnyDamageApplied -= OnDamage;
        Health.AnyDied -= OnDied;
        if (spawner != null) spawner.Cleared -= OnCleared;
        if (boss != null) boss.Defeated -= OnCleared;
    }

    void Start()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) playerHealth = go.GetComponentInParent<Health>();
    }

    void OnDestroy() => Time.timeScale = 1f;

    void OnDamage(Health h, float applied)
    {
        if (cleared) return;
        if (h == playerHealth) damageTaken += applied;
        else if (h != null && h.CompareTag(enemyTag)) damageDealt += applied;
    }

    void OnDied(Health h)
    {
        // the boss reports its defeat just before this event, so a kill right after the clear still counts
        if ((cleared && Time.time - clearedAt > 0.5f) || h == null || !h.CompareTag(enemyTag)) return;
        string type = Nice(h.gameObject.name);
        kills[type] = kills.TryGetValue(type, out int n) ? n + 1 : 1;
        totalKills++;
    }

    static string Nice(string objectName)
    {
        string n = objectName.Replace("(Clone)", "").Trim();
        return Regex.Replace(n, "(?<=[a-z])(?=[A-Z])", " ");
    }

    void OnCleared()
    {
        if (cleared) return;
        cleared = true;
        clearedAt = Time.time;
        clearTime = boss != null && boss.FightStartedAt >= 0f ? Time.time - boss.FightStartedAt
                  : spawner != null && spawner.StartedAt >= 0f ? Time.time - spawner.StartedAt : Time.timeSinceLevelLoad;
        showAt = Time.unscaledTime + resultsDelay;
    }

    void Update()
    {
        if (cleared && !shown && Time.unscaledTime >= showAt && (playerHealth == null || !playerHealth.IsDead)) ShowResults();
    }

    // ---------- results screen ----------

    void ShowResults()
    {
        shown = true;

        // freeze the game and hand the mouse to the menu
        Time.timeScale = 0f;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var inputs = player.GetComponentInParent<StarterAssetsInputs>();
            if (inputs != null)
            {
                inputs.MoveInput(Vector2.zero);
                inputs.LookInput(Vector2.zero);
                inputs.JumpInput(false);
                inputs.SprintInput(false);
                inputs.cursorLocked = false;
            }
            var playerInput = player.GetComponentInParent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;
            foreach (var caster in player.GetComponentsInChildren<PlayerSpellCaster>()) caster.enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UIKit.EnsureEventSystem();
        SpellFX.Play(Sfx.Ready, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);

        bool best = PlayerProfile.SubmitTime(stage, clearTime);
        UIKit.BeginAdopt();                // use the baked prefab (Resources/UI/StageResults) when there is one
        BuildResults(best);
        UIKit.EndAdopt();
    }

    // used by the UI baker (Tools > Magic > Bake UI Prefabs): builds the screen without a prefab, with no kills listed, and returns its canvas
    public GameObject BuildForBake()
    {
        catalog = GameCatalog.Load();
        stage = 0;
        clearTime = 95f;
        return BuildResults(false);
    }

    GameObject BuildResults(bool best)
    {
        var canvas = UIKit.MakeCanvas("StageResults", 200);

        var dim = UIKit.Box(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        UIKit.Stretch(dim.rectTransform);

        var panel = UIKit.Box(canvas.transform, "Panel", UIKit.PanelColor);
        UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 860f));

        string stageName = catalog != null && stage >= 0 && stage < catalog.stages.Length ? catalog.stages[stage].displayName : "Stage";
        var title = UIKit.Label(panel.transform, "Title", stageName.ToUpper() + " CLEARED", 66, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(940f, 100f));

        float y = 250f;
        Row(panel.transform, ref y, "Time", PlayerProfile.FormatTime(clearTime) + (best ? "   NEW BEST!" : ""), best ? UIKit.Gold : Color.white);
        Row(panel.transform, ref y, "Damage dealt", Mathf.RoundToInt(damageDealt).ToString(), Color.white);
        Row(panel.transform, ref y, "Damage taken", Mathf.RoundToInt(damageTaken).ToString(), Color.white);
        Row(panel.transform, ref y, "Enemies defeated", totalKills.ToString(), Color.white);

        // kills by type, smaller
        var types = new List<string>(kills.Keys);
        types.Sort();
        foreach (var t in types)
        {
            var line = UIKit.Label(panel.transform, "Kill_" + t, t + "   x" + kills[t], 28, UIKit.Dim, TextAnchor.MiddleCenter);
            UIKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y + 45f), new Vector2(700f, 40f));
            y -= 42f;
        }

        // next stage or main menu
        int next = stage + 1;
        bool hasNext = catalog != null && next < catalog.stages.Length && Application.CanStreamedLevelBeLoaded(catalog.stages[next].sceneName);

        var nextButton = UIKit.MakeButton(panel.transform, hasNext ? "Next Stage" : "Next Stage (coming soon)", new Vector2(520f, 80f), () => LoadStage(next), 32, "NextStage");
        UIKit.Place((RectTransform)nextButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(520f, 80f));
        nextButton.interactable = hasNext;
        nextButton.GetComponentInChildren<Text>().color = hasNext ? Color.white : UIKit.Dim;

        var menuButton = UIKit.MakeButton(panel.transform, "Main Menu", new Vector2(520f, 80f), GoToMenu, 32);
        UIKit.Place((RectTransform)menuButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(520f, 80f));
        return canvas.gameObject;
    }

    static void Row(Transform parent, ref float y, string label, string value, Color valueColor)
    {
        var l = UIKit.Label(parent, label, label, 40, UIKit.Dim, TextAnchor.MiddleLeft, true);
        UIKit.Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-250f, y), new Vector2(430f, 60f));
        var v = UIKit.Label(parent, label + "Value", value, 44, valueColor, TextAnchor.MiddleRight, true);
        v.color = valueColor;              // follows the result (gold for a new best) even when the look comes from the prefab
        UIKit.Place(v.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(190f, y), new Vector2(520f, 60f));
        y -= 72f;
    }

    void LoadStage(int index)
    {
        Time.timeScale = 1f;
        PlayerProfile.CurrentStage = index;
        SceneManager.LoadScene(catalog.stages[index].sceneName);
    }

    void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(PlayerProfile.MenuScene);
    }
}

using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Death handling: freezes input, slows time, shows a game over screen and restarts on request.
// The screen is built with UIKit, so it can be baked into a prefab and edited (Tools > Magic > Bake UI Prefabs).
public class PlayerDeath : MonoBehaviour
{
    [SerializeField] float slowMotionScale = 0.25f;
    [SerializeField] float restartPromptDelay = 1.5f;

    Health health;
    bool dead;
    float deathTime;
    CanvasGroup screen;
    GameObject prompt;

    void Awake() => health = GetComponent<Health>();

    void OnEnable()
    {
        if (health != null) health.Died += OnDied;
    }

    void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
    }

    void OnDestroy() => Time.timeScale = 1f;

    void OnDied()
    {
        if (dead) return;
        dead = true;
        deathTime = Time.unscaledTime;

        // stop the broom first so the player falls instead of hovering
        var broom = GetComponent<BroomFlight>();
        if (broom != null && broom.IsFlying) broom.Dismount();

        // zero the stored input, then switch input off
        var inputs = GetComponent<StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.JumpInput(false);
            inputs.SprintInput(false);
        }
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;

        foreach (var caster in GetComponentsInChildren<PlayerSpellCaster>()) caster.enabled = false;
        var mana = GetComponent<Mana>();
        if (mana != null) mana.enabled = false;

        UIKit.EnsureEventSystem();
        UIKit.BeginAdopt();                // use the baked prefab (Resources/UI/DeathScreen) when there is one
        BuildScreen();
        UIKit.EndAdopt();

        Time.timeScale = slowMotionScale;
        SpellFX.Play(Sfx.Death, transform.position);
        CameraShake.Add(0.6f);
    }

    // used by the UI baker (Tools > Magic > Bake UI Prefabs): builds the screen without a prefab and returns its canvas
    public GameObject BuildForBake()
    {
        var canvas = BuildScreen();
        screen.alpha = 1f;                 // visible and complete while you edit it
        prompt.SetActive(true);
        return canvas;
    }

    GameObject BuildScreen()
    {
        var canvas = UIKit.MakeCanvas("DeathScreen", 250);
        screen = canvas.GetComponent<CanvasGroup>();
        if (screen == null) screen = canvas.gameObject.AddComponent<CanvasGroup>();
        screen.interactable = false;
        screen.blocksRaycasts = false;
        screen.alpha = 0f;

        var dim = UIKit.Box(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        UIKit.Stretch(dim.rectTransform);

        var title = UIKit.Label(canvas.transform, "Title", "YOU DIED", 110, new Color(0.9f, 0.15f, 0.1f), TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1400f, 160f));

        var hint = UIKit.Label(canvas.transform, "Prompt", "Press R, Enter or click to restart      M: main menu", 36, Color.white, TextAnchor.MiddleCenter);
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1600f, 60f));
        prompt = hint.gameObject;
        prompt.SetActive(false);
        return canvas.gameObject;
    }

    void Update()
    {
        if (!dead) return;

        float t = Time.unscaledTime - deathTime;
        if (screen != null) screen.alpha = Mathf.Clamp01(t / 1.5f);
        if (t < restartPromptDelay) return;
        if (prompt != null && !prompt.activeSelf) prompt.SetActive(true);

        bool restart = (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                       || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (restart)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else if (Keyboard.current != null && (Keyboard.current.mKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
                 && Application.CanStreamedLevelBeLoaded(PlayerProfile.MenuScene))
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(PlayerProfile.MenuScene);
        }
    }
}

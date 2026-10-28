using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Placeholder death handling: freezes input, slows time, shows a game over screen and restarts on request.
public class PlayerDeath : MonoBehaviour
{
    [SerializeField] float slowMotionScale = 0.25f;
    [SerializeField] float restartPromptDelay = 1.5f;

    Health health;
    bool dead;
    float deathTime;
    GUIStyle bigStyle, smallStyle;

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

        Time.timeScale = slowMotionScale;
        SpellFX.Play(Sfx.Death, transform.position);
        CameraShake.Add(0.6f);
    }

    void Update()
    {
        if (!dead || Time.unscaledTime - deathTime < restartPromptDelay) return;

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

    void OnGUI()
    {
        if (!dead) return;

        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 72, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
            bigStyle.normal.textColor = new Color(0.9f, 0.15f, 0.1f);
            smallStyle.normal.textColor = Color.white;
        }

        float t = Time.unscaledTime - deathTime;

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / 1.5f) * 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 1f));

        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 100f), "YOU DIED", bigStyle);
        if (t >= restartPromptDelay)
            GUI.Label(new Rect(0, Screen.height * 0.35f + 100f, Screen.width, 40f), "Press R, Enter or click to restart      M: main menu", smallStyle);
        GUI.color = old;
    }
}

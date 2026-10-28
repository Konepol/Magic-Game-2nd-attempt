using UnityEngine;

// The sounds and particle effects the game uses, in one asset so they can be swapped for your own.
// Lives at Assets/_Assets/Resources/SpellFXLibrary.asset (made by Tools > Magic > Build FX Assets).
// Any slot left empty falls back to the built-in generated placeholder.
[CreateAssetMenu(menuName = "Magic/FX Library", fileName = "SpellFXLibrary")]
public class SpellFXLibrary : ScriptableObject
{
    [Header("Sounds")]
    public AudioClip cast;
    public AudioClip laser;
    public AudioClip explosion;
    public AudioClip hit;
    public AudioClip barrierHit;
    public AudioClip barrierPlace;
    public AudioClip playerHurt;
    public AudioClip death;
    public AudioClip wave;
    public AudioClip charge;
    public AudioClip fizzle;
    public AudioClip ready;

    [Header("Playback")]
    [Range(0f, 1f)] public float volume = 0.8f;
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;   // each play is a little different so repeats do not sound identical

    [Header("Particle effects (one-shot prefabs)")]
    public GameObject explosionFx;      // scaled by the blast radius, tinted by the spell colour
    public GameObject hitSparkFx;
    public GameObject barrierBreakFx;
    public GameObject castFlashFx;

    public AudioClip Get(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Cast: return cast;
            case Sfx.Laser: return laser;
            case Sfx.Explosion: return explosion;
            case Sfx.Hit: return hit;
            case Sfx.BarrierHit: return barrierHit;
            case Sfx.BarrierPlace: return barrierPlace;
            case Sfx.PlayerHurt: return playerHurt;
            case Sfx.Death: return death;
            case Sfx.Wave: return wave;
            case Sfx.Charge: return charge;
            case Sfx.Fizzle: return fizzle;
            case Sfx.Ready: return ready;
            default: return null;
        }
    }
}

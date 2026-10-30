using UnityEngine;
using UnityEngine.UI;

// Gives the menu's Glow image its soft round texture. The texture is made by the code, so this also runs in the editor: the menu then looks
// the same in the Game view while you edit the MainMenu scene as it does when playing.
[ExecuteAlways, RequireComponent(typeof(RawImage))]
public class MenuGlow : MonoBehaviour
{
    void OnEnable()
    {
        var raw = GetComponent<RawImage>();
        if (raw.texture == null) raw.texture = MainMenu.GlowTexture();
    }
}

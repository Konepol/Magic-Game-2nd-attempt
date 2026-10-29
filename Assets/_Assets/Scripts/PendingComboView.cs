using UnityEngine;
using UnityEngine.UI;

// The combined spell that is ready to be cast, above the health bar: its name, the key to hold, its cost, and a bar that runs down with the
// time left before it goes away. It disappears when the spell is cast (or charging mana removes it).
public class PendingComboView
{
    readonly PlayerSpellCaster caster;
    readonly RectTransform box, fill;
    readonly Text nameText, hintText;
    readonly Image fillImage;

    public PendingComboView(Transform parent, PlayerSpellCaster caster)
    {
        this.caster = caster;

        var bg = UIKit.Box(parent, "ReadyCombo", new Color(0.06f, 0.07f, 0.12f, 0.92f));
        box = bg.rectTransform;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.anchoredPosition = new Vector2(0f, 250f);
        box.sizeDelta = new Vector2(470f, 92f);

        nameText = UIKit.Label(box, "Name", "", 30, new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleCenter, true);
        nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        nameText.rectTransform.sizeDelta = new Vector2(0f, 38f);

        hintText = UIKit.Label(box, "Hint", "", 20, Color.white, TextAnchor.MiddleCenter);
        hintText.supportRichText = true;
        hintText.rectTransform.anchorMin = new Vector2(0f, 1f);
        hintText.rectTransform.anchorMax = new Vector2(1f, 1f);
        hintText.rectTransform.pivot = new Vector2(0.5f, 1f);
        hintText.rectTransform.anchoredPosition = new Vector2(0f, -44f);
        hintText.rectTransform.sizeDelta = new Vector2(0f, 26f);

        var track = UIKit.Box(box, "Track", new Color(0f, 0f, 0f, 0.6f));
        track.rectTransform.anchorMin = new Vector2(0f, 0f);
        track.rectTransform.anchorMax = new Vector2(1f, 0f);
        track.rectTransform.pivot = new Vector2(0.5f, 0f);
        track.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        track.rectTransform.sizeDelta = new Vector2(-24f, 10f);

        fillImage = UIKit.Box(track.rectTransform, "Fill", new Color(1f, 0.82f, 0.35f));
        fill = fillImage.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;

        box.gameObject.SetActive(false);
    }

    public void Update()
    {
        bool show = caster != null && caster.HasPending;
        if (box.gameObject.activeSelf != show) box.gameObject.SetActive(show);
        if (!show) return;

        nameText.text = caster.PendingName;
        hintText.text = "Hold <color=#FFD15A>[" + PlayerSettings.LabelOf("combine") + "]</color> to cast   <color=#9FD8FF>" + Mathf.RoundToInt(caster.PendingCost) + "</color>   <color=#808080>[" + PlayerSettings.LabelOf("barrier") + "]+[" + PlayerSettings.LabelOf("combine") + "] cancel</color>";

        float left = caster.PendingLeft01;
        fill.anchorMax = new Vector2(left, 1f);
        fillImage.color = left < 0.25f ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.82f, 0.35f);
    }
}

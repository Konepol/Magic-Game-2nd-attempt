using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows a description in a text box while the pointer is over this object (used for the options in the Options screen).
public class HoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Text target;
    public string message;
    public string idleMessage;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (target != null) target.text = message;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (target != null) target.text = idleMessage;
    }
}

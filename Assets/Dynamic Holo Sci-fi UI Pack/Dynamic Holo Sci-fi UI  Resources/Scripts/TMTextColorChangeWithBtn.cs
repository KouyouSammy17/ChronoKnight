 

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;


 
public class TMTextColorChangeWithBtn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,  IDeselectHandler, ISelectHandler
{
    public Color normalColor;
    public Color highlightColor;

    public TextMeshProUGUI targetText;

    public bool isSelect = false;

    public void Start()
    {
        normalColor = targetText.color;
    }
 
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetText.color = highlightColor;

    }

    public void OnPointerExit(PointerEventData eventData)
    {
 
        if (isSelect == false)
            targetText.color = normalColor;
    }

 
    public void OnDeselect(BaseEventData eventData)
    {
        isSelect = false;
        targetText.color = normalColor;
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelect = true;
        targetText.color = highlightColor;
    }
}

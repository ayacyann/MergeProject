using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Slot : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    public bool isSpawnOnFirstLoad = true;
    public Item target = null;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (target==null)
        {
            ShowHighlight(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (target == null)
        {
            ShowHighlight(false);
        }
    }

    public void ShowHighlight(bool isShow)
    {
        
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class ShowFrameOnSelected : MonoBehaviour, IDeselectHandler, ISelectHandler
{

    public Transform Frame;
    private void Start()
    {

 

    }

    public void OnDeselect(BaseEventData data)
    {
       if(Frame != null)
        {
            Frame.gameObject.SetActive(false);
        }
    }

    public void OnSelect(BaseEventData data)
    {
        if (Frame != null)
        {
            Frame.gameObject.SetActive(true);
        }
    }

    
}

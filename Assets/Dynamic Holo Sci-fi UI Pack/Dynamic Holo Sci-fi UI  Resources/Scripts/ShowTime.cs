using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowTime : MonoBehaviour
{
 
    public List<GameObjectFloatPair> list = new List<GameObjectFloatPair>();

    private void Start()
    {
        Hide();
        Show();
    }


    public void Show()
    {
        if (list.Count == 0) return;
        //Hide();

        if (ActionController.Instance == null) return;
 
        foreach (GameObjectFloatPair pair in list)
        {
            ActionController.Instance.DelayToCall(() => { pair.Key.SetActive(true); }, pair.Value);

        }
    }

    public  void Hide()
    {
        foreach (GameObjectFloatPair pair in list)
        {

            pair.Key.SetActive(false);

        }
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnEnable()
    {
        Show();
    }
}

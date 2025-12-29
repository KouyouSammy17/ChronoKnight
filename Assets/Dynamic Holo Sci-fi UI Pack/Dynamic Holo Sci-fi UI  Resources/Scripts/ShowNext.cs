using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowNext : MonoBehaviour
{

    public Button btnPre;
    public Button btnNext;

     public List<GameObject> Objs = new List<GameObject>();

    private void Start()
    {
        btnPre.onClick.AddListener(ShowPreUI);
        btnNext.onClick.AddListener(ShowNextUI);
    }


    int currentIndex = 0;
    void ShowPreUI()
    {
        if (Objs.Count <= 1) return;
        if (Objs[currentIndex].GetComponent<ShowTime>() != null)
        {
            Objs[currentIndex].GetComponent<ShowTime>().Hide();
        }
        Objs[currentIndex].SetActive(false);

        if (currentIndex == 0)
            currentIndex = Objs.Count - 1;
        else
            --currentIndex;
   
        Objs[currentIndex].SetActive(true);
        if(Objs[currentIndex].GetComponent<ShowTime>()!=null)
        {
            Objs[currentIndex].GetComponent<ShowTime>().Show();
        }

    }

    void ShowNextUI()
    {
        if (Objs.Count <= 1) return;
        if (Objs[currentIndex].GetComponent<ShowTime>() != null)
        {
            Objs[currentIndex].GetComponent<ShowTime>().Hide();
        }
        Objs[currentIndex].SetActive(false);
        ++currentIndex;
        if(currentIndex == Objs.Count)
        {
            currentIndex = 0;
        }
        Objs[currentIndex].SetActive(true);
        if (Objs[currentIndex].GetComponent<ShowTime>() != null)
        {
            Objs[currentIndex].GetComponent<ShowTime>().Show();
        }

    }
}

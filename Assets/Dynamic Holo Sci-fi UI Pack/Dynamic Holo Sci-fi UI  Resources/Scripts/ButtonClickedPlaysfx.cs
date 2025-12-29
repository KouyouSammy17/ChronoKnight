using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonClickedPlaysfx : MonoBehaviour
{
    

    public AudioSource ass;


    public AudioClip ac1;

    public void PlaySfx1()
    {
        ass.PlayOneShot(ac1);
    }


    public AudioClip ac2;

    public void PlaySfx2()
    {
        ass.PlayOneShot(ac2);
    }

    public AudioClip ac3;

    public void PlaySfx3()
    {
        ass.PlayOneShot(ac3);
    }

    public AudioClip ac4;

    public void PlaySfx4()
    {
        ass.PlayOneShot(ac4);
    }

    public AudioClip ac5;

    public void PlaySfx5()
    {
        ass.PlayOneShot(ac5);
    }
}

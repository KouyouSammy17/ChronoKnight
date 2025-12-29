using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class TMTextColorChange : MonoBehaviour
{
    public Animator animator;

    public Color normalColor;
    public Color highlightColor;

    private TextMeshProUGUI theText;

    private void Start()
    {
        animator = GetComponentInParent<Animator>();
        theText = GetComponent<TextMeshProUGUI>();

        normalColor = theText.color;
    }


    private void LateUpdate()
    {


        theText.color = normalColor;

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Selected") )
        {
            theText.color= highlightColor;
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Highlighted"))
        {
            theText.color = highlightColor;
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Pressed"))
        {
            theText.color = highlightColor;
        }

       

    }


 
}

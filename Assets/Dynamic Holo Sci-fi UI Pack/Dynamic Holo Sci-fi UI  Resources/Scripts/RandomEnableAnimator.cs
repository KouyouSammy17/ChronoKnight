using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomEnableAnimator : MonoBehaviour
{
    Animator am;
    // Start is called before the first frame update
   

    private void OnEnable()
    {
        am = GetComponent<Animator>();
        if (am)
        {
            am.enabled = false;
            float f = Random.Range(0.1F, 3.5f);

            StartCoroutine("Show", f);
        }
    }

    // Update is called once per frame
    IEnumerator Show(float f )
    {
        yield return new WaitForSeconds(f);
        am.enabled = true;

    }
}

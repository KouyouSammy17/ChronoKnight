using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TGUtils
{
    public class TGAutoDestroy : MonoBehaviour
    {
        public float LifeTime = 2f;

        // Start is called before the first frame update
        void Start()
        {
            Destroy(gameObject, LifeTime);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

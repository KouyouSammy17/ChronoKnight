

using System;
using UnityEngine;

[Serializable]
public class GameObjectFloatPair
{
    public GameObject Key;
    public float Value;

    public GameObjectFloatPair(GameObject key, float value)
    {
        Key = key;
        Value = value;
    }
}

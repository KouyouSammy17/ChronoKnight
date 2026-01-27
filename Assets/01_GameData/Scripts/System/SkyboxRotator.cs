using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    [SerializeField] private float _degreesPerSecond = 1f;
    [SerializeField] private bool _useUnscaledTime = true;

    private Material _skyMat;
    private static readonly int RotID = Shader.PropertyToID("_Rotation");

    private void Awake()
    {
        _skyMat = RenderSettings.skybox;
        if (_skyMat == null || !_skyMat.HasProperty(RotID))
        {
            Debug.LogWarning("Skybox material missing or has no _Rotation property.");
            enabled = false;
        }
    }

    private void Update()
    {
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float r = _skyMat.GetFloat(RotID);
        r = (r + _degreesPerSecond * dt) % 360f;
        _skyMat.SetFloat(RotID, r);
    }
}

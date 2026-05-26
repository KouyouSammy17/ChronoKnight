// スカイボックスマテリアルの_Rotationプロパティを毎フレーム回転させるスクリプト
using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    [SerializeField] private float _degreesPerSecond = 1f;     // 毎秒の回転角度（度）
    [SerializeField] private bool _useUnscaledTime = true;     // trueならtimeScaleに影響されないリアル時間を使う

    private Material _skyMat;                                   // 回転させるスカイボックスマテリアル
    private static readonly int RotID = Shader.PropertyToID("_Rotation"); // シェーダープロパティIDをキャッシュする

    private void Awake()
    {
        // シーンで使用中のスカイボックスマテリアルを取得する
        _skyMat = RenderSettings.skybox;
        // マテリアルが存在しないか_Rotationプロパティを持たない場合はスクリプトを無効化する
        if (_skyMat == null || !_skyMat.HasProperty(RotID))
        {
            Debug.LogWarning("Skybox material missing or has no _Rotation property.");
            enabled = false;
        }
    }

    private void Update()
    {
        // 設定に応じてリアル時間または通常のdeltaTimeを使用する
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        // 現在の回転角度を取得し、毎フレーム加算して360度でループさせる
        float r = _skyMat.GetFloat(RotID);
        r = (r + _degreesPerSecond * dt) % 360f;
        _skyMat.SetFloat(RotID, r);
    }
}

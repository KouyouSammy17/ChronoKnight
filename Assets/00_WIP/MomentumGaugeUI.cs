using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MomentumGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _momentumSlider;
    [SerializeField] private float _tweenDuration = 0.3f;
    private void Start()
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        _momentumSlider.maxValue = mm.MaxMomentum;
        mm.onMomentumChanged.AddListener(OnMomentumChanged);
        OnMomentumChanged(mm.CurrentMomentum);
    }

    private void OnMomentumChanged(float m)
    {
        _momentumSlider.DOValue(m, _tweenDuration).SetEase(Ease.OutQuad);
    }

    private void OnDestroy()
    {
        if (MomentumManager.Instance != null)
            MomentumManager.Instance.onMomentumChanged.RemoveListener(OnMomentumChanged);
    }
}

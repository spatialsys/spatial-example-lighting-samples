using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvatarLightControl : MonoBehaviour
{
    [SerializeField] private Light _light;
    [SerializeField] private AreaVolume _areaVolume;
    private float _lightIntensity = 1f;
    private void Awake()
    {
        _lightIntensity = _light.intensity;
        _areaVolume.OnValueChanged += OnAreaVolumeValueChanged;
    }
    private void OnDestroy()
    {
        _areaVolume.OnValueChanged -= OnAreaVolumeValueChanged;
    }
    private void OnAreaVolumeValueChanged(float value)
    {
        _light.intensity = (1f - value) * _lightIntensity;
    }
}

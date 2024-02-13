using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LightingSamples
{
    public class EnvironmentBlender : MonoBehaviour
    {
        [SerializeField] private EnvironmentSetting[] _environmentSettings;
        [SerializeField] private AreaVolume _areaVolume;

        private void Awake()
        {
            _areaVolume.OnValueChanged += OnAreaVolumeValueChanged;
        }
        private void OnDestroy()
        {
            _areaVolume.OnValueChanged -= OnAreaVolumeValueChanged;
        }
        private void OnAreaVolumeValueChanged(float value)
        {
            // RenderSettings.fogColor = Color.Lerp(_environmentSettings[0].fogColor, _environmentSettings[1].fogColor, _volumeValueCached);
            // RenderSettings.fogDensity = Mathf.Lerp(_environmentSettings[0].fogDensity, _environmentSettings[1].fogDensity, _volumeValueCached);
        }

        public void SetEnvironment(int index)
        {
            if (index < 0 || index >= _environmentSettings.Length)
            {
                Debug.LogError("Invalid index");
                return;
            }

            // RenderSettings.fogColor = _environmentSettings[index].fogColor;
            // RenderSettings.fogDensity = _environmentSettings[index].fogDensity;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(EnvironmentBlender))]
    public class EnvironmentBlenderInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Set Environment Outdoor"))
            {
                EnvironmentBlender environmentBlender = (EnvironmentBlender)target;
                environmentBlender.SetEnvironment(0);
            }

            if (GUILayout.Button("Set Environment Cave"))
            {
                EnvironmentBlender environmentBlender = (EnvironmentBlender)target;
                environmentBlender.SetEnvironment(1);
            }
        }
    }
#endif
}
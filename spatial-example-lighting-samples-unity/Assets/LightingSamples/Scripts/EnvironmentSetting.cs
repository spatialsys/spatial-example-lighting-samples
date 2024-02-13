using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LightingSamples
{
    [CreateAssetMenu(fileName = "EnvironmentSetting_", menuName = "LightingSamples/EnvironmentSetting", order = 1)]
    public class EnvironmentSetting : ScriptableObject
    {
        public Color fogColor = new Color(0.9215686f, 0.9294118f, 0.9372549f, 1f);
        public float fogDensity = 0.001f;
    }
}

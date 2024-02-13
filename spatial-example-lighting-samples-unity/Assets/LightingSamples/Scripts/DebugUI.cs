using UnityEngine;
using UnityEngine.UI;

public class DebugUI : MonoBehaviour
{
    [SerializeField] private GameObject _canvas;
    [SerializeField] private GameObject _reflectionProbeGroup;
    [SerializeField] private Toggle _toggleReflection;

    private void Awake()
    {
        _toggleReflection.onValueChanged.AddListener(OnToggleReflectionValueChanged);
    }
    private void OnDestroy()
    {
        _toggleReflection.onValueChanged.RemoveListener(OnToggleReflectionValueChanged);
    }
    private void OnToggleReflectionValueChanged(bool value)
    {
        _reflectionProbeGroup.SetActive(value);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            _canvas.SetActive(!_canvas.activeSelf);
        }
    }
}

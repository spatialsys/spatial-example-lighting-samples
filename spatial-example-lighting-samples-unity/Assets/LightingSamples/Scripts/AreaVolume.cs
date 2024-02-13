using System;
using UnityEngine;
using SpatialSys.UnitySDK;

public class AreaVolume : MonoBehaviour
{
    public float value = 1f;
    public Action<float> OnValueChanged;

    [SerializeField] private BoxCollider _boxCollider;
    [SerializeField] private float _blendDistance = 7f;
    private Vector3 _targetPosition;
    private float _valueCached;
    private bool _isInside = false;
    private int _localAvatarLayer;

    private void Awake()
    {
        _localAvatarLayer = LayerMask.NameToLayer("AvatarLocal");
    }

    private void Update()
    {
        if (!_isInside)
        {
            _targetPosition = SpatialBridge.actorService.localActor.avatar.position;
            float distance = Vector3.Distance(_boxCollider.ClosestPoint(_targetPosition), _targetPosition);
            value = Mathf.Clamp01(1f - distance / _blendDistance);
            if (_valueCached != value)
            {
                _valueCached = value;
                OnValueChanged?.Invoke(value);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == _localAvatarLayer)
        {
            _isInside = true;
            value = 1f;
            OnValueChanged?.Invoke(value);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == _localAvatarLayer)
        {
            _isInside = false;
        }
    }

    private void OnDrawGizmos()
    {
        Matrix4x4 matrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Gizmos.DrawCube(_boxCollider.center, _boxCollider.size);
        Gizmos.matrix = matrix;
    }
}

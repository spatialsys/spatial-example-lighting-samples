using UnityEngine;

public class SphereObject : MonoBehaviour
{
    [SerializeField] private float _timeOffset = 0f;
    [SerializeField] private float _moveRange = 2f;
    [SerializeField] private float _moveSpeed = 1f;
    private Vector3 _positionInitial;

    private void Start()
    {
        _positionInitial = transform.position;
    }

    private void Update()
    {
        transform.position = _positionInitial + new Vector3(0f, (Mathf.Sin(Time.time * _moveSpeed + _timeOffset) * 0.5f + 0.5f) * _moveRange, 0f);
    }
}

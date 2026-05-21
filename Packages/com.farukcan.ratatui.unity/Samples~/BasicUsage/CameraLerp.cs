using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lerps the attached camera's transform (position and rotation) to target GameObjects.
/// Press 1-9 (or Numpad 1-9) to select the target from the array.
/// Initial target is index 0 (key 1). Fires OnExit/OnEnter events on target switch.
/// </summary>
public class CameraLerp : MonoBehaviour
{
    [SerializeField] private Transform[] _targets;
    [SerializeField] private float _lerpSpeed = 3f;
    [SerializeField] private CameraTargetEvent[] _events;

    private int _activeTargetId = -1;

    private void Start()
    {
        SetTarget(0);
    }

    private void Update()
    {
        HandleInput();

        if (_activeTargetId < 0 || _activeTargetId >= _targets.Length) return;

        Transform target = _targets[_activeTargetId];
        transform.position = Vector3.Lerp(
            transform.position, target.position, _lerpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, target.rotation, _lerpSpeed * Time.deltaTime);
    }

    private void HandleInput()
    {
        for (int i = 0; i < Mathf.Min(_targets.Length, 9); i++)
        {
            KeyCode alphaKey = KeyCode.Alpha1 + i;
            KeyCode numpadKey = KeyCode.Keypad1 + i;

            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(numpadKey))
            {
                SetTarget(i);
                break;
            }
        }
    }

    private void SetTarget(int targetId)
    {
        if (targetId < 0 || targetId >= _targets.Length) return;
        if (targetId == _activeTargetId) return;

        int previousId = _activeTargetId;
        _activeTargetId = targetId;

        InvokeEvent(previousId, e => e.OnExit);
        InvokeEvent(_activeTargetId, e => e.OnEnter);
    }

    private void InvokeEvent(int targetId, Func<CameraTargetEvent, UnityEvent> selector)
    {
        if (_events == null) return;
        foreach (var entry in _events)
        {
            if (entry.TargetId == targetId)
                selector(entry)?.Invoke();
        }
    }

    [Serializable]
    public class CameraTargetEvent
    {
        public int TargetId;
        public UnityEvent OnEnter;
        public UnityEvent OnExit;
    }
}

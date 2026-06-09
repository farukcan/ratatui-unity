using System;
using RatatuiUnity;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lerps the attached camera's transform (position and rotation) to target GameObjects.
/// Use the bottom-centered OnGUI toolbar (Previous, numbered targets, [F8] Next, terminal apps) to switch targets.
/// F8 advances to the next camera target when one is available.
/// Initial target is index 0. Fires OnExit/OnEnter events on target switch.
/// </summary>
public class CameraLerp : MonoBehaviour
{
    [SerializeField] private Transform[] _targets;
    [SerializeField] private float _lerpSpeed = 3f;
    [SerializeField] private CameraTargetEvent[] _events;

    private const float ToolbarFontVmaxPercent = 1.4f;
    private const float ButtonHeightFontRatio = 1.82f;
    private const float NavButtonWidthFontRatio = 4.68f;
    private const float NextButtonWidthFontRatio = 5.85f;
    private const float AppButtonWidthFontRatio = 7f;
    private const string PreviousButtonLabel = "Previous";
    private const string NextButtonLabel = "[F8] Next";
    private const string ConsoleButtonLabel = "[F1] Console";
    private const string NotepadButtonLabel = "[F9] Notepad";
    private const string ProfilerButtonLabel = "[F10] Profiler";
    private const string ConsoleAppId = "console";
    private const string NotepadAppId = "notepad";
    private const string ProfilerAppId = "profiler";
    private const string TargetButtonLabel1 = "1";
    private const string TargetButtonLabel2 = "2";
    private const string TargetButtonLabel3 = "3";
    private const string TargetButtonLabel4 = "4";
    private const string TargetButtonLabel5 = "5";
    private const string TargetButtonLabel6 = "6";
    private const string TargetButtonLabel7 = "7";
    private const string TargetButtonLabel8 = "8";
    private const string TargetButtonLabel9 = "9";
    private static readonly string[] TargetButtonLabels =
    {
        TargetButtonLabel1, TargetButtonLabel2, TargetButtonLabel3,
        TargetButtonLabel4, TargetButtonLabel5, TargetButtonLabel6,
        TargetButtonLabel7, TargetButtonLabel8, TargetButtonLabel9,
    };
    private const float TargetButtonWidthFontRatio = 2.34f;
    private const float ToolbarSpacingFontRatio = 0.52f;

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

    private void OnGUI()
    {
        if (_targets == null || _targets.Length == 0) return;

        ComputeToolbarMetrics(
            out float fontSize,
            out float bottomMargin,
            out float rowHeight,
            out float navButtonWidth,
            out float targetButtonWidth,
            out float buttonHeight,
            out float spacing);

        var buttonStyle = GUI.skin.button;
        int previousFontSize = buttonStyle.fontSize;
        buttonStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize));

        bool guiWasEnabled = GUI.enabled;

        GUILayout.BeginArea(new Rect(0f, Screen.height - rowHeight - bottomMargin, Screen.width, rowHeight));
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUI.enabled = guiWasEnabled && _activeTargetId > 0;
        if (GUILayout.Button(PreviousButtonLabel, GUILayout.Width(navButtonWidth), GUILayout.Height(buttonHeight)))
            SetTarget(_activeTargetId - 1);

        for (int i = 0; i < _targets.Length; i++)
        {
            GUI.enabled = guiWasEnabled && i != _activeTargetId;
            if (GUILayout.Button(GetTargetButtonLabel(i), GUILayout.Width(targetButtonWidth), GUILayout.Height(buttonHeight)))
                SetTarget(i);
        }

        float nextButtonWidth = fontSize * NextButtonWidthFontRatio;
        GUI.enabled = guiWasEnabled && _activeTargetId < _targets.Length - 1;
        if (GUILayout.Button(NextButtonLabel, GUILayout.Width(nextButtonWidth), GUILayout.Height(buttonHeight)))
            SetTarget(_activeTargetId + 1);

        GUILayout.Space(spacing);

        float appButtonWidth = fontSize * AppButtonWidthFontRatio;
        GUI.enabled = guiWasEnabled;
        if (GUILayout.Button(ConsoleButtonLabel, GUILayout.Width(appButtonWidth), GUILayout.Height(buttonHeight)))
            RatatuiTerminalApps.Toggle(ConsoleAppId);

        if (GUILayout.Button(NotepadButtonLabel, GUILayout.Width(appButtonWidth), GUILayout.Height(buttonHeight)))
            RatatuiTerminalApps.Toggle(NotepadAppId);

        if (GUILayout.Button(ProfilerButtonLabel, GUILayout.Width(appButtonWidth), GUILayout.Height(buttonHeight)))
            RatatuiTerminalApps.Toggle(ProfilerAppId);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        buttonStyle.fontSize = previousFontSize;
    }

    private static string GetTargetButtonLabel(int targetIndex)
    {
        if (targetIndex >= 0 && targetIndex < TargetButtonLabels.Length)
            return TargetButtonLabels[targetIndex];
        return (targetIndex + 1).ToString();
    }

    private static void ComputeToolbarMetrics(
        out float fontSize,
        out float bottomMargin,
        out float rowHeight,
        out float navButtonWidth,
        out float targetButtonWidth,
        out float buttonHeight,
        out float spacing)
    {
        float vmax = Mathf.Max(Screen.width, Screen.height);
        fontSize = ToolbarFontVmaxPercent * vmax / 100f;
        buttonHeight = fontSize * ButtonHeightFontRatio;
        navButtonWidth = fontSize * NavButtonWidthFontRatio;
        targetButtonWidth = fontSize * TargetButtonWidthFontRatio;
        spacing = fontSize * ToolbarSpacingFontRatio;
        bottomMargin = spacing;
        rowHeight = buttonHeight + spacing;
    }

    private void HandleInput()
    {
        if (_targets == null || _targets.Length == 0) return;
        if (Input.GetKeyDown(KeyCode.F8) && _activeTargetId < _targets.Length - 1)
            SetTarget(_activeTargetId + 1);
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

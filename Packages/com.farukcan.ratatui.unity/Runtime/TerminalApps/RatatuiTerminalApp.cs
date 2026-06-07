using System;
using UnityEngine;

namespace RatatuiUnity
{
    /// <summary>
    /// Base class for terminal apps that can be opened and closed from any scene.
    /// Provides shared toggle-key handling, touch toggle, and open/close lifecycle.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public abstract class RatatuiTerminalApp : RatatuiRenderer
    {
        private bool _isOpen;
        private int _freshOpenFrames;
        private int _touchSessionMaxCount;

        /// <summary>True when the app window is visible and receiving render/input.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Fires after open state changes. Argument is the new open state.</summary>
        public event Action<bool> OnOpenChanged;

        /// <summary>Keyboard key that toggles this app. KeyCode.None disables keyboard toggle.</summary>
        protected virtual KeyCode ToggleKey => KeyCode.None;

        /// <summary>Simultaneous touch count that toggles this app on full release.</summary>
        protected virtual int TouchToggleFingerCount => 4;

        /// <summary>Skip mouse input until BuildFrame populates area_map after opening.</summary>
        protected int FreshOpenFrames => _freshOpenFrames;

        /// <summary>Open or close the app window.</summary>
        public void SetOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;

            if (open)
            {
                RequestFocus();
                _freshOpenFrames = 1;
                OnOpened();
            }
            else
            {
                OnClosed();
            }

            OnOpenChanged?.Invoke(open);
        }

        /// <summary>Toggle the app window open/closed.</summary>
        public void Toggle() => SetOpen(!_isOpen);

        /// <summary>Called when the app opens. Override for app-specific setup.</summary>
        protected virtual void OnOpened() { }

        /// <summary>Called when the app closes. Override for app-specific cleanup.</summary>
        protected virtual void OnClosed() { }

        protected override void Update()
        {
            HandleToggleKey();

            if (_freshOpenFrames > 0) _freshOpenFrames--;

            if (!_isOpen) return;
            base.Update();
        }

        protected override void OnGUI()
        {
            if (!_isOpen) return;
            base.OnGUI();
        }

        /// <summary>
        /// Drop toggle-key events so the held toggle key does not leak into the app.
        /// Call from <see cref="OnTerminalKeyDown"/> before handling other keys.
        /// </summary>
        protected bool ShouldSuppressToggleKeyEvent(TerminalKeyEvent e)
        {
            KeyCode toggle = ToggleKey;
            if (toggle == KeyCode.None) return false;
            if (!Input.GetKey(toggle)) return false;

            if (e.Key == toggle) return true;
            if (e.Character != '\0' && IsToggleKeyCharacter(toggle, e.Character))
                return true;

            return false;
        }

        /// <summary>Skip mouse events for one frame after opening when area_map is stale.</summary>
        protected bool ShouldIgnoreMouseThisFrame() => _freshOpenFrames > 0;

        private void HandleToggleKey()
        {
            KeyCode toggle = ToggleKey;
            if (toggle != KeyCode.None && Input.GetKeyDown(toggle))
                SetOpen(!_isOpen);

            HandleTouchToggle();
        }

        private void HandleTouchToggle()
        {
            int count = Input.touchCount;
            if (count > _touchSessionMaxCount) _touchSessionMaxCount = count;

            if (count == 0 && _touchSessionMaxCount > 0)
            {
                bool fire = _touchSessionMaxCount >= TouchToggleFingerCount;
                _touchSessionMaxCount = 0;
                if (fire) SetOpen(!_isOpen);
            }
        }

        private static bool IsToggleKeyCharacter(KeyCode key, char c)
        {
            switch (key)
            {
                case KeyCode.BackQuote: return c == '`' || c == '~';
                case KeyCode.Tilde: return c == '~';
                default: return false;
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThienDao.Player
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        public float MinOrtho = 5f;
        public float MaxOrtho = 640f;
        public float ZoomStep = 1.2f;
        public Vector2 WorldSize = new Vector2(1024f, 1024f);

        public Func<bool> PointerOverUI;
        public Func<bool> KeyboardBlocked;

        public Camera Cam { get; private set; }
        public float PixelsPerCell => Screen.height / (2f * Cam.orthographicSize);

        float _targetOrtho;
        bool _dragging;
        Vector3 _dragAnchor;

        void Awake()
        {
            Cam = GetComponent<Camera>();
            Cam.orthographic = true;
            _targetOrtho = Cam.orthographicSize;
        }

        public void Focus(Vector2 center, float ortho)
        {
            transform.position = new Vector3(center.x, center.y, -10f);
            _targetOrtho = Mathf.Clamp(ortho, MinOrtho, MaxOrtho);
            Cam.orthographicSize = _targetOrtho;
            _gliding = false;
        }

        bool _gliding;
        Vector2 _glideTo;

        // Fly the camera to a place (an event in the news, …); any drag, key or wheel takes control back.
        public void GlideTo(Vector2 center, float ortho)
        {
            _glideTo = center;
            _targetOrtho = Mathf.Clamp(ortho, MinOrtho, MaxOrtho);
            _gliding = true;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            bool overUI = PointerOverUI != null && PointerOverUI();
            Vector2 mp = mouse.position.ReadValue();

            float scroll = mouse.scroll.ReadValue().y;
            if (!overUI && Mathf.Abs(scroll) > 0.01f)
            {
                _targetOrtho = Mathf.Clamp(_targetOrtho * (scroll > 0f ? 1f / ZoomStep : ZoomStep), MinOrtho, MaxOrtho);
                _gliding = false;
            }

            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            float o = Mathf.Lerp(Cam.orthographicSize, _targetOrtho, _gliding ? 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime) : k);
            if (Mathf.Abs(o - _targetOrtho) < _targetOrtho * 0.002f) o = _targetOrtho;
            if (_gliding)
            {
                // Glide: ease toward the target, zooming about the screen centre.
                Cam.orthographicSize = o;
                var pos = Vector2.Lerp(transform.position, _glideTo, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                if ((pos - _glideTo).sqrMagnitude < 0.01f && o == _targetOrtho) _gliding = false;
            }
            else
            {
                // Zoom toward the cursor: keep the world point under it fixed.
                Vector3 before = Cam.ScreenToWorldPoint(mp);
                Cam.orthographicSize = o;
                Vector3 after = Cam.ScreenToWorldPoint(mp);
                transform.position += new Vector3(before.x - after.x, before.y - after.y, 0f);
            }

            bool dragHeld = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            bool dragStart = mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            if (dragStart && !overUI) _gliding = false;
            if (dragStart && !overUI)
            {
                _dragging = true;
                _dragAnchor = Cam.ScreenToWorldPoint(mp);
            }
            if (!dragHeld) _dragging = false;
            if (_dragging)
            {
                Vector3 cur = Cam.ScreenToWorldPoint(mp);
                transform.position += new Vector3(_dragAnchor.x - cur.x, _dragAnchor.y - cur.y, 0f);
            }

            var kb = Keyboard.current;
            if (kb != null && !(KeyboardBlocked != null && KeyboardBlocked()))
            {
                Vector2 dir = Vector2.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
                float speed = Cam.orthographicSize * (kb.leftShiftKey.isPressed ? 3f : 1.4f);
                transform.position += (Vector3)(dir * (speed * Time.unscaledDeltaTime));
                if (dir != Vector2.zero) _gliding = false;
            }

            float margin = Cam.orthographicSize * 0.5f;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, -margin, WorldSize.x + margin);
            p.y = Mathf.Clamp(p.y, -margin, WorldSize.y + margin);
            transform.position = p;
        }
    }
}

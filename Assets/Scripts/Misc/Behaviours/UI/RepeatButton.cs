using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 长按连发的按钮：按下立即触发一次，按住 0.4 秒后每 0.06 秒触发一次
    /// </summary>
    internal sealed class RepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float DELAY_SEC = 0.4f;
        const float INTERVAL_SEC = 0.06f;

        public Action? OnRepeat { get; set; }
        public Selectable? Target { get; set; }

        bool _isHeld = false;
        float _timer = 0f;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Target != null && !Target.IsInteractable())
            {
                return;
            }
            _isHeld = true;
            _timer = DELAY_SEC;
            OnRepeat?.Invoke();
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            _isHeld = false;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _isHeld = false;
        }
        void OnDisable()
        {
            _isHeld = false;
        }
        void Update()
        {
            if (!_isHeld)
            {
                return;
            }
            if (Target != null && !Target.IsInteractable())
            {
                _isHeld = false;
                return;
            }
            _timer -= Time.unscaledDeltaTime;
            while (_timer <= 0f && _isHeld)
            {
                _timer += INTERVAL_SEC;
                OnRepeat?.Invoke();
            }
        }
    }
}

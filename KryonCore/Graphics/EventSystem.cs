using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;

namespace KrayonCore.Core.Events
{
    public enum EventPhase
    {
        Capture,    // De padre a hijo
        Target,     // En el objeto objetivo
        Bubble      // De hijo a padre
    }

    public abstract class Event
    {
        public bool IsPropagationStopped { get; private set; }
        public bool IsDefaultPrevented { get; private set; }
        public EventPhase Phase { get; internal set; }
        public object? Target { get; internal set; }
        public object? CurrentTarget { get; internal set; }

        public void StopPropagation()
        {
            IsPropagationStopped = true;
        }

        public void PreventDefault()
        {
            IsDefaultPrevented = true;
        }
    }

    public class MouseEvent : Event
    {
        public Vector2 Position { get; set; }
        public Vector2 ScreenPosition { get; set; }
        public MouseButton Button { get; set; }
        public bool IsPressed { get; set; }
        public bool Shift { get; set; }
        public bool Control { get; set; }
        public bool Alt { get; set; }
    }

    public class ClickEvent : MouseEvent { }
    public class MouseDownEvent : MouseEvent { }
    public class MouseUpEvent : MouseEvent { }
    public class MouseMoveEvent : MouseEvent { }
    public class MouseEnterEvent : MouseEvent { }
    public class MouseLeaveEvent : MouseEvent { }

    public interface IEventTarget
    {
        void AddEventListener<T>(Action<T> listener, bool useCapture = false) where T : Event;
        void RemoveEventListener<T>(Action<T> listener, bool useCapture = false) where T : Event;
        void DispatchEvent(Event evt);
        IEventTarget? GetParent();
        bool IsPointInside(Vector2 point);
        Vector2 GetPosition();
        Vector2 GetSize();
    }

    public class EventSystem
    {
        private static EventSystem? _instance;
        public static EventSystem Instance => _instance ??= new EventSystem();

        private Dictionary<IEventTarget, Dictionary<Type, List<EventListener>>> _eventListeners;
        private Dictionary<IEventTarget, bool> _hoveredTargets;
        private IEventTarget? _capturedTarget;
        private Vector2 _lastMousePosition;
        private Dictionary<MouseButton, Vector2> _mouseDownPositions;
        private Dictionary<MouseButton, IEventTarget?> _mouseDownTargets;
        private const float CLICK_THRESHOLD = 5f; // Píxeles de tolerancia para considerar un click

        private class EventListener
        {
            public Delegate Callback { get; set; }
            public bool UseCapture { get; set; }

            public EventListener(Delegate callback, bool useCapture)
            {
                Callback = callback;
                UseCapture = useCapture;
            }
        }

        public EventSystem()
        {
            _eventListeners = new Dictionary<IEventTarget, Dictionary<Type, List<EventListener>>>();
            _hoveredTargets = new Dictionary<IEventTarget, bool>();
            _mouseDownPositions = new Dictionary<MouseButton, Vector2>();
            _mouseDownTargets = new Dictionary<MouseButton, IEventTarget?>();
        }

        public void RegisterTarget(IEventTarget target)
        {
            if (!_eventListeners.ContainsKey(target))
            {
                _eventListeners[target] = new Dictionary<Type, List<EventListener>>();
            }
        }

        public void UnregisterTarget(IEventTarget target)
        {
            _eventListeners.Remove(target);
            _hoveredTargets.Remove(target);
        }

        public void AddEventListener<T>(IEventTarget target, Action<T> listener, bool useCapture = false) where T : Event
        {
            RegisterTarget(target);

            var eventType = typeof(T);
            if (!_eventListeners[target].ContainsKey(eventType))
            {
                _eventListeners[target][eventType] = new List<EventListener>();
            }

            _eventListeners[target][eventType].Add(new EventListener(listener, useCapture));
        }

        public void RemoveEventListener<T>(IEventTarget target, Action<T> listener, bool useCapture = false) where T : Event
        {
            if (!_eventListeners.ContainsKey(target))
                return;

            var eventType = typeof(T);
            if (!_eventListeners[target].ContainsKey(eventType))
                return;

            _eventListeners[target][eventType].RemoveAll(l =>
                l.Callback.Equals(listener) && l.UseCapture == useCapture);
        }

        public void DispatchEvent(IEventTarget target, Event evt)
        {
            evt.Target = target;

            // Fase de captura (de arriba hacia abajo)
            var ancestors = GetAncestors(target);
            evt.Phase = EventPhase.Capture;

            for (int i = ancestors.Count - 1; i >= 0; i--)
            {
                if (evt.IsPropagationStopped) break;
                evt.CurrentTarget = ancestors[i];
                InvokeListeners(ancestors[i], evt, true);
            }

            // Fase de target
            if (!evt.IsPropagationStopped)
            {
                evt.Phase = EventPhase.Target;
                evt.CurrentTarget = target;
                InvokeListeners(target, evt, false);
            }

            // Fase de bubble (de abajo hacia arriba)
            evt.Phase = EventPhase.Bubble;
            foreach (var ancestor in ancestors)
            {
                if (evt.IsPropagationStopped) break;
                evt.CurrentTarget = ancestor;
                InvokeListeners(ancestor, evt, false);
            }
        }

        private List<IEventTarget> GetAncestors(IEventTarget target)
        {
            var ancestors = new List<IEventTarget>();
            var current = target.GetParent();

            while (current != null)
            {
                ancestors.Add(current);
                current = current.GetParent();
            }

            return ancestors;
        }

        private void InvokeListeners(IEventTarget target, Event evt, bool capturePhase)
        {
            if (!_eventListeners.ContainsKey(target))
                return;

            var eventType = evt.GetType();
            if (!_eventListeners[target].ContainsKey(eventType))
                return;

            var listeners = new List<EventListener>(_eventListeners[target][eventType]);

            foreach (var listener in listeners)
            {
                if (listener.UseCapture == capturePhase)
                {
                    listener.Callback.DynamicInvoke(evt);
                    if (evt.IsPropagationStopped)
                        break;
                }
            }
        }

        public IEventTarget? GetTargetAtPosition(Vector2 position, IEnumerable<IEventTarget> targets)
        {
            IEventTarget? topTarget = null;

            foreach (var target in targets)
            {
                if (target.IsPointInside(position))
                {
                    topTarget = target;
                }
            }

            return topTarget;
        }

        public void ProcessMouseMove(Vector2 mousePosition, IEnumerable<IEventTarget> targets, KeyboardState keyboard)
        {
            var target = GetTargetAtPosition(mousePosition, targets);

            // Verificar hover
            var currentHovered = new HashSet<IEventTarget>();
            if (target != null)
            {
                var current = target;
                while (current != null)
                {
                    currentHovered.Add(current);
                    current = current.GetParent();
                }
            }

            // MouseLeave para targets que ya no están en hover
            foreach (var hoveredTarget in new List<IEventTarget>(_hoveredTargets.Keys))
            {
                if (!currentHovered.Contains(hoveredTarget))
                {
                    var leaveEvent = CreateMouseEvent<MouseLeaveEvent>(mousePosition, MouseButton.Left, false, keyboard);
                    DispatchEvent(hoveredTarget, leaveEvent);
                    _hoveredTargets.Remove(hoveredTarget);
                }
            }

            // MouseEnter para nuevos targets en hover
            foreach (var hoveredTarget in currentHovered)
            {
                if (!_hoveredTargets.ContainsKey(hoveredTarget))
                {
                    var enterEvent = CreateMouseEvent<MouseEnterEvent>(mousePosition, MouseButton.Left, false, keyboard);
                    DispatchEvent(hoveredTarget, enterEvent);
                    _hoveredTargets[hoveredTarget] = true;
                }
            }

            // MouseMove
            if (target != null)
            {
                var moveEvent = CreateMouseEvent<MouseMoveEvent>(mousePosition, MouseButton.Left, false, keyboard);
                DispatchEvent(target, moveEvent);
            }

            _lastMousePosition = mousePosition;
        }

        public void ProcessMouseDown(Vector2 mousePosition, MouseButton button, IEnumerable<IEventTarget> targets, KeyboardState keyboard)
        {
            var target = GetTargetAtPosition(mousePosition, targets);

            _mouseDownPositions[button] = mousePosition;
            _mouseDownTargets[button] = target;

            if (target != null)
            {
                var downEvent = CreateMouseEvent<MouseDownEvent>(mousePosition, button, true, keyboard);
                DispatchEvent(target, downEvent);
            }
        }

        public void ProcessMouseUp(Vector2 mousePosition, MouseButton button, IEnumerable<IEventTarget> targets, KeyboardState keyboard)
        {
            var target = GetTargetAtPosition(mousePosition, targets);

            if (target != null)
            {
                var upEvent = CreateMouseEvent<MouseUpEvent>(mousePosition, button, false, keyboard);
                DispatchEvent(target, upEvent);

                // Verificar si es un click válido
                if (_mouseDownTargets.ContainsKey(button) &&
                    _mouseDownTargets[button] == target &&
                    _mouseDownPositions.ContainsKey(button))
                {
                    var distance = Vector2.Distance(mousePosition, _mouseDownPositions[button]);
                    if (distance < CLICK_THRESHOLD)
                    {
                        var clickEvent = CreateMouseEvent<ClickEvent>(mousePosition, button, false, keyboard);
                        DispatchEvent(target, clickEvent);
                    }
                }
            }

            _mouseDownTargets.Remove(button);
            _mouseDownPositions.Remove(button);
        }

        private T CreateMouseEvent<T>(Vector2 position, MouseButton button, bool isPressed, KeyboardState keyboard) where T : MouseEvent, new()
        {
            return new T
            {
                Position = position,
                ScreenPosition = position,
                Button = button,
                IsPressed = isPressed,
                Shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift),
                Control = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl),
                Alt = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt)
            };
        }
    }
}
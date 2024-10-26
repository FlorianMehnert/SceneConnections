using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.EditorWindow
{
    public class NavigableMinimap : MiniMap
    {
        private readonly GraphView _parentGraphView;
        private Vector2 _dragStartPosition;
        private Vector2 _viewStartPosition;
        private bool _isDragging;
        private float _scaleX;
        private float _scaleY;

        public NavigableMinimap(GraphView graphView)
        {
            _parentGraphView = graphView;
            SetupCallbacks();

            anchored = true;

            // Set default minimap style
            style.width = 200;
            style.height = 200;
            style.position = Position.Absolute;
            style.right = 10;
            style.top = 10;

            // Calculate initial scale factors
            UpdateScaleFactors();
        }

        private void SetupCallbacks()
        {
            RegisterCallback<GeometryChangedEvent>(_ => UpdateScaleFactors());

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // Left mouse button
                {
                    _isDragging = true;
                    _dragStartPosition = this.WorldToLocal(evt.mousePosition);
                    _viewStartPosition = _parentGraphView.viewTransform.position;

                    // Capture the mouse to continue receiving events
                    this.CaptureMouse();
                    evt.StopPropagation();
                }
            });

            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    this.ReleaseMouse();
                    evt.StopPropagation();
                }
            });

            RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    Vector2 currentMousePos = this.WorldToLocal(evt.mousePosition);
                    Vector2 dragDelta = currentMousePos - _dragStartPosition;

                    // Apply scaled delta for smoother movement
                    Vector2 scaledDelta = new(
                        dragDelta.x * 6.0f / _scaleX,
                        dragDelta.y * 4.0f / _scaleY
                    );

                    _parentGraphView.viewTransform.position = _viewStartPosition - scaledDelta;
                    evt.StopPropagation();
                }
            });
        }

        private void UpdateScaleFactors()
        {
            _scaleX = contentRect.width / _parentGraphView.contentRect.width;
            _scaleY = contentRect.height / _parentGraphView.contentRect.height;
        }
    }
}

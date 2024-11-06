using SceneConnections.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public class ComponentGraphViewer : EditorWindow
    {
        private ComponentGraphView _graphView;
        private bool _isRefreshing;

        private void OnEnable()
        {
            _graphView = new ComponentGraphView();
            rootVisualElement.Add(_graphView);

            var setComponentGraphDrawType = new DropdownField("Set Component Graph Draw Type")
            {
                choices = { "nodes are components", "nodes are game objects", "nodes are scripts" },
                value = "nodes are game objects"
            };
            setComponentGraphDrawType.RegisterValueChangedCallback(evt =>
            {
                _graphView.SetComponentGraphDrawType(Constants.ToCgdt(evt.newValue));
            });
            rootVisualElement.Add(setComponentGraphDrawType);

            var refreshButton = new Button(() =>
                {
                    if (_isRefreshing) return;
                    _isRefreshing = true;
                    EditorApplication.delayCall += () =>
                    {
                        _graphView.RefreshGraph();
                        // Schedule a second layout pass after everything is initialized
                        EditorApplication.delayCall += () => { _isRefreshing = false; };
                    };
                })
                { text = "Refresh Graph" };
            rootVisualElement.Add(refreshButton);
        }


        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }

        [MenuItem("Window/Connections v2 #&2")]
        public static void OpenWindow()
        {
            var window = GetWindow<ComponentGraphViewer>();
            window.titleContent = new GUIContent("Enhanced Component Graph");
            window.minSize = new Vector2(800, 600);
        }
    }
}
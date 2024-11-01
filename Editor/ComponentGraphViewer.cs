using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using SceneConnections;
using SceneConnections.Editor;


public class ComponentGraphViewer : EditorWindow
{
    private ComponentGraphView _graphView;
    private bool _isRefreshing;

    public bool showScripts;

    [MenuItem("Window/Connections v2 #&2")]
    public static void OpenWindow()
    {
        var window = GetWindow<ComponentGraphViewer>();
        window.titleContent = new GUIContent("Enhanced Component Graph");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        _graphView = new ComponentGraphView();
        rootVisualElement.Add(_graphView);

        var showGraphToggle = new Toggle("Show Scripts") { value = false };
        var setComponentGraphDrawType = new DropdownField("Set Component Graph Draw Type")
        {
            choices = { "nodes are components", "nodes are game objects" },
            value = "nodes are game objects"
        };

        showGraphToggle.RegisterValueChangedCallback(evt => { _graphView.SetShowScripts(evt.newValue); });
        setComponentGraphDrawType.RegisterValueChangedCallback(evt => { _graphView.SetComponentGraphDrawType(Constants.ToCgdt(evt.newValue)); });
        rootVisualElement.Add(showGraphToggle);
        rootVisualElement.Add(setComponentGraphDrawType);

        var refreshButton = new Button(() =>
            {
                if (_isRefreshing) return;
                _isRefreshing = true;
                EditorApplication.delayCall += () =>
                {
                    _graphView.RefreshGraph();
                    // Schedule a second layout pass after everything is initialized
                    EditorApplication.delayCall += () =>
                    {
                        _graphView.ForceLayoutRefresh();
                        _isRefreshing = false;
                    };
                };
            })
            { text = "Refresh Graph" };
        rootVisualElement.Add(refreshButton);
    }


    private void OnDisable()
    {
        rootVisualElement.Remove(_graphView);
    }
}
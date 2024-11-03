using SceneConnections.Editor.Utils;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    using UnityEngine;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor;

    public class GraphViewPlayground : GraphView
    {
        private int _amountOfNodes;
        private int _batchSize;

        private readonly NodeGraphBuilder _nodeGraphBuilder;

        public GraphViewPlayground()
        {
            SetupZoom(.01f, 5.0f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();
            style.flexGrow = 1;
            style.flexShrink = 1;

            DrawToolbar();
            _nodeGraphBuilder = new NodeGraphBuilder(this);
            _nodeGraphBuilder.SetupProgressBar();

            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            switch (evt.ctrlKey)
            {
                case true when evt.keyCode == KeyCode.R:
                    _nodeGraphBuilder.BuildGraph();
                    break;
            }
        }

        private void DrawToolbar()
        {
            var toolbar = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                GUI.enabled = !_nodeGraphBuilder.GetIsProcessing();
                EditorGUI.BeginChangeCheck();
                _nodeGraphBuilder.AmountOfNodes = EditorGUILayout.IntSlider("Max Nodes", _nodeGraphBuilder.AmountOfNodes, 1, 10000);
                _nodeGraphBuilder.BatchSize = EditorGUILayout.IntSlider("Max Nodes", _nodeGraphBuilder.BatchSize, 1, _nodeGraphBuilder.AmountOfNodes);


                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    DeleteElements(graphElements.ToList());
                    _nodeGraphBuilder.InitGraphAsync();
                }

                if (_nodeGraphBuilder.PerformanceMetrics.Count > 0 && GUILayout.Button("Export Data", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _nodeGraphBuilder.ExportPerformanceData();
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });

            Add(toolbar);
            toolbar.style.position = Position.Absolute;
            toolbar.style.left = 0;
            toolbar.style.top = 0;
            toolbar.style.right = 0;
        }
    }

    public class GraphViewPlaygroundViewer : EditorWindow
    {
        private GraphView _graphView;
        private bool _isRefreshing;

        [MenuItem("Window/Connections v0 #&0")]
        public static void OpenWindow()
        {
            var window = GetWindow<GraphViewPlaygroundViewer>();
            window.titleContent = new GUIContent("GraphView Playground");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _graphView = new GraphViewPlayground();
            rootVisualElement.Add(_graphView);
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }
    }
}
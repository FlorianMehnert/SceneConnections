using SceneConnections.Editor.Utils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public class GraphViewPlayground : GraphView
    {
        private readonly EdgeBuilder _edgeConnector;

        private readonly NodeGraphBuilder _nodeGraphBuilder;
        private int _amountOfNodes;
        private int _batchSize;
        private TextField _pathTextField;

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

            _edgeConnector = new EdgeBuilder(this);
            _edgeConnector.SetupProgressBar();
            _edgeConnector.AddPorts();

            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            switch (evt.ctrlKey)
            {
                case true when evt.keyCode == KeyCode.R:
                    _nodeGraphBuilder.BuildGraph();
                    break;
                case true when evt.keyCode == KeyCode.E:
                    _edgeConnector.GenerateRandomEdgesAsync();
                    RefreshNodes();
                    break;
            }
        }

        private void DrawToolbar()
        {
            // Create an IMGUIContainer to host IMGUI-based controls.
            var toolbar = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                // Disable controls while processing
                GUI.enabled = !_nodeGraphBuilder.GetIsProcessing();

                // Slider for Max Nodes
                _nodeGraphBuilder.AmountOfNodes =
                    EditorGUILayout.IntSlider("Max Nodes", _nodeGraphBuilder.AmountOfNodes, 1, 10000);

                // Slider for Batch Size
                _nodeGraphBuilder.BatchSize = EditorGUILayout.IntSlider("Batch Size", _nodeGraphBuilder.BatchSize, 1,
                    _nodeGraphBuilder.AmountOfNodes);

                // Slider for Edge Count
                _edgeConnector.EdgeCount = EditorGUILayout.IntSlider("Edge Count", _edgeConnector.EdgeCount, 1, 100000);


                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    DeleteElements(graphElements.ToList());
                    _nodeGraphBuilder.InitGraphAsync();
                }

                if (_nodeGraphBuilder.PerformanceMetrics.Count > 0 &&
                    GUILayout.Button("Export Data", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _nodeGraphBuilder.ExportPerformanceData();
                }

                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            });

            toolbar.style.position = Position.Absolute;
            toolbar.style.left = 0;
            toolbar.style.top = 0;
            toolbar.style.right = 0;

            Add(toolbar);

            var uiElementsToolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 25 // Offset from IMGUI toolbar height
                }
            };

            _pathTextField = new TextField("Path:")
            {
                isReadOnly = true,
                style =
                {
                    flexGrow = 1
                }
            };
            uiElementsToolbar.Add(_pathTextField);

            var selectPathButton = new Button(OpenPathDialog) { text = "Choose Path" };
            uiElementsToolbar.Add(selectPathButton);

            Add(uiElementsToolbar);
        }

        private void OpenPathDialog()
        {
            // Open the folder selection dialog and store the selected path
            var path = EditorUtility.OpenFolderPanel("Select Path", "", "");

            if (!string.IsNullOrEmpty(path))
            {
                _pathTextField.value = path;
            }
        }

        private void RefreshNodes()
        {
            foreach (var node in nodes)
            {
                node.RefreshExpandedState();
                node.RefreshPorts();
            }
        }
    }

    public class GraphViewPlaygroundViewer : EditorWindow
    {
        private GraphView _graphView;
        private bool _isRefreshing;

        private void OnEnable()
        {
            _graphView = new GraphViewPlayground();
            rootVisualElement.Add(_graphView);
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }

        [MenuItem("Window/Connections v0 #&0")]
        public static void OpenWindow()
        {
            var window = GetWindow<GraphViewPlaygroundViewer>();
            window.titleContent = new GUIContent("GraphView Playground");
            window.minSize = new Vector2(800, 600);
        }
    }
}
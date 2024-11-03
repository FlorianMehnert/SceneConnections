using System.Collections.Generic;
using SceneConnections.Editor.Utils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public class GraphViewPlayground : GraphView
    {
        private int _amountOfNodes;
        private readonly List<Node> _nodes;

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
            
            _nodes = new List<Node>();

            DrawToolbar();

            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            switch (evt.ctrlKey)
            {
                case true when evt.keyCode == KeyCode.R:
                    DeleteElements(graphElements.ToList());
                    InitGraph();
                    break;
            }
        }

        /// <summary>
        /// just draws nodes and layouts them: 6 seconds for 10k nodes with layout
        /// </summary>
        private void InitGraph()
        {
            for (var i = 0; i < _amountOfNodes; i++)
            {
                var node = new Node { title = "Node " + i };
                _nodes.Add(node);
                AddElement(node);
            }

            NodeLayoutManager.LayoutNodes(_nodes);
        }

        private void DrawToolbar()
        {
            var toolbar = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                // Node limit slider
                EditorGUI.BeginChangeCheck();
                _amountOfNodes = EditorGUILayout.IntSlider("Max Nodes", _amountOfNodes, 1, 10000);
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
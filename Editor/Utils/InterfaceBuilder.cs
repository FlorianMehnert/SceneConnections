using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor.Utils
{
    public class InterfaceBuilder
    {
        private readonly IConnectionGraphView _gv;

        public InterfaceBuilder(IConnectionGraphView gv)
        {
            _gv = gv;
        }

        private void DrawToolbar(VisualElement parentElement = null)
        {
            parentElement ??= (VisualElement)_gv;
            var toolbar = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                GUI.enabled = !_gv.IsBusy;
                EditorGUI.BeginChangeCheck();
                _gv.NodeGraphBuilder.AmountOfNodes = EditorGUILayout.IntSlider("Maximal Amount of Nodes",
                    _gv.NodeGraphBuilder.AmountOfNodes, 1, 10000);
                _gv.NodeGraphBuilder.BatchSize = EditorGUILayout.IntSlider("Batch Size", _gv.NodeGraphBuilder.BatchSize, 1,
                    _gv.NodeGraphBuilder.AmountOfNodes);

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    _gv.DeleteElements(_gv.GraphElements);
                    _gv.NodeGraphBuilder.InitGraphAsync();
                }

                if (_gv.NodeGraphBuilder.PerformanceMetrics.Count > 0 &&
                    GUILayout.Button("Export Data", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _gv.NodeGraphBuilder.ExportPerformanceData();
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });

            _gv.Add(toolbar);

            // Add additional UI elements below the IMGUI toolbar
            var uiElementsToolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    paddingTop = 5
                }
            };

            _gv.PathTextField = new TextField("Path:")
            {
                isReadOnly = true,
                style =
                {
                    flexGrow = 1
                }
            };

            var selectPathButton = new Button(OpenPathDialog) { text = "Choose Path" };

            uiElementsToolbar.Add(_gv.PathTextField);
            uiElementsToolbar.name = "file_chooser";
            uiElementsToolbar.Add(selectPathButton);

            _gv.Add(uiElementsToolbar);
        }

        internal void SetupUI()
        {
            var mainContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1
                }
            };
            var leftContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1
                }
            };
            var minimap = new NavigableMinimap((GraphView)_gv);
            minimap.SetPosition(new Rect(3, 55, 212, 100));
            minimap.anchored = true;
            DrawToolbar(leftContainer);
            mainContainer.Add(leftContainer);
            mainContainer.Add(minimap);
            CreateSearchBar();
        }

        private void OpenPathDialog()
        {
            var path = EditorUtility.OpenFolderPanel("Select Path", "", "");

            if (string.IsNullOrEmpty(path)) return;
            _gv.PathTextFieldValue = path;
            Debug.Log(path);
            Debug.Log(_gv.PathTextFieldValue);
        }

        private void CreateSearchBar(VisualElement parentElement = null)
        {
            parentElement ??= (VisualElement)_gv;
            _gv.SearchField = new TextField
            {
                style =
                {
                    left = 0,
                    width = 200,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                }
            };
            
            _gv.SearchField.RegisterValueChangedCallback(_gv.OnSearchTextChanged);
            parentElement.Add(_gv.SearchField);
        }
    }
}
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor.Utils
{
    public class InterfaceBuilder
    {
        private readonly IConnectionGraphView _graphView;

        public InterfaceBuilder(IConnectionGraphView graphView)
        {
            _graphView = graphView;
        }

        public void OpenPathDialog()
        {
            var path = EditorUtility.OpenFolderPanel("Select Path", "", "");

            if (string.IsNullOrEmpty(path)) return;
            _graphView.TextFieldValue = path;
            Debug.Log(path);
            Debug.Log(_graphView.TextFieldValue);
        }

        public Action<string> SearchTextChangedCallback { get; set; }

        public void CreateSearchBar(VisualElement parentElement = null)
        {
            _graphView.SearchField = new TextField
            {
                style =
                {
                    top = 25,
                    left = 0,
                    width = 200,
                    position = Position.Absolute,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f)
                }
            };

            _graphView.SearchField.RegisterValueChangedCallback(_graphView.OnSearchTextChanged);
            if (parentElement == null)
            {
                _graphView.Add(_graphView.SearchField);
            }
            else
            {
                parentElement.Add(_graphView.SearchField);
            }
        }
    }
}
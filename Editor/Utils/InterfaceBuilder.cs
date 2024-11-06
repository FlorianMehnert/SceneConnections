using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SceneConnections.Editor.Utils
{
    public class InterfaceBuilder
    {
        private readonly IConnectionGraphView _graphView;
        public string PathTextField;

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
    }
}
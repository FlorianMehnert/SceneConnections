using UnityEditor;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public interface IConnectionGraphView
    {
        string TextFieldValue { get; set; } 
    }
}
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

public class ScriptNode : Node
{
    public MonoScript Script { get; private set; }
    public Port InputPort { get; private set; }
    public Port OutputPort { get; private set; }

    public ScriptNode(MonoScript script)
    {
        Script = script;
        title = script.name;

        // Create input port for references to this script
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(MonoScript));
        InputPort.portName = "References";
        inputContainer.Add(InputPort);

        // Create output port for references this script makes
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(MonoScript));
        OutputPort.portName = "Dependencies";
        outputContainer.Add(OutputPort);

        // Add script path in project
        var pathLabel = new Label(AssetDatabase.GetAssetPath(script));
        mainContainer.Add(pathLabel);

        RefreshExpandedState();
        RefreshPorts();
    }
}
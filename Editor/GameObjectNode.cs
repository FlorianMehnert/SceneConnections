using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class GameObjectNode : Node
{
    public bool hasSize;
    public Label contentLabel;
    
    public GameObjectNode()
    {
        title = "Game Object Node";

        // Create input and output ports
        Port inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
        inputPort.portName = "Input";
        inputContainer.Add(inputPort);

        Port outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
        outputPort.portName = "Output";
        outputContainer.Add(outputPort);

        // Add a label in the main content area
        Label contentLabel = new("no size");
        contentContainer.Add(contentLabel);

        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
    }

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("set Size", _ => Debug.Log(contentContainer.worldBound));
        evt.menu.AppendAction("Action 2", _ => Debug.Log("Action 2 triggered"), DropdownMenuAction.AlwaysEnabled);
        evt.menu.AppendAction("Delete Node", _ => RemoveFromHierarchy(), DropdownMenuAction.AlwaysEnabled);
    }
}

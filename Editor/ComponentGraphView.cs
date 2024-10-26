using UnityEngine;
using UnityEditor;

using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Edge = UnityEditor.Experimental.GraphView.Edge;
using SceneConnections.EditorWindow;

public class ComponentGraphView : GraphView
{
    private readonly Dictionary<Component, Node> _componentNodes = new();
    private readonly Dictionary<MonoScript, Node> _scriptNodes = new();
    private readonly Dictionary<GameObject, Group> _gameObjectGroups = new();
    private bool _needsLayout;
    private readonly Label _loadingLabel;

    private readonly Label _debuggingLabel;

    private int _currentDebuggedRect = 0;

    private bool _showScripts;


    public ComponentGraphView()
    {
        SetupZoom(.01f, 5.0f);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        AddMiniMap();

        var gridBackground = new GridBackground();
        Insert(0, gridBackground);
        gridBackground.StretchToParentSize();
        style.flexGrow = 1;
        style.flexShrink = 1;

        // Add the loading label to the view
        _loadingLabel = new Label("Calculating layout...")
        {
            style =
            {
                display = DisplayStyle.None,
                position = Position.Absolute,
                top = 10,
                left = 10,
                backgroundColor = new Color(.5f, 0, 0, 0.8f),
                color = Color.white
            }
        };
        Add(_loadingLabel);

        _debuggingLabel = new Label("DEBUGING_PLACEHOLDER")
        {
            style =
            {
                display = DisplayStyle.Flex,
                position = Position.Absolute,
                top = 10,
                left = 500,
                backgroundColor = new Color(.2f, .8f, .8f, 0.8f),
                color = Color.black
            }
        };
        Add(_debuggingLabel);
        RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
    }

    public void SetShowScripts(bool showScripts)
    {
        _showScripts = showScripts;
    }

    private void OnKeyDownEvent(KeyDownEvent evt)
    {
        // Check for Ctrl + R or any other shortcut key combination
        if (evt.ctrlKey && evt.keyCode == KeyCode.R)
        {
            _debuggingLabel.text = "refresh graph";
            RefreshGraph();
            evt.StopPropagation(); // Prevent further handling of the event
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.L)
        {
            _debuggingLabel.text = "layout nodes";
            LayoutNodes();
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.I)
        {
            int i = 0;
            foreach (var kvp in _gameObjectGroups)
            {
                var group = kvp.Value;
                
                if (_currentDebuggedRect < i && group.containedElements.OfType<Node>().ToArray().Count() > 1){
                    _debuggingLabel.text = "i: " + i + " cur: " + _currentDebuggedRect + " size: " + group.containedElements.OfType<Node>().ToList()[1].contentRect.ToString();
                    group.selected = true;
                    break;
                }
                ++i;
            }
            ++_currentDebuggedRect;
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.T){
            NodeUtils.OptimizeGroupLayouts(_gameObjectGroups.Values.OfType<Group>().ToArray());
        }
    }

    public void RefreshGraph()
    {
        ClearGraph();
        if (_showScripts)
        {
            CreateScriptGraph();
        }
        else
        {
            CreateComponentGraph();
        }
        _loadingLabel.style.display = DisplayStyle.Flex;
        _needsLayout = true;
        EditorApplication.delayCall += PerformLayout;
    }

    private void PerformLayout()
    {
        if (!_needsLayout) return;

        LayoutNodes();
        _loadingLabel.style.display = DisplayStyle.None;
        _needsLayout = false;
    }

    private void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        _componentNodes.Clear();
        _gameObjectGroups.Clear();
    }

    private void CreateComponentGraph()
    {
        var allGameObjects = Object.FindObjectsOfType<GameObject>();

        foreach (var gameObject in allGameObjects)
        {
            CreateGameObjectGroup(gameObject);

            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                CreateComponentNode(component);
            }
        }

        CreateEdges();
        LayoutNodes();
    }

    private void CreateScriptGraph()
    {
        // query monoscripts
        MonoScript[] scripts = AssetDatabase.FindAssets("t:MonoScript")
        .Select(guid => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(script => script != null)
        .ToArray();

        CreateNodesForScripts(scripts);

        CreateEdges();
        LayoutNodes();
    }


    private void CreateNodesForScripts(MonoScript[] scripts)
    {
        foreach (var script in scripts)
        {
            var scriptType = script.GetClass();
            if (scriptType != null)
            {
                CreateScriptNode(script);
            }
        }
    }

    private void CreateScriptNode(MonoScript script)
    {
        var node = new ScriptNode(script)
        {
            title = script.GetType().Name,
            userData = script
        };

        AddElement(node);
        _scriptNodes[script] = node;
    }


    private void CreateGameObjectGroup(GameObject gameObject)
    {
        var group = new Group
        {
            title = gameObject.name,
            userData = gameObject
        };
        AddElement(group);
        _gameObjectGroups[gameObject] = group;
    }

    private void CreateComponentNode(Component component)
    {
        var node = new Node
        {
            title = component.GetType().Name,
            userData = component
        };

        var inputPort = GeneratePort(node, Direction.Input, Port.Capacity.Multi);
        node.inputContainer.Add(inputPort);

        var outputPort = GeneratePort(node, Direction.Output, Port.Capacity.Multi);
        node.outputContainer.Add(outputPort);

        // Add component properties to the node
        AddComponentProperties(node, component);

        node.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.clickCount != 2) return;
            Selection.activeObject = component;
            EditorGUIUtility.PingObject(component);
            evt.StopPropagation();
        });

        AddElement(node);
        _componentNodes[component] = node;

        if (_gameObjectGroups.TryGetValue(component.gameObject, out var group))
        {
            group.AddElement(node);
        }
    }

    private static void AddComponentProperties(Node node, Component component)
    {
        var properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && !p.GetIndexParameters().Any());

        foreach (var property in properties.Take(1)) // Limit to 5 properties to avoid cluttering
        {
            try
            {
                var value = property.GetValue(component);
                if (value == null) continue;
                var propertyLabel = new Label($"{property.Name}: {value}");
                node.mainContainer.Add(propertyLabel);
            }
            catch
            {
                // Ignore properties that throw exceptions when accessed
            }
        }
    }

    private void CreateEdges()
    {
        foreach (var (sourceComponent, sourceNode) in _componentNodes)
        {
            var fields = sourceComponent.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (!typeof(Component).IsAssignableFrom(field.FieldType)) continue;
                var targetComponent = field.GetValue(sourceComponent) as Component;
                if (targetComponent != null && _componentNodes.TryGetValue(targetComponent, out var targetNode))
                {
                    CreateEdge(sourceNode, targetNode);
                }
            }
        }
    }

    private void CreateEdge(Node sourceNode, Node targetNode)
    {
        var edge = new Edge
        {
            output = sourceNode.outputContainer[0] as Port,
            input = targetNode.inputContainer[0] as Port
        };
        edge.input?.Connect(edge);
        edge.output?.Connect(edge);
        AddElement(edge);
    }

    private static Port GeneratePort(Node node, Direction direction, Port.Capacity capacity)
    {
        return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(Component));
    }

    /// <summary>
    /// Call to organize the layout of all nodes
    /// </summary>
    private void LayoutNodes()
    {
        float x = 0;
        float y = 0;
        float maxHeightInRow = 0;
        const float groupPadding = 50;
        const float maxWidth = 5000;

        foreach (var kvp in _gameObjectGroups)
        {
            var group = kvp.Value;
            LayoutNodesInGroup(group);
            group.UpdateGeometryFromContent();

            // Check if the group exceeds the row width
            if (x + group.contentRect.width > maxWidth)
            {
                x = 0;
                y += maxHeightInRow + groupPadding;
                maxHeightInRow = 0;
            }

            group.SetPosition(new Rect(x, y, group.contentRect.width, group.contentRect.height));

            x += group.contentRect.width + groupPadding;
            maxHeightInRow = Mathf.Max(maxHeightInRow, group.contentRect.height);
        }
    }

    /// <summary>
    /// used to move nodes within a group
    /// </summary>
    /// <param name="group">a group can contain nodes</param>
    private static void LayoutNodesInGroup(Group group)
    {
        const float nodePadding = 20;
        const float maxGroupWidth = 800;
        var currentX = nodePadding;
        float currentY = 50; // Space for group title

        var nodes = group.containedElements.OfType<Node>().ToList();
        float maxHeightInRow = 0;
        float rowWidth = 0;

        foreach (var node in nodes)
        {
            // Use a minimum width if contentRect is not yet calculated
            var nodeWidth = Mathf.Max(node.contentRect.width, 50f);
            var nodeHeight = Mathf.Max(node.contentRect.height, 100f);

            // Check if we need to move to the next row
            if (currentX + nodeWidth > maxGroupWidth)
            {
                currentX = nodePadding;
                currentY += maxHeightInRow + nodePadding;
                maxHeightInRow = 0;
            }
            // Set the position
            var newRect = new Rect(currentX, currentY, nodeWidth, nodeHeight);
            node.SetPosition(newRect);

            // Update tracking variables
            currentX += nodeWidth + nodePadding;
            maxHeightInRow = Mathf.Max(maxHeightInRow, nodeHeight);
            rowWidth = Mathf.Max(rowWidth, currentX);
        }

        // Update group size
        var finalHeight = currentY + maxHeightInRow + nodePadding;
        var finalWidth = Mathf.Min(maxGroupWidth, rowWidth + nodePadding);

        group.SetPosition(new Rect(
            group.contentRect.x,
            group.contentRect.y,
            finalWidth,
            finalHeight
        ));
    }

    // Add this helper method to force a layout refresh
    public void ForceLayoutRefresh()
    {
        if (_showScripts)
        {
            foreach (var node in _scriptNodes)
            {
            }
            EditorApplication.delayCall += () =>
            {
                LayoutNodes();
                // Force the graph view to update
                UpdateViewTransform(viewTransform.position, viewTransform.scale);
            };
        }
        else
        {
            // grab all elements of type node
            foreach (var node in _gameObjectGroups.Values.SelectMany(group => group.containedElements.OfType<Node>()))
            {
                // Force the node to calculate its layout
                //node.RefreshExpandedState();
                //node.RefreshPorts();
            }

            // Schedule the layout for the next frame
            EditorApplication.delayCall += () =>
            {
                LayoutNodes();
                // Force the graph view to update
                //UpdateViewTransform(viewTransform.position, viewTransform.scale);
            };
        }

    }


    private void AddMiniMap()
    {
        var minimap = new NavigableMinimap(this);
        minimap.SetPosition(new Rect(15, 50, 200, 100));
        Add(minimap);
    }
}
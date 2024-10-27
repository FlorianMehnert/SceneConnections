using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Edge = UnityEditor.Experimental.GraphView.Edge;
using SceneConnections.EditorWindow;
using UnityEditor.UIElements;
using System.ComponentModel;
using SceneConnections;
using Object = UnityEngine.Object;


public class ComponentGraphView : GraphView
{
    private readonly Dictionary<UnityEngine.Component, Node> _componentNodes = new();
    private readonly Dictionary<MonoScript, Node> _scriptNodes = new();
    private readonly Dictionary<GameObject, Group> _gameObjectGroups = new();

    private List<GameObjectNode> _nodes;
    private bool _needsLayout;
    private readonly Label _loadingLabel;

    private readonly Label _debuggingLabel;

    private int _currentDebuggedRect = 0;

    private bool _showScripts;

    private Constants.ComponentGraphDrawType _drawType = Constants.ComponentGraphDrawType.NodesAreGameObjects;

    private TextField _searchField;
    private readonly Color _defaultNodeColor = new(0.8f, 0.8f, 0.8f, 1f);
    private readonly Color _highlightColor = new(1f, 0.8f, 0.2f, 1f);


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

        Add(_loadingLabel);
        Add(_debuggingLabel);
        RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

        // Create and configure search bar
        CreateSearchBar();
    }

    private void CreateSearchBar()
    {
        _searchField = new TextField();
        _searchField.RegisterValueChangedCallback(OnSearchTextChanged);
        _searchField.style.position = Position.Absolute;
        _searchField.style.top = 5;
        _searchField.style.left = 5;
        _searchField.style.width = 200;
        _searchField.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        Add(_searchField);
    }

    private void OnSearchTextChanged(ChangeEvent<string> evt)
    {
        string searchText = evt.newValue.ToLowerInvariant();
        SearchNodes(searchText);
    }

    private void SearchNodes(string searchText)
    {
        // Reset all nodes to default color
        foreach (var node in _nodes)
        {
            ResetNodeColor(node);
        }

        if (string.IsNullOrEmpty(searchText))
            return;

        // Find and highlight matching nodes
        var matchingNodes = _nodes.Where(n =>
            n.title.ToLowerInvariant().Contains(searchText) ||
            IsCustomNodeMatch(n, searchText)
        );

        foreach (var node in matchingNodes)
        {
            HighlightNode(node);
        }
    }

    private static bool IsCustomNodeMatch(Node node, string searchText)
    {
        // Override this method to add custom search criteria
        // Example: searching through custom node properties
        if (node is GameObjectNode customNode)
        {
            return customNode.title.ToLowerInvariant().Contains(searchText);
        }

        return false;
    }

    private void HighlightNode(Node node)
    {
        node.style.backgroundColor = _highlightColor;
        // Optional: Add visual effects or animations here
        node.MarkDirtyRepaint();
    }

    private void ResetNodeColor(Node node)
    {
        node.style.backgroundColor = _defaultNodeColor;
        node.MarkDirtyRepaint();
    }

    public void SetShowScripts(bool showScripts)
    {
        _showScripts = showScripts;
    }

    public void SetComponentGraphDrawType(Constants.ComponentGraphDrawType drawType)
    {
        _drawType = drawType;
    }

    private void OnKeyDownEvent(KeyDownEvent evt)
    {
        if (evt.ctrlKey && evt.keyCode == KeyCode.R)
        {
            _debuggingLabel.text = "refresh graph";
            RefreshGraph();
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.L)
        {
            _debuggingLabel.text = "layout nodes";
            LayoutNodes(_drawType);
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.I)
        {
            int i = 0;
            foreach (var kvp in _gameObjectGroups)
            {
                var group = kvp.Value;

                if (_currentDebuggedRect < i && group.containedElements.OfType<Node>().ToArray().Count() > 1)
                {
                    _debuggingLabel.text = "i: " + i + " cur: " + _currentDebuggedRect + " size: " + group.containedElements.OfType<Node>().ToList()[1].contentRect.ToString();
                    group.selected = true;
                    break;
                }

                ++i;
            }

            ++_currentDebuggedRect;
            evt.StopPropagation();
        }
        else if (evt.ctrlKey && evt.keyCode == KeyCode.T)
        {
            if (_drawType == Constants.ComponentGraphDrawType.NodesAreComponents)
            {
                Group[] groups = _gameObjectGroups.Values.OfType<Group>().ToArray();
                LayoutState layoutState = NodeUtils.OptimizeGroupLayouts(groups, padding: 15f);
                layoutState.ApplyLayout();
            }
            else if (_drawType == Constants.ComponentGraphDrawType.NodesAreGameObjects)
            {
                LayoutGameObjectNodes();
            }
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
            CreateGraph(_drawType);
        }

        _loadingLabel.style.display = DisplayStyle.Flex;
        _needsLayout = true;
        EditorApplication.delayCall += PerformLayout;
    }

    private void PerformLayout()
    {
        if (!_needsLayout) return;

        LayoutNodes(_drawType);
        _loadingLabel.style.display = DisplayStyle.None;
        _needsLayout = false;
    }

    private void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        _componentNodes.Clear();
        _gameObjectGroups.Clear();
    }

    /// <summary>
    /// creating node overview using all GameObjects
    /// 
    /// </summary>
    /// <param name="style">ComponentGraphDrawType deciding wheter nodes are game objects or nodes are components grouped using groups</param>
    private void CreateGraph(Constants.ComponentGraphDrawType style = Constants.ComponentGraphDrawType.NodesAreComponents)
    {
        var allGameObjects = Object.FindObjectsOfType<GameObject>();

        switch (style)
        {
            // groups contain nodes that are components of a game object
            case Constants.ComponentGraphDrawType.NodesAreComponents:
            {
                foreach (var gameObject in allGameObjects)
                {
                    CreateGameObjectGroup(gameObject);

                    var components = gameObject.GetComponents<UnityEngine.Component>();
                    foreach (var component in components)
                    {
                        CreateComponentNode(component);
                    }
                }

                break;
            }
            // nodes contain attributes that correspond to attached components
            case Constants.ComponentGraphDrawType.NodesAreGameObjects:
            {
                _nodes = new List<GameObjectNode>();
                foreach (var gameObject in allGameObjects)
                {
                    var components = gameObject.GetComponents<UnityEngine.Component>();
                    _nodes.Add(CreateGameObjectNode(gameObject, components));
                }
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(style), style, null);
        }

        CreateEdges();
        LayoutNodes(style);
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
        LayoutNodes(_drawType);
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

    private void CreateComponentNode(UnityEngine.Component component)
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

    private GameObjectNode CreateGameObjectNode(GameObject gameObject, UnityEngine.Component[] components)
    {
        var node = new GameObjectNode
        {
            title = gameObject.name,
        };

        // Add component list
        foreach (var component in components)
        {
            var componentField = new ObjectField(component.GetType().Name)
            {
                objectType = component.GetType(),
                value = component,
                allowSceneObjects = true
            };
            node.mainContainer.Add(componentField);
        }

        // Add node to graph
        AddElement(node);
        return node;
    }

    private void LayoutGameObjectNodes()
    {
        NodeLayoutManager.LayoutNodes(_nodes);
    }

    private static void AddComponentProperties(Node node, UnityEngine.Component component)
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
                if (!typeof(UnityEngine.Component).IsAssignableFrom(field.FieldType)) continue;
                var targetComponent = field.GetValue(sourceComponent) as UnityEngine.Component;
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
        return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(UnityEngine.Component));
    }

    /// <summary>
    /// Call to organize the layout of all nodes
    /// </summary>
    private void LayoutNodes(Constants.ComponentGraphDrawType style)
    {
        if (style == Constants.ComponentGraphDrawType.NodesAreComponents)
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
        else if (style == Constants.ComponentGraphDrawType.NodesAreGameObjects)
        {
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
                LayoutNodes(_drawType);
                // Force the graph view to update
                UpdateViewTransform(viewTransform.position, viewTransform.scale);
            };
        }
        else
        {
            // Schedule the layout for the next frame
            /* EditorApplication.delayCall += () =>
            {
                LayoutNodes(_drawType);
            }; */
        }
    }


    private void AddMiniMap()
    {
        var minimap = new NavigableMinimap(this);
        minimap.SetPosition(new Rect(15, 50, 200, 100));
        Add(minimap);
    }
}

internal class ComponentGraphDrawType
{
}
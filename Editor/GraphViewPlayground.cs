using System;
using System.Diagnostics;
using System.IO;
using SceneConnections.Editor.Utils;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    using UnityEngine;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Debug = UnityEngine.Debug;

    public class PerformanceMetrics
    {
        public int BatchNumber { get; set; }
        public int NodesInBatch { get; set; }
        public double BatchCreationTime { get; set; }
        public double BatchLayoutTime { get; set; }
        public double TotalBatchTime => BatchCreationTime + BatchLayoutTime;
        public DateTime Timestamp { get; set; }
    }

    public class GraphViewPlayground : GraphView
    {
        private int _amountOfNodes;
        private int _batchSize;
        private readonly List<Node> _nodes;
        private bool _isProcessing;
        private float _progress;

        // Progress UI elements
        private IMGUIContainer _progressBar;
        private bool _showProgressBar;

        // Performance tracking
        private readonly List<PerformanceMetrics> _performanceMetrics = new();
        private readonly Stopwatch _totalStopwatch = new();

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
            SetupProgressBar();

            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void SetupProgressBar()
        {
            _progressBar = new IMGUIContainer(() =>
            {
                if (!_showProgressBar) return;

                EditorGUI.ProgressBar(
                    new Rect(10, 30, 500, 20),
                    _progress,
                    $"Processing Nodes: {_progress * 100:F1}% ({_nodes.Count} nodes)"
                );

                if (_totalStopwatch.IsRunning)
                {
                    EditorGUI.LabelField(
                        new Rect(10, 55, 500, 20),
                        $"Elapsed Time: {_totalStopwatch.Elapsed.TotalSeconds:F2} seconds"
                    );
                }
            });

            Add(_progressBar);
            _progressBar.style.position = Position.Absolute;
            _progressBar.style.left = 0;
            _progressBar.style.right = 0;
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            switch (evt.ctrlKey)
            {
                case true when evt.keyCode == KeyCode.R:
                    if (!_isProcessing)
                    {
                        DeleteElements(graphElements.ToList());
                        InitGraphAsync();
                    }

                    break;
            }
        }

        private async void InitGraphAsync()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            _showProgressBar = true;
            _progress = 0;

            _performanceMetrics.Clear();
            _totalStopwatch.Restart();

            _nodes.Clear();
            var batches = Mathf.CeilToInt((float)_amountOfNodes / _batchSize);

            // Pre-create all nodes
            var nodesToAdd = new List<Node>(_amountOfNodes);
            for (var i = 0; i < _amountOfNodes; i++)
            {
                var node = new Node { title = $"Node {i}" };
                nodesToAdd.Add(node);
                _nodes.Add(node);
            }

            // Add nodes in batches
            for (var batch = 0; batch < batches; batch++)
            {
                var batchMetrics = new PerformanceMetrics
                {
                    BatchNumber = batch + 1,
                    Timestamp = DateTime.Now
                };

                var start = batch * _batchSize;
                var count = Mathf.Min(_batchSize, _amountOfNodes - start);
                batchMetrics.NodesInBatch = count;

                var batchStopwatch = Stopwatch.StartNew();

                // Add batch of nodes to graph
                for (var i = 0; i < count; i++)
                {
                    AddElement(nodesToAdd[start + i]);
                }

                batchStopwatch.Stop();
                batchMetrics.BatchCreationTime = batchStopwatch.Elapsed.TotalMilliseconds;

                _progress = (float)(batch + 1) / batches;
                _progressBar?.MarkDirtyRepaint();

                // Layout this batch
                batchStopwatch.Restart();
                var batchNodes = nodes.Skip(start).Take(count).ToList();
                NodeLayoutManager.LayoutNodes(batchNodes, silent: true);
                batchStopwatch.Stop();

                batchMetrics.BatchLayoutTime = batchStopwatch.Elapsed.TotalMilliseconds;
                _performanceMetrics.Add(batchMetrics);

                await Task.Yield();
            }

            _totalStopwatch.Stop();
            ExportPerformanceData();

            _isProcessing = false;
            _showProgressBar = false;
            _progressBar?.MarkDirtyRepaint();

            Debug.Log($"Total processing time: {_totalStopwatch.Elapsed.TotalSeconds:F2} seconds");
        }

        private void ExportPerformanceData()
        {
            var path = EditorUtility.SaveFilePanel(
                "Save Performance Data",
                "",
                $"NodeCreationPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                "csv");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                using (var writer = new StreamWriter(path))
                {
                    // Write header
                    writer.WriteLine("DateTime,BatchNumber,NodesInBatch,CreationTime(ms),LayoutTime(ms),TotalBatchTime(ms)");

                    // Write data
                    foreach (var metric in _performanceMetrics)
                    {
                        writer.WriteLine(
                            $"{metric.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                            $"{metric.BatchNumber}," +
                            $"{metric.NodesInBatch}," +
                            $"{metric.BatchCreationTime:F2}," +
                            $"{metric.BatchLayoutTime:F2}," +
                            $"{metric.TotalBatchTime:F2}"
                        );
                    }

                    // Write summary
                    writer.WriteLine();
                    writer.WriteLine("Summary Statistics");
                    writer.WriteLine($"Total Nodes,{_amountOfNodes}");
                    writer.WriteLine($"Total Time (seconds),{_totalStopwatch.Elapsed.TotalSeconds:F2}");
                    writer.WriteLine($"Average Time per Node (ms),{_totalStopwatch.Elapsed.TotalMilliseconds / _amountOfNodes:F2}");
                    writer.WriteLine($"Average Creation Time per Batch (ms),{_performanceMetrics.Average(m => m.BatchCreationTime):F2}");
                    writer.WriteLine($"Average Layout Time per Batch (ms),{_performanceMetrics.Average(m => m.BatchLayoutTime):F2}");
                }

                Debug.Log($"Performance data exported to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting performance data: {e.Message}");
            }
        }

        private void DrawToolbar()
        {
            var toolbar = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                GUI.enabled = !_isProcessing;
                EditorGUI.BeginChangeCheck();
                _amountOfNodes = EditorGUILayout.IntSlider("Max Nodes", _amountOfNodes, 1, 10000);
                _batchSize = EditorGUILayout.IntSlider("Max Nodes", _batchSize, 1, _amountOfNodes);


                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    DeleteElements(graphElements.ToList());
                    InitGraphAsync();
                }

                if (_performanceMetrics.Count > 0 && GUILayout.Button("Export Data", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    ExportPerformanceData();
                }

                GUI.enabled = true;
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
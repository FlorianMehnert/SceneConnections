using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace SceneConnections.Editor.Utils
{
    public class NodeGraphBuilder
    {
        public int AmountOfNodes = 10000;
        public int BatchSize = 2500;
        private float _progress;

        // Progress UI elements
        private bool _showProgressBar;
        private readonly IConnectionGraphView _gv;
        public readonly List<PerformanceMetrics> PerformanceMetrics = new();
        private readonly Stopwatch _totalStopwatch = new();
        private IMGUIContainer _progressBar;
        private readonly List<Node> _nodes;

        public NodeGraphBuilder(IConnectionGraphView gv)
        {
            _gv = gv;
            _nodes = new List<Node>();
        }
        
        public void SetupProgressBar()
        {
            _progressBar = new IMGUIContainer(() =>
            {
                if (!_showProgressBar) return;

                EditorGUI.ProgressBar(
                    new Rect(210, 27, 450, 17),
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

            _gv.Add(_progressBar);
            _progressBar.style.position = Position.Absolute;
            _progressBar.style.left = 0;
            _progressBar.style.right = 0;
        }
        
        public void BuildGraph()
        {
            if (_gv.IsBusy) return;
            ((GraphView) _gv).DeleteElements(_gv.GraphElements.ToList());
            InitGraphAsync();
        }
        
        public async void InitGraphAsync()
        {
            if (_gv.IsBusy) return;
            _gv.IsBusy = true;
            _showProgressBar = true;
            _progress = 0;

            PerformanceMetrics.Clear();
            _totalStopwatch.Restart();

            _nodes.Clear();
            var batches = Mathf.CeilToInt((float)AmountOfNodes / BatchSize);

            // Pre-create all nodes
            var nodesToAdd = new List<Node>(AmountOfNodes);
            for (var i = 0; i < AmountOfNodes; i++)
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

                var start = batch * BatchSize;
                var count = Mathf.Min(BatchSize, AmountOfNodes - start);
                batchMetrics.NodesInBatch = count;

                var batchStopwatch = Stopwatch.StartNew();

                // Add batch of nodes to graph
                for (var i = 0; i < count; i++)
                {
                    _gv.Add(nodesToAdd[start + i]);
                }

                batchStopwatch.Stop();
                batchMetrics.BatchCreationTime = batchStopwatch.Elapsed.TotalMilliseconds;

                _progress = (float)(batch + 1) / batches;
                _progressBar?.MarkDirtyRepaint();

                // Layout this batch
                batchStopwatch.Restart();
                var batchNodes = _nodes.Skip(start).Take(count).ToList();
                NodeLayoutManager.LayoutNodes(batchNodes, silent: true);
                batchStopwatch.Stop();

                batchMetrics.BatchLayoutTime = batchStopwatch.Elapsed.TotalMilliseconds;
                PerformanceMetrics.Add(batchMetrics);

                await Task.Yield();
            }

            _totalStopwatch.Stop();
            ExportPerformanceData();

            _gv.IsBusy = false;
            _showProgressBar = false;
            _progressBar?.MarkDirtyRepaint();

            Debug.Log($"Total processing time: {_totalStopwatch.Elapsed.TotalSeconds:F2} seconds");
        }
        
        public void ExportPerformanceData()
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
                    foreach (var metric in PerformanceMetrics)
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
                    writer.WriteLine($"Total Nodes,{AmountOfNodes}");
                    writer.WriteLine($"Total Time (seconds),{_totalStopwatch.Elapsed.TotalSeconds:F2}");
                    writer.WriteLine($"Average Time per Node (ms),{_totalStopwatch.Elapsed.TotalMilliseconds / AmountOfNodes:F2}");
                    writer.WriteLine($"Average Creation Time per Batch (ms),{PerformanceMetrics.Average(m => m.BatchCreationTime):F2}");
                    writer.WriteLine($"Average Layout Time per Batch (ms),{PerformanceMetrics.Average(m => m.BatchLayoutTime):F2}");
                }

                Debug.Log($"Performance data exported to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting performance data: {e.Message}");
            }
        }
    }
}
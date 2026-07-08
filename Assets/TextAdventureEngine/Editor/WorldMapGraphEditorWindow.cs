namespace TextEngine.EditorTools
{
    using TextEngine;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Compass directions and their opposites, in one place so the window and the
    /// node don't each carry their own copy.
    /// </summary>
    static class CompassDirections
    {
        public static readonly string[] All = { "North", "East", "South", "West", "Up", "Down" };

        public static string Opposite(string direction)
        {
            if (string.IsNullOrEmpty(direction)) return "";
            switch (direction.ToLower())
            {
                case "north": return "South";
                case "south": return "North";
                case "east": return "West";
                case "west": return "East";
                case "up": return "Down";
                case "down": return "Up";
                default: return "";
            }
        }
    }

    /// <summary>
    /// Visual editor for the world map. Each location is a node with six compass
    /// exit ports (outputs) and a single "Entrance" port (input). One edge = one
    /// exit: drag from a direction on the source room into the entrance of the
    /// destination. A two-way connection is simply two edges, so deleting one
    /// makes the passage one-way.
    /// </summary>
    public class WorldMapGraphEditorWindow : EditorWindow
    {
        private WorldMapGraphView graphView;

        [MenuItem("Window/World Map Graph Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<WorldMapGraphEditorWindow>("World Map");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            // Toolbar on top, graph fills the rest (root is a vertical flex column).
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(CreateLocation) { text = "New Location" });
            toolbar.Add(new ToolbarButton(AutoArrange) { text = "Auto-Arrange" });
            toolbar.Add(new ToolbarButton(LoadGraph) { text = "Refresh" });
            var hint = new Label("Drag a compass port → a room's Entrance to make an exit. Delete an edge to remove just that exit.");
            hint.style.marginLeft = 8;
            hint.style.unityTextAlign = TextAnchor.MiddleLeft;
            hint.style.color = new Color(0.6f, 0.6f, 0.6f);
            toolbar.Add(hint);
            rootVisualElement.Add(toolbar);

            graphView = new WorldMapGraphView(this) { name = "World Map" };
            graphView.style.flexGrow = 1;
            rootVisualElement.Add(graphView);

            LoadGraph();
        }

        private void OnDisable()
        {
            if (graphView != null)
            {
                rootVisualElement.Remove(graphView);
            }
        }

        private GraphViewChange OnGraphChange(GraphViewChange change)
        {
            bool changed = false;

            // --- Edges created: each new edge becomes an exit on the source room. ---
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (!TryGetEndpoints(edge, out var fromNode, out var toNode, out var direction)) continue;

                    SetExit(fromNode.Location, direction, toNode.Location);
                    changed = true;

                    // Auto-create the reciprocal exit, but only if the opposite
                    // direction on the destination is free (never clobber an
                    // existing exit, and never double up if it already exists).
                    string opposite = CompassDirections.Opposite(direction);
                    if (!string.IsNullOrEmpty(opposite) && !HasExit(toNode.Location, opposite))
                    {
                        SetExit(toNode.Location, opposite, fromNode.Location);

                        var reversePort = toNode.GetOutputPort(opposite);
                        var entrancePort = fromNode.EntrancePort;
                        if (reversePort != null && entrancePort != null)
                        {
                            // AddElement does not re-trigger graphViewChanged, so
                            // this won't recurse back into this handler.
                            graphView.AddElement(reversePort.ConnectTo(entrancePort));
                        }
                    }
                }
            }

            // --- Elements removed: an edge removal deletes just that one exit. ---
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge && TryGetEndpoints(edge, out var fromNode, out var toNode, out var direction))
                    {
                        RemoveExit(fromNode.Location, direction, toNode.Location);
                        changed = true;
                    }
                }
            }

            if (changed) AssetDatabase.SaveAssets();
            return change;
        }

        private static bool TryGetEndpoints(Edge edge, out LocationNode fromNode, out LocationNode toNode, out string direction)
        {
            fromNode = edge?.output?.node as LocationNode;
            toNode = edge?.input?.node as LocationNode;
            direction = edge?.output?.portName;
            return fromNode != null && toNode != null && !string.IsNullOrEmpty(direction);
        }

        private static bool HasExit(Location loc, string direction)
        {
            return loc.exits != null &&
                   loc.exits.Any(x => x != null && x.direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Sets (replacing any existing) the exit for a direction on a location.</summary>
        private static void SetExit(Location loc, string direction, Location destination)
        {
            var exits = (loc.exits ?? new Exit[0]).ToList();
            exits.RemoveAll(x => x != null && x.direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
            exits.Add(new Exit { direction = direction, destination = destination });
            loc.exits = exits.ToArray();
            EditorUtility.SetDirty(loc);
        }

        private static void RemoveExit(Location loc, string direction, Location destination)
        {
            if (loc.exits == null) return;
            var exits = loc.exits.ToList();
            exits.RemoveAll(x => x != null &&
                                 x.direction.Equals(direction, StringComparison.OrdinalIgnoreCase) &&
                                 x.destination == destination);
            loc.exits = exits.ToArray();
            EditorUtility.SetDirty(loc);
        }

        private void LoadGraph()
        {
            if (graphView == null) return;

            graphView.graphViewChanged -= OnGraphChange;
            graphView.DeleteElements(graphView.graphElements.ToList());
            graphView.graphViewChanged += OnGraphChange;

            var locations = Resources.LoadAll<Location>("Locations");
            var nodes = new Dictionary<Location, LocationNode>();

            foreach (var loc in locations)
            {
                var node = new LocationNode(loc);
                graphView.AddElement(node);
                nodes.Add(loc, node);
            }

            // One edge per exit: source direction port → destination entrance port.
            foreach (var loc in locations)
            {
                if (loc.exits == null) continue;
                foreach (var exit in loc.exits)
                {
                    if (exit?.destination == null) continue;
                    if (!nodes.TryGetValue(loc, out var fromNode)) continue;
                    if (!nodes.TryGetValue(exit.destination, out var toNode)) continue;

                    var outPort = fromNode.GetOutputPort(exit.direction);
                    var entrancePort = toNode.EntrancePort;
                    if (outPort != null && entrancePort != null)
                    {
                        graphView.AddElement(outPort.ConnectTo(entrancePort));
                    }
                }
            }
        }

        public void AutoArrange()
        {
            var nodes = graphView.nodes.OfType<LocationNode>().ToList();
            if (nodes.Count == 0) return;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count));
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 pos = new Vector2((i % cols) * 250, (i / cols) * 200);
                nodes[i].SetPosition(new Rect(pos, nodes[i].GetPosition().size));
                nodes[i].Location.editorPosition = pos;
                EditorUtility.SetDirty(nodes[i].Location);
            }
            AssetDatabase.SaveAssets();
            LoadGraph();
        }

        public void CreateLocation()
        {
            string folder = EditorPaths.Folder("Locations");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Location", "NewLocation", "asset", "Enter new location name", folder);
            if (string.IsNullOrEmpty(path)) return;

            var loc = ScriptableObject.CreateInstance<Location>();
            loc.exits = new Exit[0];
            AssetDatabase.CreateAsset(loc, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = loc;
            LoadGraph();
        }

        private class WorldMapGraphView : GraphView
        {
            private readonly WorldMapGraphEditorWindow _editorWindow;

            // Right-drag panning state.
            private bool _rightPanning;
            private Vector2 _lastPanPosition;

            public WorldMapGraphView(WorldMapGraphEditorWindow editorWindow)
            {
                _editorWindow = editorWindow;

                this.AddManipulator(new ContentZoomer());
                this.AddManipulator(new ContentDragger());   // left-drag pan (default)
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                this.AddManipulator(new EdgeConnector<Edge>(new EdgeConnectorListener()));

                var grid = new GridBackground();
                Insert(0, grid);
                grid.StretchToParentSize();

                graphViewChanged = editorWindow.OnGraphChange;

                // Right-drag panning. We handle these in the trickle-down (capture)
                // phase so we consume the right mouse button before the built-in
                // contextual menu can open. The menu's two actions now live on the
                // toolbar, so nothing is lost.
                RegisterCallback<MouseDownEvent>(OnRightPanDown, TrickleDown.TrickleDown);
                RegisterCallback<MouseMoveEvent>(OnRightPanMove, TrickleDown.TrickleDown);
                RegisterCallback<MouseUpEvent>(OnRightPanUp, TrickleDown.TrickleDown);
                RegisterCallback<ContextualMenuPopulateEvent>(evt => evt.StopImmediatePropagation());

                RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.actionKey && evt.keyCode == KeyCode.A)
                    {
                        _editorWindow.AutoArrange();
                        evt.StopPropagation();
                    }
                });
            }

            private void OnRightPanDown(MouseDownEvent evt)
            {
                if (evt.button != (int)MouseButton.RightMouse) return;
                _rightPanning = true;
                _lastPanPosition = evt.mousePosition;
                this.CaptureMouse();
                evt.StopImmediatePropagation();
            }

            private void OnRightPanMove(MouseMoveEvent evt)
            {
                if (!_rightPanning) return;
                Vector2 delta = evt.mousePosition - _lastPanPosition;
                _lastPanPosition = evt.mousePosition;
                // Move the canvas by the mouse delta (scale is unchanged).
                UpdateViewTransform(viewTransform.position + (Vector3)delta, viewTransform.scale);
                evt.StopImmediatePropagation();
            }

            private void OnRightPanUp(MouseUpEvent evt)
            {
                if (!_rightPanning || evt.button != (int)MouseButton.RightMouse) return;
                _rightPanning = false;
                this.ReleaseMouse();
                evt.StopImmediatePropagation();
            }

            // An exit runs from a direction (output) to an entrance (input) on a
            // different room.
            public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            {
                var compatible = new List<Port>();
                ports.ForEach(port =>
                {
                    if (startPort != port &&
                        startPort.node != port.node &&
                        startPort.direction != port.direction)
                    {
                        compatible.Add(port);
                    }
                });
                return compatible;
            }

        }

        private class EdgeConnectorListener : IEdgeConnectorListener
        {
            public void OnDropOutsidePort(Edge edge, Vector2 position) { }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge.input != null && edge.output != null)
                {
                    graphView.AddElement(edge);
                }
            }
        }

        private class LocationNode : Node
        {
            public Location Location { get; }

            /// <summary>The single inbound port other rooms' exits connect into.</summary>
            public Port EntrancePort { get; }

            private readonly Dictionary<string, Port> _outputPorts = new Dictionary<string, Port>();

            public LocationNode(Location loc)
            {
                Location = loc;
                title = loc.name;
                viewDataKey = loc.GetEntityId().ToString();
                style.left = loc.editorPosition.x;
                style.top = loc.editorPosition.y;

                // One shared entrance (input). Capacity Multi: many rooms can lead here.
                EntrancePort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                EntrancePort.portName = "Entrance";
                inputContainer.Add(EntrancePort);

                // Six compass exits (outputs). Capacity Single: one exit per direction.
                foreach (var dir in CompassDirections.All)
                {
                    var outPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                    outPort.portName = dir;
                    outputContainer.Add(outPort);
                    _outputPorts[dir.ToLower()] = outPort;
                }

                RefreshExpandedState();
                RefreshPorts();

                // Persist node position when the user drags it.
                RegisterCallback<MouseUpEvent>(evt =>
                {
                    var newPos = GetPosition().position;
                    if (loc.editorPosition != newPos)
                    {
                        loc.editorPosition = newPos;
                        EditorUtility.SetDirty(loc);
                    }
                });
            }

            public Port GetOutputPort(string direction)
            {
                if (string.IsNullOrEmpty(direction)) return null;
                _outputPorts.TryGetValue(direction.ToLower(), out var port);
                return port;
            }
        }
    }
}

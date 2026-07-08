namespace TextEngine.EditorTools
{
    using TextEngine;

    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Visual editor for dialogue trees. Every DialogueNode asset is a graph
    /// node: edit its text inline, drag from a response (or the failure port)
    /// into another node's entrance to link the conversation, add responses
    /// with one click, and let auto-arrange lay out each tree by depth.
    /// Characters' starting nodes are badged with the speaker's name.
    /// </summary>
    public class DialogueGraphEditorWindow : EditorWindow
    {
        private DialogueGraphView graphView;

        [MenuItem("Window/Dialogue Graph Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<DialogueGraphEditorWindow>("Dialogue Graph");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(CreateNode) { text = "New Node" });
            toolbar.Add(new ToolbarButton(AutoArrange) { text = "Auto-Arrange" });
            toolbar.Add(new ToolbarButton(LoadGraph) { text = "Refresh" });
            var hint = new Label("Drag a response → a node's Entrance to link it. Edit text directly on a node. Select a node to edit everything else in the Inspector.");
            hint.style.marginLeft = 8;
            hint.style.unityTextAlign = TextAnchor.MiddleLeft;
            hint.style.color = new Color(0.6f, 0.6f, 0.6f);
            toolbar.Add(hint);
            rootVisualElement.Add(toolbar);

            graphView = new DialogueGraphView { name = "Dialogue Graph" };
            graphView.style.flexGrow = 1;
            graphView.graphViewChanged = OnGraphChange;
            rootVisualElement.Add(graphView);

            Undo.undoRedoPerformed += LoadGraph;
            LoadGraph();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= LoadGraph;
            if (graphView != null) rootVisualElement.Remove(graphView);
        }

        private static List<DialogueNode> LoadAllNodes() =>
            AssetDatabase.FindAssets("t:DialogueNode")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DialogueNode>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(n => n != null)
                .ToList();

        private void LoadGraph()
        {
            if (graphView == null) return;

            graphView.graphViewChanged = null;
            graphView.DeleteElements(graphView.graphElements.ToList());
            graphView.graphViewChanged = OnGraphChange;

            var dialogueNodes = LoadAllNodes();

            // Entry badges: which character starts a conversation at each node.
            var entryOwners = new Dictionary<DialogueNode, string>();
            foreach (var character in AssetDatabase.FindAssets("t:Character")
                         .Select(guid => AssetDatabase.LoadAssetAtPath<Character>(AssetDatabase.GUIDToAssetPath(guid)))
                         .Where(c => c != null && c.startingDialogue != null))
            {
                entryOwners[character.startingDialogue] = character.characterName;
            }

            var graphNodes = new Dictionary<DialogueNode, DialogueGraphNode>();
            foreach (var node in dialogueNodes)
            {
                entryOwners.TryGetValue(node, out string speaker);
                var graphNode = new DialogueGraphNode(node, speaker, LoadGraph);
                graphView.AddElement(graphNode);
                graphNodes.Add(node, graphNode);
            }

            // Edges: one per linked response, plus the failure link.
            foreach (var node in dialogueNodes)
            {
                var fromNode = graphNodes[node];
                if (node.playerResponses != null)
                {
                    for (int i = 0; i < node.playerResponses.Length; i++)
                    {
                        var next = node.playerResponses[i]?.nextNode;
                        if (next == null || !graphNodes.TryGetValue(next, out var toNode)) continue;
                        graphView.AddElement(fromNode.ResponsePorts[i].ConnectTo(toNode.EntrancePort));
                    }
                }
                if (node.failureNode != null && graphNodes.TryGetValue(node.failureNode, out var failTarget))
                {
                    graphView.AddElement(fromNode.FailurePort.ConnectTo(failTarget.EntrancePort));
                }
            }
        }

        private GraphViewChange OnGraphChange(GraphViewChange change)
        {
            bool changed = false;

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.output?.node is DialogueGraphNode fromNode &&
                        edge.input?.node is DialogueGraphNode toNode &&
                        edge.output.userData is int portIndex)
                    {
                        Undo.RecordObject(fromNode.Node, "Link Dialogue Node");
                        if (portIndex == DialogueGraphNode.FailurePortIndex)
                        {
                            fromNode.Node.failureNode = toNode.Node;
                        }
                        else if (fromNode.Node.playerResponses != null && portIndex < fromNode.Node.playerResponses.Length)
                        {
                            fromNode.Node.playerResponses[portIndex].nextNode = toNode.Node;
                        }
                        EditorUtility.SetDirty(fromNode.Node);
                        changed = true;
                    }
                }
            }

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge &&
                        edge.output?.node is DialogueGraphNode fromNode &&
                        edge.output.userData is int portIndex)
                    {
                        Undo.RecordObject(fromNode.Node, "Unlink Dialogue Node");
                        if (portIndex == DialogueGraphNode.FailurePortIndex)
                        {
                            fromNode.Node.failureNode = null;
                        }
                        else if (fromNode.Node.playerResponses != null && portIndex < fromNode.Node.playerResponses.Length)
                        {
                            fromNode.Node.playerResponses[portIndex].nextNode = null;
                        }
                        EditorUtility.SetDirty(fromNode.Node);
                        changed = true;
                    }
                }
            }

            if (changed) AssetDatabase.SaveAssets();
            return change;
        }

        private void CreateNode()
        {
            string folder = EditorPaths.Folder("Dialogue");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Dialogue Node", "NewDialogueNode", "asset", "Name the new dialogue node", folder);
            if (string.IsNullOrEmpty(path)) return;

            var node = ScriptableObject.CreateInstance<DialogueNode>();
            node.playerResponses = new Response[0];
            AssetDatabase.CreateAsset(node, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = node;
            LoadGraph();
        }

        // Lays out each conversation tree left-to-right by depth: entry nodes
        // (referenced by a Character, or with no inbound links) in column 0,
        // their children in column 1, and so on.
        private void AutoArrange()
        {
            var dialogueNodes = LoadAllNodes();
            if (dialogueNodes.Count == 0) return;
            Undo.RecordObjects(dialogueNodes.Cast<Object>().ToArray(), "Auto-Arrange Dialogue Graph");

            var inbound = new HashSet<DialogueNode>();
            foreach (var node in dialogueNodes)
            {
                if (node.playerResponses != null)
                    foreach (var response in node.playerResponses)
                        if (response?.nextNode != null) inbound.Add(response.nextNode);
                if (node.failureNode != null) inbound.Add(node.failureNode);
            }

            var depths = new Dictionary<DialogueNode, int>();
            var queue = new Queue<DialogueNode>();
            foreach (var root in dialogueNodes.Where(n => !inbound.Contains(n)))
            {
                depths[root] = 0;
                queue.Enqueue(root);
            }
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                var children = new List<DialogueNode>();
                if (node.playerResponses != null)
                    children.AddRange(node.playerResponses.Where(r => r?.nextNode != null).Select(r => r.nextNode));
                if (node.failureNode != null) children.Add(node.failureNode);
                foreach (var child in children.Where(c => !depths.ContainsKey(c)))
                {
                    depths[child] = depths[node] + 1;
                    queue.Enqueue(child);
                }
            }
            // Anything unreached (cycles) goes in column 0.
            foreach (var node in dialogueNodes.Where(n => !depths.ContainsKey(n))) depths[node] = 0;

            var rowsPerColumn = new Dictionary<int, int>();
            foreach (var node in dialogueNodes.OrderBy(n => depths[n]).ThenBy(n => n.name))
            {
                int depth = depths[node];
                rowsPerColumn.TryGetValue(depth, out int row);
                node.editorPosition = new Vector2(depth * 420, row * 260);
                rowsPerColumn[depth] = row + 1;
                EditorUtility.SetDirty(node);
            }
            AssetDatabase.SaveAssets();
            LoadGraph();
        }

        private class DialogueGraphView : GraphView
        {
            // Right-drag panning state (same interaction as the World Map editor).
            private bool _rightPanning;
            private Vector2 _lastPanPosition;

            public DialogueGraphView()
            {
                this.AddManipulator(new ContentZoomer());
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                var grid = new GridBackground();
                Insert(0, grid);
                grid.StretchToParentSize();

                // Right-drag panning. Handled in the trickle-down (capture)
                // phase so we consume the right mouse button before the
                // built-in contextual menu can open; the menu's actions live
                // on the toolbar, so nothing is lost.
                RegisterCallback<MouseDownEvent>(OnRightPanDown, TrickleDown.TrickleDown);
                RegisterCallback<MouseMoveEvent>(OnRightPanMove, TrickleDown.TrickleDown);
                RegisterCallback<MouseUpEvent>(OnRightPanUp, TrickleDown.TrickleDown);
                RegisterCallback<ContextualMenuPopulateEvent>(evt => evt.StopImmediatePropagation());
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
#pragma warning disable 618 // GraphView still exposes the view transform only via ITransform
                UpdateViewTransform(viewTransform.position + (Vector3)delta, viewTransform.scale);
#pragma warning restore 618
                evt.StopImmediatePropagation();
            }

            private void OnRightPanUp(MouseUpEvent evt)
            {
                if (!_rightPanning || evt.button != (int)MouseButton.RightMouse) return;
                _rightPanning = false;
                this.ReleaseMouse();
                evt.StopImmediatePropagation();
            }

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

        private class DialogueGraphNode : Node
        {
            public const int FailurePortIndex = -1;

            public DialogueNode Node { get; }
            public Port EntrancePort { get; }
            public Port FailurePort { get; }
            public List<Port> ResponsePorts { get; } = new List<Port>();

            public DialogueGraphNode(DialogueNode node, string entrySpeaker, System.Action refreshGraph)
            {
                Node = node;
                title = entrySpeaker != null ? $"▶ {node.name}  ({entrySpeaker})" : node.name;
                viewDataKey = node.GetEntityId().ToString();
                style.left = node.editorPosition.x;
                style.top = node.editorPosition.y;
                style.maxWidth = 380;

                EntrancePort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                EntrancePort.portName = "Entrance";
                inputContainer.Add(EntrancePort);

                // Inline dialogue-text editing, recorded for undo per change.
                var textField = new TextField { value = node.dialogueText, multiline = true };
                textField.style.whiteSpace = WhiteSpace.Normal;
                textField.style.minWidth = 220;
                textField.style.maxWidth = 360;
                textField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(Node, "Edit Dialogue Text");
                    Node.dialogueText = evt.newValue;
                    EditorUtility.SetDirty(Node);
                });
                mainContainer.Insert(1, textField);

                // One output port per player response, labeled with a preview.
                if (node.playerResponses != null)
                {
                    for (int i = 0; i < node.playerResponses.Length; i++)
                    {
                        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                        port.portName = $"{i + 1}: {Truncate(node.playerResponses[i]?.responseText, 28)}";
                        port.userData = i;
                        outputContainer.Add(port);
                        ResponsePorts.Add(port);
                    }
                }

                FailurePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                FailurePort.portName = "On Failure";
                FailurePort.userData = FailurePortIndex;
                outputContainer.Add(FailurePort);

                // One-click response authoring; the graph rebuilds so the new
                // port appears immediately.
                var addResponse = new Button(() =>
                {
                    Undo.RecordObject(Node, "Add Response");
                    var responses = (Node.playerResponses ?? new Response[0]).ToList();
                    responses.Add(new Response { responseText = "New response" });
                    Node.playerResponses = responses.ToArray();
                    EditorUtility.SetDirty(Node);
                    refreshGraph?.Invoke();
                })
                { text = "+ Response" };
                outputContainer.Add(addResponse);

                // Select the asset when the node is clicked, so the Inspector
                // shows responses, flags, and actions for editing.
                RegisterCallback<MouseDownEvent>(_ => Selection.activeObject = Node);

                // Persist position on drag.
                RegisterCallback<MouseUpEvent>(_ =>
                {
                    var pos = GetPosition().position;
                    if (Node.editorPosition != pos)
                    {
                        Undo.RecordObject(Node, "Move Dialogue Node");
                        Node.editorPosition = pos;
                        EditorUtility.SetDirty(Node);
                    }
                });

                RefreshExpandedState();
                RefreshPorts();
            }

            private static string Truncate(string text, int max)
            {
                if (string.IsNullOrEmpty(text)) return "(empty)";
                return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
            }
        }
    }
}

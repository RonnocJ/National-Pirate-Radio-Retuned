#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

// NOTE: This script assumes the JSON structure produced by WriteDialogueToFile
// uses the types/field names: TextFile { List<TextBlock> blocks }, TextBlock { string name; List<TextCluster> clusters },
// TextCluster { int id; float pauseBefore; List<TextLine> lines }, TextLine { Characters speaker; string text; double wait; ; string wwiseEvent }
// If your project already has these types defined elsewhere, this loader will use them via JsonUtility.
// If not, JsonUtility will still be able to deserialize into matching names used here.

[CreateAssetMenu(fileName = "DialogueGenerator", menuName = "Objects/Utility/DialogueGenerator", order = 0)]
public class DialogueGenerator : ScriptableObject
{
    private struct EnumSource
    {
        public readonly string EnumName;
        public readonly string RelativeFolder;

        public EnumSource(string enumName, string relativeFolder)
        {
            EnumName = enumName;
            RelativeFolder = relativeFolder;
        }
    }

    private static readonly EnumSource[] DialogueEnumSources =
    {
        new EnumSource("LevelIntro", Path.Combine("Resources", "Scripts", "LevelIntro")),
        new EnumSource("AfterLevel", Path.Combine("Resources", "Scripts", "AfterLevel"))
    };

    private const string GeneratedEnumDirectory = "_SCRIPTS/Dialogue/_generated";
    private const string GeneratedEnumFileName = "DialogueEnum.cs";

    [Serializable]
    public class DialogueLine
    {
        public Characters Speaker;
        [TextArea] public string Line;
        public double Wait;
    }
    [Serializable]
    public class DialogueCluster
    {
        public float PauseBefore;
        public AudioEvent LineAudio;
        public DialogueLine[] Lines;
    }
    [Serializable]
    public class DialogueBlock
    {
        public string Name;
        public DialogueCluster[] Clusters;
    }

    public DialogueBlock[] Blocks;
    [HideInInspector] public string SaveFolderRelative;
    [HideInInspector] public string OutputFileName;

    // ---- New: field to remember last loaded path (hidden in inspector)
    [HideInInspector] public string LastLoadedFilePath;

    public void WriteDialogueToFile()
    {
        if (Blocks == null || Blocks.Length == 0)
        {
            Debug.LogWarning("No Blocks to write. Add Blocks in the inspector first.");
            return;
        }

        // Build TextFile structure expected by TextLoader/TextData
        var textFile = new TextFile { blocks = new List<TextBlock>() };

        foreach (var block in Blocks)
        {
            if (block == null) continue;

            var outBlock = new TextBlock
            {
                name = block.Name ?? string.Empty,
                clusters = new List<TextCluster>()
            };

            if (block.Clusters != null)
            {
                for (int ci = 0; ci < block.Clusters.Length; ci++)
                {
                    var c = block.Clusters[ci];
                    if (c == null) continue;

                    var outCluster = new TextCluster
                    {
                        id = ci, // cluster id from index
                        pauseBefore = c.PauseBefore,
                        wwiseEvent = c.LineAudio,
                        lines = new List<TextLine>()
                    };

                    if (c.Lines != null)
                    {
                        foreach (var ln in c.Lines)
                        {
                            if (ln == null) continue;
                            var outLine = new TextLine
                            {
                                speaker = ln.Speaker,
                                text = ln.Line ?? string.Empty,
                                wait = ln.Wait,
                            };
                            outCluster.lines.Add(outLine);
                        }
                    }

                    outBlock.clusters.Add(outCluster);
                }
            }

            textFile.blocks.Add(outBlock);
        }

        string json = JsonUtility.ToJson(textFile, true);

        // Prepare output path under Assets/Resources/Scripts
        string resourcesScriptsRoot = Path.Combine("Assets", "Resources", "Scripts");
        string sub = (SaveFolderRelative ?? string.Empty).Trim().Trim('/').Trim('\\');
        string destDir = string.IsNullOrEmpty(sub) ? resourcesScriptsRoot : Path.Combine(resourcesScriptsRoot, sub);

        try
        {
            Directory.CreateDirectory(destDir);

            string fileName = string.IsNullOrEmpty(OutputFileName) ? "dialogue" : OutputFileName;
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) fileName += ".json";
            string destPath = Path.Combine(destDir, fileName);

            File.WriteAllText(destPath, json, new UTF8Encoding(false));
            try
            {
                RegenerateDialogueEnumScript();
            }
            catch (Exception enumEx)
            {
                Debug.LogError($"Failed to regenerate dialogue enum: {enumEx.Message}");
            }
            AssetDatabase.Refresh();

            // Compute resource load path (relative to Resources root)
            string resourceRelative = string.IsNullOrEmpty(sub)
                ? $"Scripts/{Path.GetFileNameWithoutExtension(fileName)}"
                : $"Scripts/{sub.Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(fileName)}";

            Debug.Log($"Wrote dialogue JSON → {destPath}\nLoad with Resources path: '{resourceRelative}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write dialogue JSON: {ex.Message}");
        }
    }

    // ---- NEW: Load a JSON file into this DialogueGenerator's Blocks
    public void LoadDialogueFromFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"LoadDialogueFromFile: Path is invalid or file does not exist: '{path}'");
            return;
        }

        try
        {
            string json = File.ReadAllText(path, new UTF8Encoding(false));
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"LoadDialogueFromFile: File was empty: '{path}'");
                return;
            }

            // Try to deserialize into TextFile (matches the output format of WriteDialogueToFile)
            var textFile = JsonUtility.FromJson<TextFile>(json);
            if (textFile == null || textFile.blocks == null)
            {
                Debug.LogError($"LoadDialogueFromFile: JSON did not contain expected structure (blocks). Path: '{path}'");
                return;
            }

            // Map parsed TextFile -> DialogueBlock[] structure
            var newBlocks = new DialogueBlock[textFile.blocks.Count];
            for (int bi = 0; bi < textFile.blocks.Count; bi++)
            {
                var tb = textFile.blocks[bi];
                if (tb == null) continue;

                var db = new DialogueBlock();
                db.Name = tb.name ?? $"Block {bi}";

                if (tb.clusters != null)
                {
                    db.Clusters = new DialogueCluster[tb.clusters.Count];
                    for (int ci = 0; ci < tb.clusters.Count; ci++)
                    {
                        var tc = tb.clusters[ci];
                        if (tc == null) continue;

                        var dc = new DialogueCluster();
                        dc.PauseBefore = tc.pauseBefore;

                        if (tc.lines != null)
                        {
                            dc.Lines = new DialogueLine[tc.lines.Count];
                            for (int li = 0; li < tc.lines.Count; li++)
                            {
                                var tl = tc.lines[li];
                                if (tl == null) continue;
                                var dl = new DialogueLine
                                {
                                    Speaker = tl.speaker,
                                    Line = tl.text,
                                    Wait = tl.wait
                                };
                                dc.Lines[li] = dl;
                            }
                        }
                        else
                        {
                            dc.Lines = new DialogueLine[0];
                        }

                        db.Clusters[ci] = dc;
                    }
                }
                else
                {
                    db.Clusters = new DialogueCluster[0];
                }

                newBlocks[bi] = db;
            }

            // Apply changes to this ScriptableObject with undo support
            Undo.RecordObject(this, "Load Dialogue From File");
            Blocks = newBlocks;
            LastLoadedFilePath = path;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"Loaded dialogue from '{path}'. Blocks: {Blocks?.Length ?? 0}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"LoadDialogueFromFile: Failed to read/parse file '{path}': {ex.Message}");
        }
    }

    // ---- Helper JSON types used for deserialization (match the output structure)
    [Serializable]
    private class TextFile
    {
        public List<TextBlock> blocks;
    }

    [Serializable]
    private class TextBlock
    {
        public string name;
        public List<TextCluster> clusters;
    }

    [Serializable]
    private class TextCluster
    {
        public int id;
        public float pauseBefore;
        public AudioEvent wwiseEvent;
        public List<TextLine> lines;
    }

    [Serializable]
    private class TextLine
    {
        public Characters speaker;
        public string text;
        public double wait;
    }

    private static void RegenerateDialogueEnumScript()
    {
        string assetsRoot = Application.dataPath;
        string outputDirectory = Path.Combine(assetsRoot, GeneratedEnumDirectory);
        Directory.CreateDirectory(outputDirectory);

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// Generated by DialogueGenerator. Changes may be overwritten.");
        builder.AppendLine();

        builder.AppendLine("public enum Opinion");
        builder.AppendLine("{");
        builder.AppendLine("    negative = -1,");
        builder.AppendLine("    neutral = 0,");
        builder.AppendLine("    positive = 1,");
        builder.AppendLine("}");

        foreach (var source in DialogueEnumSources)
        {
            string absoluteFolder = Path.Combine(assetsRoot, source.RelativeFolder);

            string[] jsonFiles = Directory.Exists(absoluteFolder)
                ? Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"RegenerateDialogueEnumScript: No JSON files found in '{source.RelativeFolder}'. Enum '{source.EnumName}' will contain a placeholder value.");
            }

            builder.AppendLine($"public enum {source.EnumName}");
            builder.AppendLine("{");

            if (jsonFiles.Length == 0)
            {
                builder.AppendLine("    None = 0,");
            }
            else
            {
                var usedNames = new HashSet<string>();
                foreach (string filePath in jsonFiles)
                {
                    string rawName = Path.GetFileNameWithoutExtension(filePath);
                    string identifier = SanitizeEnumIdentifier(rawName);

                    if (!usedNames.Add(identifier))
                    {
                        int suffix = 1;
                        string candidate;
                        do
                        {
                            candidate = $"{identifier}_{suffix++}";
                        } while (!usedNames.Add(candidate));

                        identifier = candidate;
                        Debug.LogWarning($"RegenerateDialogueEnumScript: Duplicate enum identifier '{identifier}' derived from file '{rawName}'. Added numeric suffix to keep it unique.");
                    }

                    builder.AppendLine($"    {identifier},");
                }
            }

            builder.AppendLine("}");
            builder.AppendLine();
        }

        string outputPath = Path.Combine(outputDirectory, GeneratedEnumFileName);
        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
    }

    private static string SanitizeEnumIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "_";
        }

        var sanitized = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            sanitized.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if (!IsValidIdentifierStart(sanitized[0]))
        {
            sanitized.Insert(0, '_');
        }

        return sanitized.ToString();
    }

    private static bool IsValidIdentifierStart(char c)
    {
        return char.IsLetter(c) || c == '_';
    }
}

[CustomEditor(typeof(DialogueGenerator))]
public class DialogueGeneratorEditor : Editor
{
    private string[] _subfolders;
    private int _selectedIndex;

    private SerializedProperty _blocksProp;
    private ReorderableList _blocksList;
    private readonly Dictionary<string, ReorderableList> _clustersLists = new Dictionary<string, ReorderableList>();
    private readonly Dictionary<string, ReorderableList> _linesLists = new Dictionary<string, ReorderableList>();

    // Local editor state for load field
    private string _loadFilePath = string.Empty;

    private void OnEnable()
    {
        if (target == null) return;
        _blocksProp = serializedObject.FindProperty("Blocks");
        SetupBlocksList();

        // try to read last loaded path from target
        var dGen = target as DialogueGenerator;
        if (dGen != null && !string.IsNullOrEmpty(dGen.LastLoadedFilePath))
        {
            _loadFilePath = dGen.LastLoadedFilePath;
        }
    }

    private void SetupBlocksList()
    {
        if (_blocksProp == null) return;

        _blocksList = new ReorderableList(serializedObject, _blocksProp, true, true, true, true);
        _blocksList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Blocks");
        _blocksList.onAddCallback = list =>
        {
            int i = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            var el = list.serializedProperty.GetArrayElementAtIndex(i);
            el.isExpanded = true; // expand new entries
            el.FindPropertyRelative("Name").stringValue = $"Block {i}";
            var clusters = el.FindPropertyRelative("Clusters");
            clusters.arraySize = 0;
        };
        _blocksList.elementHeightCallback = index =>
        {
            var el = _blocksProp.GetArrayElementAtIndex(index);
            float pad = 6f;
            float height = pad + EditorGUIUtility.singleLineHeight + 2f; // Name field
            // clusters list height
            var clustersProp = el.FindPropertyRelative("Clusters");
            var clustersList = GetOrCreateClustersList(clustersProp);
            height += clustersList.GetHeight() + pad;
            return height;
        };
        _blocksList.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = _blocksProp.GetArrayElementAtIndex(index);
            float pad = 3f;
            rect = new Rect(rect.x + 4, rect.y + pad, rect.width - 8, rect.height - pad * 2);

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0; // reduce horizontal indent

            var nameRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, el.FindPropertyRelative("Name"));

            var clustersProp = el.FindPropertyRelative("Clusters");
            var clustersList = GetOrCreateClustersList(clustersProp);
            var listRect = new Rect(rect.x, nameRect.yMax + 2f, rect.width, clustersList.GetHeight());
            clustersList.DoList(listRect);

            EditorGUI.indentLevel = prevIndent;
        };
    }

    private ReorderableList GetOrCreateClustersList(SerializedProperty clustersProp)
    {
        if (clustersProp == null) return null;
        string key = clustersProp.propertyPath;
        if (_clustersLists.TryGetValue(key, out var list)) return list;

        list = new ReorderableList(serializedObject, clustersProp, true, true, true, true);
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Clusters");
        list.onAddCallback = l =>
        {
            int i = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            var el = l.serializedProperty.GetArrayElementAtIndex(i);
            el.isExpanded = true; // expand new clusters
            el.FindPropertyRelative("PauseBefore").floatValue = 1f;
            el.FindPropertyRelative("Lines").arraySize = 0;
        };
        list.elementHeightCallback = idx =>
        {
            var el = clustersProp.GetArrayElementAtIndex(idx);
            float pad = 6f;
            float height = pad + EditorGUIUtility.singleLineHeight * 2 + 4f; // PauseBefore & Speed
            var linesProp = el.FindPropertyRelative("Lines");
            var linesList = GetOrCreateLinesList(linesProp);
            height += linesList.GetHeight() + pad;
            return height;
        };
        list.drawElementCallback = (r, idx, active, focused) =>
        {
            var el = clustersProp.GetArrayElementAtIndex(idx);
            float pad = 3f;
            r = new Rect(r.x + 8, r.y + pad, r.width - 16, r.height - pad * 2);

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var lineRect = new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(lineRect, el.FindPropertyRelative("PauseBefore"));
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(lineRect, el.FindPropertyRelative("LineAudio"));

            var linesProp = el.FindPropertyRelative("Lines");
            var linesList = GetOrCreateLinesList(linesProp);
            var listRect = new Rect(r.x, lineRect.y + EditorGUIUtility.singleLineHeight + 4f, r.width, linesList.GetHeight());
            linesList.DoList(listRect);

            EditorGUI.indentLevel = prevIndent;
        };

        _clustersLists[key] = list;
        return list;
    }

    private ReorderableList GetOrCreateLinesList(SerializedProperty linesProp)
    {
        if (linesProp == null) return null;
        string key = linesProp.propertyPath;
        if (_linesLists.TryGetValue(key, out var list)) return list;

        list = new ReorderableList(serializedObject, linesProp, true, true, true, true);
        list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Lines");
        list.onAddCallback = l =>
        {
            int i = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            var el = l.serializedProperty.GetArrayElementAtIndex(i);
            el.isExpanded = true; // expand new lines
            el.FindPropertyRelative("Speaker").enumValueIndex = 0;
            el.FindPropertyRelative("Line").stringValue = string.Empty;
            el.FindPropertyRelative("Wait").floatValue = 1f;
        };
        list.elementHeightCallback = idx =>
        {
            var el = linesProp.GetArrayElementAtIndex(idx);
            float pad = 6f;
            float h = pad;
            // Speaker
            h += EditorGUIUtility.singleLineHeight + 2f;
            // Line (TextArea height via property height)
            var lineProp = el.FindPropertyRelative("Line");
            h += EditorGUI.GetPropertyHeight(lineProp, true) + 2f;
            // Wait
            h += EditorGUIUtility.singleLineHeight + 2f;
            h += pad;
            return h;
        };
        list.drawElementCallback = (r, idx, active, focused) =>
        {
            var el = linesProp.GetArrayElementAtIndex(idx);
            float pad = 3f;
            r = new Rect(r.x + 12, r.y + pad, r.width - 24, r.height - pad * 2);

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var row = new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row, el.FindPropertyRelative("Speaker"));

            row.y += EditorGUIUtility.singleLineHeight + 2f;
            var lineProp = el.FindPropertyRelative("Line");
            float lineHeight = EditorGUI.GetPropertyHeight(lineProp, true);
            row.height = lineHeight;
            EditorGUI.PropertyField(row, lineProp, new GUIContent("Text"), true);

            row.y += lineHeight + 2f;
            row.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(row, el.FindPropertyRelative("Wait"));

            EditorGUI.indentLevel = prevIndent;
        };

        _linesLists[key] = list;
        return list;
    }

    private void RefreshFolderList(DialogueGenerator dGen)
    {
        string root = Path.Combine("Assets", "Resources", "Scripts");
        if (!Directory.Exists(root))
        {
            _subfolders = new[] { "" };
            _selectedIndex = 0;
            return;
        }

        var list = new List<string> { "" }; // empty means root
        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string rel = dir.Replace('\\', '/');
            if (rel.StartsWith(root.Replace('\\', '/'))) rel = rel.Substring(root.Length).TrimStart('/', '\\');
            list.Add(rel);
        }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        _subfolders = list.ToArray();

        string current = (dGen.SaveFolderRelative ?? string.Empty).Replace('\\', '/');
        _selectedIndex = Array.FindIndex(_subfolders, s => string.Equals(s.Replace('\\', '/'), current, StringComparison.OrdinalIgnoreCase));
        if (_selectedIndex < 0) _selectedIndex = 0;
    }

    public override void OnInspectorGUI()
    {
        if (!(target is DialogueGenerator dGen)) return;

        serializedObject.Update();

        EditorGUILayout.LabelField("Dialogue Data", EditorStyles.boldLabel);
        if (_blocksList == null) SetupBlocksList();
        if (_blocksList != null)
        {
            _blocksList.DoLayoutList();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

        if (_subfolders == null)
        {
            RefreshFolderList(dGen);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Save To (Resources/Scripts)", GUILayout.Width(200));
            int newIndex = EditorGUILayout.Popup(_selectedIndex, _subfolders);
            if (newIndex != _selectedIndex)
            {
                _selectedIndex = newIndex;
                dGen.SaveFolderRelative = _subfolders[_selectedIndex];
                EditorUtility.SetDirty(dGen);
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshFolderList(dGen);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("File Name", GUILayout.Width(200));
            string newName = EditorGUILayout.TextField(dGen.OutputFileName ?? "");
            if (newName != dGen.OutputFileName)
            {
                dGen.OutputFileName = newName;
                EditorUtility.SetDirty(dGen);
            }
        }

        string previewDir = Path.Combine("Assets/Resources/Scripts", dGen.SaveFolderRelative ?? string.Empty).Replace('\\', '/');
        string previewPath = Path.Combine(previewDir, string.IsNullOrEmpty(dGen.OutputFileName) ? "dialogue.json" : dGen.OutputFileName + ".json").Replace('\\', '/');
        EditorGUILayout.HelpBox($"Output Path: {previewPath}", MessageType.Info);

        if (GUILayout.Button("Write Dialogue to File"))
        {
            dGen.WriteDialogueToFile();
        }

        // ---- NEW: Load UI
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("JSON File Path", GUILayout.Width(100));
            _loadFilePath = EditorGUILayout.TextField(_loadFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string start = string.IsNullOrEmpty(_loadFilePath) ? Application.dataPath : _loadFilePath;
                string picked = EditorUtility.OpenFilePanel("Select Dialogue JSON", start, "json");
                if (!string.IsNullOrEmpty(picked))
                {
                    _loadFilePath = picked;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load From File"))
            {
                if (string.IsNullOrEmpty(_loadFilePath))
                {
                    EditorUtility.DisplayDialog("Load Dialogue", "Please pick a JSON file path first.", "OK");
                }
                else
                {
                    dGen.LoadDialogueFromFile(_loadFilePath);
                    // update local stored last path and keep editor in sync
                    dGen.LastLoadedFilePath = _loadFilePath;
                    EditorUtility.SetDirty(dGen);
                }
            }

            if (GUILayout.Button("Clear Blocks"))
            {
                if (EditorUtility.DisplayDialog("Clear Blocks", "Are you sure you want to clear all Blocks?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(dGen, "Clear Dialogue Blocks");
                    dGen.Blocks = new DialogueGenerator.DialogueBlock[0];
                    EditorUtility.SetDirty(dGen);
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

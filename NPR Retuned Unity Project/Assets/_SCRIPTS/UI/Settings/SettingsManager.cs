using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class SettingsEntry : MonoBehaviour
{
    public bool Highlighted;
    protected virtual void Update()
    {
        if (Highlighted)
        {
            var mp = new MaterialPropertyBlock();
            mp.SetColor("_EmissionColor", Color.red * 1024);
            GetComponent<MeshRenderer>().SetPropertyBlock(mp);
        }
        else
        {
            var mp = new MaterialPropertyBlock();
            mp.SetColor("_EmissionColor", Color.black);
            GetComponent<MeshRenderer>().SetPropertyBlock(mp);
        }
    }
}
public class SettingsManager : MonoBehaviour
{
    [SerializeField] private int _columns = 1;
    [SerializeField] private int _rows = 1;
    [SerializeField] private SettingsEntry[] _entries = Array.Empty<SettingsEntry>();
    [SerializeField] private Animator _anim;
    private bool _moved;
    private Vector2Int _currentHighlighted;
    void Start()
    {
        PInputManager.root.actions[PlayerActionType.Pause].bAction += TogglePause;
        PInputManager.root.actions[PlayerActionType.Drive].onV2ValueChange += MoveHighlighted;
        var entry = GetEntry(_currentHighlighted);
        if (entry != null)
        {
            entry.Highlighted = true;
        }
    }
    public void TogglePause()
    {
        if (GameManager.root.Paused)
        {
            _anim.SetBool("open", false);
            GameManager.root.Paused = false;
            Time.timeScale = 1f;
        }
        else
        {
            _anim.SetBool("open", true);
            GameManager.root.Paused = true;
            Time.timeScale = 0f;

            GetEntry(_currentHighlighted).Highlighted = false;
            _currentHighlighted = Vector2Int.zero;
            GetEntry(_currentHighlighted).Highlighted = true;
        }
    }
    private void MoveHighlighted(Vector2 input)
    {        
        if ((_moved && input != Vector2.zero) || !GameManager.root.Paused) return;
        else if (_moved && input == Vector2.zero)
        {
            _moved = false;

            return;
        }

        _moved = true;

        var potentialMove = new Vector2Int(
            input.x > 0 ? 1 : input.x < 0 ? -1 : 0,
            input.y < 0 ? 1 : input.y > 0 ? -1 : 0);


        var targetPosition = _currentHighlighted + potentialMove;

        if (!IsWithinBounds(targetPosition)) return;

        var targetEntry = GetEntry(targetPosition);
        if (targetEntry == null) return;

        var currentEntry = GetEntry(_currentHighlighted);
        currentEntry.Highlighted = false;

        _currentHighlighted = targetPosition;
        targetEntry.Highlighted = true;
    }

    private bool IsWithinBounds(Vector2Int position)
    {
        return position.x >= 0 && position.x < _columns &&
               position.y >= 0 && position.y < _rows;
    }

    public SettingsEntry GetEntry(Vector2Int position)
    {
        if (!IsWithinBounds(position))
        {
            return null;
        }

        var index = position.y * _columns + position.x;

        if (index < 0 || index >= _entries.Length)
        {
            return null;
        }

        return _entries[index];
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SettingsManager))]
public class SettingsManagerEditor : Editor
{
    private Vector2 _gridScroll;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var columnsProperty = serializedObject.FindProperty("_columns");
        var rowsProperty = serializedObject.FindProperty("_rows");
        var entriesProperty = serializedObject.FindProperty("_entries");
        var animProperty = serializedObject.FindProperty("_anim");

        if (columnsProperty == null || rowsProperty == null || entriesProperty == null)
        {
            EditorGUILayout.HelpBox("Failed to load grid properties.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();

            return;
        }

        var columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", columnsProperty.intValue));
        var rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", rowsProperty.intValue));

        if (columns != columnsProperty.intValue)
        {
            columnsProperty.intValue = columns;
        }

        if (rows != rowsProperty.intValue)
        {
            rowsProperty.intValue = rows;
        }

        var expectedSize = columns * rows;

        if (entriesProperty.arraySize != expectedSize)
        {
            entriesProperty.arraySize = expectedSize;
        }

        EditorGUILayout.Space();

        const float cellWidth = 80f;
        const float cellHeight = 42f;
        const float scrollMaxHeight = 260f;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _gridScroll = EditorGUILayout.BeginScrollView(
                _gridScroll,
                GUILayout.Height(Mathf.Min(scrollMaxHeight, rows * cellHeight)));

            for (var y = 0; y < rows; y++)
            {
                using (new EditorGUILayout.HorizontalScope(GUIStyle.none))
                {
                    for (var x = 0; x < columns; x++)
                    {
                        var index = y * columns + x;
                        var cellProperty = entriesProperty.GetArrayElementAtIndex(index);

                        using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.Width(cellWidth)))
                        {
                            EditorGUILayout.LabelField($"[{x}, {y}]", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(cellWidth));

                            var reference = cellProperty.objectReferenceValue;
                            var newReference = (SettingsEntry)EditorGUILayout.ObjectField(
                                reference,
                                typeof(SettingsEntry),
                                true,
                                GUILayout.Width(cellWidth),
                                GUILayout.Height(cellHeight - EditorGUIUtility.singleLineHeight));

                            if (newReference != reference)
                            {
                                cellProperty.objectReferenceValue = newReference;
                            }
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.PropertyField(animProperty);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
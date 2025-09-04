using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TextGlyph", menuName = "Objects/TextGlyph", order = 2)]
public class TextGlyph : ScriptableSingleton<TextGlyph>
{
    [Tooltip("Assign per-character meshes. Name each mesh with the character it represents (e.g., A, B, 0, !).")]
    public Mesh[] Meshes;

    private Dictionary<char, Mesh> _textMeshes = new Dictionary<char, Mesh>();

    public IReadOnlyDictionary<char, Mesh> TextMeshes
    {
        get
        {
            if (_textMeshes == null || _textMeshes.Count == 0)
                RebuildCache();
            return _textMeshes;
        }
    }

    public bool TryGet(char ch, out Mesh mesh)
    {
        if (_textMeshes == null || _textMeshes.Count == 0)
            RebuildCache();

        if (_textMeshes.TryGetValue(ch, out mesh))
            return true;
        char up = char.ToUpperInvariant(ch);
        if (_textMeshes.TryGetValue(up, out mesh))
            return true;
        return false;
    }

    private void OnEnable()
    {
        RebuildCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildCache();
    }
#endif

    private void RebuildCache()
    {
        if (_textMeshes == null)
            _textMeshes = new Dictionary<char, Mesh>();
        _textMeshes.Clear();

        if (Meshes == null) return;
        foreach (var m in Meshes)
        {
            if (m == null) continue;
            if (char.TryParse(m.name, out char c))
            {
                _textMeshes[c] = m;
                // Also map lowercase to the same mesh by default
                char lower = char.ToLowerInvariant(c);
                if (!_textMeshes.ContainsKey(lower))
                    _textMeshes[lower] = m;
            }
        }
    }
}

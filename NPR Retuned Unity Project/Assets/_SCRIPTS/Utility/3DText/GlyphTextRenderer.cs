using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GlyphTextRenderer : MonoBehaviour
{
    public enum HorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    [TextArea]
    public string text;

    [Header("Layout")]
    [Min(0f)] public float glyphScale = 1f;
    [Min(0f)] public float letterSpacing = 0.02f; // extra spacing between glyphs (world units)
    [Min(0f)] public float wordSpacing = 0.1f;    // spacing for spaces
    [Min(0f)] public float lineSpacing = 1.0f;    // spacing multiplier for new lines (relative to 1 unit glyph height)

    [Header("Alignment")]
    public HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left;
    [Tooltip("When enabled, alignment offsets are computed per line instead of across the entire block.")]
    public bool alignPerLine = false;

    [Header("Typing")]
    [Tooltip("Default delay between characters when typing.")]
    [Min(0f)] public float defaultCharDelay = 0.03f;

    [Header("Source Orientation Fix")]
    [Tooltip("Apply an extra rotation to each glyph mesh to correct source orientation (degrees). Common fix: Y=180.")]
    public Vector3 preRotationEuler = Vector3.zero;
    [Tooltip("Apply an extra scale to each glyph mesh. Set X=-1 to mirror horizontally if imported backwards.")]
    public Vector3 preScale = Vector3.one;
    [Tooltip("If the preScale mirrors the mesh (negative determinant), reverse triangle winding to keep front faces.")]
    public bool fixWindingOnMirror = true;
    [Tooltip("Recalculate normals after building. Useful if applying mirroring or non-uniform scale.")]
    public bool recalculateNormals = true;

    [SerializeField] private MeshFilter _mf;
    private string _lastBuiltText;
    private Coroutine _textRoutine;
    private readonly List<Vector3> _alignedVerts = new List<Vector3>();
    private readonly List<int> _vertexLineIndices = new List<int>();
    private readonly List<float> _lineMinCache = new List<float>();
    private readonly List<float> _lineMaxCache = new List<float>();
    private readonly List<float> _lineOffsetCache = new List<float>();
    private int _alreadyVisibleCharacters;
    public void SetText(string content)
    {
        SetText(content, defaultCharDelay);
    }

    public void SetText(string content, float charDelay, bool playAudio = false)
    {
        SetText(content, charDelay, playAudio, 0);
    }

    public void SetText(string content, float charDelay, bool playAudio, int alreadyVisibleCharacters)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        _mf.sharedMesh = mesh;
        if (content == null)
        {
            text = string.Empty;
            if (_textRoutine != null) StopCoroutine(_textRoutine);
            _textRoutine = null;
            return;
        }

        text = content;
        if (_textRoutine != null) StopCoroutine(_textRoutine);
        _alreadyVisibleCharacters = Mathf.Clamp(alreadyVisibleCharacters, 0, content.Length);
        _textRoutine = StartCoroutine(TypeOut(content, charDelay, playAudio));
    }

    private IEnumerator TypeOut(string content, float charDelay, bool playAudio)
    {
        _lastBuiltText = content ?? string.Empty;
        int skipCharacters = Mathf.Clamp(_alreadyVisibleCharacters, 0, _lastBuiltText.Length);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color32>();
        var tris = new List<int>();
        var vertexLines = _vertexLineIndices;
        vertexLines.Clear();

        Vector3 pen = Vector3.zero;
        int v0 = 0;
        float baseLineHeight = 1f;
        int currentLine = 0;

        var preRot = Quaternion.Euler(preRotationEuler);
        bool mirrored = (preScale.x * preScale.y * preScale.z) < 0f;

        if (playAudio) AudioManager.root.PlaySound(AudioEvent.playTTSVoice, gameObject, 1);

        for (int i = 0; i < _lastBuiltText.Length; i++)
        {
            char ch = _lastBuiltText[i];
            if (ch == '\n')
            {
                pen.x = 0f;
                pen.y -= baseLineHeight * lineSpacing * glyphScale;
                currentLine++;
                continue;
            }
            if (ch == ' ')
            {
                pen.x += wordSpacing + letterSpacing;
                continue;
            }

            if (!TextGlyph.root.TryGet(ch, out var glyphMesh) || glyphMesh == null)
            {
                // Skip unknown glyphs
                continue;
            }

            if (playAudio)
            {
                int index;

                if (int.TryParse(ch.ToString(), out int num))
                {
                    index = num;
                }
                else
                {
                    index = char.ToUpper(ch) - 55;
                }

                AudioManager.root.SetRTPC(AudioRTPC.TTS_Character, index, false, AudioEvent.playTTSVoice, gameObject);
            }

            var gVerts = glyphMesh.vertices;
            var gNorms = glyphMesh.normals;
            var gTris = glyphMesh.triangles;

            // Transform and append vertices
            for (int v = 0; v < gVerts.Length; v++)
            {
                var p = gVerts[v];
                p = Vector3.Scale(p, preScale);
                p = preRot * p;
                p = p * glyphScale + pen;
                verts.Add(p);
                vertexLines.Add(currentLine);

                if (gNorms != null && gNorms.Length == gVerts.Length)
                {
                    var n = gNorms[v];
                    n = preRot * n;
                    norms.Add(n.normalized);
                }
                else
                {
                    norms.Add(Vector3.forward);
                }
                cols.Add(Color.white);
            }

            if (fixWindingOnMirror && mirrored)
            {
                for (int t = 0; t < gTris.Length; t += 3)
                {
                    int a = gTris[t];
                    int b = gTris[t + 1];
                    int c = gTris[t + 2];
                    tris.Add(v0 + a);
                    tris.Add(v0 + c);
                    tris.Add(v0 + b);
                }
            }
            else
            {
                for (int t = 0; t < gTris.Length; t++)
                    tris.Add(v0 + gTris[t]);
            }

            v0 += gVerts.Length;

            var width = glyphMesh.bounds.size.x * Mathf.Abs(preScale.x) * glyphScale;
            pen.x += width + letterSpacing;

            var mesh = _mf.sharedMesh;

            mesh.Clear();
            ApplyHorizontalAlignment(verts, vertexLines, _alignedVerts);
            mesh.SetVertices(_alignedVerts);

            if (!recalculateNormals) mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);

            if (recalculateNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (charDelay > 0f)
            {
                if (i >= skipCharacters)
                {
                    yield return new WaitForSeconds(charDelay);
                }
            }
        }

        _alreadyVisibleCharacters = 0;
        if (playAudio) AudioManager.root.StopSound(AudioEvent.playTTSVoice, gameObject, 1);
    }
    public void ClearText()
    {
        var mesh = _mf.mesh;

        mesh.Clear();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void ApplyHorizontalAlignment(List<Vector3> source, List<int> lineIndices, List<Vector3> destination)
    {
        destination.Clear();
        if (source == null || source.Count == 0)
            return;

        if (!alignPerLine)
        {
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;

            for (int i = 0; i < source.Count; i++)
            {
                float x = source[i].x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }

            float offsetX = 0f;
            switch (horizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    offsetX = -minX;
                    break;
                case HorizontalAlignment.Center:
                    offsetX = -0.5f * (minX + maxX);
                    break;
                case HorizontalAlignment.Right:
                    offsetX = -maxX;
                    break;
            }

            for (int i = 0; i < source.Count; i++)
            {
                var p = source[i];
                p.x += offsetX;
                destination.Add(p);
            }
            return;
        }

        int lineCount = 0;
        for (int i = 0; i < lineIndices.Count; i++)
        {
            int line = lineIndices[i];
            if (line + 1 > lineCount) lineCount = line + 1;
        }

        EnsureListCapacity(_lineMinCache, lineCount, float.PositiveInfinity);
        EnsureListCapacity(_lineMaxCache, lineCount, float.NegativeInfinity);
        EnsureListCapacity(_lineOffsetCache, lineCount, 0f);

        for (int i = 0; i < lineCount; i++)
        {
            _lineMinCache[i] = float.PositiveInfinity;
            _lineMaxCache[i] = float.NegativeInfinity;
            _lineOffsetCache[i] = 0f;
        }

        for (int i = 0; i < source.Count; i++)
        {
            int line = lineIndices[i];
            float x = source[i].x;
            if (x < _lineMinCache[line]) _lineMinCache[line] = x;
            if (x > _lineMaxCache[line]) _lineMaxCache[line] = x;
        }

        for (int line = 0; line < lineCount; line++)
        {
            float minX = _lineMinCache[line];
            float maxX = _lineMaxCache[line];
            if (float.IsInfinity(minX) || float.IsInfinity(maxX))
            {
                _lineOffsetCache[line] = 0f;
                continue;
            }

            switch (horizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    _lineOffsetCache[line] = -minX;
                    break;
                case HorizontalAlignment.Center:
                    _lineOffsetCache[line] = -0.5f * (minX + maxX);
                    break;
                case HorizontalAlignment.Right:
                    _lineOffsetCache[line] = -maxX;
                    break;
            }
        }

        for (int i = 0; i < source.Count; i++)
        {
            int line = lineIndices[i];
            var p = source[i];
            p.x += _lineOffsetCache[line];
            destination.Add(p);
        }
    }

    private static void EnsureListCapacity(List<float> list, int requiredCount, float fillValue)
    {
        while (list.Count < requiredCount)
            list.Add(fillValue);
    }
}


#if UNITY_EDITOR

[CustomEditor(typeof(GlyphTextRenderer))]
public class GlyphTextRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Set Text") && target is GlyphTextRenderer gr) gr.SetText(gr.text);
    }
}

#endif

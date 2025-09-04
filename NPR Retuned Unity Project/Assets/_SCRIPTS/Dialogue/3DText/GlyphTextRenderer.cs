using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GlyphTextRenderer : MonoBehaviour
{
    [TextArea]
    public string text;

    [Header("Layout")]
    [Min(0f)] public float glyphScale = 1f;
    [Min(0f)] public float letterSpacing = 0.02f; // extra spacing between glyphs (world units)
    [Min(0f)] public float wordSpacing = 0.1f;    // spacing for spaces
    [Min(0f)] public float lineSpacing = 1.0f;    // spacing multiplier for new lines (relative to 1 unit glyph height)

    [Header("Source Orientation Fix")]
    [Tooltip("Apply an extra rotation to each glyph mesh to correct source orientation (degrees). Common fix: Y=180.")]
    public Vector3 preRotationEuler = Vector3.zero;
    [Tooltip("Apply an extra scale to each glyph mesh. Set X=-1 to mirror horizontally if imported backwards.")]
    public Vector3 preScale = Vector3.one;
    [Tooltip("If the preScale mirrors the mesh (negative determinant), reverse triangle winding to keep front faces.")]
    public bool fixWindingOnMirror = true;
    [Tooltip("Recalculate normals after building. Useful if applying mirroring or non-uniform scale.")]
    public bool recalculateNormals = true;

    private MeshFilter _mf;
    private string _lastBuiltText;
    private Coroutine _textRoutine;
    void Awake()
    {
        _mf = GetComponent<MeshFilter>();

        Mesh mesh = new Mesh();
        mesh.name = name;
        _mf.mesh = mesh;
    }
    public void SetText(DialogueLine line)
    {
        if (line == null)
        {
            text = string.Empty;
            if (_textRoutine != null) StopCoroutine(_textRoutine);
            _textRoutine = null;
            return;
        }
        //if (line.text == text) return;
        text = line.text;

        if (_textRoutine != null) StopCoroutine(_textRoutine);
        _textRoutine = StartCoroutine(BuildMesh(line));
    }

    public IEnumerator BuildMesh(DialogueLine line)
    {
        _lastBuiltText = line.text ?? string.Empty;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color32>();
        var tris = new List<int>();

        Vector3 pen = Vector3.zero;
        int v0 = 0;
        float baseLineHeight = 1f;

        var preRot = Quaternion.Euler(preRotationEuler);
        bool mirrored = (preScale.x * preScale.y * preScale.z) < 0f;

        for (int i = 0; i < _lastBuiltText.Length; i++)
        {
            char ch = _lastBuiltText[i];
            if (ch == '\n')
            {
                pen.x = 0f;
                pen.y -= baseLineHeight * lineSpacing * glyphScale;
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

            var mesh = _mf.mesh;

            mesh.Clear();
            mesh.SetVertices(verts);

            if (!recalculateNormals) mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);

            if (recalculateNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            yield return new WaitForSeconds(line.speed);
        }

    }
}

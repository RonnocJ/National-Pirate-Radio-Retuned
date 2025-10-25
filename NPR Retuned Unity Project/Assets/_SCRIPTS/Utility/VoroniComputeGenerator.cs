using UnityEngine;

namespace CodeCopilot.Noise
{
    public enum DistanceMetric { Euclidean = 0, Manhattan = 1, Chebyshev = 2 }
    public enum VoronoiValue { F1 = 0, F2 = 1, F2MinusF1 = 2 }

    /// <summary>
    /// Runtime Voronoi/Worley texture generator using a ComputeShader.
    /// Use Generate() to get an RFloat RenderTexture suitable for sampling in shaders or CPU baking.
    /// </summary>
    [ExecuteAlways]
    public class VoronoiComputeGenerator : MonoBehaviour
    {
        [Header("Shader")]
        [Tooltip("Assign Assets/Shaders/Voronoi.compute")]
        public ComputeShader voronoiCompute;

        [Header("Output")]
        public int width = 512;
        public int height = 512;
        [Tooltip("Auto-created if null or size mismatch.")]
        public RenderTexture output;

        [Header("Noise Params")]
        public int seed = 1337;
        [Tooltip("Cells per [0,1] range.")]
        public float scale = 64f;
        [Range(0f, 1.5f)]
        public float jitter = 0.8f;
        public DistanceMetric metric = DistanceMetric.Euclidean;
        public VoronoiValue valueType = VoronoiValue.F1;
        public bool invert = false;

        [Header("Tiling (cells)")]
        [Tooltip("Wrap after N cells horizontally (0 = no tiling).")]
        public int tileX = 0;
        [Tooltip("Wrap after N cells vertically (0 = no tiling).")]
        public int tileY = 0;

        const int THREADS = 8;
        int _kernel = -1;

        /// <summary>Create/resize the output RT if needed (RFloat, random-write).</summary>
        RenderTexture EnsureRT(int w, int h)
        {
            if (output != null && (output.width != w || output.height != h))
            {
                if (Application.isPlaying) Destroy(output);
                else DestroyImmediate(output);
                output = null;
            }

            if (output == null)
            {
                output = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = (tileX > 0 || tileY > 0) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    name = $"Voronoi_{w}x{h}"
                };
                output.Create();
            }
            else
            {
                // why: keep wrap consistent with tiling changes
                output.wrapMode = (tileX > 0 || tileY > 0) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            }

            return output;
        }

        /// <summary>Dispatch compute and return the RenderTexture.</summary>
        public RenderTexture Generate(int w = -1, int h = -1)
        {
            if (voronoiCompute == null)
            {
                Debug.LogError("VoronoiComputeGenerator: ComputeShader not assigned.");
                return null;
            }

            int W = (w > 0) ? w : Mathf.Max(1, width);
            int H = (h > 0) ? h : Mathf.Max(1, height);

            var rt = EnsureRT(W, H);

            if (_kernel < 0) _kernel = voronoiCompute.FindKernel("CSMain");

            voronoiCompute.SetInt("Width", W);
            voronoiCompute.SetInt("Height", H);
            voronoiCompute.SetInt("Seed", seed);
            voronoiCompute.SetFloat("Scale", Mathf.Max(0.0001f, scale));
            voronoiCompute.SetFloat("Jitter", Mathf.Max(0f, jitter));
            voronoiCompute.SetInt("Metric", (int)metric);
            voronoiCompute.SetInt("ValueType", (int)valueType);
            voronoiCompute.SetInt("Invert", invert ? 1 : 0);
            voronoiCompute.SetInt("TileX", Mathf.Max(0, tileX));
            voronoiCompute.SetInt("TileY", Mathf.Max(0, tileY));
            voronoiCompute.SetTexture(_kernel, "Result", rt);

            int gx = (W + THREADS - 1) / THREADS;
            int gy = (H + THREADS - 1) / THREADS;

            voronoiCompute.Dispatch(_kernel, gx, gy, 1);

            return rt;
        }

        /// <summary>Blocking CPU copy. Use only if you truly need a Texture2D.</summary>
        public Texture2D ToTexture2D()
        {
            var rt = Generate();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RFloat, true, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(true, false);
            RenderTexture.active = prev;
            return tex;
        }

        void OnValidate() { if (!Application.isPlaying) Generate(); }
        void Start() { if (Application.isPlaying) Generate(); }
    }
}
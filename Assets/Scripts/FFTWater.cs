using System;
using System.Collections;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FFTWater : MonoBehaviour {
    public Shader waterShader;
    public ComputeShader fftComputeShader;

    public Atmosphere atmosphere;

    public int planeLength = 10;
    public int quadRes = 10;

    private Camera cam;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] normals;

    public struct SpectrumSettings {
        public float scale;
        public float angle;
        public float spreadBlend;
        public float swell;
        public float alpha;
        public float peakOmega;
        public float gamma;
        public float shortWavesFade; 
    }

    SpectrumSettings[] spectrums = new SpectrumSettings[2];

    [System.Serializable]
    public struct DisplaySpectrumSettings {
        [Range(0, 1)]
        public float scale;
        public float windSpeed;
        [Range(0.0f, 360.0f)]
        public float windDirection;
        public float fetch;
        [Range(0, 1)]
        public float spreadBlend;
        [Range(0, 1)]
        public float swell;
        public float peakEnhancement;
        public float shortWavesFade;
    }

    [Header("Spectrum Settings")]
    [Range(0, 2048)]
    public int lengthScale = 256;
    
    [Range(0.01f, 200.0f)]
    public float tile = 8.0f;

    [Range(0, 100000)]
    public int seed = 0;

    [Range(0.0f, 0.1f)]
    public float lowCutoff = 0.0001f;

    [Range(0.1f, 9000.0f)]
    public float highCutoff = 9000.0f;

    [Range(0.0f, 20.0f)]
    public float gravity = 9.81f;

    [Range(2.0f, 20.0f)]
    public float depth = 20.0f;

    [Range(0.0f, 200.0f)]
    public float repeatTime = 200.0f;

    [SerializeField]
    public DisplaySpectrumSettings spectrum1;
    
    [SerializeField]
    public DisplaySpectrumSettings spectrum2;

    public bool updateSpectrum = false;

    [Range(0.0f, 5.0f)]
    public float speed = 1.0f;

    public Vector2 lambda = new Vector2(-1.0f, -1.0f);



    [Header("Material Settings")]
    [Range(0.0f, 20.0f)]
    public float normalStrength = 1;

    // Shader Settings
    [ColorUsageAttribute(false, true)]
    public Color ambient;

    [ColorUsageAttribute(false, true)]
    public Color diffuseReflectance;

    [ColorUsageAttribute(false, true)]
    public Color specularReflectance;

    [Range(0.0f, 10.0f)]
    public float shininess = 1.0f;

    [Range(0.0f, 5.0f)]
    public float specularNormalStrength = 1.0f;

    [ColorUsageAttribute(false, true)]
    public Color fresnelColor;

    public bool useTextureForFresnel = false;
    public Texture environmentTexture;

    [Range(0.0f, 1.0f)]
    public float fresnelBias = 0.0f;

    [Range(0.0f, 3.0f)]
    public float fresnelStrength = 1.0f;

    [Range(0.0f, 20.0f)]
    public float fresnelShininess = 5.0f;

    [Range(0.0f, 5.0f)]
    public float fresnelNormalStrength = 1.0f;

    [ColorUsageAttribute(false, true)]
    public Color tipColor;

    [Header("Foam Settings")]
    [Range(-2.0f, 2.0f)]
    public float foamBias = -0.5f;

    [Range(-10.0f, 10.0f)]
    public float foamThreshold = 0.0f;

    [Range(0.0f, 1.0f)]
    public float foamAdd = 0.5f;

    [Range(0.0f, 1.0f)]
    public float foamDecayRate = 0.05f;

    private RenderTexture displacementTex, 
                          normalTex, 
                          initialSpectrumTex, 
                          pingPongTex, 
                          pingPongTex2, 
                          htildeSlopeTex, 
                          htildeDisplacementTex,
                          spectrumTextures;

    private ComputeBuffer spectrumBuffer;

    private int N, logN, threadGroupsX, threadGroupsY;

    public RenderTexture GetDisplacementMap() {
        return displacementTex;
    }

    public RenderTexture GetNormalMap() {
        return normalTex;
    }

    public RenderTexture GetInitialSpectrum() {
        return initialSpectrumTex;
    }

    public RenderTexture GetSlope() {
        return htildeSlopeTex;
    }

    public RenderTexture GetDisplacementSpectrum() {
        return htildeDisplacementTex;
    }

    private void CreateWaterPlane() {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Water";
        mesh.indexFormat = IndexFormat.UInt32;

        float halfLength = planeLength * 0.5f;
        int sideVertCount = planeLength * quadRes;

        vertices = new Vector3[(sideVertCount + 1) * (sideVertCount + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for (int i = 0, x = 0; x <= sideVertCount; ++x) {
            for (int z = 0; z <= sideVertCount; ++z, ++i) {
                vertices[i] = new Vector3(((float)x / sideVertCount * planeLength) - halfLength, 0, ((float)z / sideVertCount * planeLength) - halfLength);
                uv[i] = new Vector2((float)x / sideVertCount, (float)z / sideVertCount);
                tangents[i] = tangent;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.tangents = tangents;

        int[] triangles = new int[sideVertCount * sideVertCount * 6];

        for (int ti = 0, vi = 0, x = 0; x < sideVertCount; ++vi, ++x) {
            for (int z = 0; z < sideVertCount; ti += 6, ++vi, ++z) {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + sideVertCount + 2;
                triangles[ti + 3] = vi;
                triangles[ti + 4] = vi + sideVertCount + 2;
                triangles[ti + 5] = vi + sideVertCount + 1;
            }
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        normals = mesh.normals;
    }

    void CreateMaterial() {
        if (waterShader == null) return;

        waterMaterial = new Material(waterShader);

        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void SetFFTUniforms() {
        fftComputeShader.SetVector("_Lambda", lambda);
        fftComputeShader.SetFloat("_FrameTime", Time.time * speed);
        fftComputeShader.SetFloat("_DeltaTime", Time.deltaTime);
        fftComputeShader.SetFloat("_Gravity", gravity);
        fftComputeShader.SetFloat("_RepeatTime", repeatTime);
        fftComputeShader.SetInt("_N", N);
        fftComputeShader.SetInt("_Seed", seed);
        fftComputeShader.SetInt("_LengthScale", lengthScale);
        fftComputeShader.SetFloat("_NormalStrength", normalStrength);
        fftComputeShader.SetFloat("_FoamThreshold", foamThreshold);
        fftComputeShader.SetFloat("_Depth", depth);
        fftComputeShader.SetFloat("_LowCutoff", lowCutoff);
        fftComputeShader.SetFloat("_HighCutoff", highCutoff);
        fftComputeShader.SetFloat("_FoamBias", foamBias);
        fftComputeShader.SetFloat("_FoamDecayRate", foamDecayRate);
        fftComputeShader.SetFloat("_FoamThreshold", foamThreshold);
        fftComputeShader.SetFloat("_FoamAdd", foamAdd);
    }

    float JonswapAlpha(float fetch, float windSpeed) {
        return 0.076f * Mathf.Pow(gravity * fetch / windSpeed / windSpeed, -0.22f);
    }

    float JonswapPeakFrequency(float fetch, float windSpeed) {
        return 22 * Mathf.Pow(windSpeed * fetch / gravity / gravity, -0.33f);
    }

    void FillSpectrumStruct(DisplaySpectrumSettings displaySettings, ref SpectrumSettings computeSettings) {
        computeSettings.scale = displaySettings.scale;
        computeSettings.angle = displaySettings.windDirection / 180 * Mathf.PI;
        computeSettings.spreadBlend = displaySettings.spreadBlend;
        computeSettings.swell = Mathf.Clamp(displaySettings.swell, 0.01f, 1);
        computeSettings.alpha = JonswapAlpha(displaySettings.fetch, displaySettings.windSpeed);
        computeSettings.peakOmega = JonswapPeakFrequency(displaySettings.fetch, displaySettings.windSpeed);
        computeSettings.gamma = displaySettings.peakEnhancement;
        computeSettings.shortWavesFade = displaySettings.shortWavesFade;
    }

    void SetSpectrumBuffers() {
        FillSpectrumStruct(spectrum1, ref spectrums[0]);
        FillSpectrumStruct(spectrum2, ref spectrums[1]);

        spectrumBuffer.SetData(spectrums);
        fftComputeShader.SetBuffer(0, "_Spectrums", spectrumBuffer);
    }

    void InverseFFT(RenderTexture spectrumTextures) {
        fftComputeShader.SetTexture(3, "_FourierTarget", spectrumTextures);
        fftComputeShader.Dispatch(3, 1, N, 1);
        fftComputeShader.SetTexture(4, "_FourierTarget", spectrumTextures);
        fftComputeShader.Dispatch(4, 1, N, 1);
    }

    RenderTexture CreateRenderTex(int width, int height, RenderTextureFormat format, bool useMips) {
        RenderTexture rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.enableRandomWrite = true;
        rt.Create();

        return rt;
    }


    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();

        N = 1024;
        logN = (int)Mathf.Log(N, 2.0f);
        threadGroupsX = Mathf.CeilToInt(N / 8.0f);
        threadGroupsY = Mathf.CeilToInt(N / 8.0f);

        initialSpectrumTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        htildeSlopeTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        htildeDisplacementTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        pingPongTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        pingPongTex2 = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, false);
        displacementTex = CreateRenderTex(N, N, RenderTextureFormat.ARGBHalf, true);
        normalTex = CreateRenderTex(N, N, RenderTextureFormat.RGHalf, true);

        spectrumTextures = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        spectrumTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        spectrumTextures.enableRandomWrite = true;
        spectrumTextures.volumeDepth = 2;
        spectrumTextures.Create();

        spectrumBuffer = new ComputeBuffer(2, 8 * sizeof(float));

        SetFFTUniforms();
        SetSpectrumBuffers();
        // Compute initial JONSWAP spectrum
        fftComputeShader.SetTexture(0, "_InitialSpectrumTex", initialSpectrumTex);
        fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        fftComputeShader.SetTexture(1, "_InitialSpectrumTex", initialSpectrumTex);
        fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);
    }

    void Update() {
        waterMaterial.SetVector("_Ambient", ambient);
        waterMaterial.SetVector("_DiffuseReflectance", diffuseReflectance);
        waterMaterial.SetVector("_SpecularReflectance", specularReflectance);
        waterMaterial.SetVector("_TipColor", tipColor);
        waterMaterial.SetVector("_FresnelColor", fresnelColor);
        waterMaterial.SetFloat("_Shininess", shininess * 100);
        waterMaterial.SetFloat("_FresnelBias", fresnelBias);
        waterMaterial.SetFloat("_FresnelStrength", fresnelStrength);
        waterMaterial.SetFloat("_FresnelShininess", fresnelShininess);
        waterMaterial.SetFloat("_NormalStrength", normalStrength);
        waterMaterial.SetFloat("_FresnelNormalStrength", fresnelNormalStrength);
        waterMaterial.SetFloat("_SpecularNormalStrength", specularNormalStrength);
        waterMaterial.SetInt("_UseEnvironmentMap", useTextureForFresnel ? 1 : 0);
        waterMaterial.SetFloat("_Tile", tile);

        SetFFTUniforms();
        if (updateSpectrum) {
            SetSpectrumBuffers();
            fftComputeShader.SetTexture(0, "_InitialSpectrumTex", initialSpectrumTex);
            fftComputeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
            fftComputeShader.SetTexture(1, "_InitialSpectrumTex", initialSpectrumTex);
            fftComputeShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);
        }
        
        // Progress Spectrum For FFT
        fftComputeShader.SetTexture(2, "_InitialSpectrumTex", initialSpectrumTex);
        fftComputeShader.SetTexture(2, "_HTildeSlopeTex", htildeSlopeTex);
        fftComputeShader.SetTexture(2, "_HTildeDisplacementTex", htildeDisplacementTex);
        fftComputeShader.SetTexture(2, "_SpectrumTextures", spectrumTextures);
        fftComputeShader.Dispatch(2, threadGroupsX, threadGroupsY, 1);

        // Compute FFT For Height
        InverseFFT(spectrumTextures);

        // Assemble maps
        fftComputeShader.SetTexture(5, "_HTildeSlopeTex", htildeSlopeTex);
        fftComputeShader.SetTexture(5, "_HTildeDisplacementTex", htildeDisplacementTex);
        fftComputeShader.SetTexture(5, "_DisplacementTex", displacementTex);
        fftComputeShader.SetTexture(5, "_SpectrumTextures", spectrumTextures);
        fftComputeShader.SetTexture(5, "_NormalTex", normalTex);
        fftComputeShader.Dispatch(5, threadGroupsX, threadGroupsY, 1);

        
        displacementTex.GenerateMips();
        normalTex.GenerateMips();

        


        //fftComputeShader.SetTexture(8, "_SpectrumTextures", spectrumTextures);
        //fftComputeShader.Dispatch(8, threadGroupsX, threadGroupsY, 1);

        //Graphics.Blit(foamTex, pingPongTex);

        //fftComputeShader.SetTexture(9, "_PingTex", pingPongTex);
        //fftComputeShader.SetTexture(9, "_FoamTex", foamTex);
        //fftComputeShader.Dispatch(9, threadGroupsX, threadGroupsY, 1);

        waterMaterial.SetTexture("_DisplacementTex", displacementTex);
        waterMaterial.SetTexture("_NormalTex", normalTex);
        waterMaterial.SetTexture("_SpectrumTextures", spectrumTextures);

        if (useTextureForFresnel) {
            waterMaterial.SetTexture("_EnvironmentMap", environmentTexture);
        }

        if (atmosphere != null) {
            waterMaterial.SetVector("_SunDirection", atmosphere.GetSunDirection());
            waterMaterial.SetVector("_SunColor", atmosphere.GetSunColor());
        }

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;
        waterMaterial.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);
    }

    void OnDisable() {
        if (waterMaterial != null) {
            Destroy(waterMaterial);
            waterMaterial = null;
        }

        if (mesh != null) {
            Destroy(mesh);
            mesh = null;
            vertices = null;
            normals = null;
        }

        Destroy(displacementTex);
        Destroy(normalTex);
        Destroy(initialSpectrumTex);
        Destroy(pingPongTex);
        Destroy(pingPongTex2);
        Destroy(htildeSlopeTex);
        Destroy(htildeDisplacementTex);
    }

    private void OnDrawGizmos() {
        /*
        if (vertices == null) return;

        for (int i = 0; i < vertices.Length; ++i) {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(transform.TransformPoint(displacedVertices[i]), 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.TransformPoint(displacedVertices[i]), displacedNormals[i]);
        }
        */
    }
}

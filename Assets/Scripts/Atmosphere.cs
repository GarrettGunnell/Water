using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class Atmosphere : MonoBehaviour {
    public Shader atmosphereShader;

    [Header("Skybox Settings")]
    public Texture skyboxTex;
    public Vector3 skyboxDirection = new Vector3(0.0f, -1.0f, 0.0f);
    [Range(0.0f, 2.0f)]
    public float skyboxSpeed = 0.1f;

    [Header("Sun Settings")]
    public Vector3 sunDirection = new Vector3(0.0f, 1.0f, 0.0f);

    [ColorUsageAttribute(false, true)]
    public Color sunColor;

    [Header("Fog Settings")]
    [Range(0.0f, 1000.0f)]
    public float fogHeight = 500.0f;

    [Range(0.01f, 5.0f)]
    public float fogAttenuation = 1.2f;

    public Color fogColor;
    
    [Range(0.0f, 2.0f)]
    public float fogDensity = 0.0f;

    [Range(0.0f, 1000.0f)]
    public float fogOffset = 0.0f;

    private Camera cam;
    private Material atmosphereMaterial;
    private RenderTexture colorTexture, depthTexture;
    private Vector2 currentResolution = new Vector2(0.0f, 0.0f);

    public Vector3 GetSunDirection() {
        return sunDirection;
    }

    public Vector3 GetSkyboxDirection() {
        return skyboxDirection;
    }

    public float GetSkyboxSpeed() {
        return skyboxSpeed;
    }

    public Color GetSunColor() {
        return sunColor;
    }

    public RenderTexture GetRenderTarget() {
        return colorTexture;
    }

    void OnEnable() {
        atmosphereMaterial = new Material(atmosphereShader);
        cam = GetComponent<Camera>();
        if (currentResolution.x != Screen.width || currentResolution.y != Screen.height) {
            if (colorTexture)
                Destroy(colorTexture);
            if (depthTexture)
                Destroy(depthTexture);
            
            
            colorTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            depthTexture = new RenderTexture(Screen.width, Screen.height, 32, RenderTextureFormat.Depth);
            
            cam.SetTargetBuffers(colorTexture.colorBuffer, depthTexture.depthBuffer);
            currentResolution = new Vector2(Screen.width, Screen.height);
        }
    }

    void Update() {
        if (currentResolution.x != Screen.width || currentResolution.y != Screen.height) {
            if (colorTexture)
                Destroy(colorTexture);
            if (depthTexture)
                Destroy(depthTexture);
            
            
            colorTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            depthTexture = new RenderTexture(Screen.width, Screen.height, 32, RenderTextureFormat.Depth);
            
            cam.SetTargetBuffers(colorTexture.colorBuffer, depthTexture.depthBuffer);
            currentResolution = new Vector2(Screen.width, Screen.height);
            Debug.Log("Regenerated target buffers");
        }
    }

    void OnDisable() {
        Destroy(atmosphereMaterial);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination) {
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;
        atmosphereMaterial.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);
        atmosphereMaterial.SetTexture("_DepthTexture", depthTexture);
        atmosphereMaterial.SetVector("_FogColor", fogColor);
        atmosphereMaterial.SetVector("_SunColor", sunColor);
        atmosphereMaterial.SetVector("_SunDirection", sunDirection);
        atmosphereMaterial.SetVector("_SkyboxDirection", skyboxDirection);
        atmosphereMaterial.SetFloat("_FogDensity", fogDensity);
        atmosphereMaterial.SetFloat("_FogOffset", fogOffset);
        atmosphereMaterial.SetFloat("_FogHeight", fogHeight);
        atmosphereMaterial.SetFloat("_FogAttenuation", fogAttenuation);
        atmosphereMaterial.SetFloat("_SkyboxSpeed", skyboxSpeed);
        atmosphereMaterial.SetTexture("_SkyboxTex", skyboxTex);

        Graphics.Blit(colorTexture, destination, atmosphereMaterial);
        Graphics.Blit(destination, colorTexture);
    }
}

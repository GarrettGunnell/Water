using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Atmosphere : MonoBehaviour {
    public Shader atmosphereShader;

    [Header("Fog Settings")]
    public Color fogColor;
    
    [Range(0.0f, 2.0f)]
    public float fogDensity = 0.0f;

    [Range(0.0f, 1000.0f)]
    public float fogOffset = 0.0f;

    private Camera cam;
    private Material atmosphereMaterial;
    private RenderTexture colorTexture, depthTexture;
    private Vector2 currentResolution = new Vector2(0.0f, 0.0f);

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
        atmosphereMaterial.SetFloat("_FogDensity", fogDensity);
        atmosphereMaterial.SetFloat("_FogOffset", fogOffset);

        Graphics.Blit(colorTexture, destination, atmosphereMaterial);
    }
}

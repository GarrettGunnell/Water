using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Buoyancy : MonoBehaviour {
    public FFTWater waterSource;

    private AsyncGPUReadbackRequest heightRequest;

    private int cachedFrameCount;
    
    void Start() {
        if (waterSource == null) return;

        heightRequest = AsyncGPUReadback.Request(waterSource.GetDisplacementMap(), 0, 0, 1, 0, 1, 0, 1, null);
        cachedFrameCount = Time.frameCount;
    }

    void Update() {
        if (waterSource == null) return;

        if (heightRequest.done) {
            //Debug.Log("Water height query finished!");

            if (heightRequest.hasError) {
                Debug.Log("Error detected!");
                return;
            }

            //Debug.LogFormat("There were {0} frames of latency.", Time.frameCount - cachedFrameCount);
            cachedFrameCount = Time.frameCount;

            NativeArray<ushort> waterHeightQuery = heightRequest.GetData<ushort>();

            float height = Mathf.HalfToFloat(waterHeightQuery[1]);

            this.transform.position = new Vector3(0, height, 0);

            //Debug.Log("Starting new query...");
            heightRequest = AsyncGPUReadback.Request(waterSource.GetDisplacementMap(), 0, 0, 1, 0, 1, 0, 1, null);
        }
    }

    void OnDisable() {

    }
}

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

public class Buoyancy : MonoBehaviour {
    public FFTWater waterSource;

    private AsyncGPUReadbackRequest buoyancyDataRequest, slopeRequest;

    private Rigidbody rigidBody;
    private int cachedFrameCount;
    private Vector3 cachedBuoyancyData = Vector3.zero;
    private Vector3 cachedNormal = Vector3.zero;
    private float timeSinceUpdate = 0.0f;

    private Queue<float> cachedHeights;
    
    private float getAverageWaterHeight() {
        if (cachedHeights.Count == 0) return 0.0f;

        float heightSum = 0.0f;
        float[] heightArray = cachedHeights.ToArray();

        for (int i = 0; i < heightArray.Length; ++i)
            heightSum += heightArray[i];

        return heightSum / heightArray.Length;
    }


    void OnEnable() {
        if (waterSource == null) return;

        rigidBody = GetComponent<Rigidbody>();

        cachedHeights = new Queue<float>();

        buoyancyDataRequest = AsyncGPUReadback.Request(waterSource.GetDisplacementMap(), 0, 0, 1, 0, 1, 0, 1, null);
        slopeRequest = AsyncGPUReadback.Request(waterSource.GetSlopeMap(), 0, 0, 1, 0, 1, 0, 1, null);
        cachedFrameCount = Time.frameCount;
    }

    void Update() {
        if (waterSource == null) return;

        timeSinceUpdate += Time.deltaTime * 20;

        if (buoyancyDataRequest.done) {
            //Debug.Log("Water height query finished!");

            if (buoyancyDataRequest.hasError) {
                Debug.Log("Height Error detected!");
                return;
            }

            //Debug.LogFormat("There were {0} frames of latency.", Time.frameCount - cachedFrameCount);
            cachedFrameCount = Time.frameCount;

            NativeArray<ushort> buoyancyDataQuery = buoyancyDataRequest.GetData<ushort>();

            cachedBuoyancyData = new Vector3(Mathf.HalfToFloat(buoyancyDataQuery[0]), Mathf.HalfToFloat(buoyancyDataQuery[1]), Mathf.HalfToFloat(buoyancyDataQuery[2]));

            cachedHeights.Enqueue(cachedBuoyancyData.x);
            if (cachedHeights.Count > 10) cachedHeights.Dequeue();

            
            cachedNormal = new Vector3(-cachedBuoyancyData.y, 1.0f, -cachedBuoyancyData.z);
            cachedNormal.Normalize();

            timeSinceUpdate = 0.0f;

            //Debug.Log("Starting new query...");
            buoyancyDataRequest = AsyncGPUReadback.Request(waterSource.GetDisplacementMap(), 0, 0, 1, 0, 1, 0, 1, null);
        }
    }

    void FixedUpdate() {
        Vector3 pos = this.transform.position;

        float volume = 1.5f * 1.5f * 1.5f;
        float density = rigidBody.mass / volume;

        float averageWaterHeight = getAverageWaterHeight();
        float waterHeight = Mathf.Lerp(cachedBuoyancyData.x, averageWaterHeight, Mathf.Min(1.0f, timeSinceUpdate));

        float buoyHeight = pos.y - 1.0f; // origin offset

        float depth = Mathf.Min(1.75f, Mathf.Max(0.0f, waterHeight - buoyHeight)) / 1.75f;

        Vector3 maxBuoyancy = 1.0f * volume * -Physics.gravity;

        Vector3 surfaceNormal = cachedNormal;
        Quaternion surfaceRotation = Quaternion.FromToRotation(new Vector3(0, 1, 0), surfaceNormal);
        surfaceRotation = Quaternion.Slerp(surfaceRotation, Quaternion.identity, depth);

        Vector3 F = surfaceRotation * (maxBuoyancy * depth);

        rigidBody.AddForce(F);
        rigidBody.drag = Mathf.Lerp(0.0f, 0.5f, depth);

    }

    void OnDisable() {
        cachedHeights = null;
    }
}

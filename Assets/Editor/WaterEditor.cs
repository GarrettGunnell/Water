using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Water))]
[CanEditMultipleObjects]
public class WaterEditor : Editor {
    private float speedMin = 0.0f;
    private float speedMax = 10.0f;

    private float amplitudeMin = 0.0f;
    private float amplitudeMax = 3.0f;

    private float wavelengthMin = 0.0f;
    private float wavelengthMax = 10.0f;

    private float steepnessMin = 0.0f;
    private float steepnessMax = 10.0f;

    private bool usingVertexDisplacement = false;
    private bool usingPixelShaderNormals = false;
    private bool usingCircularWaves = false;
    private bool letJesusTakeTheWheel = false;
    private bool usingFBM = false;

    private Water.WaveFunction waveFunction = Water.WaveFunction.Sine;
    
    SerializedProperty waterShader;
    SerializedProperty atmosphere;
    SerializedProperty planeLength;
    SerializedProperty quadRes;
    SerializedProperty waveType;
    SerializedProperty updateStatics;
    SerializedProperty direction1, direction2, direction3, direction4;
    SerializedProperty origin1, origin2, origin3, origin4;
    SerializedProperty speed1, speed2, speed3, speed4;
    SerializedProperty amplitude1, amplitude2, amplitude3, amplitude4;
    SerializedProperty wavelength1, wavelength2, wavelength3, wavelength4;
    SerializedProperty steepness1, steepness2, steepness3, steepness4;
    SerializedProperty medianWavelength, wavelengthRange;
    SerializedProperty medianSpeed, speedRange;
    SerializedProperty medianDirection, directionalRange;
    SerializedProperty medianAmplitude;
    SerializedProperty steepness;

    SerializedProperty waveCount;

    SerializedProperty ambient;
    SerializedProperty diffuseReflectance;
    SerializedProperty specularReflectance;
    SerializedProperty shininess;
    SerializedProperty fresnelColor;
    SerializedProperty environmentTexture;
    SerializedProperty useTextureForFresnel;
    SerializedProperty fresnelBias;
    SerializedProperty fresnelShininess;
    SerializedProperty fresnelStrength;
    SerializedProperty tipColor;
    SerializedProperty tipAttenuation;

    SerializedProperty vertexWaveCount;
    SerializedProperty fragmentWaveCount;
    SerializedProperty vertexSeed, vertexSeedIter, vertexFrequency, vertexFrequencyMult, vertexAmplitude, vertexAmplitudeMult, vertexInitialSpeed, vertexSpeedRamp, vertexDrag, vertexHeight, vertexMaxPeak, vertexPeakOffset;
    SerializedProperty fragmentSeed, fragmentSeedIter, fragmentFrequency, fragmentFrequencyMult, fragmentAmplitude, fragmentAmplitudeMult, fragmentInitialSpeed, fragmentSpeedRamp, fragmentDrag, fragmentHeight, fragmentMaxPeak, fragmentPeakOffset;
    SerializedProperty normalStrength, fresnelNormalStrength, specularNormalStrength;

    void OnEnable() {
        waterShader = serializedObject.FindProperty("waterShader");
        atmosphere = serializedObject.FindProperty("atmosphere");
        planeLength = serializedObject.FindProperty("planeLength");
        quadRes = serializedObject.FindProperty("quadRes");
        waveType = serializedObject.FindProperty("waveType");
        updateStatics = serializedObject.FindProperty("updateStatics");
        direction1 = serializedObject.FindProperty("direction1");
        direction2 = serializedObject.FindProperty("direction2");
        direction3 = serializedObject.FindProperty("direction3");
        direction4 = serializedObject.FindProperty("direction4");
        origin1 = serializedObject.FindProperty("origin1");
        origin2 = serializedObject.FindProperty("origin2");
        origin3 = serializedObject.FindProperty("origin3");
        origin4 = serializedObject.FindProperty("origin4");
        speed1 = serializedObject.FindProperty("speed1");
        speed2 = serializedObject.FindProperty("speed2");
        speed3 = serializedObject.FindProperty("speed3");
        speed4 = serializedObject.FindProperty("speed4");
        amplitude1 = serializedObject.FindProperty("amplitude1");
        amplitude2 = serializedObject.FindProperty("amplitude2");
        amplitude3 = serializedObject.FindProperty("amplitude3");
        amplitude4 = serializedObject.FindProperty("amplitude4");
        wavelength1 = serializedObject.FindProperty("wavelength1");
        wavelength2 = serializedObject.FindProperty("wavelength2");
        wavelength3 = serializedObject.FindProperty("wavelength3");
        wavelength4 = serializedObject.FindProperty("wavelength4");
        steepness1 = serializedObject.FindProperty("steepness1");
        steepness2 = serializedObject.FindProperty("steepness2");
        steepness3 = serializedObject.FindProperty("steepness3");
        steepness4 = serializedObject.FindProperty("steepness4");
        medianWavelength = serializedObject.FindProperty("medianWavelength");
        wavelengthRange = serializedObject.FindProperty("wavelengthRange");
        medianDirection = serializedObject.FindProperty("medianDirection");
        directionalRange = serializedObject.FindProperty("directionalRange");
        medianAmplitude = serializedObject.FindProperty("medianAmplitude");
        medianSpeed = serializedObject.FindProperty("medianSpeed");
        speedRange = serializedObject.FindProperty("speedRange");
        steepness = serializedObject.FindProperty("steepness");
        ambient = serializedObject.FindProperty("ambient");
        diffuseReflectance = serializedObject.FindProperty("diffuseReflectance");
        specularReflectance = serializedObject.FindProperty("specularReflectance");
        shininess = serializedObject.FindProperty("shininess");
        fresnelColor = serializedObject.FindProperty("fresnelColor");
        fresnelBias = serializedObject.FindProperty("fresnelBias");
        fresnelStrength = serializedObject.FindProperty("fresnelStrength");
        fresnelShininess = serializedObject.FindProperty("fresnelShininess");
        waveCount = serializedObject.FindProperty("waveCount");
        vertexSeed = serializedObject.FindProperty("vertexSeed");
        vertexSeedIter = serializedObject.FindProperty("vertexSeedIter");
        vertexFrequency = serializedObject.FindProperty("vertexFrequency");
        vertexFrequencyMult = serializedObject.FindProperty("vertexFrequencyMult");
        vertexAmplitude = serializedObject.FindProperty("vertexAmplitude");
        vertexAmplitudeMult = serializedObject.FindProperty("vertexAmplitudeMult");
        vertexInitialSpeed = serializedObject.FindProperty("vertexInitialSpeed");
        vertexSpeedRamp = serializedObject.FindProperty("vertexSpeedRamp");
        vertexDrag = serializedObject.FindProperty("vertexDrag");
        vertexHeight = serializedObject.FindProperty("vertexHeight");
        vertexMaxPeak = serializedObject.FindProperty("vertexMaxPeak");
        vertexPeakOffset = serializedObject.FindProperty("vertexPeakOffset");
        fragmentSeed = serializedObject.FindProperty("fragmentSeed");
        fragmentSeedIter = serializedObject.FindProperty("fragmentSeedIter");
        fragmentFrequency = serializedObject.FindProperty("fragmentFrequency");
        fragmentFrequencyMult = serializedObject.FindProperty("fragmentFrequencyMult");
        fragmentAmplitude = serializedObject.FindProperty("fragmentAmplitude");
        fragmentAmplitudeMult = serializedObject.FindProperty("fragmentAmplitudeMult");
        fragmentInitialSpeed = serializedObject.FindProperty("fragmentInitialSpeed");
        fragmentSpeedRamp = serializedObject.FindProperty("fragmentSpeedRamp");
        fragmentDrag = serializedObject.FindProperty("fragmentDrag");
        fragmentHeight = serializedObject.FindProperty("fragmentHeight");
        fragmentMaxPeak = serializedObject.FindProperty("fragmentMaxPeak");
        fragmentPeakOffset = serializedObject.FindProperty("fragmentPeakOffset");
        vertexWaveCount = serializedObject.FindProperty("vertexWaveCount");
        fragmentWaveCount = serializedObject.FindProperty("fragmentWaveCount");
        normalStrength = serializedObject.FindProperty("normalStrength");
        tipColor = serializedObject.FindProperty("tipColor");
        tipAttenuation = serializedObject.FindProperty("tipAttenuation");
        fresnelNormalStrength = serializedObject.FindProperty("fresnelNormalStrength");
        specularNormalStrength = serializedObject.FindProperty("specularNormalStrength");
        useTextureForFresnel = serializedObject.FindProperty("useTextureForFresnel");
        environmentTexture = serializedObject.FindProperty("environmentTexture");
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();
        
        EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(waterShader);
        EditorGUILayout.PropertyField(atmosphere);
        EditorGUILayout.PropertyField(planeLength);
        EditorGUILayout.PropertyField(quadRes);
        EditorGUILayout.Space();

        waveFunction = ((Water)target).waveFunction;
        if (GUILayout.Button("Function: " + waveFunction.ToString())) {
            Water water = (Water)target;
            water.CycleWaveFunction();
        }
        EditorGUILayout.Space();

        usingVertexDisplacement = ((Water)target).usingVertexDisplacement;
        if (GUILayout.Button("Using GPU Vertex Displacement: " + usingVertexDisplacement.ToString())) {
            Water water = (Water)target;
            water.ToggleVertexDisplacementMethod();
        }
        if (usingVertexDisplacement) {
            EditorGUILayout.PropertyField(updateStatics);
        }
        EditorGUILayout.Space();

        usingPixelShaderNormals = ((Water)target).usingPixelShaderNormals;
        if (GUILayout.Button("Using Pixel Shader Normals: " + usingPixelShaderNormals.ToString())) {
            Water water = (Water)target;
            water.ToggleNormalGeneration();
        }
        EditorGUILayout.Space();

        usingCircularWaves = ((Water)target).usingCircularWaves;
        if (GUILayout.Button("Using Circular Waves: " + usingCircularWaves.ToString())) {
            Water water = (Water)target;
            water.ToggleCircularWaves();
        }
        EditorGUILayout.Space();

        letJesusTakeTheWheel = ((Water)target).letJesusTakeTheWheel;
        if (GUILayout.Button("Procedurally Generate: " + letJesusTakeTheWheel.ToString())) {
            Water water = (Water)target;
            water.ToggleJesus();
        }
        EditorGUILayout.Space();

        if (letJesusTakeTheWheel) {
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Procedural Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            usingFBM = ((Water)target).usingFBM;

            if (usingFBM) {
                EditorGUILayout.IntSlider(vertexWaveCount, 0, 64, new GUIContent("Vertex Wave Count"));
                EditorGUILayout.IntSlider(fragmentWaveCount, 0, 64, new GUIContent("Fragment Wave Count"));
            } else
                EditorGUILayout.IntSlider(waveCount, 0, 64, new GUIContent("Wave Count"));

            if (GUILayout.Button("Use FBM: " + usingFBM.ToString())) {
                Water water = (Water)target;
                water.ToggleFBM();
            }
            EditorGUILayout.Space();

            if (usingFBM) {
                EditorGUI.indentLevel--;
                EditorGUILayout.LabelField("Vertex Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                EditorGUILayout.Slider(vertexSeed, 0.0f, 300.0f, new GUIContent("Vertex Seed"));
                EditorGUILayout.Slider(vertexSeedIter, 0.0f, 2000.0f, new GUIContent("Vertex Seed Iterator"));
                EditorGUILayout.Slider(vertexFrequency, 0.0f, 2.0f, new GUIContent("Vertex Frequency"));
                EditorGUILayout.Slider(vertexFrequencyMult, 0.0f, 2.0f, new GUIContent("Vertex Frequency Mult"));
                EditorGUILayout.Slider(vertexAmplitude, 0.0f, 2.0f, new GUIContent("Vertex Amplitude"));
                EditorGUILayout.Slider(vertexAmplitudeMult, 0.0f, 2.0f, new GUIContent("Vertex Amplitude Mult"));
                EditorGUILayout.Slider(vertexMaxPeak, 0.0f, 2.0f, new GUIContent("Vertex Max Peak"));
                EditorGUILayout.Slider(vertexPeakOffset, 0.0f, 2.0f, new GUIContent("Vertex Peak Offset"));
                EditorGUILayout.Slider(vertexInitialSpeed, 0.0f, 2.0f, new GUIContent("Vertex Initial Speed"));
                EditorGUILayout.Slider(vertexSpeedRamp, 0.0f, 2.0f, new GUIContent("Vertex Speed Ramp"));
                EditorGUILayout.Slider(vertexDrag, 0.0f, 2.0f, new GUIContent("Vertex Drag"));
                EditorGUILayout.Slider(vertexHeight, 0.0f, 2.0f, new GUIContent("Vertex Height"));
                EditorGUI.indentLevel--;
                EditorGUILayout.LabelField("Fragment Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                EditorGUILayout.Slider(fragmentSeed, 0.0f, 300.0f, new GUIContent("Fragment Seed"));
                EditorGUILayout.Slider(fragmentSeedIter, 0.0f, 2000.0f, new GUIContent("Fragment Seed Iterator"));
                EditorGUILayout.Slider(fragmentFrequency, 0.0f, 2.0f, new GUIContent("Fragment Frequency"));
                EditorGUILayout.Slider(fragmentFrequencyMult, 0.0f, 2.0f, new GUIContent("Fragment Frequency Mult"));
                EditorGUILayout.Slider(fragmentAmplitude, 0.0f, 2.0f, new GUIContent("Fragment Amplitude"));
                EditorGUILayout.Slider(fragmentAmplitudeMult, 0.0f, 2.0f, new GUIContent("Fragment Amplitude Mult"));
                EditorGUILayout.Slider(fragmentMaxPeak, 0.0f, 2.0f, new GUIContent("Fragment Max Peak"));
                EditorGUILayout.Slider(fragmentPeakOffset, 0.0f, 2.0f, new GUIContent("Fragment Peak Offset"));
                EditorGUILayout.Slider(fragmentInitialSpeed, 0.0f, 2.0f, new GUIContent("Fragment Initial Speed"));
                EditorGUILayout.Slider(fragmentSpeedRamp, 0.0f, 2.0f, new GUIContent("Fragment Speed Ramp"));
                EditorGUILayout.Slider(fragmentDrag, 0.0f, 2.0f, new GUIContent("Fragment Drag"));
                EditorGUILayout.Slider(fragmentHeight, 0.0f, 2.0f, new GUIContent("Fragment Height"));
                EditorGUILayout.Slider(normalStrength, 0.0f, 2.0f, new GUIContent("Normal Strength"));
            } else {
                EditorGUILayout.Slider(medianWavelength, 0.0f, 3.0f, new GUIContent("Median Wavelength"));
                EditorGUILayout.Slider(wavelengthRange, 0.0f, 2.0f, new GUIContent("Wavelength Range"));
                EditorGUILayout.Slider(medianDirection, 0.0f, 360.0f, new GUIContent("Median Direction"));
                EditorGUILayout.Slider(directionalRange, 0.0f, 360.0f, new GUIContent("Directional Range"));
                EditorGUILayout.Slider(medianAmplitude, 0.0f, 3.0f, new GUIContent("Median Amplitude"));
                EditorGUILayout.Slider(medianSpeed, 0.0f, 2.0f, new GUIContent("Median Speed"));
                EditorGUILayout.Slider(speedRange, 0.0f, 1.0f, new GUIContent("Speed Range"));

                if (waveFunction == Water.WaveFunction.SteepSine) {
                    EditorGUILayout.Slider(steepness, 1.0f, 10.0f, new GUIContent("Steepness"));
                } else if (waveFunction == Water.WaveFunction.Gerstner) {
                    EditorGUILayout.Slider(steepness, 0.0f, 1.0f, new GUIContent("Steepness"));
                }

                if (GUILayout.Button("Regenerate Waves")) {
                    Water water = (Water)target;
                    water.GenerateNewWaves();
                }
            }
        } else {
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Wave One", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(direction1, 0.0f, 360.0f, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(origin1, new GUIContent("Origin"));
            EditorGUILayout.Slider(speed1, speedMin, speedMax, new GUIContent("Speed"));
            EditorGUILayout.Slider(amplitude1, amplitudeMin, amplitudeMax, new GUIContent("Amplitude"));
            EditorGUILayout.Slider(wavelength1, wavelengthMin, wavelengthMax, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(steepness1, steepnessMin, steepnessMax, new GUIContent("Steepness"));
            EditorGUILayout.Space();
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Wave Two", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(direction2, 0.0f, 360.0f, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(origin2, new GUIContent("Origin"));
            EditorGUILayout.Slider(speed2, speedMin, speedMax, new GUIContent("Speed"));
            EditorGUILayout.Slider(amplitude2, amplitudeMin, amplitudeMax, new GUIContent("Amplitude"));
            EditorGUILayout.Slider(wavelength2, wavelengthMin, wavelengthMax, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(steepness2, steepnessMin, steepnessMax, new GUIContent("Steepness"));
            EditorGUILayout.Space();
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Wave Three", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(direction3, 0.0f, 360.0f, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(origin3, new GUIContent("Origin"));
            EditorGUILayout.Slider(speed3, speedMin, speedMax, new GUIContent("Speed"));
            EditorGUILayout.Slider(amplitude3, amplitudeMin, amplitudeMax, new GUIContent("Amplitude"));
            EditorGUILayout.Slider(wavelength3, wavelengthMin, wavelengthMax, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(steepness3, steepnessMin, steepnessMax, new GUIContent("Steepness"));
            EditorGUILayout.Space();
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Wave Four", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(direction4, 0.0f, 360.0f, new GUIContent("Direction"));
            EditorGUILayout.PropertyField(origin4, new GUIContent("Origin"));
            EditorGUILayout.Slider(speed4, speedMin, speedMax, new GUIContent("Speed"));
            EditorGUILayout.Slider(amplitude4, amplitudeMin, amplitudeMax, new GUIContent("Amplitude"));
            EditorGUILayout.Slider(wavelength4, wavelengthMin, wavelengthMax, new GUIContent("Wavelength"));
            EditorGUILayout.Slider(steepness4, steepnessMin, steepnessMax, new GUIContent("Steepness"));
        }
        
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Material Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(ambient);
        EditorGUILayout.PropertyField(diffuseReflectance);
        EditorGUILayout.PropertyField(specularReflectance);
        EditorGUILayout.Slider(shininess, 0.0f, 100.0f, new GUIContent("Shininess"));
        EditorGUILayout.Slider(specularNormalStrength, 0.0f, 5.0f, new GUIContent("Specular Normal Strength"));
        EditorGUILayout.PropertyField(fresnelColor);
        EditorGUILayout.PropertyField(environmentTexture);
        EditorGUILayout.PropertyField(useTextureForFresnel);
        EditorGUILayout.Slider(fresnelBias, 0.0f, 1.0f, new GUIContent("Fresnel Bias"));
        EditorGUILayout.Slider(fresnelStrength, 0.0f, 1.0f, new GUIContent("Fresnel Strength"));
        EditorGUILayout.Slider(fresnelShininess, 0.0f, 20.0f, new GUIContent("Fresnel Shininess"));
        EditorGUILayout.Slider(fresnelNormalStrength, 0.0f, 5.0f, new GUIContent("Fresnel Normal Strength"));
        EditorGUILayout.PropertyField(tipColor);
        EditorGUILayout.Slider(tipAttenuation, 0.0f, 5.0f, new GUIContent("Tip Attenuation"));

        serializedObject.ApplyModifiedProperties();
    }
}

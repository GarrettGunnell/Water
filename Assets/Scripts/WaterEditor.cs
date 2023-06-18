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
    private float amplitudeMax = 10.0f;

    private float wavelengthMin = 0.0f;
    private float wavelengthMax = 10.0f;

    private float steepnessMin = 0.0f;
    private float steepnessMax = 10.0f;
    

    SerializedProperty waterShader;
    SerializedProperty planeLength;
    SerializedProperty quadRes;
    SerializedProperty waveFunction;
    SerializedProperty waveType;
    SerializedProperty direction1, direction2, direction3, direction4;
    SerializedProperty origin1, origin2, origin3, origin4;
    SerializedProperty speed1, speed2, speed3, speed4;
    SerializedProperty amplitude1, amplitude2, amplitude3, amplitude4;
    SerializedProperty wavelength1, wavelength2, wavelength3, wavelength4;
    SerializedProperty steepness1, steepness2, steepness3, steepness4;

    void OnEnable() {
        waterShader = serializedObject.FindProperty("waterShader");
        planeLength = serializedObject.FindProperty("planeLength");
        quadRes = serializedObject.FindProperty("quadRes");
        waveFunction = serializedObject.FindProperty("waveFunction");
        waveType = serializedObject.FindProperty("waveType");
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
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();
        
        EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(waterShader);
        EditorGUILayout.PropertyField(planeLength);
        EditorGUILayout.PropertyField(quadRes);
        EditorGUILayout.PropertyField(waveFunction);
        EditorGUILayout.PropertyField(waveType);

        if (GUILayout.Button("Use Vertex Displacement")) {
            Water water = (Water)target;
            water.ToggleVertexDisplacementMethod();
        }
        EditorGUILayout.Space();

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
    
        serializedObject.ApplyModifiedProperties();
    }
}
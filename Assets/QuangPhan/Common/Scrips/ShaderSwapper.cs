using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MaterialShaderSwapper : EditorWindow
{
    private List<ShaderPair> shaderPairs = new List<ShaderPair>();

    private class ShaderPair
    {
        public Shader oldShader;
        public Shader newShader;
    }

    [MenuItem("Window/QuangPhan/Material Shader Swapper")]
    public static void ShowWindow()
    {
        // Create the window
        MaterialShaderSwapper window = GetWindow<MaterialShaderSwapper>("Material Shader Swapper", true);

        // Set the minimum size of the window
        window.minSize = new Vector2(300f, 150f);
    }

    private void OnGUI()
    {
        GUILayout.Label("Material Shader Swapper", EditorStyles.boldLabel);

        // Shader pairs
        for (int i = 0; i < shaderPairs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            shaderPairs[i].oldShader = (Shader)EditorGUILayout.ObjectField("Old Shader", shaderPairs[i].oldShader, typeof(Shader), false);
            shaderPairs[i].newShader = (Shader)EditorGUILayout.ObjectField("New Shader", shaderPairs[i].newShader, typeof(Shader), false);
            if (GUILayout.Button("Remove Pair", GUILayout.Width(100)))
            {
                shaderPairs.RemoveAt(i);
                i--;
                continue;
            }
            EditorGUILayout.EndHorizontal();
        }

        // Add button
        EditorGUILayout.Space();
        if (GUILayout.Button("Add Pair"))
        {
            shaderPairs.Add(new ShaderPair());
        }

        // Swap and Revert buttons
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Swap Shaders"))
        {
            SwapShaders();
        }
        if (GUILayout.Button("Revert Swap Shaders"))
        {
            RevertSwapShaders();
        }
        EditorGUILayout.EndHorizontal();

        // Usage instructions and tutorial link
        EditorGUILayout.Space();
        GUILayout.Label("How to use: Select a shader in the 'Old Shader' and 'New Shader' fields and click the 'Swap Shaders' button. Click the 'Revert Swap Shaders' button to revert all materials back to their original shaders.");
        GUILayout.Label("For a video tutorial, check out: ");
        if (GUILayout.Button("Material Shader Swapper Tutorial"))
        {
            Application.OpenURL("https://youtu.be/b6Upa9aPnJo");
        }
        EditorGUILayout.Space();
        GUILayout.Label("Additional Informations:");
        if (GUILayout.Button("My Discord channel"))
        {
            Application.OpenURL("https://discord.gg/BpwdQnm");
        }
        if (GUILayout.Button("My Youtube Channel"))
        {
            Application.OpenURL("https://www.youtube.com/c/QuangPhan0310");
        }
        if (GUILayout.Button("My Email"))
        {
            Application.OpenURL("mailto:arch.quangphan@gmail.com");
        }
        if (GUILayout.Button("My Asset Store"))
        {
            Application.OpenURL("https://assetstore.unity.com/publishers/24321");
        }
    }

    private void SwapShaders()
    {
        // Get all materials in the project
        string[] guids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            // Swap shader if it matches any of the old shaders
            for (int i = 0; i < shaderPairs.Count; i++)
            {
                if (shaderPairs[i].oldShader != null && material.shader == shaderPairs[i].oldShader)
                {
                    material.shader = shaderPairs[i].newShader != null ? shaderPairs[i].newShader : material.shader;
                    EditorUtility.SetDirty(material);
                    break;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void RevertSwapShaders()
    {
        // Get all materials in the project
        string[] guids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            // Revert shader if it matches any of the new shaders
            for (int i = 0; i < shaderPairs.Count; i++)
            {
                if (shaderPairs[i].newShader != null && material.shader == shaderPairs[i].newShader)
                {
                    material.shader = shaderPairs[i].oldShader != null ? shaderPairs[i].oldShader : material.shader;
                    EditorUtility.SetDirty(material);
                    break;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
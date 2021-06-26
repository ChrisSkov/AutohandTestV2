using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading;

[InitializeOnLoad]
public class AutoHandSetupWizard : EditorWindow{
    static AutoHandSetupWizard window;
    static string[] requiredLayerNames = { "Releasing", "Grabbing", "Grabbable", "HandHolding", "Hand", "HandPlayer"};
    static string assetPath;

	static AutoHandSetupWizard(){
		EditorApplication.update += Update;
	}

    
	static void Update(){
        if (ShowSetupWindow()){
			OpenWindow();
            assetPath = Application.dataPath;
		}

		EditorApplication.update -= Update;
    }
    
    [MenuItem ("Window/Autohand/Setup Window")]
    public static void OpenWindow() {
		window = GetWindow<AutoHandSetupWizard>(true);
		window.minSize = new Vector2(320, 440);
        window.titleContent = new GUIContent("Auto Hand Setup");
    }


    public void OnGUI(){
        bool done = true;
        if(IsGenerated() && !IsIgnoreCollisionSet()){
            UpdateRequiredCollisionLayers();
        }
        
        if(!IsGenerated()){
            GUILayout.Space(10f);
            GUILayout.Space(10f);
            GUILayout.Label("Required Settings:", new GUIStyle{ fontStyle = FontStyle.Bold });
            GUILayout.Space(2f);
            GUILayout.Label("Automatically includes required physics layers");
            GUILayout.Space(5f);
            if(GUILayout.Button("Enable")) 
                GenerateAutoHandLayers();
            done = false;
        }
        
        GUILayout.Space(10f);
        
        if(!IsStronglyRecommendedSet()) {
            GUILayout.Space(10f);
            GUILayout.Space(10f);
            GUILayout.Label("Strongly Recommended:", new GUIStyle{ fontStyle = FontStyle.Bold });
            GUILayout.Space(2f);
            GUILayout.Label("Increases time.fixedTime to at least 1/60");
            GUILayout.Label("Increases max angular velocity to 50");
            GUILayout.Space(5f);
            if(GUILayout.Button("Enable")) 
                StronglyRecommendedPhysicsSettings();
            done = false;
        }

        GUILayout.Space(10f);
        
        if(!IsRecommendedSet()) {
            GUILayout.Space(10f);
            GUILayout.Space(10f);
            GUILayout.Label("Recommended Settings:", new GUIStyle{ fontStyle = FontStyle.Bold });
            GUILayout.Space(2f);
            GUILayout.Label("Increases the default solver iterations to at least 25");
            GUILayout.Label("Increases the default solver velocity iterations to at least 25");
            GUILayout.Space(5f);
            if(GUILayout.Button("Enable")) 
                RecommendedPhysicsSettings();
            done = false;
        }

        if(done)
            GUI.Label(EditorGUILayout.GetControlRect(), "All set!");
        

    }


    static bool ShowSetupWindow() {

        return !IsIgnoreCollisionSet() || !IsGenerated() || !IsStronglyRecommendedSet();
    }


    static void GenerateAutoHandLayers() {
        assetPath = Application.dataPath;
        var path = assetPath.Substring(0, assetPath.Length - 6);
        path += "ProjectSettings/TagManager.asset";

        List<string> layerNames = new List<string>();
        for(int i = 0; i < requiredLayerNames.Length; i++) {
            layerNames.Add(requiredLayerNames[i]);
        }

        StreamReader reader = new StreamReader(path); 
        string line = reader.ReadLine();
        string[] lines = File.ReadAllLines(path);
        
        int lineIndex = 0;
        for(lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            for(int i = 0; i < layerNames.Count; i++) {
                if(lines[lineIndex].Contains(layerNames[i])){
                    layerNames.RemoveAt(i);
                }
            }
        }

        List<int> lineTargetList = new List<int>();
        lineIndex = 0;
        while((line = reader.ReadLine()) != null){  
            if(line == "  - "){
                lineTargetList.Add(lineIndex);
            }
            lineIndex++;
        }  
        reader.Close();

        var lineTarget = new int[layerNames.Count];
        if(lineTargetList.Count < lineTarget.Length){
            Debug.LogError("AUTO HAND - SETUP FAILED: Requires 6 available physics layers for automatic setup.");
            return;
        }

        int j = 0;
        for(int i = lineTargetList.Count-1; j < lineTarget.Length; j++) {
            lineTarget[j] = lineTargetList[i];
            i--;
        }
        
        StreamWriter writer = new StreamWriter(path);
        lineIndex = 0;
        for(lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            bool found = false;
            for(int i = 0; i < lineTarget.Length; i++){
                if(lineIndex == lineTarget[i]+1){
                    writer.WriteLine("  - " + layerNames[i]);
                    found = true;
                }
            }
            if(!found)
                writer.WriteLine(lines[lineIndex]);
            
        }
        Debug.Log("Autohand - Layer setup successful");
        writer.Close();
        AssetDatabase.Refresh();
#if UNITY_2020
#if !UNITY_2020_1
        AssetDatabase.RefreshSettings();
#endif
#endif
    }

    static void UpdateRequiredCollisionLayers() {
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Hand"), LayerMask.NameToLayer("Hand"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Hand"), LayerMask.NameToLayer("HandHolding"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Hand"), LayerMask.NameToLayer("Releasing"), true);

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandHolding"), LayerMask.NameToLayer("HandHolding"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandHolding"), LayerMask.NameToLayer("Grabbing"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandHolding"), LayerMask.NameToLayer("Releasing"), true);

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Grabbable"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Grabbing"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Releasing"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Hand"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("HandHolding"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("HandPlayer"), true);
    }


    public static bool IsGenerated(){
        foreach(var layer in requiredLayerNames) {
            if(LayerMask.NameToLayer(layer) == -1)
                return false;
        }
        return true;
    }

    public static bool IsIgnoreCollisionSet() {
        return IsGenerated() &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Hand"), LayerMask.NameToLayer("Hand")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Hand"), LayerMask.NameToLayer("HandHolding")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Hand"), LayerMask.NameToLayer("Releasing")) &&

        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandHolding"), LayerMask.NameToLayer("HandHolding")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandHolding"), LayerMask.NameToLayer("Grabbing")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandHolding"), LayerMask.NameToLayer("Releasing")) &&

        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Grabbable")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Grabbing")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Releasing")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("Hand")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("HandHolding")) &&
        Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("HandPlayer"), LayerMask.NameToLayer("HandPlayer"));

    }
    


    public static bool IsStronglyRecommendedSet() {
        if(Physics.defaultMaxAngularSpeed == 50 && Physics.defaultSolverIterations == 25 && Time.fixedDeltaTime == 0.02f){
            Time.fixedDeltaTime = 1/60f;
            return true;
        }
        else if(Physics.defaultMaxAngularSpeed == 50 && Time.fixedDeltaTime != 0.02f){
            return true;
        }
        return Time.fixedDeltaTime <= 1/60f && Physics.defaultMaxAngularSpeed >= 40;
    }

    public static void StronglyRecommendedPhysicsSettings() {
        Time.fixedDeltaTime = 1/60f;
        Physics.defaultMaxAngularSpeed = 50;
    }



    public static bool IsRecommendedSet() {
        return Physics.defaultSolverIterations >= 25 && Physics.defaultSolverVelocityIterations >= 25;
    }

    public static void RecommendedPhysicsSettings() {
        Physics.defaultSolverIterations = 25;
        Physics.defaultSolverVelocityIterations = 25;
    }
}

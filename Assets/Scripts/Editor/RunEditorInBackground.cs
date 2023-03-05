// using UnityEngine;
// using UnityEditor;
//
//
// [InitializeOnLoad]
// public class RunEditorInBackground  : EditorWindow
// {
//     // T$$anonymous$$s constructor is called on load because of Initialise on Load tag
//     static RunEditorInBackground()
//     {
//         // Need to delay t$$anonymous$$s as cant call EditorPrefs in static constructor
//         EditorApplication.playModeStateChanged += Running;
//     }
//  
//     private static void Running(PlayModeStateChange obj)
//     {
//         if(EditorApplication.isPlaying)
//             Application.runInBackground = true;
//     }
// }
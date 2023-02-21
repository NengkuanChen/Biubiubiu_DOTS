#if UNITY_EDITOR

using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Editor
{
public class AvatarMaskFromTransformHierarchy
{
	[MenuItem("CONTEXT/Transform/[Rukhanka] Rig Definition from Hierarchy")]
	static void CreateAvatarMaskFromTransformHierarchy()
	{
		if (Selection.objects.Length > 1)
		{ 
			var errorMsg = "[Error] Multiple selection is not supported!";
			EditorUtility.DisplayDialog("Rig Definition from Hierarchy", errorMsg, "Close");
			Debug.LogError(errorMsg);
			return;
		}

		var a = (GameObject)Selection.objects[0];
		var t = a.transform;
		if (t.parent != null)
		{
			var errorMsg = "[Error] For correct rig creation, object transform must not have parent!";
			EditorUtility.DisplayDialog("Rig Definition from Hierarchy", errorMsg, "Close");
			return;
		}

		var am = new AvatarMask();
		am.AddTransformPath(t, true);

		var curProjectPath = GetCurrentProjectPath();
		var assetPath = $"{curProjectPath}/{a.transform.name}_Rig.mask";
		AssetDatabase.CreateAsset(am, assetPath);
		Debug.Log($"Avatar mask saved as: '{assetPath}'");
		AssetDatabase.SaveAssets();

		EditorUtility.DisplayDialog("Rig Definition from Hierarchy", $"[Success] Rig Definition successfully created and saved at path '{assetPath}'", "Close");
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static string GetCurrentProjectPath()
	{
		var projectWindowUtilType = typeof(ProjectWindowUtil);
		MethodInfo getActiveFolderPath = projectWindowUtilType.GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
		object obj = getActiveFolderPath.Invoke(null, new object[0]);
		string pathToCurrentFolder = obj.ToString();
		return pathToCurrentFolder;
	}
}
}

#endif // UNITY_EDITOR

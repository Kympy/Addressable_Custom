using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class CustomAddressableBuild_New : EditorWindow
{
	[MenuItem("Addressable/Show Addressable Window")]
	private static void ShowAddressableWindow() // 어드레서블 윈도우 바로가기
	{
		EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
	}

	[MenuItem("Addressable/0. One Click Addressable Build (1 ~ 3)")]
	private static void OneClickAddressable()
	{
		CleanUpAddressable();
		ListUpAddressable();
		BuildAddressable();
	}

	[MenuItem("Addressable/1. Clean Up")]
	private static void CleanUpAddressable() // 어드레서블 리스트 모두 지우기
	{
		CheckAddressableDefaultSetting();
		AddressableAssetSettings setting = AddressableAssetSettingsDefaultObject.Settings;
		if (setting == null)
		{
			Debug.LogError("Default setting is NULL");
			return;
		}

		// 라벨 모두 삭제
		List<string> labels = setting.GetLabels();
		foreach (string label in labels)
		{
			setting.RemoveLabel(label, true);
		}

		// 그룹 가져와서 삭제
		List<AddressableAssetGroup> groups = setting.groups;
		for (int i = 0; i < groups.Count; i++)
		{
			if (groups[i] == null)
			{
				continue;
			}

			EditorUtility.DisplayCancelableProgressBar("Clean Up List", $"{groups[i].Name}", 0f);
			if (groups[i].entries == null || groups[i].entries.Count == 0)
			{
				setting.RemoveGroup(groups[i]);
				continue;
			}
			// 라벨 제거 및 엔트리 삭제
			AddressableAssetEntry[] entries = groups[i].entries.ToArray();
			for (int j = 0; j < entries.Length; j++)
			{
				EditorUtility.DisplayCancelableProgressBar("Clean Up List", $"{entries[j].address}", 0f);
				entries[j].labels.Clear();
				groups[i].RemoveAssetEntry(entries[j], false); // 그룹 내부 에셋 삭제
			}
			
			groups[i].SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entries, false, true);
			setting.RemoveGroup(groups[i]); // 그룹 삭제
			i--;
		}
		setting.groups.Clear();
		setting.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, groups, false, false);

		EditorUtility.ClearProgressBar();
		Debug.Log("Finished clean up addressable list.");
	}

	private static readonly char SLASH = '/'; // 구분자
	private static readonly string META_FILE = ".meta"; // 메타 파일 제거
	private static readonly string ASSET_SLASH = "Assets/"; // 에셋 경로
	private static readonly string RESOURCE_FOLDER_NAME = "Resources_moved";

	[MenuItem("Addressable/2. List Up")]
	private static void ListUpAddressable() // 어드레서블 에셋 리스트 생성
	{
		// 어드레서블 에셋 경로 지정
		string rootPath = $"{Application.dataPath}/{RESOURCE_FOLDER_NAME}/";
		if (Directory.Exists(rootPath) == false)
		{
			Debug.LogError("Resources_moved folder is not exist.");
			return;
		}

		EditorUtility.DisplayCancelableProgressBar("Addressable List Up", "Get asset directories...", 0f);

		// 경로 내의 첫번째 상위 폴더들을 저장 => 그룹이 된다.
		List<string> folders = new List<string>();
		folders.Clear();

		// 첫 번째 상위 폴더 이름들은 라벨이 된다.
		string[] bigCategory = Directory.GetDirectories(rootPath);
		if (bigCategory == null || bigCategory.Length == 0)
		{
			Debug.LogError("There's no big category.");
			return;
		}

		AddressableAssetSettings setting = AddressableAssetSettingsDefaultObject.Settings;

		// 상위 폴더 갯수 만큼 돌면서 리스트업
		for (int i = 0; i < bigCategory.Length; i++)
		{
			// 경로에서 폴더 이름만을 추출한다.
			string bigCategoryName = bigCategory[i].Substring(bigCategory[i].LastIndexOf(SLASH) + 1);

			// 해당 이름으로 그룹 생성
			var currentGroup = CreateGroup(bigCategoryName);

			// 최상위 폴더의 하위 1차까지만 탐색
			string[] subFolders = GetFirstSubFolder(bigCategory[i]);

			// 하위 폴더가 없다면
			if (subFolders.Length == 0)
			{
				// 메타파일을 제거한 파일 목록 가져오기
				string[] files = Directory.GetFiles(bigCategory[i]).Where((x) => x.EndsWith(META_FILE) == false).ToArray();
				if (files.Length == 0) continue;

				CreateEntry(ref files, ref setting, ref currentGroup);
				continue;
			}

			// 하위 폴더가 있다면
			for (int j = 0; j < subFolders.Length; j++)
			{
				// 하위 폴더 포함, 그보다 더 하위에 있는 모든 파일들을 메타 제거 후 가져온다.
				string[] files = Directory.GetFiles(subFolders[j], "*", SearchOption.AllDirectories).Where((x) => x.EndsWith(META_FILE) == false).ToArray();
				if (files.Length == 0) continue;

				CreateEntry(ref files, ref setting, ref currentGroup);
			}
		}

		AssetDatabase.SaveAssets();
		EditorUtility.ClearProgressBar();
		Debug.Log("Finished listing up addressables.");
	}

	// 인자로 받은 그룹에 인자로 받은 파일 목록들을 엔트리로 생성
	private static void CreateEntry(ref string[] files, ref AddressableAssetSettings setting, ref AddressableAssetGroup group)
	{
		// 리스트업 된 에셋을 캐싱하는 리스트
		List<AddressableAssetEntry> movedList = new List<AddressableAssetEntry>();
		movedList.Clear();

		// 파일들을 어드레서블로 지정하고 리스트업
		for (int k = 0; k < files.Length; k++)
		{
			string subPath = files[k].Substring(files[k].IndexOf(ASSET_SLASH));
			string guid = AssetDatabase.AssetPathToGUID(subPath);

			EditorUtility.DisplayCancelableProgressBar("Addressable List Up", $"{subPath}", 0f);

			AddressableAssetEntry moved = setting.CreateOrMoveEntry(guid, group, false, false);
			// 경로를 키로 지정
			moved.SetAddress(moved.AssetPath, true);
			// 그룹이름으로 라벨 지정
			moved.SetLabel(group.Name, true, true, true);
			// 사용한 것들을 리스트에 캐싱
			movedList.Add(moved);
		}
		group.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, movedList, false, true);
		setting.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, movedList, false, false);
	}

	// 어드레서블 빌드 실행
	[MenuItem("Addressable/3. Build")]
	private static void BuildAddressable()
	{
		try
		{
			AddressableAssetSettings.CleanPlayerContent();
			AddressableAssetSettings.BuildPlayerContent();
			Debug.Log("Successfully built addressables.");
		}
		catch (System.Exception ex)
		{
			Debug.LogError(ex);
			Debug.LogError("Failed to addressable build.");
		}
	}

	private static void CheckAddressableDefaultSetting()
	{
		// 어드레서블 default 세팅이 없다면 생성
		if (AddressableAssetSettingsDefaultObject.Settings == null)
		{
			AddressableAssetSettings.Create("Assets/AddressableAssetsData", "AddressableAssetSettings", true, true);
			AssetDatabase.SaveAssets();
		}
	}

	// 인자 폴더의 첫번째 하위 폴더목록을 반환
	private static string[] GetFirstSubFolder(string folderPath)
	{
		string[] sub = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);
		return sub;
	}

	/// <summary>
	/// 그룹 생성
	/// </summary>
	/// <param name="groupName">그룹 이름</param>
	/// <returns>그룹</returns>
	private static AddressableAssetGroup CreateGroup(string groupName)
	{
		var group = AddressableAssetSettingsDefaultObject.Settings.FindGroup(groupName);
		if (group == null)
		{
			group = AddressableAssetSettingsDefaultObject.Settings.CreateGroup(groupName, false, false, false, null, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
			group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
		}
		return group;
	}
}

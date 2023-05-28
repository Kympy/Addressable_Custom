using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class CustomMenu : EditorWindow
{
    [MenuItem("Addressable/Show Addressable Window")]
    private static void ShowAddressableWindow() // 어드레서블 윈도우 바로가기
    {
        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
    }
    [MenuItem("Addressable/0. Addressable Build (1 ~ 3)")]
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
        List<AddressableAssetGroup> groups = setting.groups; // 그룹가져오기
        try
        {
            for (int i = 0; i < groups.Count; i++)
            {
                EditorUtility.DisplayCancelableProgressBar("Clean Up List", $"{groups[i].Name}", 0f);

                AddressableAssetEntry[] entries = groups[i].entries.ToArray();
                for (int j = 0; j < entries.Length; j++)
                {
                    EditorUtility.DisplayCancelableProgressBar("Clean Up List", $"{entries[j].address}", 0f);
                    groups[i].RemoveAssetEntry(entries[j], false); // 그룹 내부 에셋 삭제
                }
                groups[i].SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entries, false, true);
                setting.RemoveGroup(groups[i]); // 그룹 삭제
                i--;
            }
            setting.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, groups, false, false);

            EditorUtility.ClearProgressBar();
            Debug.Log("Finished clean up addressable list.");
        }
        catch(System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(ex);
            Debug.LogError("Failed clean up addressable list.");
        }
    }
    private static char slash = '/'; // 구분자
    private static string metaFile = ".meta"; // 메타 파일 제거
    private static string assetPath = "Assets/"; // 에셋 경로
    [MenuItem("Addressable/2. List Up")]
    private static void ListUpAddressable() // 어드레서블 에셋 리스트 생성
    {
        string rootPath = $"{Application.dataPath}/Resources_moved/"; // 어드레서블 에셋 경로 지정
        if (Directory.Exists(rootPath) == false)
        {
            Debug.LogError("Resources_moved folder is not exist.");
            return;
        }
        try
        {
            EditorUtility.DisplayCancelableProgressBar("Addressable List Up", "Get asset directories...", 0f);

            List<string> folders = new List<string>(); // 경로 내의 폴더들을 저장
            folders.Clear();

            void GetAllFolderDepth(string folderPath) // 재귀적으로 폴더 내부를 탐색하여 모든 폴더들을 가져옴
            {
                string[] sub = Directory.GetDirectories(folderPath); // 하위폴더 탐색
                if (sub.Length == 0) return; // 더 이상 하위폴더가 없다면 종료
                folders.AddRange(sub); // 검색된 폴더들을 리스트에 추가
                foreach(var subFolder in sub) // 하위폴더들의 하위폴더를 다시 재귀 탐색
                {
                    GetAllFolderDepth(subFolder);
                }
                return;
            }
            GetAllFolderDepth(rootPath);
            
            AddressableAssetSettings setting = AddressableAssetSettingsDefaultObject.Settings;
            List<AddressableAssetEntry> movedList = new List<AddressableAssetEntry>(); // 리스트업 된 에셋을 캐싱하는 리스트

            for (int i = 0; i < folders.Count; i++) // 리스트내의 폴더 순회
            {
                // 폴더 내부 파일 탐색
                string[] files = Directory.GetFiles(folders[i]).Where((x) => x.EndsWith(metaFile) == false).ToArray();
                if (files.Length == 0) continue;

                string groupName = folders[i].Substring(folders[i].LastIndexOf(slash) + 1); // 폴더 이름으로 그룹명 지정

                EditorUtility.DisplayCancelableProgressBar("Addressable List Up", $"{groupName}", 0f);

                AddressableAssetGroup group = setting.FindGroup(groupName); // 그룹명이 존재하는지 확인
                if (group == null) // 없으면 생성
                {
                    group = setting.CreateGroup(groupName, false, false, false, null, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
                    group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                }

                movedList.Clear();

                for (int j = 0; j < files.Length; j++) // 파일들을 어드레서블로 지정하고 리스트업
                {
                    string subPath = files[j].Substring(files[j].IndexOf(assetPath));
                    string guid = AssetDatabase.AssetPathToGUID(subPath);

                    EditorUtility.DisplayCancelableProgressBar("Addressable List Up", $"{subPath}", 0f);

                    var moved = setting.CreateOrMoveEntry(guid, group, false, false);
                    movedList.Add(moved); // 사용한 것들을 리스트에 캐싱
                }
                group.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, movedList, false, true);
                setting.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, movedList, false, false);
            }
            AssetDatabase.SaveAssets();

            EditorUtility.ClearProgressBar();
            Debug.Log("Finished listing up addressables.");
        }
        catch(System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(ex);
            Debug.LogError("Failed to listing up addressables.");
        }
    }
    [MenuItem("Addressable/3. Build")]
    private static void BuildAddressable() // 어드레서블 빌드 실행
    {
        try
        {
            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("Successfully built addressables.");
        }
        catch(System.Exception ex)
        {
            Debug.LogError(ex);
            Debug.LogError("Failed to addressable build.");
        }
    }
    private static void CheckAddressableDefaultSetting()
    {
        if (AddressableAssetSettingsDefaultObject.Settings == null) // 어드레서블 default 세팅이 없다면 생성
        {
            AddressableAssetSettings.Create("Assets/AddressableAssetsData", "AddressableAssetSettings", true, true);
            AssetDatabase.SaveAssets();
        }
    }
}

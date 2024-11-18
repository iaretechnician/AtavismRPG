using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.IO;
using UnityEditor.SceneManagement;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Atavism
{
    [InitializeOnLoad]
    class InitialSetup
    {
        static InitialSetup()
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/..") + "/Assets/TextMesh Pro/Resources"))
                InstallTMPEsenc();

            if (!File.Exists(Path.GetFullPath("Assets/..") + "/InitialSetup.txt"))
            {
                EditorApplication.update += Update;

            }
        }

        [MenuItem("Window/Atavism/Atavism Unity Setup")]
        public static void SetupAtavismUnity()
        {
            if (EditorUtility.DisplayDialog("Atavism Setup", "Are you sure you want to process Atavism Setup for Unity?\n" +
                                                             "We will set your project settings as following:\n" +
                                                             "- Add necessary layers\n" +
                                                             "- Add scenes to the build settings\n" +
                                                             "- Add TextMesh Pro with installed Essentials\n" +
                                                             "- Set Player settings(Color space to Linear, and.NET to 4.x)", "Setup", "Do Not Setup"))
            {
                Setup();
            }
        }
        
        [MenuItem("Window/Atavism/Rebuild Assets Bundles")]
        public static void RebuildAssets()
        {
            if (!Directory.Exists(Application.streamingAssetsPath))
                Directory.CreateDirectory(Application.streamingAssetsPath);
            BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
        }

        static void Update()
        {
            PlayerPrefs.SetInt("AtavismSetup", 1);
            PlayerPrefs.Save();
            EditorApplication.update -= Update;
            TextWriter tw = new StreamWriter(Path.GetFullPath("Assets/..") + "/InitialSetup.txt", true);
            tw.Close();
            if (EditorUtility.DisplayDialog("Atavism Setup", "Are you sure you want to process Atavism Setup for Unity?\n" +
                                                             "We will set your project settings as following:\n" +
                                                             "- Add necessary layers\n" +
                                                             "- Add scenes to the build settings\n" +
                                                             "- Add TextMesh Pro with installed Essentials\n" +
                                                             "- Set Player settings(Color space to Linear, and.NET to 4.x)", "Setup", "Do Not Setup"))
            {
                Setup();
            }
        }

        static void InstallTMPEsenc()
        {
            AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
            PlayerPrefs.SetInt("AtavismSetupTMP", 0);
            PlayerPrefs.Save();
            //   EditorSceneManager.OpenScene("Assets/Dragonsan/Scenes/Login.unity"); 

        }

        static void Setup()
        {
            string MainFolder = "Assets/Dragonsan/Scenes/";
            string SceneType = ".unity";
            string[] ScenesList = new string[] { "Login", "CharacterSelection", "MainWorld", "Arena1v1", "Arena2v2", "Deathmatch 1v1", "Deathmatch 2v2", "SinglePlayerPrivate", "GuildPrivate", };
            int ii = 0;
            int notexist = 0;
            for (ii = 0; ii < ScenesList.Length; ii++)
            {
                if (!File.Exists(MainFolder + ScenesList[ii] + SceneType))
                {
                    notexist++;
                }
            }
            // EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] newSettings = new EditorBuildSettingsScene[/*original.Length + */ScenesList.Length - notexist];
            //  System.Array.Copy(original, newSettings, original.Length);
            int i = 0;
            int index = 0;/* original.Length;*/
            for (i = 0; i < ScenesList.Length; i++)
            {
                if (File.Exists(MainFolder + ScenesList[i] + SceneType))
                {
                    EditorBuildSettingsScene sceneToAdd = new EditorBuildSettingsScene(MainFolder + ScenesList[i] + SceneType, true);
                    newSettings[index] = sceneToAdd;
                    index++;
                }
            }
            EditorBuildSettings.scenes = newSettings;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layerProps = tagManager.FindProperty("layers");
            var layerProp8 = layerProps.GetArrayElementAtIndex(8);
            layerProp8.stringValue = "Targetable";
            var layerProp9 = layerProps.GetArrayElementAtIndex(9);
            layerProp9.stringValue = "MiniMap";
            var layerProp10 = layerProps.GetArrayElementAtIndex(10);
            layerProp10.stringValue = "AtavismText";
            var layerProp11 = layerProps.GetArrayElementAtIndex(11);
            layerProp11.stringValue = "Socket";
            tagManager.ApplyModifiedProperties();
            // Client.Remove("TextMesh Pro");
            if (Directory.Exists(Path.GetFullPath("Assets/..") + "/Assets/TextMesh Pro"))
                Directory.Delete(Path.GetFullPath("Assets/..") + "/Assets/TextMesh Pro", true);
#if UNITY_2018_2
            // Client.Add("com.unity.textmeshpro@1.2.4");
            AssetDatabase.ImportPackage("Assets/Dragonsan/AtavismObjects/External/SystemThreading.unitypackage", false);
#endif
#if UNITY_2018_3
            AssetDatabase.ImportPackage("Assets/Dragonsan/AtavismObjects/External/SystemThreading.unitypackage", false);
#endif
#if UNITY_2018_2_OR_NEWER
            //   Client.Add("com.unity.textmeshpro");
#endif
            PlayerPrefs.SetInt("AtavismSetupTMP", 1);
            PlayerPrefs.Save();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.graphicsJobs = true;
            if (PlayerSettings.scriptingRuntimeVersion != ScriptingRuntimeVersion.Latest)
                PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Latest;
            
            if (!Directory.Exists(Application.streamingAssetsPath))
                Directory.CreateDirectory(Application.streamingAssetsPath);
            BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
            
            //    AssetDatabase.ImportPackage("Assets/Dragonsan/AtavismEditor/Editor/Interfaces/system35.unitypackage", false);
            if (Directory.Exists(Path.GetFullPath("Assets/..") + "/Assets/TextMesh Pro"))
                Directory.Delete(Path.GetFullPath("Assets/..") + "/Assets/TextMesh Pro", true);

            Request = Client.List();    // List packages installed for the Project
            EditorApplication.update += Progress;
            //PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_2_0_Subset);
         //  PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_4_6);

        }
       static void Progress()
        {
            Debug.Log("Progress "+Request.IsCompleted);
            if (Request.IsCompleted)
            {
                if (Request.Status == StatusCode.Success)
                    foreach (var package in Request.Result)
                    {
                        //Debug.Log("Progress "+package.name);
                        if (package.name.Equals("com.unity.textmeshpro"))
                        {
                            tmpro = package;
                            Debug.Log("Progress founf tmpro");
                        }

                        if (package.name.Equals("com.unity.addressables"))
                        {
                            Debug.Log("Progress found addressables");
                            addr = package;
                        }
                        
                        if (package.name.Equals("com.unity.shadergraph"))
                        {
                            Debug.Log("Progress found shadergraph");
                            shadergraph = package;
                        }
                    }

                if (tmpro != null)
                {
                    if (!tmpro.version.Equals(tmpro.versions.recommended))
                    {
                        Request2 = Client.Add("com.unity.textmeshpro" + (tmpro.versions.recommended != "" ? '@' + tmpro.versions.recommended : ""));
                        EditorApplication.update += ProgressTmpro;
                    }
                    else
                    {
                        AssetDatabase.Refresh();
                        try
                        {
                            AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    

                        if (addr != null)
                        {
                            if (!addr.version.Equals(addr.versions.recommended))
                            {
                                Debug.Log("ProgressTmpro install addressables");
                                EditorApplication.update += ProgressAddressables;
                                Request3 = Client.Add("com.unity.addressables" + (addr.versions.recommended != "" ? '@' + addr.versions.recommended : ""));
                            }
                            else
                            {
                                if (shadergraph != null)
                                {
                                    if (!shadergraph.version.Equals(shadergraph.versions.recommended))
                                    {
                                        Debug.Log("ProgressTmpro install shadergraph");
                                        EditorApplication.update += ProgressShadergraph;
                                        Request3 = Client.Add("com.unity.shadergraph" + (addr.versions.recommended != "" ? '@' + addr.versions.recommended : ""));
                                    }
                                    else
                                    {
                                        AddAddressablesSympol();
                                        EditorUtility.DisplayDialog("Atavism Setup", "Atavism setup was successful", "OK", "");
                                    }
                                }
                                else
                                {
                                    Debug.Log("ProgressTmpro search shadergraph");
                                    EditorApplication.update += ProgressSearchShadergraph;
                                    RequestSearch = Client.Search("com.unity.shadergraph");
                                }
                            }
                        }
                        else
                        {
                            Debug.Log("ProgressTmpro search addressables");
                            EditorApplication.update += ProgressSearchAddressables;
                            RequestSearch = Client.Search("com.unity.addressables");
                        }

                    }
                }
                else
                {
                    Debug.Log("ProgressTmpro search textmeshpro");
                    EditorApplication.update += ProgressSearchTmpro;
                    RequestSearchTmp = Client.Search("com.unity.textmeshpro");
                }
                EditorApplication.update -= Progress;
            }
        }

       static void ProgressSearchShadergraph()
       {
           Debug.Log("ProgressSearchShadergraph "+RequestSearch.IsCompleted);
           if (RequestSearch.IsCompleted)
           {
               EditorApplication.update -= ProgressSearchShadergraph;
               if (RequestSearch.Status == StatusCode.Success)
               {
                   Debug.Log("ProgressSearchShadergraph success ");
                   foreach (var package in RequestSearch.Result)
                   {
                       Debug.Log("ProgressSearchShadergraph Package name: " + package.name + " " + package.version + " " + package.versions.latestCompatible + " " + package.versions.latest + " " + package.versions.recommended);

                       if (package.name.Equals("com.unity.shadergraph"))
                       {
                           Debug.Log("ProgressSearchShadergraph install shadergraph");
                           EditorApplication.update += ProgressShadergraph;
                           Request3 = Client.Add("com.unity.shadergraph" + (package.versions.recommended != "" ? '@' + package.versions.recommended : ""));
                       }
                   }
               }
               else
               {
                   Debug.Log("ProgressSearchShadergraph no success ");
               }
           }
       }
       
        static void ProgressSearchAddressables()
        {
            Debug.Log("ProgressSearchAddressables "+RequestSearch.IsCompleted);
            if (RequestSearch.IsCompleted)
            {
                EditorApplication.update -= ProgressSearchAddressables;
                if (RequestSearch.Status == StatusCode.Success)
                {
                    Debug.Log("ProgressSearchAddressables success ");
                    foreach (var package in RequestSearch.Result)
                    {
                        Debug.Log("ProgressSearchAddressables Package name: " + package.name + " " + package.version + " " + package.versions.latestCompatible + " " + package.versions.latest + " " + package.versions.recommended);

                        if (package.name.Equals("com.unity.addressables"))
                        {
                            Debug.Log("ProgressSearchAddressables install addressables");
                            EditorApplication.update += ProgressAddressables;
                            Request3 = Client.Add("com.unity.addressables" + (package.versions.recommended != "" ? '@' + package.versions.recommended : ""));
                        }
                    }
                }
                else
                {
                    Debug.Log("ProgressSearchAddressables no success ");
                }
            }
        }

        static void ProgressSearchTmpro()
        {
            Debug.Log("ProgressSearchTmpro "+RequestSearchTmp.IsCompleted);
            if (RequestSearchTmp.IsCompleted)
            {
                EditorApplication.update -= ProgressSearchTmpro;
                if (RequestSearchTmp.Status == StatusCode.Success)
                {
                    Debug.Log("ProgressSearchTmpro success ");
                    foreach (var package in RequestSearchTmp.Result)
                    {
                        Debug.Log("ProgressSearchTmpro Package name: " + package.name + " " + package.version + " " + package.versions.latestCompatible + " " + package.versions.latest + " " + package.versions.recommended);
                        if (package.name.Equals("com.unity.textmeshpro"))
                        {
                            Request2 = Client.Add("com.unity.textmeshpro" + (package.versions.recommended != "" ? '@' + package.versions.recommended : ""));
                            EditorApplication.update += ProgressTmpro;
                        }
                    }
                }
                else
                {
                    Debug.Log("ProgressSearchTmpro no success ");
                }
            }
        }


        static void ProgressTmpro()
        {
            Debug.Log("ProgressTmpro");

            if (Request2.IsCompleted)
            {
                if (Request2.Status == StatusCode.Success)
                {
                    EditorApplication.update -= ProgressTmpro;
                    //   AssetDatabase.Refresh();
                    try
                    {
                        //  AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    if (addr != null)
                    {
                        Debug.Log("ProgressTmpro addr not null");
                        if (!addr.version.Equals(addr.versions.recommended))
                        {
                            Debug.Log("ProgressTmpro install addressables");
                            EditorApplication.update += ProgressAddressables;
                            Request3 = Client.Add("com.unity.addressables" + (addr.versions.recommended != "" ? '@' + addr.versions.recommended : ""));
                        }
                        else
                        {
                            if (shadergraph != null)
                            {
                                if (!shadergraph.version.Equals(shadergraph.versions.recommended))
                                {
                                    Debug.Log("ProgressTmpro install shadergraph");
                                    EditorApplication.update += ProgressShadergraph;
                                    Request3 = Client.Add("com.unity.shadergraph" + (addr.versions.recommended != "" ? '@' + addr.versions.recommended : ""));
                                }
                                else
                                {
                                    AddAddressablesSympol();
                                    EditorUtility.DisplayDialog("Atavism Setup", "Atavism setup was successful", "OK", "");
                                    AssetDatabase.Refresh();
                                    try
                                    {
                                        AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                }
                            }
                            else
                            {
                                Debug.Log("ProgressTmpro search shadergraph");
                                EditorApplication.update += ProgressSearchShadergraph;
                                RequestSearch = Client.Search("com.unity.shadergraph");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("ProgressTmpro search addr");
                        RequestSearch = Client.Search("com.unity.addressables");
                        EditorApplication.update += ProgressSearchAddressables;
                    }
                }
            }
        }

     
        
        
        
        
        
        static void ProgressAddressables()
        {
            Debug.Log("ProgressAddressables");

            if (Request3.IsCompleted)
            {
                Debug.Log("ProgressAddressables IsCompleted");
                EditorApplication.update -= ProgressAddressables;
                if (Request3.Status == StatusCode.Success)
                {
                    Debug.Log("ProgressAddressables Success");
                    if (shadergraph != null)
                    {
                        if (!shadergraph.version.Equals(shadergraph.versions.recommended))
                        {
                            Debug.Log("ProgressAddressables install shadergraph");
                            EditorApplication.update += ProgressShadergraph;
                            Request3 = Client.Add("com.unity.shadergraph" + (addr.versions.recommended != "" ? '@' + addr.versions.recommended : ""));
                        }
                        else
                        {
                            AddAddressablesSympol();
                            EditorUtility.DisplayDialog("Atavism Setup", "Atavism setup was successful", "OK", "");
                            AssetDatabase.Refresh();
                            try
                            {
                                AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("ProgressAddressables search shadergraph");
                        EditorApplication.update += ProgressSearchShadergraph;
                        RequestSearch = Client.Search("com.unity.shadergraph");
                    }

                }
                else if (Request.Status >= StatusCode.Failure)
                    Debug.Log("Failure "+Request.Error.message);

              
            }
        }
        
        static void ProgressShadergraph()
        {
            Debug.Log("ProgressShadergraph");

            if (Request3.IsCompleted)
            {
                Debug.Log("ProgressShadergraph IsCompleted");
                EditorApplication.update -= ProgressShadergraph;
                if (Request3.Status == StatusCode.Success)
                {
                    Debug.Log("ProgressShadergraph Success");
                    AddAddressablesSympol();
                    AssetDatabase.Refresh();
                    try
                    {
                        AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    EditorUtility.DisplayDialog("Atavism Setup", "Atavism setup was successful", "OK", "");

                }
                else if (Request.Status >= StatusCode.Failure)
                    Debug.Log("Failure "+Request.Error.message);

              
            }
        }

        static void AddAddressablesSympol()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!symbols.Contains("AT_ADDRESSABLES"))
            {
                symbols += ";" + "AT_ADDRESSABLES";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
            }
        }

        private static PackageInfo tmpro;
        private static PackageInfo addr;
        private static PackageInfo shadergraph;
        static ListRequest Request;
        static SearchRequest RequestSearch;
        static SearchRequest RequestSearchTmp;
        static AddRequest Request2;
        static AddRequest Request3;
    }
}

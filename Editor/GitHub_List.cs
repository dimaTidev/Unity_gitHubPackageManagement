// About GitHub OAuth 
// https://docs.github.com/en/developers/apps/building-oauth-apps/authorizing-oauth-apps

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using Unity.EditorCoroutines.Editor;
using System;
using System.IO;
using System.Linq;

using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;

//TODO: save all receive data about packageList.
//      If isSaveToTempDataAssets then in OnEnable request list data and compare with saved data for find difference
//      and update ONLY packages which has changes!

//TODO: make filters for switch from Keywords to Category
//TODO: make toggle for show packageName or packageDisplayedName
//TODO: make search depend show name or displayedName

//TODO: make gridDragPackageView with save positions data to github
//TODO: gridDragPackageView - creation menu by right click
//TODO: gridDragPackageView - create empty node
//TODO: gridDragPackageView - resize node
//TODO: gridDragPackageView - setup empty node by packages
//TODO: gridDragPackageView - or you can dragDrop packages to Empty block and it parented
//TODO: gridDragPackageView - text node
//TODO: gridDragPackageView - with packages which not exist in area


namespace PackageCreator
{
    public class GitHub_List : EditorWindow
    {
        [MenuItem("Tools/GitHub_Packages")]
        static void Init() => ((GitHub_List)EditorWindow.GetWindow(typeof(GitHub_List))).Show();

        public Dictionary<string, RepoData> repos = new Dictionary<string, RepoData>();
        public class RepoData
        {
            public string rawData;
            public PackageJson package;
            public string url;

            public RepoData(string url) => this.url = url;
            public RepoData(PackageJson package) 
            {
                this.package = package;
                url = package.repository.url;
            }
            public void SetRawData(string rawData) => this.rawData = rawData;

            public string ToJson()
            {
                if (package == null)
                    return "";
                string value = JsonUtility.ToJson(package, true);
                return package.SerializeDictionaryToJson(value);
            }
        }


        private void OnEnable()
        {
            CheckGitIgnore();
            LoadTempSettings();

            if (LoadTempData())
                return;
            string url = "https://api.github.com/search/repositories?q=user:dimaTidev";
            EditorCoroutineUtility.StartCoroutine(SendRequest(url, GetToken(), CreateRepolist), this);
        }

        #region InstalledPackages
        //------------------------------------------------------------------------------------------------------
        Dictionary<string, UnityEditor.PackageManager.PackageInfo> installedPackages;

        void OnEnable_Packages()
        {
            ListRequest request = Client.List();
            EditorApplication.update += () => Progress(request);
        }

        void Progress(ListRequest request)
        {
            if (request.IsCompleted)
            {
                if (request.Status == StatusCode.Success)
                {
                    installedPackages = new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();
                    foreach (var package in request.Result)
                        installedPackages.Add(package.name, package);
                }  
                else
                if (request.Status >= StatusCode.Failure)
                    Debug.LogError(request.Error.message);

                EditorApplication.update -= () => Progress(request);
                Repaint();
            }
        }

        //------------------------------------------------------------------------------------------------------
        #endregion

        #region PackagesInAssets
        //-------------------------------------------------------------------------------------------------------------------
        Dictionary<string, PackageJson> packagesInAssets;

        void OnEnable_Search_PackagesInAssets()
        {
            string[] paths = Directory.GetFiles(Application.dataPath, "package.json", SearchOption.AllDirectories);

            packagesInAssets = new Dictionary<string, PackageJson>();

            for (int i = 0; i < paths.Length; i++)
            {
                PackageJson package = PackageJson.Parce(File.ReadAllText(paths[i]));
                packagesInAssets.Add(package.name, package);
            }   
        }
        //-------------------------------------------------------------------------------------------------------------------
        #endregion

        #region Search
        //-----------------------------------------------------------------------------------------------------------------------
        string search;
        void OnGUI_Search()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("d_ViewToolZoom"), GUILayout.MaxWidth(20));
            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField(search);
            if (EditorGUI.EndChangeCheck())
                SortIds();
            EditorGUILayout.EndHorizontal();
        }
        //-----------------------------------------------------------------------------------------------------------------------
        #endregion

        #region OnGUI
        //-------------------------------------------------------------------------------------------------------------------
        void OnGUI()
        {
            OnGUI_gitIgnore();
            //--------------------------------------------------
            EditorGUILayout.BeginHorizontal("Helpbox");


            if (OnGUI_Setting_Token())
            {
                EditorGUILayout.EndHorizontal();
                return;
            }

            GUI.enabled = !isEnterToken;

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Refresh"), GUILayout.Width(30)))
            {
                string url = "https://api.github.com/search/repositories?q=user:dimaTidev";
                EditorCoroutineUtility.StartCoroutine(SendRequest(url, GetToken(), CreateRepolist), this);
            }

            OnGUI_Settings_Filters();

            OnGUI_Settings_SaveTempData();

            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            EditorGUILayout.BeginHorizontal();
            OnGUI_Search();
            OnGUI_SortButtons();
            EditorGUILayout.EndHorizontal();

            OnGUI_DrawFilters();
            //--------------------------------------------------
            EditorGUILayout.BeginHorizontal("Helpbox");

            EditorGUILayout.BeginVertical("Helpbox", GUILayout.Width(250));
            OnGUI_DrawRepos();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("Helpbox");
            DrawPackageData();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        Vector2 scrollPos_repos;
        void OnGUI_DrawRepos()
        {
            if (repos == null || repos.Count == 0)
                return;

            scrollPos_repos = EditorGUILayout.BeginScrollView(scrollPos_repos, false, true, GUILayout.Width(280)); //---------

            if(filterMode == FilterModes.enable)
            {
                if(ids != null && ids.Count > 0)
                    for (int i = 0; i < ids[0].Count; i++)
                    {
                        var item = repos[ids[0][i]];

                        if (item.package == null)
                            continue;
                        if (filtersSelected.Count == 0)
                            DrawRepoButton(item);
                        else if (item.package.keywords.Any(x => filtersSelected.Any(y => x == y)))
                            DrawRepoButton(item);
                    }

              //  foreach (var item in repos)
              //  {
              //      if (item.Value.package == null)
              //          continue;
              //      if (filtersSelected.Count == 0)
              //          DrawRepoButton(item.Value);
              //      else if (item.Value.package.keywords.Any(x => filtersSelected.Any(y => x == y)))
              //          DrawRepoButton(item.Value);
              //  }
            }
            else if (filterMode == FilterModes.folding)
            {
                if(ids != null)
                {
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (ids[i] == null)
                            continue;

                        expands[i] = EditorGUILayout.Foldout(expands[i], i < filtersSelected.Count ? filtersSelected[i] : "All packages");

                        if (expands[i] == false)
                            continue;

                        for (int k = 0; k < ids[i].Count; k++)
                        {
                            var item = repos[ids[i][k]];
                            DrawRepoButton(item);
                        }
                    }
                }
             
            }
            EditorGUILayout.EndScrollView();

            // EditorGUILayout.TextField(item.Key, item.Value.url);
            // DrawPackageData(item.Value.package);
        }

        void OnGUI_SortButtons()
        {
            EditorGUILayout.LabelField("HighLight Installed Packages:", GUILayout.Width(170));
            isHighLighted_installedPackages = EditorGUILayout.Toggle(isHighLighted_installedPackages, GUILayout.Width(20));
            EditorGUILayout.LabelField("Show Installed Packages:", GUILayout.Width(150));
            isShow_installedPackages = EditorGUILayout.Toggle(isShow_installedPackages, GUILayout.Width(20));
            EditorGUILayout.LabelField("Show Packages in Assets:", GUILayout.Width(153));
            isShow_packagesInAssets = EditorGUILayout.Toggle(isShow_packagesInAssets, GUILayout.Width(20));
        }

        bool
            isHighLighted_installedPackages,
            isShow_installedPackages,
            isShow_packagesInAssets;

        void DrawRepoButton(RepoData item)
        {
            if (item == null || item.package == null)
                return;

            if (!isShow_installedPackages && installedPackages != null && installedPackages.ContainsKey(item.package.name))
                return;

            if (!isShow_packagesInAssets && packagesInAssets != null && packagesInAssets.ContainsKey(item.package.name))
                return;

            EditorGUILayout.BeginHorizontal();

            if (isHighLighted_installedPackages)
            {
                if (installedPackages != null && installedPackages.ContainsKey(item.package.name))
                    GUI.color = Color.yellow;

                if (packagesInAssets != null && packagesInAssets.ContainsKey(item.package.name))
                    GUI.color = Color.magenta;
                if (installedPackages != null && installedPackages.ContainsKey(item.package.name) ||
                    packagesInAssets != null && packagesInAssets.ContainsKey(item.package.name))
                {
                    EditorGUILayout.LabelField("installed", GUILayout.Width(50));
                    GUI.color = Color.grey;
                }
            }

            GUI.skin.button.alignment = TextAnchor.MiddleLeft;
            if (GUILayout.Button(item.package.displayName))//, GUILayout.Width(200)))
                packageJson = item.package;

         //   EditorGUILayout.LabelField("v" + item.package.version, GUILayout.Width(50));
            GUI.skin.button.alignment = TextAnchor.MiddleCenter;

            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }


        public PackageJson packageJson = new PackageJson(false);
        void DrawPackageData()
        {
            ScriptableObject target = this;
            SerializedObject so = new SerializedObject(target);

            SerializedProperty list = so.FindProperty("packageJson");
            if(list != null)
                EditorGUILayout.PropertyField(list, true);
        }

        //-------------------------------------------------------------------------------------------------------------------
        #endregion

        #region OAuth_Test
        //-------------------------------------------------------------------------------------------------------
        //  /repos?per_page=100  - сколько pages отобразить

        // Походу надо добавить хедер в запрос, но я хз как. запросы надо отправлять через curl но как это в юнити сделать?
        // https://developer.github.com/changes/2020-02-10-deprecating-auth-through-query-param/
        // https://api.github.com/users/dimaTidev/repos?access_token=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 - выдает ошибку
        // https://github.community/t/how-to-get-list-of-private-repositories-via-api-call/120175/3
        // https://stackoverflow.com/questions/57354154/how-to-use-curl-in-unity-by-changing-curl-to-wwwform - добавление хедера в WWW()


        // Рабочая команда через командную строку
        //"curl -H "Authorization: token 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" https://api.github.com/search/repositories?q=user:dimaTidev;

        void OnGUI_Auth()
        {
            // EditorGUILayout.LabelField(Application.dataPath);

            // if (GUILayout.Button("OAuth"))
            //     EditorCoroutineUtility.StartCoroutine(SendAuth("https://github.com/login/oauth/authorize"), this);
            //
            // if (GUILayout.Button("OAuth access token"))
            //     EditorCoroutineUtility.StartCoroutine(SendAuth("https://github.com/login/oauth/access_token"), this);

            //   if (GUILayout.Button("OAuth"))
            //   {
            //       Debug.Log("OAuth request");
            //       EditorCoroutineUtility.StartCoroutine(SendAuth("https://github.com/login/device/code"), this);
            //
            //       
            //   }
        }

        IEnumerator SendAuth(string url)
        {
            var request = UnityWebRequest.Get(url);

            request.SetRequestHeader("client_id", ""); //mail
            request.SetRequestHeader("lient_secret", ""); //pass

            yield return request.SendWebRequest();

            if (!request.isHttpError && !request.isNetworkError)
                Debug.Log(request.downloadHandler.text);
            else
                Debug.LogError($"error request: {request.error}");

            Debug.Log("SendAuth End");
            request.Dispose();
        }


        //-------------------------------------------------------------------------------------------------------
        #endregion

        #region Filters
        //----------------------------------------------------------------------------------------------------------------------
        List<string> filters = new List<string>();
        public enum FilterModes
        {
            enable,
            folding
        }

        FilterModes filterMode = FilterModes.folding;
        void OnGUI_Settings_Filters()
        {
            EditorGUILayout.LabelField("FilterMode: ", GUILayout.Width(65), GUILayout.ExpandWidth(false));
            EditorGUI.BeginChangeCheck();
            filterMode = (FilterModes)EditorGUILayout.EnumPopup(filterMode, GUILayout.Width(80), GUILayout.ExpandWidth(false));
            if (EditorGUI.EndChangeCheck())
                SortIds();
        }
        void OnGUI_DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Filters: ", GUILayout.Width(43), GUILayout.ExpandWidth(false));
            for (int i = 0; i < filters.Count; i++)
            {
                GUI.color = filtersSelected.Contains(filters[i]) ? Color.yellow : Color.white;
                if (GUILayout.Button(filters[i], GUILayout.MaxWidth(Mathf.Max(25, filters[i].Length * 10f))))
                {
                    if (filtersSelected.Contains(filters[i]))
                        filtersSelected.Remove(filters[i]);
                    else
                        filtersSelected.Add(filters[i]);
                    SaveTempFilters();
                    SortIds();
                }
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        void CreateFilters()
        {
            filters = new List<string>();
            foreach (var item in repos)
            {
                if (item.Value.package == null || item.Value.package.keywords == null)
                    continue;

                for (int i = 0; i < item.Value.package.keywords.Count; i++)
                {
                    if (!filters.Contains(item.Value.package.keywords[i]))
                        filters.Add(item.Value.package.keywords[i]);
                }
            }
        }
        List<string> filtersSelected = new List<string>();
        void SaveTempFilters()
        {
            if (filtersSelected == null)
                return;

            string collapse = "";

            for (int i = 0; i < filtersSelected.Count; i++)
            {
                if (i > 0)
                    collapse += ",";
                collapse += filtersSelected[i];
            }

            if (PathTempData(out string path, FileNameFilters))
                File.WriteAllText(path, collapse);
        }
        void LoadTempFilters()
        {
            if (filtersSelected == null)
                filtersSelected = new List<string>();
            filtersSelected.Clear();

            string collapse = "";

            for (int i = 0; i < filtersSelected.Count; i++)
            {
                if (i > 0)
                    collapse += ",";
                collapse += filtersSelected[i];
            }

            if (PathTempData(out string path, FileNameFilters))
            {
                if (!File.Exists(path))
                    return;
                string data = File.ReadAllText(path);
                string[] split = data.Split(',');
                for (int i = 0; i < split.Length; i++)
                {
                    if (split[i] == "")
                        continue;
                    if (!filtersSelected.Contains(split[i]))
                        filtersSelected.Add(split[i]);
                }
            }
        }
        //----------------------------------------------------------------------------------------------------------------------
        #endregion

        #region RepoList
        //--------------------------------------------------------------------------------------------------------------------------------
        List<List<string>> ids;
        List<bool> expands = new List<bool>();

        void CreateRepolist(string urlData) 
        {
            repos = Parce_RepoList(urlData);
            EditorCoroutineUtility.StartCoroutine(RequestPackageJsonData(), this);
        }
        IEnumerator RequestPackageJsonData()
        {
            List<string> keys = repos.Keys.ToList();

            float steps = keys.Count;
            float curStep = 0;

            for (int i = 0; i < keys.Count; i++)
            {
                RepoData data = repos[keys[i]];
                string url = "https://api.github.com/repos/dimaTidev/" + System.IO.Path.GetFileNameWithoutExtension(data.url) + "/contents/package.json";

                //  float time = Time.time;
                curStep++;
                EditorUtility.DisplayProgressBar("Request repo data from github", $"Request github/{System.IO.Path.GetFileNameWithoutExtension(data.url)}...", curStep / steps);
                yield return EditorCoroutineUtility.StartCoroutine(SendRequest(url, GetToken(), data.SetRawData, false), this);
              //  Debug.Log($"Finished: {Time.time - time} sec:  " + url + "\n" + data.rawData);
               // yield return new WaitForSecondsRealtime(0.1f);
            }
            EditorUtility.ClearProgressBar();

            curStep = 0;

            for (int i = 0; i < keys.Count; i++)
            {
                curStep++;
                EditorUtility.DisplayProgressBar("Parse Data", $"Parsing {keys[i]}...", curStep / steps);
                repos[keys[i]].package = PackageJson.Parce(RawBase64Decode(ClearString(repos[keys[i]].rawData, "content")));
            }
            EditorUtility.ClearProgressBar();

            SaveTempData();
            LoadTempData();
        }

        void SortIds()
        {
            ids = new List<List<string>>();
            expands = new List<bool>();

            if (filterMode == FilterModes.folding && filtersSelected.Count > 0)
            {
                ids = new List<List<string>>();

                for (int i = 0; i < filtersSelected.Count; i++)
                {
                    ids.Add(new List<string>());
                    expands.Add(true);
                }

                for (int i = 0; i < ids.Count; i++)
                {
                    foreach (var item in repos)
                    {
                        if (item.Value.package == null || item.Value.package.keywords == null)
                            continue;
                        if (item.Value.package.keywords.Contains(filtersSelected[i]))
                            ids[i].Add(item.Key);
                    }
                }
            }
            else if (filterMode == FilterModes.enable || filtersSelected.Count == 0)
            {
                ids.Add(new List<string>());
                expands.Add(true);

                foreach (var item in repos)
                {
                    ids[0].Add(item.Key);
                }

                ids[0] = ids[0].OrderBy(x => x).ToList();
            }


            //Search correction
            if(search != null && search != "" && repos != null)
            {
                for (int i = ids.Count - 1; i >= 0; i--)
                {
                    for (int k = ids[i].Count - 1; k >= 0; k--)
                    {
                        if (!repos.ContainsKey(ids[i][k]) || repos[ids[i][k]].package == null)
                            continue;

                        if (!repos[ids[i][k]].package.displayName.ToLower().Contains(search.ToLower()))
                        {
                            ids[i].RemoveAt(k);
                        }
                    }
                }

                for (int i = ids.Count - 1; i >= 0; i--)
                {
                    if (ids[i].Count == 0)
                        ids.RemoveAt(i);
                }
            }
                
        }

        //--------------------------------------------------------------------------------------------------------------------------------
        #endregion
       
        #region SaveTempData
        //--------------------------------------------------------------------------------------------------------------------------------
        [System.Serializable]
        public class SaveRepoData
        {
            public List<string> keys = new List<string>();
            public List<string> values = new List<string>();
        }

        bool isSaveToProjectFolder;
        string FileNameJsonList => ".PackageTempData";
        string FileNameFilters => ".Filters";

        void OnGUI_Settings_SaveTempData()
        {
            EditorGUILayout.LabelField("is Save tempData to Assets:", GUILayout.Width(165), GUILayout.ExpandWidth(false));
            EditorGUI.BeginChangeCheck();
            isSaveToProjectFolder = EditorGUILayout.Toggle(isSaveToProjectFolder, GUILayout.Width(20), GUILayout.ExpandWidth(false));
            if (EditorGUI.EndChangeCheck())
                SaveTempSettings();
            if (isSaveToProjectFolder)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("It means you need refresh packages manualy!");
                GUI.color = Color.white;
            }
                
        }

        string PathToTempSettings 
        {
            get
            {
                string path = Application.dataPath;
                if (!path.EndsWith("/"))
                    path += "/";
                return path + ".settings" + saveDataExtension;
            }
        }

        void SaveTempSettings()
        {
            string path = PathToTempSettings;
            File.WriteAllText(path, isSaveToProjectFolder ? "1" : "0");
        }

        void LoadTempSettings()
        {
            string path = PathToTempSettings;
            if (!File.Exists(path))
                return;
            string data = File.ReadAllText(path);
            isSaveToProjectFolder = data == "1";
        }

        bool PathTempData(out string path, string fileName = "")
        {
            path = Application.dataPath;
            if (!path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                path += System.IO.Path.DirectorySeparatorChar.ToString();


            //   Debug.Log("paht: " + path + "PackageTempData.json");

            if (!isSaveToProjectFolder)
            {
                path = Path.GetFullPath(Path.Combine(path, @"..\"));
                path += "Temp/";
            }
           

            if (!Directory.Exists(path))
            {
                Debug.LogError("Path not exist: " + path);
                return false;
            }

            path += fileName + saveDataExtension;
            return true;
        }
        void SaveTempData()
        {
            SaveRepoData saveList = new SaveRepoData();
            List<string> keys = repos.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                saveList.keys.Add(keys[i]);
                saveList.values.Add(repos[keys[i]].ToJson());
            }

            for (int i = saveList.values.Count - 1; i >= 0; i--)
            {
                if(saveList.values[i] == "")
                {
                    saveList.keys.RemoveAt(i);
                    saveList.values.RemoveAt(i);
                }
            }

            string saveResult = JsonUtility.ToJson(saveList, true);

            if(PathTempData(out string path, FileNameJsonList))
                File.WriteAllText(path, saveResult);     
        }
        bool LoadTempData()
        {
            if (!PathTempData(out string path, FileNameJsonList))
                return false;

            if (!File.Exists(path))
                return false;

            string json = File.ReadAllText(path);
            SaveRepoData saveList = JsonUtility.FromJson<SaveRepoData>(json);

            repos = new Dictionary<string, RepoData>();

            for (int i = 0; i < saveList.keys.Count; i++)
                repos.Add(saveList.keys[i], new RepoData(PackageJson.Parce(saveList.values[i])));

            CreateFilters();
            LoadTempFilters();
            SortIds();

            OnEnable_Packages();
            OnEnable_Search_PackagesInAssets();

            return true;
        }
        //--------------------------------------------------------------------------------------------------------------------------------
        #endregion

        #region GitIgnoreCheck
        //--------------------------------------------------------------------------------------------------------------------------------
        string saveDataExtension = ".githubListData";

        int gitIgnoreStatus = 0;
        void OnGUI_gitIgnore()
        {
            if (gitIgnoreStatus == 0)
                return;

            GUI.color = Color.red;
            EditorGUILayout.BeginHorizontal("Helpbox");
            GUI.color = Color.white;

            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("console.erroricon"), GUILayout.Width(40), GUILayout.Height(40));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"Git ignore file does not {(gitIgnoreStatus == 1 ? $"contain ignore extension: *{saveDataExtension} " : $" Exist")}. Of course if you didn't want to push to project repo your GitHub personal token!");
            EditorGUILayout.LabelField("If you don't care, ignore this error. But keep in mind: your GitHub personal token pushed to project repo!");
            EditorGUILayout.LabelField("Check path: " + Application.dataPath.Replace("Assets", "") + ".gitignore");
            EditorGUILayout.LabelField("For recheck need reopen window");
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        void CheckGitIgnore()
        {
            string gitIgnoreName = ".gitignore";
            string path = Application.dataPath;
            path = Path.Combine(path, @"..\");
            if (File.Exists(path + gitIgnoreName))
            {
                string fileData = File.ReadAllText(path + gitIgnoreName);
                if (!fileData.Contains($"*{saveDataExtension}"))
                {
                    gitIgnoreStatus = 1;
                    //Debug.LogError($"Git ignore file does not contain ignore extension: *{saveDataExtension}. Of course if you didn't want to push to project repo your GitHub personal token! If you don't care, ignore this error. But keep in mind: your GitHub personal token pushed to project repo!");
                }
            }
            else
                gitIgnoreStatus = 2;
                //Debug.LogError($"Git ignore file does not Exist. Of course if you didn't want to push to project repo your GitHub personal token! If you don't care, ignore this error. But keep in mind: your GitHub personal token pushed to project repo!");
        }
        //--------------------------------------------------------------------------------------------------------------------------------
        #endregion

        #region Token
        //--------------------------------------------------------------------------------------------------------------------------------
        public string token = "";
        bool isEnterToken;

        bool OnGUI_Setting_Token()
        {
            if (!ExistTokenFile)
                isEnterToken = true;

            if (!isEnterToken)
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_Settings"), GUILayout.Width(30))) //d_MoreOptions // "Enter new token"
                    isEnterToken = true;

            OnGUI_TokenEnter();
            return isEnterToken;
        }
        void OnGUI_TokenEnter()
        {
            if (!isEnterToken)
                return;

            EditorGUILayout.BeginVertical();

            // EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Exist token:", GetToken());
            token = EditorGUILayout.TextField("New token:", token);
            //if (EditorGUI.EndChangeCheck())
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cancel"))
                isEnterToken = false;
            GUI.color = token != "" ? Color.green : Color.grey;
            GUI.enabled = token != "";
            if (GUILayout.Button("Save token"))
            {
                SaveToken(token);
                isEnterToken = false;
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        bool ExistTokenFile => File.Exists(PathToToken);
        string PathToToken
        {
            get
            {
                // Unity not generate meta for next:
                //  Files and folders which start with ‘.’.
                //  Files and folders which end with ‘~’.
                //  Files and folders named cvs.
                //  Files with the extension.tm
                //https://docs.unity3d.com/Manual/SpecialFolders.html

                string path = Application.dataPath;
                if (!path.EndsWith("/")) path += "/";
                path += ".GitHubPersonalToken" + saveDataExtension;
                return path;
            }
        }
        string GetToken() => ExistTokenFile ? File.ReadAllText(PathToToken) : token;
        void SaveToken(string token) => File.WriteAllText(PathToToken, token);
        Dictionary<string, RepoData> Parce_RepoList(string rawText)
        {
            //Debug.Log("rawText: " + rawText);
            Dictionary<string, RepoData> result = new Dictionary<string, RepoData>();

            string[] splited = rawText.Split(new string[] { "\"name\":" }, StringSplitOptions.None);

            for (int i = 1; i < splited.Length - 1; i++) //Add back name
                splited[i] = "\"name\":" + splited[i];

            for (int i = 1; i < splited.Length - 1; i++)
                result.Add(ClearString(splited[i], "name"), new RepoData(ClearString(splited[i], "clone_url")));

            // foreach (var item in result)
            //     Debug.Log("item: " + item.Key + " --- " + item.Value);
            // 
            return result;
        }
        //--------------------------------------------------------------------------------------------------------------------------------
        #endregion

        #region Extensions
        //---------------------------------------------------------------------------------------------------------------------------
        string ClearString(string targetText, string removeValue)
        {
            if (targetText == null || targetText == "")
                return "";

            removeValue = $"\"{removeValue}\": ";

            int idStart = targetText.IndexOf(removeValue);
            int idEnd = targetText.IndexOf("\n", idStart);
            string value = (string)targetText.Clone();
            value = value.Remove(idEnd - 1); // - 1    = remove koma
            value = value.Remove(0, idStart + removeValue.Length);
            value = value.Replace("\"", "");
            return value;
        }

        #endregion

        #region EncodeDecode_base64
        public static string RawBase64Decode(string raw_base64EncodedData)
        {
            raw_base64EncodedData = raw_base64EncodedData.Replace(@"\n", "").Replace("\"", "");
            return Base64Decode(raw_base64EncodedData);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        static public string DecodeFrom64(string encodedData)
        {
            byte[] encodedDataAsBytes = System.Convert.FromBase64String(encodedData);
            string returnValue = System.Text.ASCIIEncoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }
        #endregion

        IEnumerator SendRequest(string url, string headerToken, Action<string> callback, bool isUseError = true)
        {
            if (headerToken == "")
            {
                Debug.LogError("Enter access token for gitHub!");
                yield return null;
            }

            var request = UnityWebRequest.Get(url);

            request.SetRequestHeader("Authorization", " token " + headerToken);

            yield return request.SendWebRequest();

            if (!request.isHttpError && !request.isNetworkError)
                callback?.Invoke(request.downloadHandler.text);
            else
            {
                if (isUseError)
                    Debug.LogErrorFormat("Error request [{0}, {1}]", url, request.error);
                // else
                //     Debug.LogWarningFormat("Warning request [{0}, {1}]", url, request.error);
            }



            request.Dispose();
        }

    }
}

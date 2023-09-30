using System.IO.Compression;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Drawing;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ClothesMenuPatcher{
    public static void Run(GameObject mita){
        try{
            CreateMenuTab();
        } catch (Exception e){
            UnityEngine.Debug.LogError("Error while creating menu tab\n" + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
        }
        if (GlobalGame.clothMita != "original" && GlobalGame.clothMita != "FIIdClSchool")
        {
            GlobalGame.clothMita = "original";
            GlobalGame.clothVariantMita = 0;
            Reflection.FindObjectsOfType<MenuClothes>()[0].indexOpenNow = 0;
        }
        MitaClothesResource clothes = 
            Reflection.FindObjectsOfType<MenuClothes>()[0].resourceClothes.GetComponent<MitaClothesResource>();
        Dictionary<string, DataClothMita> clothesDict = new Dictionary<string, DataClothMita>();

        foreach (var cloth in clothes.clothes)
            clothesDict[cloth.fileSave] = cloth;

        var folderPath = PluginInfo.AssetsFolder;
        var directories = Directory.GetDirectories(folderPath);
        var archives = Directory.GetFiles(folderPath, "*.zip");

        foreach (var directory in directories.Concat(archives)){
            try{
                Dictionary<string, Assimp.Mesh[]> loadedModels = new Dictionary<string, Assimp.Mesh[]>();
	            Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

                string configText;
                if (directory.EndsWith(".zip")){
                    using (FileStream zipToOpen = new FileStream(directory, FileMode.Open))
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read)){
                        ZipArchiveEntry config = archive.GetEntry("config.txt");
                        if (config == null) continue;
                        using (StreamReader reader = new StreamReader(config.Open()))
                            configText = reader.ReadToEnd();

                        foreach (var file in archive.Entries){
                            if (file.Name.EndsWith(".jpg") || file.Name.EndsWith(".jpeg") || file.Name.EndsWith(".png")){
                                var texture = AssetLoader.LoadTexture(Path.GetFileNameWithoutExtension(file.Name), file.Open());
                                loadedTextures[texture.name] = texture;
                            }
                            else if (file.Name.EndsWith(".fbx")){
                                var models = AssetLoader.LoadFBX(file.Open());
                                loadedModels[Path.GetFileNameWithoutExtension(file.Name)] = models;
                            }
                        }
                    }
                }
                else{
                    var configPath = Path.Combine(directory, "config.txt");
                    if (!File.Exists(configPath)) continue;
                    configText = File.ReadAllText(configPath);

                    foreach (var file in AssetLoader.GetAllFilesWithExtensions(directory, "png", "jpg", "jpeg")){
                        var texture = AssetLoader.LoadTexture(file);
                        loadedTextures[texture.name] = texture;
                    }
                    foreach (var file in AssetLoader.GetAllFilesWithExtensions(directory, "fbx")){
                        var models = AssetLoader.LoadFBX(file);
                        loadedModels[Path.GetFileNameWithoutExtension(file)] = models;
                    }
                }

                var configData = AssetLoader.ParseYAML(configText);
                var clothName = configData["name"].ToString();
                DataClothMita cloth = null;
                if (clothesDict.ContainsKey(clothName))
                    cloth = clothesDict[clothName];
                else{
                    cloth = new DataClothMita();
                    cloth.isMod = true;
                    cloth.modNameCloth = clothName;
                    cloth.fileSave = clothName;
                    clothesDict[cloth.fileSave] = cloth;
                    clothes.clothes.Add(cloth);
                }

                foreach (var key in configData.Keys){
                    if (key == "name") continue;
                    if (key == "variants"){
                        var variants = configData[key] as List<object>;
                        var newVariantsList = cloth.variants != null ?
                            new List<DataClothMitaVariant>(cloth.variants) :
                            new List<DataClothMitaVariant>();
                        foreach (var _item in variants){
                            var newVariant = CreateClothVariant();
                            var variantData = _item as Dictionary<string, object>;
                            foreach (var vkey in variantData.Keys){
                                if (vkey.Contains("color")){
                                    UnityEngine.Color color = UnityEngine.Color.white;
                                    ColorUtility.TryParseHtmlString(variantData[vkey].ToString(), out color);
                                    newVariant.GetType().GetProperty(vkey).SetValue(newVariant, color);
                                    continue;
                                }
                                
                                if (vkey.Contains("Textures")){
                                    var texturesList = new List<Texture2D>();
                                    if (variantData[vkey] is string)
                                        variantData[vkey] = new List<object>(new object[]{ variantData[vkey] });
                                    foreach (var configTextures in variantData[vkey] as List<object>){
                                        var textureKey = (configTextures as Dictionary<string, object>).Keys.First();
                                        if (textureKey.StartsWith('*'))
                                            texturesList.Add(GetTextureFromCloth(textureKey, vkey, texturesList.Count));
                                        else
                                            texturesList.Add(loadedTextures[textureKey]);
                                    }
                                    newVariant.GetType().GetProperty(vkey).SetValue(newVariant, (Il2CppReferenceArray<Texture2D>) texturesList.ToArray());
                                }
                            }
                            newVariantsList.Add(newVariant);
                        }
                        cloth.variants = newVariantsList.ToArray();
                        continue;
                    }
                    if (key.Contains("color")){
                        UnityEngine.Color color = UnityEngine.Color.white;
                        ColorUtility.TryParseHtmlString(configData[key].ToString(), out color);
                        cloth.GetType().GetProperty(key).SetValue(cloth, color);
                        continue;
                    }
                    if (key.Contains("Mesh")){
                        Mesh mesh;
                        if (configData[key] == null)
                            mesh = new Mesh();
                        else if (configData[key].ToString().StartsWith('*'))
                            mesh = GetMeshFromCloth(configData[key].ToString(), key);
                        else{
                            string[] parts = configData[key].ToString().Split('#');
                            var sourceMesh = loadedModels[parts[0]].First(mesh => mesh.Name == parts[1]);
                            mesh = AssetLoader.BuildMesh(sourceMesh, new AssetLoader.ArmatureData(mita));
                        }
                        cloth.GetType().GetProperty(key).SetValue(cloth, mesh);
                    }
                }
                Debug.Log("Successfully loaded " + directory);
            }
            catch (Exception e){
                UnityEngine.Debug.LogError("Error while parsing " + directory + "\n" + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        DataClothMitaVariant CreateClothVariant(){
            DataClothMitaVariant result = new DataClothMitaVariant();
            DataClothMitaVariant source = clothesDict["original"].variants[0];
            result.bodyTextures = source.bodyTextures;
            result.bodyMaterials = source.bodyMaterials;
            result.bodyMaterialsDX9 = source.bodyMaterialsDX9;
            result.attributeTextures = source.attributeTextures;
            result.attributeMaterials = source.attributeMaterials;
            result.attributeMaterialsDX9 = source.attributeMaterialsDX9;
            result.pantyhoseTextures = source.pantyhoseTextures;
            result.pantyhoseMaterials = source.pantyhoseMaterials;
            result.pantyhoseMaterialsDX9 = source.pantyhoseMaterialsDX9;
            result.shoesTextures = source.shoesTextures;
            result.shoesMaterials = source.shoesMaterials;
            result.shoesMaterialsDX9 = source.shoesMaterialsDX9;
            result.skirtTextures = source.skirtTextures;
            result.skirtMaterials = source.skirtMaterials;
            result.skirtMaterialsDX9 = source.skirtMaterialsDX9;
            result.sweaterTextures = source.sweaterTextures;
            result.sweaterMaterials = source.sweaterMaterials;
            result.sweaterMaterialsDX9 = source.sweaterMaterialsDX9;
            return result;
        }

        Texture2D GetTextureFromCloth(string name, string slot, int texIndex){
            name = name.Substring(1);
            int variantIndex = 0;
            if (name.Contains('#')){
                variantIndex = int.Parse(name.Split("#")[1]) - 1;
                name = name.Split("#")[0];
            }
            var variant = clothesDict[name].variants[variantIndex];
            return (variant.GetType().GetProperty(slot).GetValue(variant) as Il2CppReferenceArray<Texture2D>)[texIndex];
        }

        Mesh GetMeshFromCloth(string name, string slot){
            name = name.Substring(1);
            var cloth = clothesDict[name];
            return cloth.GetType().GetProperty(slot).GetValue(cloth) as Mesh;
        }
    }

    private static GameObject _addonButtonPrefab;
    private static Dictionary<string, GameObject> addonButtons = new Dictionary<string, GameObject>();

    private static void CreateMenuTab(){
        var clothesMenu = Reflection.FindObjectsOfType<MenuClothes>()[0].gameObject;

        var tabs = new GameObject("Tabs");
        var rect = tabs.AddComponent<RectTransform>();
        rect.SetParent(clothesMenu.transform);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition3D = new Vector3(200, 200, 0);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(500, 64);

        var clothesButton = new GameObject("ClothesTabButton");
        rect = clothesButton.AddComponent<RectTransform>();
        rect.SetParent(tabs.transform);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition3D = new Vector3(0, 0, 0);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(200, 64);
        var text = clothesMenu.transform.Find("Text");
        text.SetParent(rect);
        text.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(0, 0, 0);
        text.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 64);
        UnityEngine.Object.Destroy(text.GetComponent<UI_Colors>());
        var button1 = clothesButton.AddComponent<UnityEngine.UI.Button>();
        button1.targetGraphic = button1.gameObject.AddComponent<UnityEngine.UI.Image>();
        button1.targetGraphic.color = new UnityEngine.Color(1,1,1, 0.005f);

        var addonsButton = GameObject.Instantiate(clothesButton, tabs.transform);
        addonsButton.name = "AddonsTabButton";
        rect = addonsButton.GetComponent<RectTransform>();
        rect.anchoredPosition3D = new Vector3(220, 0, 0);
        rect.sizeDelta = new Vector2(200, 64);
        rect.localScale = Vector3.one;
        text = rect.Find("Text");
        UnityEngine.Object.Destroy(text.GetComponent<Localization_UIText>());
        text.GetComponent<UnityEngine.UI.Text>().text = "Addons";
        var button2 = addonsButton.GetComponent<UnityEngine.UI.Button>();

        var addonsList = clothesMenu.transform.parent.Find("Location OptionsChange").GetComponentInChildren<ScrollRect>().gameObject;
        addonsList = GameObject.Instantiate(addonsList, clothesMenu.transform);
        addonsList.gameObject.name = "AddonsList";
        rect = addonsList.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition3D = new Vector3(0, 125, 0);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(530, 300);
        var content = addonsList.GetComponentInChildren<MenuScrolac>().transform;
        UnityEngine.Object.Destroy(content.GetComponent<MenuScrolac>());
        UnityEngine.Object.Destroy(content.Find("Change").gameObject);
        UnityEngine.Object.Destroy(content.Find("ChangeTarget").gameObject);
        _addonButtonPrefab = content.GetChild(2).gameObject;
        _addonButtonPrefab.GetComponent<RectTransform>().Find("Text").GetComponent<UnityEngine.UI.Text>().text = "Default";
        _addonButtonPrefab.SetActive(false);
        CreateAddonButtons();
        //var button3 = _addonButtonPrefab.AddComponent<UnityEngine.UI.Button>();
        //button3.onClick.AddListener((UnityAction)LogOnClick);


        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        addonsList.SetActive(false);
        
        void ShowClothesTab(){
            button1.interactable = false;
            button2.interactable = true;
            for (int i = 0; i < clothesMenu.transform.childCount; i++){
                var cloth = clothesMenu.transform.GetChild(i);
                if (cloth.name.StartsWith("CaseCloth"))
                    cloth.gameObject.SetActive(true);
            }
            addonsList.SetActive(false);
        }

        void ShowAddonsTab(){
            button1.interactable = true;
            button2.interactable = false;
            for (int i = 0; i < clothesMenu.transform.childCount; i++){
                var cloth = clothesMenu.transform.GetChild(i);
                if (cloth.name.StartsWith("CaseCloth"))
                    cloth.gameObject.SetActive(false);
            }
            addonsList.SetActive(true);
        }

        button1.onClick.AddListener((UnityAction) ShowClothesTab);
        button2.onClick.AddListener((UnityAction) ShowAddonsTab);

        var uiColors = tabs.AddComponent<UI_Colors>();
        uiColors.ui_images = new();
        uiColors.ui_imagesColor = new();
        uiColors.ui_text = new();
        uiColors.ui_textColor = new();

        var ml = clothesMenu.GetComponent<MenuLocation>();
        ml.objects.Clear();
        ml.objects.Add(tabs.GetComponent<RectTransform>());
    }
    static void LogOnClick(string name){
        Debug.Log("clicked: " +  name);
        bool active = !addonButtons[name].GetComponent<RectTransform>().Find("Text").GetComponent<Text>().text.EndsWith("(*)");
        addonButtons[name].GetComponent<RectTransform>().Find("Text").GetComponent<Text>().text = name + ((!active) ? "" : "(*)");
        string filePath = PluginInfo.AssetsFolder + "/addons_config.txt";
        try{
            using (StreamReader sr = new StreamReader(filePath)){
                string line, currentName = "";
                while ((line = sr.ReadLine()) != null){
                    // Ignore empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                        continue;
                    if (line.StartsWith("*")){
                        currentName = line.Substring(1);
                        continue;
                    }
                    if (currentName == name){
                        if (!active){
                            if (!line.StartsWith("-")){
                                string[] parts1 = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                Plugin.assetCommands.RemoveAll(command =>
                                    command.name == parts1[0] && Enumerable.SequenceEqual(command.args, parts1.Skip(1).ToArray()));
                                continue;
                            }
                            line = line.Substring(1);
                        } else if (line.StartsWith("-")){
                            string[] parts1 = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            Plugin.assetCommands.RemoveAll(command =>
                                command.name == parts1[0] && Enumerable.SequenceEqual(command.args, parts1.Skip(1).ToArray()));
                            continue;
                        }
                        // Split line on commands with arguments list
                        string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        Plugin.assetCommands.RemoveAll(command =>
                            command.name == parts[0] && Enumerable.SequenceEqual(command.args,parts.Skip(1).ToArray()));
                        Plugin.assetCommands.Add((parts[0], parts.Skip(1).ToArray()));
                    }
                }
                Plugin.FindMita();
            }
        }
        catch (Exception e){
            Console.WriteLine("Error: " + e.Message);
        }
    }
    static void CreateAddonButtons() {
        string filePath = PluginInfo.AssetsFolder + "/addons_config.txt";
        try{
            using (StreamReader sr = new StreamReader(filePath)){
                string line;
                while ((line = sr.ReadLine()) != null){
                    if (!line.StartsWith("*"))
                        continue;
                    addonButtons[line.Substring(1)] = GameObject.Instantiate(_addonButtonPrefab, _addonButtonPrefab.transform.parent);
                    addonButtons[line.Substring(1)].SetActive(true);
                    var button = addonButtons[line.Substring(1)].AddComponent<UnityEngine.UI.Button>();

                    string line1 = line.Substring(1);
                    button.onClick.AddListener((UnityAction)(() => { LogOnClick(line1); }));

                    var rect = addonButtons[line.Substring(1)].GetComponent<RectTransform>();
                    rect.anchoredPosition3D += new Vector3(0, 40 * (addonButtons.Count - 1), 0);
                    addonButtons[line.Substring(1)].GetComponent<RectTransform>().Find("Text").GetComponent<UnityEngine.UI.Text>().text = line.Substring(1);
                }
            }
        }
        catch (Exception e){
            Console.WriteLine("Error: " + e.Message);
        }
    }
}

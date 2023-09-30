using System.IO.Compression;
using System.Reflection;
using Coffee.UIEffects;
using Dummiesman;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LibCpp2IL;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
public class Plugin : MonoBehaviour{
	private static string currentSceneName;

	private void Start(){
		ReadAssetsConfig();
		LoadAssetsForPatch();
    }

	private static Dictionary<string, Assimp.Mesh[]> loadedModels;
	private static Dictionary<string, Texture2D> loadedTextures;
	private static Dictionary<string, AudioClip> loadedAudio;
	public static List<(string name, string[] args)> assetCommands;

    private static GameObject mita = null, mitaTamagochi = null, mitaCards = null;

    void ReadAssetsConfig(){
		string filePath = PluginInfo.AssetsFolder + "/assets_config.txt";
		assetCommands = new();
        try{
            using (StreamReader sr = new StreamReader(filePath)){
                string line;
                while ((line = sr.ReadLine()) != null){
                    // Ignore empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                        continue;
                    
                    // Split line on commands with arguments list
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    assetCommands.Add((parts[0], parts.Skip(1).ToArray()));
                }
            }
        }
        catch (Exception e){
            Console.WriteLine("Error: " + e.Message);
        }
	}

	void LoadAssetsForPatch(){
		if (loadedModels != null) return;

		loadedModels = new();
		loadedTextures = new();
		loadedAudio = new();
		
		// audio
		string[] files = AssetLoader.GetAllFilesWithExtensions(PluginInfo.AssetsFolder, "ogg");
		foreach (var file in files){
			var audioFile = AssetLoader.LoadAudio(file);
			var filename = Path.GetFileNameWithoutExtension(file);
			loadedAudio.Add(filename, audioFile);
			PluginInfo.Instance.Logger.LogInfo($"Loaded audio from file: '{filename}'");
		}

		// meshes
		files = AssetLoader.GetAllFilesWithExtensions(PluginInfo.AssetsFolder, "fbx");
		foreach (var file in files){
			var meshes = AssetLoader.LoadFBX(file);
			var filename = Path.GetFileNameWithoutExtension(file);
			loadedModels.Add(filename, meshes);
			PluginInfo.Instance.Logger.LogInfo($"Loaded meshes from file: '{filename}', {meshes.Length} meshes");
		}
		
		/*files = 
    		Directory.GetFiles(PluginInfo.AssetsFolder, "*.obj", SearchOption.AllDirectories);
		foreach (var file in files)
		{
			var loader = new OBJLoader();
			loader.Load(file);
			var meshes = loader.Builders.Values.Select(builder => builder.Build()).ToArray();
			var filename = Path.GetFileNameWithoutExtension(file);
			loadedModels.Add(filename, meshes);
			PluginInfo.Instance.Logger.LogInfo($"Loaded meshes from file: '{filename}', {loader.Builders.Count} meshes");
		}*/

		// textures
		files = AssetLoader.GetAllFilesWithExtensions(PluginInfo.AssetsFolder, "png", "jpg", "jpeg");

		foreach (var file in files){
			Texture2D texture = AssetLoader.LoadTexture(file);
			if (texture != null){
				var filename = Path.GetFileNameWithoutExtension(file);
				loadedTextures.Add(filename, texture);
				PluginInfo.Instance.Logger.LogInfo($"Loaded texture from file: '{filename}' " + loadedTextures[filename]);
			}
		}
	}

    public static void FindMita(){
		var animators = Reflection.FindObjectsOfType<Animator>(true);
        GameObject mitaAnimator = null, mitaTamagochiAnimator = null, mitaCardsAnimator = null;
        foreach (var obj in animators){
            var anim = obj.Cast<Animator>();
            if (anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.name == "Mita")
                mitaAnimator = anim.gameObject;
            if (anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.name == "MitaGame")
                mitaCardsAnimator = anim.gameObject;
        }

        if (mitaAnimator == null){
            Debug.Log("Found no animators for Mita to patch");
            mita = null;
        }
        else{
            mita = mitaAnimator;
            //Debug.Log("Mita name: " + mita.name);
            Debug.Log("Patching Mita");
            PatchMita(mita);
            Debug.Log("Patching Mita completed");
        }
        if (mitaCardsAnimator == null){
            Debug.Log("Found no animators for MitaCards to patch");
            mitaCards = null;
        }
        else{
            mitaCards = mitaCardsAnimator;
            //Debug.Log("Mita name: " + mitaCards.name);
            Debug.Log("Patching MitaCards");
            PatchMita(mitaCards);
            Debug.Log("Patching MitaCards completed");
        }
        if (mitaTamagochiAnimator == null){
            //Debug.Log("Found no animators for MitaTamagochi to patch");
            mitaTamagochi = null;
        }
        else{
            mitaTamagochi = mitaTamagochiAnimator;
            //Debug.Log("Mita name: " + mitaTamagochi.name);
            Debug.Log("Patching MitaTamagochi");
            PatchMita(mitaTamagochi);
            Debug.Log("Patching MitaTamagochi completed");
        }
    }

	public static void PatchMita(GameObject mita){
        List<(string name, string[] args)> NewAssetCommands = new List<(string name, string[] args)>(assetCommands);
		if (currentSceneName != "SceneMenu"){
			var folderPath = PluginInfo.AssetsFolder;
			var directories = Directory.GetDirectories(folderPath);
			var archives = Directory.GetFiles(folderPath, "*.zip");

			foreach (var directory in directories.Concat(archives))  {
				try{
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
					if (clothName != GlobalGame.clothMita)
						continue;
					foreach (var key in configData.Keys){
						if (key == "name") continue;
						if (key == "variants"){
							var variants = configData[key] as List<object>;
							var _item = variants[GlobalGame.clothVariantMita];
							var variantData = _item as Dictionary<string, object>;
							foreach (var vkey in variantData.Keys){
								if (vkey.Contains("color"))
									continue;
								if (vkey.Contains("Textures")){
									if (variantData[vkey] is string)
										variantData[vkey] = new List<object>(new object[] { variantData[vkey] });
									foreach (var configTextures in variantData[vkey] as List<object>){
										var textureKey = (configTextures as Dictionary<string, object>).Keys.First();
										if (textureKey.StartsWith('*'))
											continue;
										string elem = String.Concat(vkey);
										elem = elem.Remove(elem.Length - 8);
										elem = char.ToUpper(elem.First()) + String.Concat(elem.Skip(1));{
											string line = "replace_tex Mita " + elem + " " + textureKey;
											string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                            NewAssetCommands.Add((parts[0], parts.Skip(1).ToArray()));
                                            Debug.Log("added command=" + line);
										}
									}
								}
							}
							continue;
						}
						if (key.Contains("color"))
							continue;
						if (key.Contains("Mesh")){
							string elem;
							if (configData[key] != null && configData[key].ToString().StartsWith('*'))
								continue;
							elem = String.Concat(key);
							elem = elem.Remove(elem.Length - 4);
							elem = char.ToUpper(elem.First()) + String.Concat(elem.Skip(1));{
								string line;
								if (configData[key] == null)
									line = "replace_mesh Mita " + elem + " null null";
								else{
                                    string[] parts1 = configData[key].ToString().Split('#');
                                    line = "replace_mesh Mita " + elem + " " + parts1[0] + " " + parts1[1];
								}
								string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                NewAssetCommands.Add((parts[0], parts.Skip(1).ToArray()));
                                Debug.Log("added command=" + line);
							}

						}
					}
					Debug.Log("Successfully loaded " + directory);
				}
				catch (Exception e)
				{
					UnityEngine.Debug.LogError("Error while parsing " + directory + "\n" + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
				}
			}
		}
        var renderersList = Reflection.GetComponentsInChildren<SkinnedMeshRenderer>(mita, true);
        var staticRenderersList = Reflection.GetComponentsInChildren<MeshRenderer>(mita, true);
        var renderers = new Dictionary<string, Renderer>();
        var staticRenderers = new Dictionary<string, Renderer>();
        foreach (var renderer in renderersList)
            renderers[renderer.name.Trim()] = renderer;
        foreach (var renderer in staticRenderersList)
            staticRenderers[renderer.name.Trim()] = renderer;

        foreach (var command in NewAssetCommands){
            if (command.args.Length == 0 || command.args[0] != "Mita")
                continue;
            try{
                if (command.name == "remove"){
                    if (renderers.ContainsKey(command.args[1])){
                        renderers[command.args[1]].gameObject.SetActive(false);
                        Debug.Log("Removed skinned appendix " + command.args[1]);
                    }
                    else if (staticRenderers.ContainsKey(command.args[1])){
                        staticRenderers[command.args[1]].gameObject.SetActive(false);
                        Debug.Log("Removed static appendix " + command.args[1]);
                    }
                    else
                        Debug.Log(command.args[1] + " not found");
                }
                else if (command.name == "recover"){
                    if (renderers.ContainsKey(command.args[1])){
                        renderers[command.args[1]].gameObject.SetActive(true);
                        Debug.Log("Recovered skinned appendix " + command.args[1]);
                    }
                    else if (staticRenderers.ContainsKey(command.args[1])){
                        staticRenderers[command.args[1]].gameObject.SetActive(true);
                        Debug.Log("Recovered static appendix " + command.args[1]);
                    }
                    else
                        Debug.Log(command.args[1] + " not found");
                }
                else if (command.name == "replace_tex"){
                    if (renderers.ContainsKey(command.args[1])){
                        renderers[command.args[1]].material.mainTexture = loadedTextures[command.args[2]];
                        if (command.args.Length >= 4 && ColorUtility.TryParseHtmlString(command.args[3], out Color color))
                            renderers[command.args[1]].material.color = color;
                        Debug.Log("Replaced texture of skinned " + command.args[1]);
                    }
                    else if (staticRenderers.ContainsKey(command.args[1])){
                        staticRenderers[command.args[1]].material.mainTexture = loadedTextures[command.args[2]];
                        if (command.args.Length >= 4 && ColorUtility.TryParseHtmlString(command.args[3], out Color color))
                            staticRenderers[command.args[1]].material.color = color;
                        Debug.Log("Replaced texture of static " + command.args[1]);
                    }
                    else
                        Debug.Log(command.args[1] + " not found");
                }
                else if (command.name == "replace_mesh"){
					Assimp.Mesh meshData = null;
                    if (command.args[2] != "null"){
						meshData = loadedModels[command.args[2]].First(mesh =>
							mesh.Name == (command.args.Length >= 4 ? command.args[3] : command.args[2]));
					}
                    if (renderers.ContainsKey(command.args[1])){
                        if (renderers[command.args[1]] is SkinnedMeshRenderer sk){
							if (command.args[2] == "null" && command.args[3] == "null")
								sk.sharedMesh = new Mesh();
							else
								sk.sharedMesh = AssetLoader.BuildMesh(meshData, new AssetLoader.ArmatureData(sk));
                            Debug.Log("Replaced mesh of skinned " + command.args[1]);
                        }
                        else{
                            if (command.args[2] == "null" && command.args[3] == "null")
                                renderers[command.args[1]].GetComponent<MeshFilter>().mesh = new Mesh();
                            else
                                renderers[command.args[1]].GetComponent<MeshFilter>().mesh = AssetLoader.BuildMesh(meshData);
                            Debug.Log("Replaced mesh of skinned(static method) " + command.args[1]);
                        }
                    }
                    else if (staticRenderers.ContainsKey(command.args[1])){
                        if (staticRenderers[command.args[1]] is SkinnedMeshRenderer sk){
                            if (command.args[2] == "null" && command.args[3] == "null")
                                sk.sharedMesh = new Mesh();
                            else
                                sk.sharedMesh = AssetLoader.BuildMesh(meshData, new AssetLoader.ArmatureData(sk));
                            Debug.Log("Replaced mesh of static(skinned method) " + command.args[1]);
                        }
                        else{
                            if (command.args[2] == "null" && command.args[3] == "null")
                                staticRenderers[command.args[1]].GetComponent<MeshFilter>().mesh = new Mesh();
                            else
                                staticRenderers[command.args[1]].GetComponent<MeshFilter>().mesh = AssetLoader.BuildMesh(meshData);
                            Debug.Log("Replaced mesh of static " + command.args[1]);
                        }
                    }
                    else
                        Debug.Log(command.args[1] + " not found");
                }
                else if (command.name == "create_skinned_appendix"){
                    var parent = renderers[command.args[2]];
                    if (renderers.ContainsKey(command.args[1])){
                        if (renderers[command.args[1]].gameObject.active == false)
                            renderers[command.args[1]].gameObject.SetActive(true);
                        continue;
                    }
                    SkinnedMeshRenderer obj = UnityEngine.Object.Instantiate(
                        parent,
                        parent.transform.position,
                        parent.transform.rotation,
                        parent.transform.parent).Cast<SkinnedMeshRenderer>();
                    obj.name = command.args[1];
                    obj.material = new Material(parent.material);
                    obj.gameObject.SetActive(true);
                    renderers[command.args[1]] = obj;
                    Debug.Log("Added skinned appendix " + obj.name);
                }
                else if (command.name == "create_static_appendix"){
                    if (staticRenderers.ContainsKey(command.args[1])){
                        if (staticRenderers[command.args[1]].gameObject.active == false)
                            staticRenderers[command.args[1]].gameObject.SetActive(true);
                        continue;
                    }
                    MeshRenderer obj = new GameObject().AddComponent<MeshRenderer>();
                    obj.name = command.args[1];
                    obj.material = new Material(mita.transform.Find("Attribute").GetComponent<SkinnedMeshRenderer>().material);
                    obj.gameObject.AddComponent<MeshFilter>();
                    obj.transform.parent = RecursiveFindChild(mita.transform.Find("Armature"), command.args[2]);
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localScale = Vector3.one;
                    obj.transform.localEulerAngles = new Vector3(-90f, 0, 0);
                    obj.gameObject.SetActive(true);
                    renderers[command.args[1]] = obj;
                    Debug.Log("Added static appendix " + obj.name);
                }
            }
            catch (Exception e){
                Debug.LogError("Error while processing command " + command.name + " " + string.Join(' ', command.args) + "\n" + e.ToString());
            }
        }
    }

	static Transform RecursiveFindChild(Transform parent, string childName){
		for (int i = 0; i < parent.childCount; i++){
			var child = parent.GetChild(i);
			if(child.name == childName) return child;
			else {
				Transform found = RecursiveFindChild(child, childName);
				if (found != null) return found;
			}
		}
		return null;
	}

	VideoPlayer currentVideoPlayer = null;
	Action onCurrentVideoEnded = null;
	Image logo = null;

	void PatchMenuScene(){
		Debug.Log("Patching game scene");
		var command = assetCommands.FirstOrDefault<(string? name, string[]? args)>(item => item.name == "menu_logo", (null, null));
		if (command.name != null){
			var animators = Reflection.FindObjectsOfType<Animator>(true);
			GameObject gameName = null;
			foreach (var obj in animators) 
				if (obj.name == "NameGame"){ 
					gameName = obj.Cast<Animator>().gameObject; 
					Destroy(obj);
					break;
				}

			for (int i = 0; i < gameName.transform.childCount; i++){
				var tr = gameName.transform.GetChild(i);
				if (tr.name == "Background"){
					Texture2D tex = loadedTextures[command.args[0]];
					Destroy(Reflection.GetComponent<UIShiny>(tr));

					logo = Reflection.GetComponent<Image>(tr);
					logo.preserveAspect = true;
					logo.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one / 2.0f);
					Reflection.GetComponent<RectTransform>(tr).sizeDelta = new Vector2(1600, 400);
				}
				else
					tr.gameObject.SetActive(false);
			}
		}
		
		command = assetCommands.FirstOrDefault<(string? name, string[]? args)>(item => item.name == "menu_music", (null, null));
		if (command.name != null){
			var musicSources = Reflection.FindObjectsOfType<AudioSource>(true);
			foreach (var source in musicSources)
				if (source.name == "Music"){
					source.clip = loadedAudio[command.args[0]];
					source.volume = 1;
					source.Play();
					break;
				}
		}

		ClothesMenuPatcher.Run(mita);

		Debug.Log("Patching completed");
	}
	void Update(){
		if (currentSceneName != SceneManager.GetActiveScene().name){
			currentSceneName = SceneManager.GetActiveScene().name;
			OnSceneChanged();
		}
        if (currentVideoPlayer != null){
			if ((ulong) currentVideoPlayer.frame + 5 > currentVideoPlayer.frameCount){
				Debug.Log("Video ended");
				Destroy(currentVideoPlayer.transform.parent.gameObject);
				currentVideoPlayer = null;
				onCurrentVideoEnded?.Invoke();
				onCurrentVideoEnded = null;
			}
		}
		if (currentSceneName == "SceneMenu"){
			if (logo != null)
				logo.color = Color.white;
		}
	}
	void OnSceneChanged(){
		try{
			Debug.Log("Scene changed to " + currentSceneName);
            FindMita();
			if (currentSceneName == "SceneMenu")
				PatchMenuScene();
        } catch (Exception e){
			Debug.Log(e.ToString());
			enabled = false;
		}
	}

	void PlayFullscreenVideo(Action onVideoEnd){
		var rootGO = SceneManager.GetActiveScene().GetRootGameObjects();
		foreach (var go in rootGO)
			if (go != gameObject) go.gameObject.SetActive(false);

		Camera camera = new GameObject("VideoCamera").AddComponent<Camera>();
		VideoPlayer videoPlayer = new GameObject("VideoPlayer").AddComponent<VideoPlayer>();
		videoPlayer.transform.parent = camera.transform;
		camera.backgroundColor = Color.black;
		camera.gameObject.AddComponent<AudioListener>();
		videoPlayer.playOnAwake = false;
		videoPlayer.targetCamera = camera;
		videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
		videoPlayer.url = "file://" + PluginInfo.AssetsFolder + "/intro.mp4";
		videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
		videoPlayer.SetTargetAudioSource(0, videoPlayer.gameObject.AddComponent<AudioSource>());
		
		currentVideoPlayer = videoPlayer;
		onCurrentVideoEnded = onVideoEnd;

		videoPlayer.Play();
		Debug.Log("Video started");
	}
}

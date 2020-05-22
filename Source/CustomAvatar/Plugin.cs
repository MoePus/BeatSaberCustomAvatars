using CustomAvatar.StereoRendering;
using IPA;
using System;
using System.Reflection;
using BeatSaberMarkupLanguage.MenuButtons;
using CustomAvatar.Lighting;
using CustomAvatar.UI;
using CustomAvatar.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using Logger = IPA.Logging.Logger;
using Object = UnityEngine.Object;
using BeatSaberMarkupLanguage;
using CustomAvatar.Logging;
using HarmonyLib;
using ILogger = CustomAvatar.Logging.ILogger;

namespace CustomAvatar
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    internal class Plugin
    {
        private PlayerAvatarManager _avatarManager;
        private GameScenesManager _scenesManager;
        private AvatarMenuFlowCoordinator _flowCoordinator;
        private Settings _settings;
        private SettingsManager _settingsManager;
        private MirrorHelper _mirrorHelper;

        private SceneContext _sceneContext;
        private GameObject _mirrorContainer;
        private MenuLightingController _menuLightingRig;
        private KeyboardInputHandler _keyboardInputHandler;
        private GameplayLightingController _gameplayLightingController;
        
        public event Action<ScenesTransitionSetupDataSO, DiContainer> sceneTransitionDidFinish;

        public static Plugin instance { get; private set; }

        private ILogger _logger;
        private static Logger _ipaLogger;

        [Init]
        public Plugin(Logger logger)
        {
            instance = this;
            _ipaLogger = logger;

            // can't inject at this point so just create it
            _logger = new IPALogger<Plugin>(logger);

            try
            {
                BeatSaberEvents.ApplyPatches();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to apply patches");
                _logger.Error(ex);
            }

            PatchAppCoreInstallerInstallBindings();
        }

        [Inject]
        private void Inject(PlayerAvatarManager playerAvatarManager, GameScenesManager gameScenesManager, AvatarMenuFlowCoordinator avatarMenuFlowCoordinator, Settings settings, SettingsManager settingsManager, MirrorHelper mirrorHelper)
        {
            _avatarManager = playerAvatarManager;
            _scenesManager = gameScenesManager;
            _flowCoordinator = avatarMenuFlowCoordinator;
            _settings = settings;
            _settingsManager = settingsManager;
            _mirrorHelper = mirrorHelper;
        }

        private void PatchAppCoreInstallerInstallBindings()
        {
            Harmony harmony = new Harmony("com.nicoco007.beatsabercustomavatars");

            var methodToPatch = typeof(AppCoreInstaller).GetMethod("InstallBindings", BindingFlags.Public | BindingFlags.Instance);
            var patch = new HarmonyMethod(GetType().GetMethod(nameof(InstallBindings), BindingFlags.Static | BindingFlags.NonPublic));

            harmony.Patch(methodToPatch, null, patch);

            methodToPatch = typeof(MirrorRendererSO).GetMethod("CreateOrUpdateMirrorCamera", BindingFlags.NonPublic | BindingFlags.Instance);
            patch = new HarmonyMethod(GetType().GetMethod(nameof(PatchMirrorLayers), BindingFlags.Static | BindingFlags.NonPublic));

            harmony.Patch(methodToPatch, null, patch);
        }

        private static void InstallBindings(AppCoreInstaller __instance)
        {
            DiContainer container = new Traverse(__instance).Property<DiContainer>("Container").Value;

            container.Install<CustomAvatarsInstaller>(new object[] { _ipaLogger });
            container.Install<UIInstaller>();
        }

        private static void PatchMirrorLayers(MirrorRendererSO __instance)
        {
            Camera mirrorCamera = new Traverse(__instance).Field<Camera>("_mirrorCamera").Value;

            mirrorCamera.cullingMask |= (1 << AvatarLayers.kOnlyInThirdPerson) | (1 << AvatarLayers.kOnlyInFirstPerson) | (1 << AvatarLayers.kAlwaysVisible);
        }

        [OnStart]
        public void OnStart()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        [OnExit]
        public void OnExit()
        {
            if (_scenesManager != null)
            {
                _scenesManager.transitionDidFinishEvent -= sceneTransitionDidFinish;
                _scenesManager.transitionDidFinishEvent -= SceneTransitionDidFinish;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            _settingsManager.Save(_settings);
        }

        private void OnSceneLoaded(Scene newScene, LoadSceneMode mode)
        {
            if (newScene.name == "PCInit")
            {
                // Beat Saber has one instance of SceneContext in the PCInit scene
                _sceneContext = Object.FindObjectOfType<SceneContext>();

                // handle scene context already being installed when the game is first loaded
                if (_sceneContext.HasInstalled)
                {
                    OnSceneContextPostInstall();
                }
                else
                {
                    _sceneContext.OnPostInstall.AddListener(OnSceneContextPostInstall);
                }
            }

            if (newScene.name == "MenuCore")
            {
                try
                {
                    MenuButtons.instance.RegisterButton(new MenuButton("Avatars", () =>
                    {
                        BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(_flowCoordinator, null, true);
                    }));
                }
                catch (Exception)
                {
                    _logger.Warning("Failed to add menu button, spawning mirror instead");

                    _mirrorContainer = new GameObject();
                    Object.DontDestroyOnLoad(_mirrorContainer);
                    Vector2 mirrorSize = _settings.mirror.size;
                    _mirrorHelper.CreateMirror(new Vector3(0, mirrorSize.y / 2, -1.5f), Quaternion.Euler(-90f, 180f, 0), mirrorSize, _mirrorContainer.transform);
                }
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "PCInit")
            {
                Object.Destroy(_keyboardInputHandler);
                Object.Destroy(_menuLightingRig);
                Object.Destroy(_mirrorContainer);
                Object.Destroy(_avatarManager.currentlySpawnedAvatar);
            }
        }

        private void OnSceneContextPostInstall()
        {
            _sceneContext.Container.Inject(this);

            _scenesManager.transitionDidFinishEvent += sceneTransitionDidFinish;
            _scenesManager.transitionDidFinishEvent += SceneTransitionDidFinish;

            _avatarManager.LoadAvatarFromSettingsAsync();

            _keyboardInputHandler = _sceneContext.Container.InstantiateComponentOnNewGameObject<KeyboardInputHandler>(nameof(KeyboardInputHandler));

            if (_settings.lighting.castShadows)
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = _settings.lighting.shadowResolution;
                QualitySettings.shadowDistance = 10;
            }
        }

        private void SceneTransitionDidFinish(ScenesTransitionSetupDataSO setupData, DiContainer container)
        {
            foreach (Camera camera in Camera.allCameras)
            {
                var detector = camera.gameObject.GetComponent<VRRenderEventDetector>();

                if (detector == null)
                {
                    _sceneContext.Container.InstantiateComponent<VRRenderEventDetector>(camera.gameObject);
                    _logger.Info($"Added {nameof(VRRenderEventDetector)} to {camera}");
                }
            }
            
            Camera mainCamera = Camera.main;

            if (mainCamera)
            {
                SetCameraCullingMask(mainCamera);
                mainCamera.nearClipPlane = _settings.cameraNearClipPlane;
            }
            else
            {
                _logger.Error("Could not find main camera!");
            }

            if (_settings.lighting.enabled)
            {
                if (_scenesManager.IsSceneInStack("GameplayCore") && _settings.lighting.enableDynamicLighting)
                {
                    Object.Destroy(_menuLightingRig);

                    if (!_gameplayLightingController)
                    {
                        _gameplayLightingController = container.InstantiateComponentOnNewGameObject<GameplayLightingController>();
                    }
                }
                else
                {
                    Object.Destroy(_gameplayLightingController);

                    if (!_menuLightingRig)
                    {
                        _menuLightingRig = _sceneContext.Container.InstantiateComponentOnNewGameObject<MenuLightingController>(nameof(MenuLightingController));
                        Object.DontDestroyOnLoad(_menuLightingRig);
                    }
                }
            }
        }

        private void SetCameraCullingMask(Camera camera)
        {
            _logger.Trace($"Setting up avatar culling mask on '{camera.name}'");

            camera.cullingMask &= ~(1 << AvatarLayers.kOnlyInThirdPerson);
            camera.cullingMask |= (1 << AvatarLayers.kAlwaysVisible);
            camera.cullingMask |= (1 << AvatarLayers.kOnlyInFirstPerson);
        }
    }
}

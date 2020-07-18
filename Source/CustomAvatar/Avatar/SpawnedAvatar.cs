extern alias BeatSaberFinalIK;

using System;
using System.Reflection;
using AvatarScriptPack;
using CustomAvatar.Logging;
using CustomAvatar.Tracking;
using UnityEngine;
using Zenject;
using ILogger = CustomAvatar.Logging.ILogger;

namespace CustomAvatar.Avatar
{
	public class SpawnedAvatar : MonoBehaviour
	{
		public LoadedAvatar avatar { get; private set; }
		public IAvatarInput input { get; private set; }

        public float verticalPosition
        {
            get => transform.position.y - _initialPosition.y;
            set => transform.position = _initialPosition + value * Vector3.up;
        }

        public float scale
        {
            get => transform.localScale.y / _initialScale.y;
            set
            {
                transform.localScale = _initialScale * value;
                _logger.Info("Avatar resized with scale: " + value);
            }
        }

        public float eyeHeight { get; private set; }
        public float armSpan { get; private set; }
        public bool supportsFingerTracking { get; private set; }
        public bool supportsFullBodyTracking { get; private set; }

        public Transform head { get; private set; }
        public Transform body { get; private set; }
        public Transform leftHand { get; private set; }
        public Transform rightHand { get; private set; }
        public Transform leftLeg { get; private set; }
        public Transform rightLeg { get; private set; }
        public Transform pelvis { get; private set; }

		public AvatarTracking tracking { get; private set; }
        public AvatarIK ik { get; private set; }
        public AvatarSRTracking sr { get; private set; }
        public AvatarFingerTracking fingerTracking { get; private set; }

        private ILogger _logger;
        private GameScenesManager _gameScenesManager;

        private FirstPersonExclusion[] _firstPersonExclusions;
        private Renderer[] _renderers;
        private EventManager _eventManager;
        private AvatarGameplayEventsPlayer _gameplayEventsPlayer;

        private bool _isCalibrationModeEnabled;

        private Vector3 _initialPosition;
        private Vector3 _initialScale;

        public void EnableCalibrationMode()
        {
            if (_isCalibrationModeEnabled || !ik) return;

            _isCalibrationModeEnabled = true;

            tracking.isCalibrationModeEnabled = true;
            ik.EnableCalibrationMode();
        }

        public void DisableCalibrationMode()
        {
            if (!_isCalibrationModeEnabled || !ik) return;

            tracking.isCalibrationModeEnabled = false;
            ik.DisableCalibrationMode();

            _isCalibrationModeEnabled = false;
        }

        public void UpdateFirstPersonVisibility(FirstPersonVisibility visibility)
        {
            switch (visibility)
            {
                case FirstPersonVisibility.Visible:
                    SetChildrenToLayer(AvatarLayers.kAlwaysVisible);
                    break;

                case FirstPersonVisibility.VisibleWithExclusionsApplied:
                    SetChildrenToLayer(AvatarLayers.kAlwaysVisible);
                    ApplyFirstPersonExclusions();
                    break;

                case FirstPersonVisibility.None:
                    SetChildrenToLayer(AvatarLayers.kOnlyInThirdPerson);
                    break;
            }
        }

        #region Behaviour Lifecycle

        private void Awake()
        {
            _initialPosition = transform.localPosition;
            _initialScale = transform.localScale;
        }
        
        [Inject]
        private void Inject(DiContainer container, ILoggerProvider loggerProvider, LoadedAvatar loadedAvatar, IAvatarInput avatarInput, GameScenesManager gameScenesManager)
        {
            avatar = loadedAvatar ?? throw new ArgumentNullException(nameof(loadedAvatar));
            input = avatarInput ?? throw new ArgumentNullException(nameof(avatarInput));

            container.Bind<SpawnedAvatar>().FromInstance(this);

            _logger = loggerProvider.CreateLogger<SpawnedAvatar>(loadedAvatar.descriptor.name);
            _gameScenesManager = gameScenesManager;

            _eventManager = GetComponent<EventManager>();
            _firstPersonExclusions = GetComponentsInChildren<FirstPersonExclusion>();
            _renderers = GetComponentsInChildren<Renderer>();

            var poseManager = GetComponentInChildren<PoseManager>();

            supportsFingerTracking = poseManager && poseManager.isValid;

            VRIKManager vrikManager = GetComponentInChildren<VRIKManager>();

            #pragma warning disable CS0618
            IKManager ikManager = GetComponentInChildren<IKManager>();
            #pragma warning restore CS0618

            // migrate IKManager/IKManagerAdvanced to VRIKManager
            if (ikManager)
            {
                if (!vrikManager) vrikManager = container.InstantiateComponent<VRIKManager>(gameObject);
                
                _logger.Warning("IKManager and IKManagerAdvanced are deprecated; please migrate to VRIKManager");

                ApplyIKManagerFields(vrikManager, ikManager);
                Destroy(ikManager);
            }

            head      = transform.Find("Head");
            body      = transform.Find("Body");
            leftHand  = transform.Find("LeftHand");
            rightHand = transform.Find("RightHand");
            leftLeg   = transform.Find("LeftLeg");
            rightLeg  = transform.Find("RightLeg");
            pelvis    = transform.Find("Pelvis");

            supportsFullBodyTracking = pelvis || leftLeg || rightLeg;

            if (vrikManager)
            {
                if (!vrikManager.areReferencesFilled)
                {
                    vrikManager.AutoDetectReferences();
                }

                FixTrackingReferences(vrikManager);
            }

            eyeHeight = GetEyeHeight();
            armSpan = GetArmSpan();

            tracking = container.InstantiateComponent<AvatarTracking>(gameObject);

            if (avatar.isIKAvatar)
            {
                ik = container.InstantiateComponent<AvatarIK>(gameObject);
                sr = container.InstantiateComponent<AvatarSRTracking>(gameObject);
            }

            if (supportsFingerTracking)
            {
                fingerTracking = container.InstantiateComponent<AvatarFingerTracking>(gameObject);
            }

            if (_initialPosition.magnitude > 0.0f)
            {
                _logger.Warning("Avatar root position is not at origin; resizing by height and floor adjust may not work properly.");
            }

            DontDestroyOnLoad(this);

            _gameScenesManager.transitionDidFinishEvent += OnTransitionDidFinish;
        }

        private void OnDestroy()
        {
            _gameScenesManager.transitionDidFinishEvent -= OnTransitionDidFinish;

            input.Dispose();

            Destroy(gameObject);
        }

        #endregion

        private void OnTransitionDidFinish(ScenesTransitionSetupDataSO setupData, DiContainer container)
        {
            if (_gameScenesManager.GetCurrentlyLoadedSceneNames().Contains("GameplayCore"))
            {
                if (_eventManager && !_gameplayEventsPlayer)
                {
                    _logger.Info($"Adding {nameof(AvatarGameplayEventsPlayer)}");
                    _gameplayEventsPlayer = container.InstantiateComponent<AvatarGameplayEventsPlayer>(gameObject, new object[] { avatar });
                }
            }
            else
            {
                if (_gameplayEventsPlayer)
                {
                    _logger.Info($"Removing {nameof(AvatarGameplayEventsPlayer)}");
                    Destroy(_gameplayEventsPlayer);
                }

                if (_eventManager && _gameScenesManager.GetCurrentlyLoadedSceneNames().Contains("MainMenu"))
                {
                    _eventManager.OnMenuEnter?.Invoke();
                }
            }
        }

        private void SetChildrenToLayer(int layer)
        {
	        foreach (Renderer renderer in _renderers)
            {
                renderer.gameObject.layer = layer;
	        }
        }

        private void ApplyFirstPersonExclusions()
        {
            foreach (FirstPersonExclusion firstPersonExclusion in _firstPersonExclusions)
            {
                foreach (GameObject gameObj in firstPersonExclusion.exclude)
                {
                    if (!gameObj) continue;

                    _logger.Trace($"Excluding '{gameObj.name}' from first person view");
                    gameObj.layer = AvatarLayers.kOnlyInThirdPerson;
                }
            }
        }

        private float GetEyeHeight()
        {
            if (!head)
            {
                _logger.Warning("Avatar does not have a head tracking reference");
                return MainSettingsModelSO.kDefaultPlayerHeight - MainSettingsModelSO.kHeadPosToPlayerHeightOffset;
            }

            // many avatars rely on this being global because their root position isn't at (0, 0, 0)
            return head.position.y;
        }

        private void FixTrackingReferences(VRIKManager vrikManager)
        {
            FixTrackingReference("Head",       head,      vrikManager.references_head,                                          vrikManager.solver_spine_headTarget);
            FixTrackingReference("Left Hand",  leftHand,  vrikManager.references_leftHand,                                      vrikManager.solver_leftArm_target);
            FixTrackingReference("Right Hand", rightHand, vrikManager.references_rightHand,                                     vrikManager.solver_rightArm_target);
            FixTrackingReference("Waist",      pelvis,    vrikManager.references_pelvis,                                        vrikManager.solver_spine_pelvisTarget);
            FixTrackingReference("Left Foot",  leftLeg,   vrikManager.references_leftToes  ?? vrikManager.references_leftFoot,  vrikManager.solver_leftLeg_target);
            FixTrackingReference("Right Foot", rightLeg,  vrikManager.references_rightToes ?? vrikManager.references_rightFoot, vrikManager.solver_rightLeg_target);
        }

        private void FixTrackingReference(string name, Transform tracker, Transform reference, Transform target)
        {
            if (!reference)
            {
                _logger.Warning($"Could not find {name} reference");
                return;
            }

            if (!target)
            {
                // target will be added automatically, no need to adjust
                return;
            }

            Vector3 offset = target.position - reference.position;
            
            // only warn if offset is larger than 1 mm
            if (offset.magnitude > 0.001f)
            {
                // manually putting each coordinate gives more resolution
                _logger.Warning($"{name} bone and target are not at the same position; moving '{tracker.name}' by ({offset.x:0.000}, {offset.y:0.000}, {offset.z:0.000})");
                tracker.position -= offset;
            }
        }

        /// <summary>
        /// Measure avatar arm span. Since the player's measured arm span is actually from palm to palm
        /// (approximately) due to the way the controllers are held, this isn't "true" arm span.
        /// </summary>
        private float GetArmSpan()
        {
            // TODO using animator here probably isn't a good idea, use VRIKManager references instead
            Animator animator = GetComponentInChildren<Animator>();

            if (!animator) return AvatarTailor.kDefaultPlayerArmSpan;

            Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

            Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            if (!leftShoulder || !leftUpperArm || !leftLowerArm || !rightShoulder || !rightUpperArm || !rightLowerArm)
            {
                _logger.Warning("Could not calculate avatar arm span due to missing bones");
                return AvatarTailor.kDefaultPlayerArmSpan;
            }

            if (!leftHand || !rightHand)
            {
                _logger.Warning("Could not calculate avatar arm span due to missing tracking references");
                return AvatarTailor.kDefaultPlayerArmSpan;
            }

            float leftArmLength = Vector3.Distance(leftShoulder.position, leftUpperArm.position) + Vector3.Distance(leftUpperArm.position, leftLowerArm.position) + Vector3.Distance(leftLowerArm.position, leftHand.position);
            float rightArmLength = Vector3.Distance(rightShoulder.position, rightUpperArm.position) + Vector3.Distance(rightUpperArm.position, rightLowerArm.position) + Vector3.Distance(rightLowerArm.position, rightHand.position);
            float shoulderToShoulderDistance = Vector3.Distance(leftShoulder.position, rightShoulder.position);

            float totalLength = leftArmLength + shoulderToShoulderDistance + rightArmLength;

            return totalLength;
        }
        
        #pragma warning disable CS0618
        private void ApplyIKManagerFields(VRIKManager vrikManager, IKManager ikManager)
        {
            vrikManager.solver_spine_headTarget = ikManager.HeadTarget;
            vrikManager.solver_leftArm_target   = ikManager.LeftHandTarget;
            vrikManager.solver_rightArm_target  = ikManager.RightHandTarget;

            if (!(ikManager is IKManagerAdvanced ikManagerAdvanced)) return;

            FieldInfo[] fieldInfos = typeof(IKManagerAdvanced).GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                string[] propertyName = fieldInfo.Name.Split('_');
                var value = fieldInfo.GetValue(ikManagerAdvanced);

                if (propertyName.Length > 1)
                {
                    if ("Spine" == propertyName[0])
                    {
                        SetField(vrikManager, "solver_spine_" + propertyName[1], value);
                    }
                    else if ("LeftArm" == propertyName[0])
                    {
                        SetField(vrikManager, "solver_leftArm_" + propertyName[1], value);
                    }
                    else if ("RightArm" == propertyName[0])
                    {
                        SetField(vrikManager, "solver_rightArm_" + propertyName[1], value);
                    }
                    else if ("LeftLeg" == propertyName[0])
                    {
                        SetField(vrikManager, "solver_leftLeg_" + propertyName[1], value);
                    }
                    else if ("RightLeg" == propertyName[0])
                    {
                        SetField(vrikManager, "solver_rightLeg_" + propertyName[1], value);
                    }
                    else if ("Locomotion" == propertyName[0])
                    {
                        SetField(vrikManager, "solver_locomotion_" + propertyName[1], value);
                    }
                }
            }
        }
        #pragma warning restore CS0618

        private void SetField(object target, string fieldName, object value)
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            if (fieldName == null) throw new NullReferenceException(nameof(fieldName));

            try
            {
                _logger.Trace($"Set {fieldName} = {value}");

                Type targetObjectType = target.GetType();
                FieldInfo field = targetObjectType.GetField(fieldName);

                if (field == null)
                {
                    _logger.Warning($"{fieldName} does not exist on {targetObjectType.FullName}");
                    return;
                }
                
                Type sourceType = value?.GetType();
                Type targetType = field.FieldType;

                if (value == null && targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    _logger.Warning($"Tried setting non-nullable type {targetType.FullName} to null");
                    return;
                }
                
                if (sourceType != null)
                {
                    if (sourceType != targetType)
                    {
                        _logger.Warning($"Converting value from {sourceType.FullName} to {targetType.FullName}");
                    }

                    if (sourceType.IsEnum)
                    {
                        Type sourceUnderlyingType = Enum.GetUnderlyingType(sourceType);
                        _logger.Trace($"Underlying type for source {sourceType.FullName} is {sourceUnderlyingType.FullName}");
                    }
                }

                if (targetType.IsEnum)
                {
                    Type targetUnderlyingType = Enum.GetUnderlyingType(targetType);
                    _logger.Trace($"Underlying type for target {targetType.FullName} is {targetUnderlyingType.FullName}");

                    targetType = targetUnderlyingType;
                }
                
                field.SetValue(target, Convert.ChangeType(value, targetType));
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }
    }
}

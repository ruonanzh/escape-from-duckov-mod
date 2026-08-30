# Assembly-CSharp.dll

197 public types

## Demo [class] : UnityEngine.MonoBehaviour

- method Void ChangeAnimal()
- method Void ChangeAnimation()
- method Void ChangeShapekey()
- method Void GoToWebsite(String URL)
- method Void NextAnimal()
- method Void NextAnimation()
- method Void NextShapekey()
- method Void PrevAnimal()
- method Void PrevAnimation()
- method Void PrevShapekey()

## ECM2.Examples.FirstPerson.FirstPersonCharacter [class] : ECM2.Character

- method Void AddControlPitchInput(Single value, Single minPitch, Single maxPitch)
- method Void AddControlYawInput(Single value)

## ECM2.Examples.Glide.GlideAbility [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean glideInputPressed
- method Void Glide()
- method Boolean IsGliding()
- method Void StopGliding()

## ECM2.Examples.Jump.JumpAbility [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean canEverJump
- prop [get] Single jumpButtonHeldDownTime
- prop [get] Boolean jumpButtonPressed
- prop [get] Int32 jumpCount
- prop [get] Single jumpHoldTime
- prop [get/set] Single jumpImpulse
- prop [get/set] Int32 jumpMaxCount
- prop [get/set] Single jumpMaxHoldTime
- prop [get/set] Single jumpPostGroundedTime
- prop [get/set] Single jumpPreGroundedTime
- prop [get/set] Boolean jumpWhileCrouching
- method Boolean CanJump()
- method Int32 GetJumpCount()
- method Boolean IsJumping()
- method Void Jump()
- method Void StopJumping()

## ECM2.Examples.Ladders.Ladder [class] : UnityEngine.MonoBehaviour

- prop [get] Vector3 bottomAnchorPoint
- prop [get] Vector3 topAnchorPoint
- method Vector3 ClosestPointOnPath(Vector3 position, Single& pathPosition)

## ECM2.Examples.Ladders.LadderClimbAbility [class] : UnityEngine.MonoBehaviour

- method Void Climb()
- method Boolean IsClimbing()
- method Void StopClimbing()

## ECM2.Examples.Ladders.LadderClimbAbility.ClimbingState [enum]

- None = 0
- Grabbing = 1
- Grabbed = 2
- Releasing = 3

## ECM2.Examples.Ladders.LadderClimbAbility.CustomMovementMode [enum]

- Climbing = 1

## ECM2.Examples.OrientToGround.CharacterOrientToGround [class] : UnityEngine.MonoBehaviour

- method Boolean Filter(Collider otherCollider)

## ECM2.Examples.PlanetWalk.ThirdPersonController [class] : ECM2.Examples.ThirdPerson.ThirdPersonController

- method Void AddControlYawInput(Single value)

## ECM2.Examples.SimpleCameraController [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single distanceToTarget
- prop [get/set] Transform target
- method Void LateUpdate()
- method Void OnValidate()
- method Void Start()

## ECM2.Examples.Slide.PlayerCharacter [class] : ECM2.Character

- method Single GetMaxAcceleration()
- method Single GetMaxSpeed()
- method Boolean IsSliding()
- method Boolean IsWalking()

## ECM2.Examples.SlopeSpeedModifier.MyCharacter [class] : ECM2.Character

- method Single GetMaxSpeed()

## ECM2.Examples.Teleport.Teleporter [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean isTeleporterEnabled

## ECM2.Examples.ThirdPerson.ThirdPersonController [class] : UnityEngine.MonoBehaviour

- method Void AddControlPitchInput(Single value, Single minValue, Single maxValue)
- method Void AddControlYawInput(Single value)
- method Void AddControlZoomInput(Single value)

## ECM2.Examples.TwinStickMovement.TwinStickCharacter [class] : ECM2.Character

- method Vector3 GetAimDirection()
- method Void SetAimDirection(Vector3 worldDirection)

## ECM2.Walkthrough.Ex41.SprintableCharacter [class] : ECM2.Character

- method Single GetMaxSpeed()
- method Boolean IsSprinting()
- method Void Sprint()
- method Void StopSprinting()

## ECM2.Walkthrough.Ex42.SprintAbility [class] : UnityEngine.MonoBehaviour

- method Boolean IsSprinting()
- method Void Sprint()
- method Void StopSprinting()

## ECM2.Walkthrough.Ex43.DashingCharacter [class] : ECM2.Character

- method Void Dash()
- method Boolean IsDashAllowed()
- method Boolean IsDashing()
- method Void StopDashing()

## ECM2.Walkthrough.Ex43.DashingCharacter.ECustomMovementMode [enum]

- None = 0
- Dashing = 1

## ECM2.Walkthrough.Ex71.KinematicMove [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single moveTime
- prop [get/set] Vector3 offset
- method Void Awake()
- method Single EaseInOut(Single time, Single duration) (static)
- method Void FixedUpdate()
- method Void OnValidate()

## ECM2.Walkthrough.Ex71.KinematicRotate [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single angle
- prop [get/set] Single rotationSpeed
- method Void Awake()
- method Void FixedUpdate()
- method Void OnValidate()

## ECM2.Walkthrough.Ex91.FirstPersonController [class] : UnityEngine.MonoBehaviour

- method Void AddControlPitchInput(Single value, Single minValue, Single maxValue)
- method Void AddControlYawInput(Single value)

## ECM2.Walkthrough.Ex92.ThirdPersonController [class] : UnityEngine.MonoBehaviour

- method Void AddControlPitchInput(Single value, Single minValue, Single maxValue)
- method Void AddControlYawInput(Single value, Single minValue, Single maxValue)
- method Void AddControlZoomInput(Single value)

## FOW.Demos.FowCharacterDemo [class] : UnityEngine.MonoBehaviour

- method Void setInput()

## JoshH.Extensions.Vector2Extension [class]

- method Vector2 Rotate(Vector2 v, Single degrees) (static)

## JoshH.UI.UIGradient [class] : UnityEngine.UI.BaseMeshEffect

- prop [get/set] Single Angle
- prop [get/set] UIGradientBlendMode BlendMode
- prop [get/set] Color CornerColorLowerLeft
- prop [get/set] Color CornerColorLowerRight
- prop [get/set] Color CornerColorUpperLeft
- prop [get/set] Color CornerColorUpperRight
- prop [get/set] UIGradientType GradientType
- prop [get/set] Single Intensity
- prop [get/set] Color LinearColor1
- prop [get/set] Color LinearColor2
- prop [get/set] Gradient LinearGradient
- method Void ForceUpdateGraphic()
- method Void ModifyMesh(VertexHelper vh)

## JoshH.UI.UIGradient.UIGradientBlendMode [enum]

- Override = 0
- Multiply = 1

## JoshH.UI.UIGradient.UIGradientType [enum]

- Linear = 0
- Corner = 1
- ComplexLinear = 2

## KINEMATION.MagicBlend.Runtime.AtomPose [struct]

- method AtomPose Lerp(AtomPose a, AtomPose b, Single alpha) (static)

## KINEMATION.MagicBlend.Runtime.BlendStreamAtom [struct]

- method AtomPose GetBlendedAtomPose(Single blendWeight)

## KINEMATION.MagicBlend.Runtime.LayeringJob [struct]

- method Void ProcessAnimation(AnimationStream stream)
- method Void ProcessRootMotion(AnimationStream stream)

## KINEMATION.MagicBlend.Runtime.MagicBlendAsset [class] : UnityEngine.ScriptableObject

- method KRig GetRigAsset()

## KINEMATION.MagicBlend.Runtime.MagicBlendLibrary [class]

- method Void ConnectPose(AnimationScriptPlayable playable, PlayableGraph graph, AnimationClip pose) (static)
- method NativeArray<BlendStreamAtom> SetupBlendAtoms(Animator animator, KRigComponent rigComponent) (static)

## KINEMATION.MagicBlend.Runtime.MagicBlendState [class] : UnityEngine.StateMachineBehaviour

- method Void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, Int32 layerIndex)

## KINEMATION.MagicBlend.Runtime.MagicBlending [class] : UnityEngine.MonoBehaviour

- prop [get] MagicBlendAsset BlendAsset
- method Single GetOverlayTime(Boolean isNormalized)
- method Void UpdateMagicBlendAsset(MagicBlendAsset newAsset, Boolean useBlending, Single blendTime, Boolean useCurve)

## KINEMATION.MagicBlend.Runtime.OverlayJob [struct]

- method Void ProcessAnimation(AnimationStream stream)
- method Void ProcessRootMotion(AnimationStream stream)

## KINEMATION.MagicBlend.Runtime.PoseJob [struct]

- method Void ProcessAnimation(AnimationStream stream)
- method Void ProcessRootMotion(AnimationStream stream)

## RandomGenerator [class] : UnityEngine.MonoBehaviour

- method Void ClearGeneratedObjects()
- method Void Generate()

## UI_Spline_Renderer.Example.DraggableSplinePointExample [class] : UnityEngine.MonoBehaviour

- method Void OnBeginDrag(PointerEventData eventData)
- method Void OnDrag(PointerEventData eventData)
- method Void OnEndDrag(PointerEventData eventData)

## VLB.AttenuationEquation [enum]

- Linear = 0
- Quadratic = 1
- Blend = 2

## VLB.AttenuationEquationHD [enum]

- Linear = 0
- Quadratic = 1

## VLB.BatchingHelper [class]

- prop [get] Boolean forceEnableDepthBlend (static)
- method Boolean CanBeBatched(VolumetricLightBeamSD beam, String& reasons) (static)
- method Boolean CanBeBatched(VolumetricLightBeamHD beam, String& reasons) (static)
- method Boolean CanBeBatched(VolumetricLightBeamSD beamA, VolumetricLightBeamSD beamB, String& reasons) (static)
- method Boolean CanBeBatched(VolumetricLightBeamHD beamA, VolumetricLightBeamHD beamB, String& reasons) (static)
- method Boolean CanBeBatched(VolumetricLightBeamAbstractBase beamA, VolumetricLightBeamAbstractBase beamB, String& reasons) (static)
- method Boolean IsGpuInstancingEnabled(Material material) (static)
- method Void SetMaterialProperties(Material material, Boolean enableGpuInstancing) (static)

## VLB.BeamGeometryAbstractBase [class] : UnityEngine.MonoBehaviour

- prop [get/set] Mesh coneMesh
- prop [get/set] MeshFilter meshFilter
- prop [get/set] MeshRenderer meshRenderer
- method Void DestroyBeamGeometryGameObject(BeamGeometryAbstractBase beamGeom) (static)

## VLB.BeamGeometryHD [class] : VLB.BeamGeometryAbstractBase

- prop [get] Boolean isCustomRenderPipelineSupported (static)
- prop [/set] Int32 sortingLayerID
- prop [/set] Int32 sortingOrder
- prop [/set] Boolean visible
- method Void Initialize(VolumetricLightBeamHD master)
- method Void RegenerateMesh()
- method Void SetMaterialProp(Int32 nameID, Single value)
- method Void SetMaterialProp(Int32 nameID, Vector4 value)
- method Void SetMaterialProp(Int32 nameID, Color value)
- method Void SetMaterialProp(Int32 nameID, Matrix4x4 value)
- method Void SetMaterialProp(Int32 nameID, Texture value)
- method Void SetMaterialProp(Int32 nameID, InvalidTexture invalidTexture)
- method Void SetPropertyDirty(DirtyProps prop)

## VLB.BeamGeometryHD.InvalidTexture [enum]

- Null = 0
- NoDepth = 1

## VLB.BeamGeometrySD [class] : VLB.BeamGeometryAbstractBase

- prop [get] Boolean _INTERNAL_IsFadeOutCoroutineRunning
- prop [get] Boolean isCustomRenderPipelineSupported (static)
- prop [get/set] Int32 sortingLayerID
- prop [get/set] Int32 sortingOrder
- method Void Initialize(VolumetricLightBeamSD master)
- method Void OnMasterDisable()
- method Void OnMasterEnable()
- method Void RegenerateMesh(Boolean masterEnabled)
- method Void RestartFadeOutCoroutine()
- method Void SetDynamicOcclusionCallback(String shaderKeyword, Callback cb)
- method Void SetMaterialProp(Int32 nameID, Single value)
- method Void SetMaterialProp(Int32 nameID, Vector4 value)
- method Void SetMaterialProp(Int32 nameID, Color value)
- method Void SetMaterialProp(Int32 nameID, Matrix4x4 value)
- method Void SetMaterialProp(Int32 nameID, Texture value)
- method Void UpdateMaterialAndBounds()

## VLB.BeamProps [enum]

- Transform = 1
- Color = 2
- BlendingMode = 4
- Intensity = 8
- SideSoftness = 16
- SpotShape = 32
- FallOffAttenuation = 64
- Noise3D = 128
- SDConeGeometry = 256
- SDSoftIntersectBlendingDist = 512
- Props2D = 1024

## VLB.BlendingMode [enum]

- Additive = 0
- SoftAdditive = 1
- TraditionalTransparency = 2

## VLB.ColorMode [enum]

- Flat = 0
- Gradient = 1

## VLB.Config [class] : UnityEngine.ScriptableObject

- prop [get] Config Instance (static)
- prop [get] Boolean SD_requiresDoubleSidedMesh
- prop [get] Boolean SD_useSinglePassShader
- prop [get] Int32 defaultRaymarchingQualityUniqueID
- prop [get] String fadeOutCameraName
- prop [get] Transform fadeOutCameraTransform
- prop [get] Boolean hasRenderPipelineMismatch
- prop [get] Boolean isHDRPExposureWeightSupported
- prop [get] Int32 raymarchingQualitiesCount
- prop [get/set] RenderPipeline renderPipeline
- prop [get/set] RenderingMode renderingMode
- field String ClassName (static)
- field String kAssetName (static)
- field String kAssetNameExt (static)
- method Void ForceUpdateFadeOutCamera()
- method RenderingMode GetActualRenderingMode(ShaderMode shaderMode)
- method Shader GetBeamShader(ShaderMode mode)
- method RaymarchingQuality GetRaymarchingQualityForIndex(Int32 index)
- method RaymarchingQuality GetRaymarchingQualityForUniqueID(Int32 id)
- method Int32 GetRaymarchingQualityIndexForUniqueID(Int32 id)
- method Boolean IsRaymarchingQualityUniqueIDValid(Int32 id)
- method Boolean IsSRPBatcherSupported()
- method Material NewMaterialTransient(ShaderMode mode, Boolean gpuInstanced)
- method ParticleSystem NewVolumetricDustParticles()
- method Void Reset()
- method Void ResetInternalData()
- method Void SetURPScriptableRendererIndexToDepthCamera(Camera camera)

## VLB.Consts [class]

- field String PluginFolder (static)

## VLB.Consts.Beam [class]

- field Single AttenuationCustomBlendingDefault (static)
- field Single AttenuationCustomBlendingMax (static)
- field Single AttenuationCustomBlendingMin (static)
- field AttenuationEquation AttenuationEquationDefault (static)
- field BlendingMode BlendingModeDefault (static)
- field Single CameraClippingDistance (static)
- field ColorMode ColorModeDefault (static)
- field Single ConeRadiusStart (static)
- field Single DepthBlendDistance (static)
- field Dimensions DimensionsDefault (static)
- field Single FadeOutBeginDefault (static)
- field Single FadeOutEndDefault (static)
- field Single FallOffDistancesMinThreshold (static)
- field Single FallOffEnd (static)
- field Single FallOffStart (static)
- field Color FlatColor (static)
- field Boolean GeomCap (static)
- field MeshType GeomMeshType (static)
- field Int32 GeomSegmentsDefault (static)
- field Int32 GeomSegmentsMax (static)
- field Int32 GeomSegmentsMin (static)
- field Int32 GeomSidesDefault (static)
- field Int32 GeomSidesMax (static)
- field Int32 GeomSidesMin (static)
- field Single HDRPExposureWeightDefault (static)
- field Single HDRPExposureWeightMax (static)
- field Single HDRPExposureWeightMin (static)
- field Single IntensityDefault (static)
- field Single IntensityMin (static)
- field Single MultiplierDefault (static)
- field Single MultiplierMin (static)
- field Single NoiseIntensityDefault (static)
- field Single NoiseIntensityMax (static)
- field Single NoiseIntensityMin (static)
- field NoiseMode NoiseModeDefault (static)
- field Single NoiseScaleDefault (static)
- field Single NoiseScaleMax (static)
- field Single NoiseScaleMin (static)
- field Vector3 NoiseVelocityDefault (static)
- field Boolean ScalableDefault (static)
- field ShaderAccuracy ShaderAccuracyDefault (static)
- field Single SpotAngleDefault (static)
- field Single SpotAngleMax (static)
- field Single SpotAngleMin (static)

## VLB.Consts.Beam.HD [class]

- field AttenuationEquationHD AttenuationEquationDefault (static)
- field Single JitteringFactorDefault (static)
- field Single JitteringFactorMin (static)
- field Int32 JitteringFrameRateDefault (static)
- field Int32 JitteringFrameRateMax (static)
- field Int32 JitteringFrameRateMin (static)
- field MinMaxRangeFloat JitteringLerpRange (static)
- field Single SideSoftnessDefault (static)
- field Single SideSoftnessMax (static)
- field Single SideSoftnessMin (static)

## VLB.Consts.Beam.SD [class]

- field Transform ClippingPlaneTransformDefault (static)
- field Single FresnelPow (static)
- field Single FresnelPowMaxValue (static)
- field Single GlareBehindDefault (static)
- field Single GlareFrontalDefault (static)
- field Single GlareMax (static)
- field Single GlareMin (static)
- field Vector3 SkewingLocalForwardDirectionDefault (static)
- field Vector2 TiltDefault (static)

## VLB.Consts.Config [class]

- field Single DitheringFactor (static)
- field String FadeOutCameraTagDefault (static)
- field FeatureEnabledColorGradient FeatureEnabledColorGradientDefault (static)
- field Boolean FeatureEnabledDefault (static)
- field Int32 GeometryLayerIDDefault (static)
- field Boolean GeometryOverrideLayerDefault (static)
- field RenderPipeline GeometryRenderPipelineDefault (static)
- field RenderQueue GeometryRenderQueueDefault (static)
- field RenderingMode GeometryRenderingModeDefault (static)
- field String GeometryTagDefault (static)
- field Int32 Noise3DSizeDefault (static)
- field Int32 SharedMeshSegmentsDefault (static)
- field Int32 SharedMeshSegmentsMax (static)
- field Int32 SharedMeshSegmentsMin (static)
- field Int32 SharedMeshSidesDefault (static)
- field Int32 SharedMeshSidesMax (static)
- field Int32 SharedMeshSidesMin (static)
- field Boolean UseLightColorTemperatureDefault (static)

## VLB.Consts.Config.HD [class]

- field Single CameraBlendingDistance (static)
- field RenderQueue GeometryRenderQueueDefault (static)
- field Int32 RaymarchingQualitiesStepsMin (static)

## VLB.Consts.Cookie [class]

- field CookieChannel ChannelDefault (static)
- field Single ContributionDefault (static)
- field Single ContributionMax (static)
- field Single ContributionMin (static)
- field Texture CookieTextureDefault (static)
- field Boolean NegativeDefault (static)
- field Single RotationDefault (static)
- field Vector2 ScaleDefault (static)
- field Vector2 TranslationDefault (static)

## VLB.Consts.DustParticles [class]

- field Single AlphaDefault (static)
- field Boolean CullingEnabledDefault (static)
- field Single CullingMaxDistanceDefault (static)
- field Single CullingMaxDistanceMin (static)
- field Single DensityDefault (static)
- field Single DensityMax (static)
- field Single DensityMin (static)
- field ParticlesDirection DirectionDefault (static)
- field Single SizeDefault (static)
- field MinMaxRangeFloat SpawnDistanceRangeDefault (static)
- field Vector3 VelocityDefault (static)

## VLB.Consts.DynOcclusion [class]

- field Int32 DepthBufferDepthMapResolutionDefault (static)
- field Single DepthBufferFadeDistanceToSurfaceDefault (static)
- field Boolean DepthBufferOcclusionCullingDefault (static)
- field LayerMask LayerMaskDefault (static)
- field Boolean RaycastingConsiderTriggersDefault (static)
- field Dimensions RaycastingDimensionsDefault (static)
- field Single RaycastingFadeDistanceToSurfaceDefault (static)
- field Single RaycastingMaxSurfaceAngleMax (static)
- field Single RaycastingMaxSurfaceAngleMin (static)
- field Single RaycastingMaxSurfaceDotDefault (static)
- field Single RaycastingMinOccluderAreaDefault (static)
- field Single RaycastingMinSurfaceRatioDefault (static)
- field Single RaycastingMinSurfaceRatioMax (static)
- field Single RaycastingMinSurfaceRatioMin (static)
- field PlaneAlignment RaycastingPlaneAlignmentDefault (static)
- field Single RaycastingPlaneOffsetDefault (static)
- field DynamicOcclusionUpdateRate UpdateRateDefault (static)
- field Int32 WaitFramesCountDefault (static)

## VLB.Consts.Effects [class]

- field ComponentsToChange ComponentsToChangeDefault (static)
- field MinMaxRangeFloat FlickeringDurationDefault (static)
- field Single FrequencyDefault (static)
- field MinMaxRangeFloat IntensityAmplitudeDefault (static)
- field MinMaxRangeFloat PauseDurationDefault (static)
- field Boolean PerformPausesDefault (static)
- field Boolean RestoreIntensityOnDisableDefault (static)
- field Boolean RestoreIntensityOnPauseDefault (static)
- field Single SmoothingDefault (static)

## VLB.Consts.Help [class]

- field String AddComponentMenuBase (static)
- field String AddComponentMenuCommon (static)
- field String AddComponentMenuDustParticles (static)
- field String AddComponentMenuEffectFlicker (static)
- field String AddComponentMenuEffectFromProfile (static)
- field String AddComponentMenuEffectPulse (static)
- field String AddComponentMenuTriggerZone (static)
- field String UrlConfig (static)
- field String UrlDustParticles (static)
- field String UrlEffectFlicker (static)
- field String UrlEffectFromProfile (static)
- field String UrlEffectPulse (static)
- field String UrlLODBeamGroup (static)
- field String UrlTriggerZone (static)

## VLB.Consts.Help.HD [class]

- field String AddComponentMenuBeam2D (static)
- field String AddComponentMenuBeam3D (static)
- field String AddComponentMenuCookie (static)
- field String AddComponentMenuHD (static)
- field String AddComponentMenuShadow (static)
- field String AddComponentMenuTrackRealtimeChangesOnLight (static)
- field String UrlBeam (static)
- field String UrlCookie (static)
- field String UrlShadow (static)
- field String UrlTrackRealtimeChangesOnLight (static)

## VLB.Consts.Help.SD [class]

- field String AddComponentMenuBeam (static)
- field String AddComponentMenuDynamicOcclusionDepthBuffer (static)
- field String AddComponentMenuDynamicOcclusionRaycasting (static)
- field String AddComponentMenuSD (static)
- field String UrlBeam (static)
- field String UrlDynamicOcclusionDepthBuffer (static)
- field String UrlDynamicOcclusionRaycasting (static)
- field String UrlSkewingHandle (static)

## VLB.Consts.Internal [class]

- prop [get] HideFlags ProceduralObjectsHideFlags (static)
- field Boolean ProceduralObjectsVisibleInEditor (static)

## VLB.Consts.Shadow [class]

- field Int32 DepthMapResolutionDefault (static)
- field LayerMask LayerMaskDefault (static)
- field Boolean OcclusionCullingDefault (static)
- field Single StrengthDefault (static)
- field Single StrengthMax (static)
- field Single StrengthMin (static)
- field ShadowUpdateRate UpdateRateDefault (static)
- field Int32 WaitFramesCountDefault (static)
- method String GetErrorChangeRuntimeDepthMapResolution(VolumetricShadowHD comp) (static)

## VLB.CookieChannel [enum]

- Red = 0
- Green = 1
- Blue = 2
- Alpha = 3
- RGBA = 4

## VLB.Dimensions [enum]

- Dim3D = 0
- Dim2D = 1

## VLB.DirtyProps [enum]

- None = 0
- Intensity = 2
- HDRPExposureWeight = 4
- ColorMode = 8
- Color = 16
- BlendingMode = 32
- Cone = 64
- SideSoftness = 128
- Attenuation = 256
- Dimensions = 512
- RaymarchingQuality = 1024
- Jittering = 2048
- NoiseMode = 4096
- OnlyMaterialChangeOnly = 5928
- NoiseIntensity = 8192
- NoiseVelocityAndScale = 16384
- CookieProps = 32768
- ShadowProps = 65536
- AllWithoutMaterialChange = 125142
- All = 131070

## VLB.DynamicOcclusionAbstractBase [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 _INTERNAL_LastFrameRendered
- field String ClassName (static)
- field Boolean _INTERNAL_ApplyRandomFrameOffset (static)
- method Void ProcessOcclusionManually()

## VLB.DynamicOcclusionDepthBuffer [class] : VLB.DynamicOcclusionAbstractBase

- field String ClassName (static)
- method Boolean HasLayerMaskIssues()

## VLB.DynamicOcclusionRaycasting [class] : VLB.DynamicOcclusionAbstractBase

- prop [get/set] Single fadeDistanceToPlane
- prop [get/set] Plane planeEquationWS
- field String ClassName (static)
- method Boolean IsColliderHiddenByDynamicOccluder(Collider collider)

## VLB.DynamicOcclusionRaycasting.HitResult [struct]

- prop [get] Bounds bounds
- prop [get] Boolean hasCollider
- prop [get] String name
- method Void SetNull()

## VLB.DynamicOcclusionUpdateRate [enum]

- Never = 1
- OnEnable = 2
- OnBeamMove = 4
- EveryXFrames = 8
- OnBeamMoveAndEveryXFrames = 12

## VLB.EffectAbstractBase [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean restoreBaseIntensity
- field String ClassName (static)
- method Void InitFrom(EffectAbstractBase Source)

## VLB.EffectAbstractBase.ComponentsToChange [enum]

- UnityLight = 1
- VolumetricLightBeam = 2
- VolumetricDustParticles = 4

## VLB.EffectFlicker [class] : VLB.EffectAbstractBase

- field String ClassName (static)
- method Void InitFrom(EffectAbstractBase source)

## VLB.EffectFromProfile [class] : UnityEngine.MonoBehaviour

- prop [get/set] EffectAbstractBase effectProfile
- field String ClassName (static)
- method Void InitInstanceFromProfile()

## VLB.EffectPulse [class] : VLB.EffectAbstractBase

- field String ClassName (static)
- method Void InitFrom(EffectAbstractBase source)

## VLB.FeatureEnabledColorGradient [enum]

- Off = 0
- HighOnly = 1
- HighAndLow = 2

## VLB.GlobalMeshHD [class]

- method Void Destroy() (static)
- method Mesh Get() (static)

## VLB.GlobalMeshSD [class]

- method Void Destroy() (static)
- method Mesh Get() (static)

## VLB.LODBeamGroup [class] : UnityEngine.MonoBehaviour

- method LOD[] GetLODsFromLODGroup()

## VLB.MaterialManager [class]

- field MaterialPropertyBlock materialPropertyBlock (static)
- method Boolean EnableGPUInstancing(ShaderMode shaderMode, Boolean enabled) (static)
- method Material GetInstancedMaterial(UInt32 groupID, StaticPropertiesSD& staticProps) (static)
- method Material GetInstancedMaterial(UInt32 groupID, StaticPropertiesHD& staticProps) (static)
- method Material NewMaterialPersistent(Shader shader, Boolean gpuInstanced) (static)

## VLB.MaterialManager.BlendingMode [enum]

- Additive = 0
- SoftAdditive = 1
- TraditionalTransparency = 2
- Count = 3

## VLB.MaterialManager.ColorGradient [enum]

- Off = 0
- MatrixLow = 1
- MatrixHigh = 2
- Count = 3

## VLB.MaterialManager.HD.Attenuation [enum]

- Linear = 0
- Quadratic = 1
- Count = 2

## VLB.MaterialManager.HD.Cookie [enum]

- Off = 0
- SingleChannel = 1
- RGBA = 2
- Count = 3

## VLB.MaterialManager.HD.Shadow [enum]

- Off = 0
- On = 1
- Count = 2

## VLB.MaterialManager.Noise3D [enum]

- Off = 0
- On = 1
- Count = 2

## VLB.MaterialManager.SD.DepthBlend [enum]

- Off = 0
- On = 1
- Count = 2

## VLB.MaterialManager.SD.DynamicOcclusion [enum]

- Off = 0
- ClippingPlane = 1
- DepthTexture = 2
- Count = 3

## VLB.MaterialManager.SD.MeshSkewing [enum]

- Off = 0
- On = 1
- Count = 2

## VLB.MaterialManager.SD.ShaderAccuracy [enum]

- Fast = 0
- High = 1
- Count = 2

## VLB.MaterialManager.StaticPropertiesHD [struct]

- prop [get] Int32 staticPropertiesCount (static)
- method Void ApplyToMaterial(Material mat)
- method Int32 GetMaterialID()
- method Int32 GetPropertiesCount()
- method ShaderMode GetShaderMode()

## VLB.MaterialManager.StaticPropertiesSD [struct]

- prop [get] Int32 staticPropertiesCount (static)
- method Void ApplyToMaterial(Material mat)
- method Int32 GetMaterialID()
- method Int32 GetPropertiesCount()
- method ShaderMode GetShaderMode()

## VLB.MaterialModifier.Callback [class] : System.MulticastDelegate

- method IAsyncResult BeginInvoke(Interface owner, AsyncCallback callback, Object object)
- method Void EndInvoke(IAsyncResult result)
- method Void Invoke(Interface owner)

## VLB.MaterialModifier.Interface [interface]

- method Void SetMaterialProp(Int32 nameID, Single value)
- method Void SetMaterialProp(Int32 nameID, Vector4 value)
- method Void SetMaterialProp(Int32 nameID, Color value)
- method Void SetMaterialProp(Int32 nameID, Matrix4x4 value)
- method Void SetMaterialProp(Int32 nameID, Texture value)

## VLB.MeshGenerator [class]

- method Bounds ComputeBounds(Single lengthZ, Single radiusStart, Single radiusEnd) (static)
- method Mesh GenerateConeZ_Angle(Single lengthZ, Single coneAngle, Int32 numSides, Int32 numSegments, Boolean cap, Boolean doubleSided) (static)
- method Mesh GenerateConeZ_Radii(Single lengthZ, Single radiusStart, Single radiusEnd, Int32 numSides, Int32 numSegments, Boolean cap, Boolean doubleSided) (static)
- method Mesh GenerateConeZ_Radii_DoubleCaps(Single lengthZ, Single radiusStart, Single radiusEnd, Int32 numSides, Boolean inverted) (static)
- method Mesh GenerateConeZ_RadiusAndAngle(Single lengthZ, Single radiusStart, Single coneAngle, Int32 numSides, Int32 numSegments, Boolean cap, Boolean doubleSided) (static)
- method Int32 GetIndicesCount(Int32 numSides, Int32 numSegments, CapMode capMode, Boolean doubleSided) (static)
- method Int32 GetSharedMeshHDIndicesCount() (static)
- method Int32 GetSharedMeshHDVertexCount() (static)
- method Int32 GetSharedMeshIndicesCount() (static)
- method Int32 GetSharedMeshVertexCount() (static)
- method Int32 GetVertexCount(Int32 numSides, Int32 numSegments, CapMode capMode, Boolean doubleSided) (static)

## VLB.MeshGenerator.CapMode [enum]

- None = 0
- OneVertexPerCap_1Cap = 1
- OneVertexPerCap_2Caps = 2
- SpecificVerticesPerCap_1Cap = 3
- SpecificVerticesPerCap_2Caps = 4

## VLB.MeshType [enum]

- Shared = 0
- Custom = 1

## VLB.MinMaxRangeAttribute [class] : System.Attribute

- prop [get/set] Single maxValue
- prop [get/set] Single minValue

## VLB.MinMaxRangeFloat [struct]

- prop [get] Vector2 asVector2
- prop [get] Single maxValue
- prop [get] Single minValue
- prop [get] Single randomValue
- method Boolean Equals(Object obj)
- method Boolean Equals(MinMaxRangeFloat other)
- method Int32 GetHashCode()
- method Single GetLerpedValue(Single lerp01)

## VLB.Noise3D [class]

- prop [get] String isNotSupportedString (static)
- prop [get] Boolean isProperlyLoaded (static)
- prop [get] Boolean isSupported (static)
- method Void LoadIfNeeded() (static)

## VLB.NoiseMode [enum]

- Disabled = 0
- WorldSpace = 1
- LocalSpace = 2

## VLB.ParticlesDirection [enum]

- Random = 0
- LocalSpace = 1
- WorldSpace = 2

## VLB.PlaneAlignment [enum]

- Surface = 0
- Beam = 1

## VLB.PlatformHelper [class]

- method String GetCurrentPlatformSuffix() (static)

## VLB.PolygonHelper.Plane2D [struct]

- method Vector2 ClosestPoint(Vector2 pt)
- method Vector2[] CutConvex(Vector2[] poly)
- method Single Distance(Vector2 point)
- method Void Flip()
- method Plane2D FromNormalAndPoint(Vector3 normalizedNormal, Vector3 p1) (static)
- method Plane2D FromPoints(Vector3 p1, Vector3 p2) (static)
- method Boolean GetSide(Vector2 point)
- method Vector2 Intersect(Vector2 p1, Vector2 p2)
- method String ToString()

## VLB.RaymarchingQuality [class]

- prop [get] RaymarchingQuality defaultInstance (static)
- prop [get] Boolean hasValidUniqueID
- prop [get] Int32 uniqueID
- method RaymarchingQuality New() (static)
- method RaymarchingQuality New(String name, Int32 forcedUniqueID, Int32 stepCount) (static)

## VLB.RenderPipeline [enum]

- BuiltIn = 0
- URP = 1
- HDRP = 2

## VLB.RenderQueue [enum]

- Custom = 0
- Background = 1000
- Geometry = 2000
- AlphaTest = 2450
- GeometryLast = 2500
- Transparent = 3000
- Overlay = 4000

## VLB.RenderingMode [enum]

- MultiPass = 0
- Default = 1
- GPUInstancing = 2
- SRPBatcher = 3

## VLB.SRPHelper [class]

- prop [get] RenderPipeline projectRenderPipeline (static)
- prop [get] String renderPipelineScriptingDefineSymbolAsString (static)
- method Boolean IsUsingCustomRenderPipeline() (static)
- method Void RegisterOnBeginCameraRendering(Action<ScriptableRenderContext, Camera> cb) (static)
- method Void UnregisterOnBeginCameraRendering(Action<ScriptableRenderContext, Camera> cb) (static)

## VLB.ShaderAccuracy [enum]

- Fast = 0
- High = 1

## VLB.ShaderKeywords [class]

- field String AlphaAsBlack (static)
- field String ColorGradientMatrixHigh (static)
- field String ColorGradientMatrixLow (static)
- field String Noise3D (static)

## VLB.ShaderKeywords.HD [class]

- field String AttenuationLinear (static)
- field String AttenuationQuad (static)
- field String CookieRGBA (static)
- field String CookieSingleChannel (static)
- field String RaymarchingStepCount (static)
- field String Shadow (static)
- method String GetRaymarchingQuality(Int32 id) (static)

## VLB.ShaderKeywords.SD [class]

- field String DepthBlend (static)
- field String MeshSkewing (static)
- field String OcclusionClippingPlane (static)
- field String OcclusionDepthTexture (static)
- field String ShaderAccuracyHigh (static)

## VLB.ShaderMode [enum]

- SD = 0
- HD = 1

## VLB.ShaderProperties [class]

- field Int32 BlendDstFactor (static)
- field Int32 BlendSrcFactor (static)
- field Int32 ColorFlat (static)
- field Int32 ColorGradientMatrix (static)
- field Int32 ConeGeomProps (static)
- field Int32 ConeRadius (static)
- field Int32 DistanceFallOff (static)
- field Int32 GlobalDitheringFactor (static)
- field Int32 GlobalDitheringNoiseTex (static)
- field Int32 GlobalNoiseCustomTime (static)
- field Int32 GlobalNoiseTex3D (static)
- field Int32 GlobalUsesReversedZBuffer (static)
- field Int32 HDRPExposureWeight (static)
- field Int32 LocalToWorldMatrix (static)
- field Int32 NoiseParam (static)
- field Int32 NoiseVelocityAndScale (static)
- field Int32 ParticlesTintColor (static)
- field Int32 WorldToLocalMatrix (static)
- field Int32 ZTest (static)

## VLB.ShaderProperties.HD [class]

- field Int32 CameraForwardOS (static)
- field Int32 CameraForwardWS (static)
- field Int32 CookiePosAndScale (static)
- field Int32 CookieProperties (static)
- field Int32 CookieTexture (static)
- field Int32 GlobalCameraBlendingDistance (static)
- field Int32 GlobalJitteringNoiseTex (static)
- field Int32 Intensity (static)
- field Int32 Jittering (static)
- field Int32 ShadowDepthTexture (static)
- field Int32 ShadowProps (static)
- field Int32 SideSoftness (static)
- field Int32 TransformScale (static)

## VLB.ShaderProperties.SD [class]

- field Int32 AdditionalClippingPlaneWS (static)
- field Int32 AlphaInside (static)
- field Int32 AlphaOutside (static)
- field Int32 AttenuationLerpLinearQuad (static)
- field Int32 CameraParams (static)
- field Int32 ConeSlopeCosSin (static)
- field Int32 DepthBlendDistance (static)
- field Int32 DistanceCamClipping (static)
- field Int32 DrawCap (static)
- field Int32 DynamicOcclusionClippingPlaneProps (static)
- field Int32 DynamicOcclusionClippingPlaneWS (static)
- field Int32 DynamicOcclusionDepthProps (static)
- field Int32 DynamicOcclusionDepthTexture (static)
- field Int32 FadeOutFactor (static)
- field Int32 FresnelPow (static)
- field Int32 GlareBehind (static)
- field Int32 GlareFrontal (static)
- field Int32 LocalForwardDirection (static)
- field Int32 TiltVector (static)

## VLB.ShadowUpdateRate [enum]

- Never = 1
- OnEnable = 2
- OnBeamMove = 4
- EveryXFrames = 8
- OnBeamMoveAndEveryXFrames = 12

## VLB.SkewingHandleSD [class] : UnityEngine.MonoBehaviour

- field String ClassName (static)
- method Boolean CanSetSkewingVector()
- method Boolean CanUpdateEachFrame()
- method Boolean IsAttachedToSelf()

## VLB.SpotLightHelper [class]

- method Single GetFallOffEnd(Light light) (static)
- method Single GetIntensity(Light light) (static)
- method Single GetSpotAngle(Light light) (static)

## VLB.TrackRealtimeChangesOnLightHD [class] : UnityEngine.MonoBehaviour

- field String ClassName (static)

## VLB.TransformUtils [class]

- method Packed GetWorldPacked(Transform self) (static)

## VLB.TransformUtils.Packed [struct]

- method Boolean IsSame(Transform transf)

## VLB.TriggerZone [class] : UnityEngine.MonoBehaviour

- field String ClassName (static)

## VLB.Utils [class]

- method Boolean Approximately(Single a, Single b, Single epsilon) (static)
- method Boolean Approximately(Vector2 a, Vector2 b, Single epsilon) (static)
- method Boolean Approximately(Vector3 a, Vector3 b, Single epsilon) (static)
- method Boolean Approximately(Vector4 a, Vector4 b, Single epsilon) (static)
- method Vector4 AsVector4(Vector3 vec3, Single w) (static)
- method Vector3 ClosestPointOnPlaneCustom(Plane plane, Vector3 point) (static)
- method Color ComputeComplementaryColor(Color self, Boolean blackAndWhite) (static)
- method Single ComputeConeRadiusEnd(Single fallOffEnd, Single spotAngle) (static)
- method Single ComputeSpotAngle(Single fallOffEnd, Single coneRadiusEnd) (static)
- method Vector3 Divide(Vector3 aVector, Vector3 scale) (static)
- method Void ForeachComponentsInAnyChildrenOnly(GameObject self, Action<T> lambda, Boolean includeInactive) (static)
- method Void ForeachComponentsInDirectChildrenOnly(GameObject self, Action<T> lambda, Boolean includeInactive) (static)
- method FloatPackingPrecision GetFloatPackingPrecision() (static)
- method Single GetMaxArea2D(Bounds self) (static)
- method T GetOrAddComponent(GameObject self) (static)
- method T GetOrAddComponent(MonoBehaviour self) (static)
- method String GetPath(Transform current) (static)
- method Single GetVolumeCubic(Bounds self) (static)
- method Boolean HasAtLeastOneFlag(Enum mask, Enum flags) (static)
- method Boolean HasFlag(Enum mask, Enum flags) (static)
- method Boolean IsAlmostZero(Single f) (static)
- method Boolean IsValid(Plane plane) (static)
- method Boolean IsValidIndex(T[] array, Int32 idx) (static)
- method Void MarkCurrentSceneDirty() (static)
- method Void MarkObjectDirty(Object obj) (static)
- method T NewWithComponent(String name) (static)
- method Color Opaque(Color self) (static)
- method Single PackToFloat(Color color, Int32 floatPackingPrecision) (static)
- method Vector4 PlaneEquation(Vector3 normalizedNormal, Vector3 pt) (static)
- method Void ResizeArray(T[]& array, Int32 newSize) (static)
- method Color[] SampleInArray(Gradient self, Int32 samplesCount) (static)
- method Matrix4x4 SampleInMatrix(Gradient self, Int32 floatPackingPrecision) (static)
- method Void SetKeywordEnabled(Material mat, String name, Boolean enabled) (static)
- method Void SetShaderKeywordEnabled(String name, Boolean enabled) (static)
- method Void SetupDepthCamera(Camera depthCamera, Single coneApexOffsetZ, Single maxGeometryDistance, Single coneRadiusStart, Single coneRadiusEnd, Vector3 beamLocalForward, Vector3 lossyScale, Boolean isScalable, Quaternion beamInternalLocalRotation, Boolean shouldScaleMinNearClipPlane) (static)
- method Void Swap(T& a, T& b) (static)
- method Plane TranslateCustom(Plane plane, Vector3 translation) (static)
- method Vector2 xy(Vector3 aVector) (static)
- method Vector2 xz(Vector3 aVector) (static)
- method Vector2 yx(Vector3 aVector) (static)
- method Vector2 yz(Vector3 aVector) (static)
- method Vector2 zx(Vector3 aVector) (static)
- method Vector2 zy(Vector3 aVector) (static)

## VLB.Utils.FloatPackingPrecision [enum]

- Undef = 0
- Low = 8
- High = 64

## VLB.UtilsBeamProps [class]

- method Boolean CanChangeDuringPlaytime(VolumetricLightBeamAbstractBase self) (static)
- method AttenuationEquation ConvertAttenuation(AttenuationEquationHD value) (static)
- method AttenuationEquationHD ConvertAttenuation(AttenuationEquation value) (static)
- method Color GetColorFlat(VolumetricLightBeamAbstractBase self) (static)
- method Gradient GetColorGradient(VolumetricLightBeamAbstractBase self) (static)
- method ColorMode GetColorMode(VolumetricLightBeamAbstractBase self) (static)
- method Single GetConeAngle(VolumetricLightBeamAbstractBase self) (static)
- method Single GetConeRadiusEnd(VolumetricLightBeamAbstractBase self) (static)
- method Single GetConeRadiusStart(VolumetricLightBeamAbstractBase self) (static)
- method Dimensions GetDimensions(VolumetricLightBeamAbstractBase self) (static)
- method Boolean GetFadeOutEnabled(VolumetricLightBeamAbstractBase self) (static)
- method Single GetFadeOutEnd(VolumetricLightBeamAbstractBase self) (static)
- method Single GetFallOffEnd(VolumetricLightBeamAbstractBase self) (static)
- method Int32 GetGeomSides(VolumetricLightBeamAbstractBase self) (static)
- method Quaternion GetInternalLocalRotation(VolumetricLightBeamAbstractBase self) (static)
- method Int32 GetSortingLayerID(VolumetricLightBeamAbstractBase self) (static)
- method Int32 GetSortingOrder(VolumetricLightBeamAbstractBase self) (static)
- method Single GetThickness(VolumetricLightBeamAbstractBase self) (static)
- method Void SetColorFromLight(VolumetricLightBeamAbstractBase self, Boolean fromLight) (static)
- method Void SetFallOffEndFromLight(VolumetricLightBeamAbstractBase self, Boolean fromLight) (static)
- method Void SetIntensityFromLight(VolumetricLightBeamAbstractBase self, Boolean fromLight) (static)
- method Void SetSpotAngleFromLight(VolumetricLightBeamAbstractBase self, Boolean fromLight) (static)
- method Void SetThickness(VolumetricLightBeamAbstractBase self, Single value) (static)

## VLB.Version [class]

- prop [get] String CurrentAsString (static)
- field Int32 Current (static)

## VLB.VolumetricCookieHD [class] : UnityEngine.MonoBehaviour

- prop [get/set] CookieChannel channel
- prop [get/set] Single contribution
- prop [get/set] Texture cookieTexture
- prop [get/set] Boolean negative
- prop [get/set] Single rotation
- prop [get/set] Vector2 scale
- prop [get/set] Vector2 translation
- field String ClassName (static)
- method Void ApplyMaterialProperties(VolumetricCookieHD instance, BeamGeometryHD geom) (static)

## VLB.VolumetricDustParticles [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single alphaAdditionalRuntime
- prop [get/set] Boolean isCulled
- prop [get] Boolean particlesAreInstantiated
- prop [get] Int32 particlesCurrentCount
- prop [get] Int32 particlesMaxCount
- field String ClassName (static)
- method ParticleSystemRenderer FindRenderer()

## VLB.VolumetricLightBeamAbstractBase [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 _INTERNAL_pluginVersion
- prop [get] Bounds bounds
- prop [get] Boolean hasGeometry
- prop [get] Light lightSpotAttached
- field String ClassName (static)
- method Void CopyPropsFrom(VolumetricLightBeamAbstractBase beamSrc, BeamProps beamProps)
- method Void GenerateGeometry()
- method BeamGeometryAbstractBase GetBeamGeometry()
- method Light GetLightSpotAttachedSlow(AttachedLightType& lightType)
- method Vector3 GetLossyScale()
- method Boolean IsScalable()
- method Void RegisterBeamGeometryGeneratedCallback(BeamGeometryGeneratedHandler callback)

## VLB.VolumetricLightBeamAbstractBase.AttachedLightType [enum]

- NoLight = 0
- OtherLight = 1
- SpotLight = 2

## VLB.VolumetricLightBeamAbstractBase.BeamGeometryGeneratedHandler [class] : System.MulticastDelegate

- method IAsyncResult BeginInvoke(VolumetricLightBeamAbstractBase beam, AsyncCallback callback, Object object)
- method Void EndInvoke(IAsyncResult result)
- method Void Invoke(VolumetricLightBeamAbstractBase beam)

## VLB.VolumetricLightBeamHD [class] : VLB.VolumetricLightBeamAbstractBase

- prop [get/set] UInt32 _INTERNAL_InstancedMaterialGroupID
- prop [get/set] AttenuationEquationHD attenuationEquation
- prop [get] Vector3 beamGlobalForward
- prop [get] Quaternion beamInternalLocalRotation
- prop [get] Vector3 beamLocalForward
- prop [get/set] BlendingMode blendingMode
- prop [get] Int32 blendingModeAsInt
- prop [get/set] Color colorFlat
- prop [get/set] Boolean colorFromLight
- prop [get/set] Gradient colorGradient
- prop [get/set] ColorMode colorMode
- prop [get] Single coneAngle
- prop [get/set] Single coneRadiusEnd
- prop [get/set] Single coneRadiusStart
- prop [get] Single coneVolume
- prop [get/set] Single fallOffEnd
- prop [get/set] Single fallOffEndMultiplier
- prop [get/set] Single fallOffStart
- prop [get/set] Single hdrpExposureWeight
- prop [get/set] Single intensity
- prop [get/set] Single intensityMultiplier
- prop [get] Boolean isNoiseEnabled
- prop [get/set] Single jitteringFactor
- prop [get/set] Int32 jitteringFrameRate
- prop [get/set] MinMaxRangeFloat jitteringLerpRange
- prop [get] Single maxGeometryDistance
- prop [get/set] Single noiseIntensity
- prop [get/set] NoiseMode noiseMode
- prop [get/set] Single noiseScaleLocal
- prop [get/set] Boolean noiseScaleUseGlobal
- prop [get/set] Vector3 noiseVelocityLocal
- prop [get/set] Boolean noiseVelocityUseGlobal
- prop [get/set] Int32 raymarchingQualityID
- prop [get/set] Int32 raymarchingQualityIndex
- prop [get/set] Boolean scalable
- prop [get/set] Single sideSoftness
- prop [get/set] Single spotAngle
- prop [get/set] Single spotAngleMultiplier
- prop [get/set] Boolean useFallOffEndFromAttachedLightSpot
- prop [get/set] Boolean useIntensityFromAttachedLightSpot
- prop [get/set] Boolean useSpotAngleFromAttachedLightSpot
- field String ClassName (static)
- method Void AssignPropertiesFromAttachedSpotLight()
- method Boolean DoesSupportSorting2D()
- method Void GenerateGeometry()
- method VolumetricCookieHD GetAdditionalComponentCookie()
- method VolumetricShadowHD GetAdditionalComponentShadow()
- method BeamGeometryAbstractBase GetBeamGeometry()
- method Single GetConeApexOffsetZ(Boolean counterApplyScaleForUnscalableBeam)
- method Dimensions GetDimensions()
- method Single GetInsideBeamFactor(Vector3 posWS)
- method Single GetInsideBeamFactorFromObjectSpacePos(Vector3 posOS)
- method Vector3 GetLossyScale()
- method Int32 GetSortingLayerID()
- method Int32 GetSortingOrder()
- method Boolean IsScalable()
- method Void SetPropertyDirty(DirtyProps flags)
- method Void UpdateAfterManualPropertyChange()

## VLB.VolumetricLightBeamHD2D [class] : VLB.VolumetricLightBeamHD

- prop [get/set] Int32 sortingLayerID
- prop [get/set] String sortingLayerName
- prop [get/set] Int32 sortingOrder
- method Void CopyPropsFrom(VolumetricLightBeamAbstractBase beamSrc, BeamProps beamProps)
- method Boolean DoesSupportSorting2D()
- method Dimensions GetDimensions()
- method Int32 GetSortingLayerID()
- method Int32 GetSortingOrder()

## VLB.VolumetricLightBeamSD [class] : VLB.VolumetricLightBeamAbstractBase

- prop [get/set] DynamicOcclusion _INTERNAL_DynamicOcclusionMode
- prop [get] DynamicOcclusion _INTERNAL_DynamicOcclusionMode_Runtime
- prop [get/set] UInt32 _INTERNAL_InstancedMaterialGroupID
- prop [get] Vector4 additionalClippingPlane
- prop [get/set] Single alphaInside
- prop [get/set] Single alphaOutside
- prop [get] Single attenuationLerpLinearQuad
- prop [get] Vector3 beamGlobalForward
- prop [get] Quaternion beamInternalLocalRotation
- prop [get] Vector3 beamLocalForward
- prop [get] Int32 blendingModeAsInt
- prop [get] Boolean canHaveMeshSkewing
- prop [get] Single coneAngle
- prop [get] Single coneApexOffsetZ
- prop [get] Vector3 coneApexPositionGlobal
- prop [get] Vector3 coneApexPositionLocal
- prop [get/set] Single coneRadiusEnd
- prop [get] Single coneVolume
- prop [get/set] Single fadeEnd
- prop [get/set] Boolean fadeEndFromLight
- prop [get/set] Single fadeOutBegin
- prop [get/set] Single fadeOutEnd
- prop [get/set] Single fadeStart
- prop [get/set] Int32 geomSegments
- prop [get/set] Int32 geomSides
- prop [get] Boolean hasMeshSkewing
- prop [get/set] Single intensityGlobal
- prop [get] Boolean isCurrentlyTrackingChanges
- prop [get] Boolean isFadeOutEnabled
- prop [get] Boolean isNoiseEnabled
- prop [get] Boolean isTilted
- prop [get] Single maxGeometryDistance
- prop [get] String meshStats
- prop [get] Int32 meshTrianglesCount
- prop [get] Int32 meshVerticesCount
- prop [get/set] Boolean noiseEnabled
- prop [get] Single raycastDistance
- prop [get] Vector3 raycastGlobalForward
- prop [get] Vector3 raycastGlobalRight
- prop [get] Vector3 raycastGlobalUp
- prop [get] Vector3 skewingLocalForwardDirectionNormalized
- prop [get/set] Int32 sortingLayerID
- prop [get/set] String sortingLayerName
- prop [get/set] Int32 sortingOrder
- prop [get/set] Boolean trackChangesDuringPlaytime
- prop [get] Boolean useFallOffEndFromAttachedLightSpot
- prop [get] Boolean useIntensityFromAttachedLightSpot
- prop [get] Boolean useSpotAngleFromAttachedLightSpot
- prop [get] ColorMode usedColorMode
- field String ClassName (static)
- method Void Generate()
- method Void GenerateGeometry()
- method BeamGeometryAbstractBase GetBeamGeometry()
- method Void GetInsideAndOutsideIntensity(Single& inside, Single& outside)
- method Single GetInsideBeamFactor(Vector3 posWS)
- method Single GetInsideBeamFactorFromObjectSpacePos(Vector3 posOS)
- method Vector3 GetLossyScale()
- method Boolean IsScalable()
- method Void RegisterOnBeamGeometryInitializedCallback(OnBeamGeometryInitialized cb)
- method Void UpdateAfterManualPropertyChange()
- method Void _INTERNAL_OnWillCameraRenderThisBeam(Camera cam)
- method Void _INTERNAL_SetDynamicOcclusionCallback(String shaderKeyword, Callback cb)

## VLB.VolumetricLightBeamSD.OnBeamGeometryInitialized [class] : System.MulticastDelegate

- method IAsyncResult BeginInvoke(AsyncCallback callback, Object object)
- method Void EndInvoke(IAsyncResult result)
- method Void Invoke()

## VLB.VolumetricLightBeamSD.OnWillCameraRenderCB [class] : System.MulticastDelegate

- method IAsyncResult BeginInvoke(Camera cam, AsyncCallback callback, Object object)
- method Void EndInvoke(IAsyncResult result)
- method Void Invoke(Camera cam)

## VLB.VolumetricShadowHD [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 _INTERNAL_LastFrameRendered
- prop [get/set] Int32 depthMapResolution
- prop [get/set] LayerMask layerMask
- prop [get/set] Single strength
- prop [get/set] ShadowUpdateRate updateRate
- prop [get/set] Boolean useOcclusionCulling
- prop [get/set] Int32 waitXFrames
- field String ClassName (static)
- field Boolean _INTERNAL_ApplyRandomFrameOffset (static)
- method Void ApplyMaterialProperties(VolumetricShadowHD instance, BeamGeometryHD geom) (static)
- method Void OnWillCameraRenderThisBeam(Camera cam, BeamGeometryHD beamGeom)
- method Void ProcessOcclusionManually()
- method Void UpdateDepthCameraProperties()

## VLB_Samples.LightGenerator [class] : UnityEngine.MonoBehaviour

- method Void Generate()


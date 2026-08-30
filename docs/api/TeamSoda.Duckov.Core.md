# TeamSoda.Duckov.Core.dll

1239 public types

## ADSAimMarker [class] : UnityEngine.MonoBehaviour

- prop [get] Single CanvasAlpha
- method Void CollectCrosshairs()
- method Void OnShoot()
- method Void SetAdsValue(Single _adsValue)
- method Void SetAimMarkerPos(Vector3 pos)
- method Void SetScatter(Single _currentScatter, Single _minScatter)

## AICharacterController [class] : UnityEngine.MonoBehaviour

- prop [get] CharacterMainControl CharacterMainControl
- prop [get] CharacterMainControl NoticeFromCharacter
- prop [get] Vector3 NoticeFromDirection
- prop [get] Vector3 NoticeFromPos
- prop [get] Boolean Sleeping
- method Void AddItemSkill(ItemSetting_Skill skill)
- method Void CheckAndAddDrugItem(Item targetItem)
- method Item GetDrugItem()
- method ItemSetting_Skill GetItemSkill(Boolean random)
- method Boolean HasPath()
- method Void Init(CharacterMainControl _characterMainControl, Vector3 patrolCenter, VoiceType voiceType, FootStepMaterialType footStepMatType)
- method Boolean IsHurt(Single timeThreshold, Int32 damageThreshold, DamageInfo& dmgInfo)
- method Boolean IsMoving()
- method Void MoveToPos(Vector3 pos)
- method Single NightReactionTimeMultiplier()
- method Void PutBackWeapon()
- method Boolean ReachedEndOfPath()
- method Void SetAimInput(Vector3 aimInput, AimTypes aimType)
- method Void SetNoticedToTarget(DamageReceiver target)
- method Void SetTarget(Transform _aimTarget)
- method Void StopMove()
- method Void TakeOutWeapon()
- method Boolean WaitingForPathResult()
- method Boolean isNoticing(Single timeThreshold)

## AIMainBrain [class] : UnityEngine.MonoBehaviour

- method Void AddCheckObsticleTask(Vector3 start, Vector3 end, Boolean thermalOn, Boolean ignoreFowBlockLayer, Action<Boolean> callback)
- method Void AddSearchTask(Vector3 center, Vector3 dir, Single searchAngle, Single searchDistance, Teams selfTeam, Boolean checkObsticle, Boolean thermalOn, Boolean ignoreFowBlockLayer, Int32 searchPickupID, Action<DamageReceiver, InteractablePickup> callback)
- method Void Awake()
- method Void MakeSound(AISound sound) (static)

## AISpecialAttachmentBase [class] : UnityEngine.MonoBehaviour

- method Void AddBuff(Buff buffPfb)
- method Void Init(AICharacterController _ai, CharacterMainControl _character)

## AISpecialAttachment_Horse [class] : AISpecialAttachmentBase

- method Void MoveToPos(Vector3 targetPos)
- method Void OnRideByCharacter(CharacterMainControl interactByCharacter, InteractableBase interactable)

## AI_PathControl [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Moving
- prop [get] Boolean ReachedEndOfPath
- prop [get] Boolean WaitingForPathResult
- method Void MoveToPos(Vector3 pos)
- method Void OnPathComplete(Path p)
- method Void Start()
- method Void StopMove()
- method Void Update()

## ATMPanel [class] : UnityEngine.MonoBehaviour

- field Int64 MaxDrawAmount (static)
- method UniTask<Boolean> Draw(Int64 amount) (static)
- method Boolean Save(Int64 amount) (static)
- method Void ShowDrawPanel()
- method Void ShowSavePanel()
- method Void ShowSelectPanel(Boolean skipHideOthers)

## ATMView [class] : Duckov.UI.View

- prop [get] ATMView Instance (static)
- method Void Show() (static)

## AccessoryBase [class] : UnityEngine.MonoBehaviour

- method Void Init(DuckovItemAgent _parentAgent, Item _selfItem)

## ActionProgressHUD [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean InProgress
- method Void Update()

## Action_Fishing [class] : CharacterActionBase

- prop [get] FishingStates FishingState
- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanEditInventory()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Void CatchButton()
- method List<Item> GetAllBaits()
- method Boolean IsReady()
- method Void Quit()

## Action_Fishing.FishingStates [enum]

- notStarted = 0
- intro = 1
- selectingBait = 2
- fishing = 3
- catching = 4
- over = 5

## Action_FishingV2 [class] : CharacterActionBase

- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanEditInventory()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Boolean IsReady()
- method Boolean IsStopable()
- method Void TryCatch()

## Action_FishingV2.FishingStates [enum]

- non = 0
- throwing = 1
- waiting = 2
- ring = 3
- cancleBack = 4
- successBack = 5
- failBack = 6

## AddToWishListButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)
- method Void ShowPage() (static)

## AimMarker [class] : UnityEngine.MonoBehaviour

- method Void SetAimMarkerPosScreenSpace(Vector3 pos)

## AimTargetFinder [class] : UnityEngine.MonoBehaviour

- method Transform Find(Boolean search, Vector3 findPoint, CharacterMainControl& foundCharacter)

## AimTypes [enum]

- normalAim = 0
- characterSkill = 1
- handheldSkill = 2

## AudioEventProxy [class] : UnityEngine.MonoBehaviour

- method Void Post()

## BaseBGMSelector [class] : UnityEngine.MonoBehaviour

- method Int32 GetIndex(String switchName)
- method Void Set(String switchName)
- method Void Set(Int32 index, Boolean showInfo, Boolean play)
- method Void SetNext()
- method Void SetPrevious()

## BaseRoomTypes [enum]

- main = 0
- restroom = 1

## BaseWallMaterialSetter [class] : UnityEngine.MonoBehaviour

- field Action<BaseWallPaper, BaseRoomTypes> SetPresetEvent (static)
- method Void SetWallPaperStatic(BaseWallPaper wallpaper, BaseRoomTypes roomType) (static)
- method Void Sync()

## BezierSpline [class] : ShapeProvider

- method OrientedPoint[] GenerateShape()
- method OrientedPoint[] GenerateShape(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Int32 subdivisions) (static)
- method Vector3 GetNormal(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Single t) (static)
- method Vector3 GetPoint(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Single t) (static)
- method Vector3 GetTangent(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Single t) (static)

## BitcoinMinerView [class] : Duckov.UI.View

- prop [get] BitcoinMinerView Instance (static)
- method Void Show() (static)

## BoundaryGenerator [class] : UnityEngine.MonoBehaviour

- method Void OnDrawGizmos()
- method Void OnPointsUpdated(Boolean OnValidate)
- method Void RemoveAllPoints()
- method Void RespawnColliders()
- method Void SetYtoZero()
- method Void UpdateColliderParameters()
- method Void UpdateColliders()

## Breakable [class] : UnityEngine.MonoBehaviour

- prop [get] String SaveKey

## BuffLayersGreaterOrEqual [class] : ItemStatsSystem.EffectFilter

- prop [get] String DisplayName

## BuffVFX [class] : UnityEngine.MonoBehaviour

- method Void AutoSetup()

## BulletCountHUD [class] : UnityEngine.MonoBehaviour

- method Void Update()

## BulletPool [class] : UnityEngine.MonoBehaviour

- method Projectile GetABullet(Projectile bulletPrefab)
- method Boolean Release(Projectile instance, Projectile prefab)

## BulletTypeHUD [class] : UnityEngine.MonoBehaviour

- method Void CloseList()
- method Void SetBulletType(Int32 typeID)
- method Void Update()

## BulletTypeSelectButton [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 BulletTypeID
- method String GetBulletName(Int32 id)
- method Void Init(Int32 id, Int32 count)
- method Void RefreshContent()
- method Void SetSelection(Boolean selected)

## BunkerDoorVisual [class] : UnityEngine.MonoBehaviour

- method Void OnEnter()
- method Void OnExit()

## CA_Attack [class] : CharacterActionBase

- prop [get] Boolean DamageDealed
- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method Boolean IsReady()

## CA_Carry [class] : CharacterActionBase

- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method Single GetWeight()
- method Boolean IsReady()

## CA_ControlOtherCharacter [class] : CharacterActionBase

- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method Boolean IsReady()
- method Boolean IsStopable()

## CA_Dash [class] : CharacterActionBase

- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method Boolean IsReady()

## CA_Interact [class] : CharacterActionBase

- prop [get] Int32 InteractIndexInGroup
- prop [get] InteractableBase InteractTarget
- prop [get] InteractableBase InteractingTarget
- prop [get] InteractableBase MasterInteractableAround
- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method Boolean IsReady()
- method Void RefreshInteractTarget()
- method Void SearchInteractableAround()
- method Void SetInteractableTarget(InteractableBase interactable)
- method Void SwitchInteractable(Int32 dir)

## CA_Reload [class] : CharacterActionBase

- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanEditInventory()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Boolean GetGunReloadable()
- method Progress GetProgress()
- method Boolean IsReady()

## CA_Skill [class] : CharacterActionBase

- prop [get] SkillBase CurrentRunningSkill
- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanEditInventory()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method CharacterSkillKeeper GetSkillKeeper(SkillTypes skillType)
- method Boolean IsReady()
- method Boolean IsSkillHasEnoughStaminaAndCD(SkillBase skill)
- method Boolean ReleaseSkill(SkillTypes skillType)
- method Void SetNextSkillType(SkillTypes skillType)
- method Boolean SetSkillOfType(SkillTypes skillType, SkillBase _skill, GameObject _bindingObject)

## CA_UseItem [class] : CharacterActionBase

- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Progress GetProgress()
- method Boolean IsReady()
- method Void SetUseItem(Item _item)

## Calibers [enum]

- AR_S = 0
- AR_B = 1
- SMG = 2
- Sniper = 3
- ShotGun = 4
- Grenade = 5

## CameraArm [class] : UnityEngine.MonoBehaviour

- field Single globalDistance (static)
- field Single globalPitch (static)
- field Single globalYaw (static)
- method Void ToggleView() (static)

## CameraMode [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Active (static)
- prop [get/set] CameraMode Instance (static)
- field Action OnCameraModeActivated (static)
- field Action<Boolean> OnCameraModeChanged (static)
- field Action OnCameraModeDeactivated (static)
- method Void Activate() (static)
- method Void Deactivate() (static)

## CameraModeController [class] : UnityEngine.MonoBehaviour

- method Void OpenFolder() (static)

## CameraShaker [class] : UnityEngine.MonoBehaviour

- method Void Shake(Vector3 velocity, CameraShakeTypes shakeType) (static)

## CameraShaker.CameraShakeTypes [enum]

- recoil = 0
- explosion = 1
- meleeAttackHit = 2

## Carriable [class] : UnityEngine.MonoBehaviour

- method Void Drop()
- method Single GetWeight()
- method Void OnCarriableUpdate(Single deltaTime)
- method Void Take(CA_Carry _carrier)

## CharacterActionBase [class] : UnityEngine.MonoBehaviour

- prop [get] Single ActionTimer
- prop [get] Boolean Running
- method ActionPriorities ActionPriority()
- method Boolean CanControlAim()
- method Boolean CanEditInventory()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Boolean IsReady()
- method Boolean IsStopable()
- method Boolean StartActionByCharacter(CharacterMainControl _character)
- method Boolean StopAction()
- method Void UpdateAction(Single deltaTime)

## CharacterActionBase.ActionPriorities [enum]

- Whatever = 0
- Reload = 1
- Attack = 2
- usingItem = 3
- Dash = 4
- Skills = 5
- Fishing = 6
- Interact = 7
- ControlOtherCharacter = 8

## CharacterAnimationControl [class] : UnityEngine.MonoBehaviour

- method Void OnAttack()

## CharacterAnimationControl_MagicBlend [class] : UnityEngine.MonoBehaviour

- method Void OnAttack()

## CharacterCreator [class] : UnityEngine.MonoBehaviour

- prop [get] CharacterMainControl characterPfb
- method UniTask<CharacterMainControl> CreateCharacter(Item itemInstance, CharacterModel modelPrefab, Vector3 pos, Quaternion rotation)
- method UniTask<Item> LoadOrCreateCharacterItemInstance(Int32 itemTypeID)

## CharacterEquipmentController [class] : UnityEngine.MonoBehaviour

- field Int32 armorHash (static)
- field Int32 backpackHash (static)
- field Int32 equipmentModelHash (static)
- field Int32 faceMaskHash (static)
- field Int32 headsetHash (static)
- field Int32 helmatHash (static)
- method Void SetItem(Item _item)

## CharacterIconTypes [enum]

- none = 0
- elete = 1
- pmc = 2
- boss = 3
- merchant = 4
- pet = 5

## CharacterInputControl [class] : UnityEngine.MonoBehaviour

- prop [get/set] CharacterInputControl Instance (static)
- method Boolean GetChangeBulletTypeWasPressed() (static)
- method Void OnCancelSkillInput(CallbackContext context)
- method Void OnCharacterSkillRelease()
- method Void OnDashInput(CallbackContext context)
- method Void OnInteractInput(CallbackContext context)
- method Void OnMouseScollerInput(CallbackContext context)
- method Void OnPlayerAdsInput(CallbackContext context)
- method Void OnPlayerMouseDelta(CallbackContext context)
- method Void OnPlayerMouseMove(CallbackContext context)
- method Void OnPlayerMoveInput(CallbackContext context)
- method Void OnPlayerRunInput(CallbackContext context)
- method Void OnPlayerStopAction(CallbackContext context)
- method Void OnPlayerSwitchItemAgent1(CallbackContext context)
- method Void OnPlayerSwitchItemAgent2(CallbackContext context)
- method Void OnPlayerSwitchItemAgentMelee(CallbackContext context)
- method Void OnPlayerTriggerInputUsingMouseKeyboard(CallbackContext context)
- method Void OnPutAwayInput(CallbackContext context)
- method Void OnQuackInput(CallbackContext context)
- method Void OnReloadInput(CallbackContext context)
- method Void OnShortCutInput3(CallbackContext context)
- method Void OnShortCutInput4(CallbackContext context)
- method Void OnShortCutInput5(CallbackContext context)
- method Void OnShortCutInput6(CallbackContext context)
- method Void OnShortCutInput7(CallbackContext context)
- method Void OnShortCutInput8(CallbackContext context)
- method Void OnStartCharacterSkillAim(CallbackContext context)
- method Void OnSwitchInteractAndBulletTypeInput(CallbackContext context)
- method Void OnSwitchWeaponInput(CallbackContext context)
- method Void OnToggleNightVisionInput(CallbackContext context)
- method Void OnToggleViewInput(CallbackContext context)
- method Void OnUIInventoryInput(CallbackContext context)
- method Void OnUIMapInput(CallbackContext context)
- method Void OnUIQuestViewInput(CallbackContext context)

## CharacterIsRunning [class] : ItemStatsSystem.EffectFilter

- prop [get] String DisplayName

## CharacterItemControl [class] : UnityEngine.MonoBehaviour

- method Boolean PickupItem(Item item)

## CharacterMainControl [class] : UnityEngine.MonoBehaviour

- prop [get] Single AdsValue
- prop [get] Single AdsWalkSpeedMultiplier
- prop [get] AimTypes AimType
- prop [get] Vector2 AnimationLocalMoveDirectionValue
- prop [get] Single AnimationMoveSpeedValue
- prop [get/set] VoiceType AudioVoiceType
- prop [get] Boolean BodyInWater
- prop [get] Boolean CanSwim
- prop [get] Single CharacterAimTurnSpeed
- prop [get] Item CharacterItem
- prop [get] Single CharacterMoveability
- prop [get] Single CharacterOriginRunSpeed
- prop [get] Single CharacterOriginWalkSpeed
- prop [get] Single CharacterRunAcc
- prop [get] Single CharacterRunSpeed
- prop [get] Single CharacterTurnSpeed
- prop [get] Single CharacterWalkAcc
- prop [get] Single CharacterWalkSpeed
- prop [get] Single ColdProtection
- prop [get] CA_ControlOtherCharacter ControlOtherCharacterAction
- prop [get] CharacterActionBase CurrentAction
- prop [get] Vector3 CurrentAimDirection
- prop [get/set] Single CurrentEnergy
- prop [get] DuckovItemAgent CurrentHoldItemAgent
- prop [get] Vector3 CurrentMoveDirection
- prop [get] Single CurrentOxygen
- prop [get] Single CurrentStamina
- prop [get] Transform CurrentUsingAimSocket
- prop [get/set] Single CurrentWater
- prop [get/set] Boolean DashCanControl
- prop [get] Single DashSpeed
- prop [get] Boolean Dashing
- prop [get] Single EnergyCostPerMin
- prop [get] CharacterEquipmentController EquipmentController
- prop [get] Transform[] ExitVehicleSockets
- prop [get] Boolean FlashLight
- prop [get] Single FoodGain
- prop [get] Boolean FootInWater
- prop [get/set] FootStepMaterialType FootStepMaterialType
- prop [get] Single GunBulletSpeedMultiplier
- prop [get] Single GunCritDamageGain
- prop [get] Single GunCritRateGain
- prop [get] Single GunDamageMultiplier
- prop [get] Single GunDistanceMultiplier
- prop [get] Single GunScatterMultiplier
- prop [get] Single GunShootSpeedMultiplier
- prop [get] Single HackRangeFactor
- prop [get] Int32 HackSkillLevel
- prop [get] Single HackTimeFactor
- prop [get] Boolean HasGasMask
- prop [get] Boolean HeadInWater
- prop [get] Single HealGain
- prop [get] Health Health
- prop [get] Single HearingAbility
- prop [get] Single HeatProtection
- prop [get] Boolean Hidden
- prop [get] Single InventoryCapacity
- prop [get] Boolean IsControllingCharacter
- prop [get] Boolean IsInAdsInput
- prop [get] Boolean IsMainCharacter
- prop [get] Boolean IsOnGround
- prop [get] CharacterMainControl Main (static)
- prop [get] Single MaxEnergy
- prop [get] Single MaxOxygen
- prop [get] Single MaxStamina
- prop [get] Single MaxWater
- prop [get] Single MaxWeight
- prop [get] Single MeleeCritDamageGain
- prop [get] Single MeleeCritRateGain
- prop [get] Single MeleeDamageMultiplier
- prop [get] Vector3 MoveInput
- prop [get] Boolean NeedToSearchTarget
- prop [get] Single NightVisionAbility
- prop [get] Single NightVisionType
- prop [get] Int32 PetCapcity
- prop [get] Single RecoilControl
- prop [get] Single ReloadSpeedGain
- prop [get] Transform RightHandSocket
- prop [get] Single RunSoundRange
- prop [get] Boolean Running
- prop [get] Single SenseRange
- prop [get] Boolean Sleeping
- prop [get] String SoundKey
- prop [get] Single SoundVisable
- prop [get] Single StaminaDrainRate
- prop [get] Single StaminaRecoverRate
- prop [get] Single StaminaRecoverTime
- prop [get] Single StormProtection
- prop [get] Boolean Swimming
- prop [get] Vector3 TargetVelocity
- prop [get] Teams Team
- prop [get] Boolean ThermalOn
- prop [get] Transform VehicleSocket
- prop [get] Vector3 Velocity
- prop [get] Single ViewAngle
- prop [get] Single ViewDistance
- prop [get] Single VisableDistanceFactor
- prop [get] Single WalkSoundRange
- prop [get] Single WaterCostPerMin
- prop [get] Single WaterEnergyRecoverMultiplier
- field Action<CharacterMainControl, DuckovItemAgent> OnMainCharacterChangeHoldItemAgentEvent (static)
- field Action<CharacterMainControl, Inventory, Int32> OnMainCharacterInventoryChangedEvent (static)
- field Action<CharacterMainControl, Slot> OnMainCharacterSlotContentChangedEvent (static)
- field Single weightThreshold_Heavy (static)
- field Single weightThreshold_Light (static)
- field Single weightThreshold_superWeight (static)
- method Void AddBuff(Buff buffPrefab, CharacterMainControl fromWho, Int32 overrideWeaponID)
- method Void AddEnergy(Single energyValue)
- method Void AddHealth(Single healthValue)
- method Void AddSubVisuals(CharacterSubVisuals subVisuals)
- method Void AddWater(Single waterValue)
- method Void AddnearByHalfObsticles(List<GameObject> objs)
- method Slot ArmorSlot()
- method Boolean Attack()
- method Slot BackpackSlot()
- method Void CallHorse()
- method Boolean CanControlAim()
- method Boolean CanEditInventory()
- method Boolean CanMove()
- method Boolean CanRun()
- method Boolean CanUseHand()
- method Boolean CancleSkill()
- method Void Carry(Carriable target)
- method Boolean ChangeHoldItem(Item item)
- method Boolean ControlOtherCharacter(CharacterMainControl target, Single timeLimit)
- method Void Dash()
- method Void DestroyAllItem()
- method Void DestroyCharacter()
- method Void DestroyItemsThatNeededToBeDestriedInBase()
- method Void DropAllItems()
- method Single EquipmentRepairLossFactor() (static)
- method Single GetAimRange()
- method Item GetArmorItem()
- method CharacterBuffManager GetBuffManager()
- method Vector3 GetCurrentAimPoint()
- method SkillBase GetCurrentRunningSkill()
- method Vector3 GetCurrentSkillAimPoint()
- method Item GetFaceMaskItem()
- method ItemAgent_Gun GetGun()
- method Boolean GetGunReloadable()
- method Item GetHelmatItem()
- method InteractableBase GetInteractableTargetToInteract()
- method ItemAgent_MeleeWeapon GetMeleeWeapon()
- method GameObject[] GetNearByHalfObsticles()
- method Slot GetSlot(Int32 hash)
- method Boolean HasBuff(Int32 buffID)
- method Boolean HasNearByHalfObsticle()
- method Slot HelmatSlot()
- method Void Hide()
- method Void Interact()
- method Void Interact(InteractableBase _target)
- method Boolean IsAiming()
- method Boolean IsNearByHalfObsticle(GameObject target)
- method Slot MeleeWeaponSlot()
- method Boolean PickupItem(Item item)
- method Void PopText(String text, Single speed)
- method Slot PrimWeaponSlot()
- method Void Quack()
- method Void RefreshInteractTarget()
- method Boolean ReleaseSkill(SkillTypes skillType)
- method Void RemoveBuff(Int32 buffID, Boolean removeOneLayer)
- method Void RemoveBuffsByTag(BuffExclusiveTags tag, Boolean removeOneLayer)
- method Void RemoveNearByHalfObsticles(List<GameObject> objs)
- method Void RemoveVisual(CharacterSubVisuals subVisuals)
- method Slot SecWeaponSlot()
- method Void SetAdsInput(Boolean _adsInput)
- method Void SetAimPoint(Vector3 _aimPoint)
- method Void SetAimType(AimTypes _aimType)
- method Void SetCharacterModel(CharacterModel _characterModel)
- method Void SetDefaultCharacterModel(CharacterModel characterModelPrefab)
- method Void SetForceMoveVelocity(Vector3 _velocity)
- method Void SetItem(Item _item)
- method Void SetMoveInput(Vector3 moveInput)
- method Void SetPosition(Vector3 pos)
- method Void SetRelatedScene(Int32 _relatedScene, Boolean setActiveByPlayerDistance)
- method Void SetRunInput(Boolean _runInput)
- method Boolean SetSkill(SkillTypes skillType, SkillBase skill, GameObject bindingObject)
- method Void SetSleeping(Boolean _sleeping)
- method Void SetTeam(Teams _team)
- method Void Show()
- method Boolean StartAction(CharacterActionBase newAction)
- method Boolean StartSkillAim(SkillTypes skillType)
- method Void StoreHoldWeaponBeforeUse()
- method Void SwitchHoldAgentInSlot(Int32 slotHash)
- method Void SwitchInteractSelection(Int32 dir)
- method Boolean SwitchToFirstAvailableWeapon()
- method Boolean SwitchToWeapon(Int32 index)
- method Void SwitchToWeaponBeforeUse()
- method Void SwitchWeapon(Int32 dir)
- method Void TickVariables(Single deltaTime, Single tickTime)
- method Void ToggleNightVision()
- method Void Trigger(Boolean trigger, Boolean triggerThisFrame, Boolean releaseThisFrame)
- method Void TriggerShootEvent(DuckovItemAgent shootByAgent)
- method Void TryCatchFishInput()
- method Boolean TryToReload(Item preferedBulletToLoad)
- method Void UpdateCold()
- method Void UpdateThirstyAndStarve()
- method Void UpdateWeightState()
- method Void UseItem(Item item)
- method Void UseStamina(Single value)
- method Single WeaponRepairLossFactor() (static)

## CharacterMainControl.WeightStates [enum]

- light = 0
- normal = 1
- heavy = 2
- superHeavy = 3
- overWeight = 4

## CharacterMainControlExtensions [class]

- method Boolean IsMainCharacter(CharacterMainControl character) (static)

## CharacterModel [class] : UnityEngine.MonoBehaviour

- prop [get] Transform ArmorSocket
- prop [get] Transform BackpackSocket
- prop [get] CustomFaceInstance CustomFace
- prop [get] Transform[] ExitVehicleSockets
- prop [get] Transform FaceMaskSocket
- prop [get] Transform HelmatSocket
- prop [get] Boolean Hidden
- prop [get] Transform LefthandSocket
- prop [get] Transform MeleeWeaponSocket
- prop [get] Transform PopTextSocket
- prop [get] Transform RightHandSocket
- prop [get] Transform VehicleSocket
- method Void AddSubVisuals(CharacterSubVisuals visuals)
- method Void ForcePlayAttackAnimation()
- method Void OnMainCharacterSetted(CharacterMainControl _characterMainControl)
- method Void RemoveVisual(CharacterSubVisuals _subVisuals)
- method Void SetFaceFromData(CustomFaceSettingData data)
- method Void SetFaceFromPreset(CustomFacePreset preset)
- method Void SyncHiddenToMainCharacter()

## CharacterModelAudioPoster [class] : UnityEngine.MonoBehaviour

- method Void PostFootStepSound()

## CharacterRandomPreset [class] : UnityEngine.ScriptableObject

- prop [get] CharacterModel CharacterModel
- prop [get] String DisplayName
- prop [get] CustomFacePreset FacePreset
- prop [get] String Name
- method UniTask<CharacterMainControl> CreateCharacterAsync(Vector3 pos, Vector3 dir, Int32 relatedScene, CharacterSpawnerGroup group, Boolean isLeader)
- method Sprite GetCharacterIcon()

## CharacterSkillKeeper [class]

- prop [get] SkillBase Skill
- method Boolean CheckSkillAndBinding()
- method Void SetSkill(SkillBase _skill, GameObject _bindingObject)

## CharacterSoundMaker [class] : UnityEngine.MonoBehaviour

- prop [get] Single runSoundDistance
- prop [get] Single walkSoundDistance
- field Action<Vector3, FootStepTypes, CharacterMainControl> OnFootStepSound (static)
- method Void PostFootStepSound()

## CharacterSoundMaker.FootStepTypes [enum]

- walkLight = 0
- walkHeavy = 1
- runLight = 2
- runHeavy = 3
- swim = 4

## CharacterSpawnerComponentBase [class] : UnityEngine.MonoBehaviour

- method Void Init(CharacterSpawnerRoot root)
- method Void StartSpawn()

## CharacterSpawnerGroup [class] : CharacterSpawnerComponentBase

- prop [get] AICharacterController LeaderAI
- method Void AddCharacterSpawned(AICharacterController _character, Boolean isLeader)
- method Void Awake()
- method Void Collect()
- method Void Init(CharacterSpawnerRoot root)
- method Void StartSpawn()

## CharacterSpawnerGroupSelector [class] : CharacterSpawnerComponentBase

- method Void Collect()
- method Void Init(CharacterSpawnerRoot root)
- method Void RandomSpawn(Int32 count)
- method Void StartSpawn()

## CharacterSpawnerRoot [class] : UnityEngine.MonoBehaviour

- prop [get] List<CharacterMainControl> CreatedCharacters
- prop [get] Int32 RelatedScene
- method Void AddCreatedCharacter(CharacterMainControl c)
- method Void SetSleeping(Boolean _sleep)
- method Void SetTeam(Teams team)

## CharacterSubVisuals [class] : UnityEngine.MonoBehaviour

- method Void AddRenderer(Renderer renderer)
- method Void SetCharacter(CharacterMainControl newCharacter)
- method Void SetRenderers()
- method Void SetRenderersHidden(Boolean _hidden)

## CharacterTouchInputControl [class] : UnityEngine.MonoBehaviour

- method Void CharacterSkillRelease(Boolean trigger)
- method Void ItemSkillRelease(Boolean trigger)
- method Void SetAdsInput(Boolean holding)
- method Void SetCharacterSkillAimInput(Vector2 axisInput, Boolean holding)
- method Void SetGunAimInput(Vector2 axisInput, Boolean holding)
- method Void SetItemSkillAimInput(Vector2 axisInput, Boolean holding)
- method Void SetMoveInput(Vector2 axisInput, Boolean holding)
- method Void SetRunInput(Boolean holding)
- method Void StartCharacterSkillAim()
- method Void StartItemSkillAim()

## CheatingManager [class] : UnityEngine.MonoBehaviour

- prop [get] CheatingManager Instance (static)
- method Void AddExp()
- method Void CheatMove()
- method Void CreateItem(Int32 id, Int32 quantity)
- method Void CreateTestVehicle()
- method Void ToggleInvincible()

## CircularExtrudeShape [class] : ShapeProvider

- method OrientedPoint[] GenerateShape()

## ColorExtensions [class]

- method String ToHexString(Color color) (static)

## ColorPunch [class] : UnityEngine.MonoBehaviour

- method Void Punch()

## Condition_CharacterLevel [class] : Duckov.Quests.Condition

- prop [get] String DisplayText
- method Boolean Evaluate()

## Condition_HasBeenToScene [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Condition_RaidDead [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Condition_TimeOfDay [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Condition_XiaoHeiZi [class] : Duckov.Quests.Condition

- prop [get] String DisplayText
- method Boolean Evaluate()

## Conditon_BuildingConstructed [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## ConfirmDialogue [class] : UnityEngine.MonoBehaviour

- method UniTask<Boolean> Execute()

## ConstructionSite [class] : UnityEngine.MonoBehaviour

- method Void RefreshGameObjects()

## ControlMindTypes [enum]

- none = 0
- brain = 1
- hack = 2

## CopySaveFileButton [class] : UnityEngine.MonoBehaviour

- method Void Apply()

## CopyTextOnClick [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## CostDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean CashOnly
- method Void ForceRefresh()
- method Void Setup(Cost cost, Int32 multiplier)

## CostTaker [class] : InteractableBase

- prop [get] ReadOnlyCollection<CostTaker> ActiveCostTakers (static)
- prop [get] Boolean CashOnly
- prop [get] Cost Cost
- method Void Register(CostTaker costTaker) (static)
- method Void SetCost(Cost cost)
- method Void Unregister(CostTaker costTaker) (static)

## CostTakerHUD_Entry [class] : UnityEngine.MonoBehaviour

- prop [get/set] CostTaker Target

## CountDownArea [class] : UnityEngine.MonoBehaviour

- prop [get] Single Progress
- prop [get] Single RemainingTime
- prop [get] Single RequiredExtrationTime

## CraftView [class] : Duckov.UI.View

- method CraftView_ListEntry GetSelection()
- method Void SetFilter(Int32 index)
- method Boolean SetSelection(CraftView_ListEntry selection)
- method Void SetupAndOpen(Predicate<CraftingFormula> predicate)
- method Void SetupAndOpenView(Predicate<CraftingFormula> predicate) (static)

## CraftViewFilterBtnEntry [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(CraftView master, FilterInfo filterInfo, Int32 index, Boolean selected)

## CraftView_ListEntry [class] : UnityEngine.MonoBehaviour

- prop [get/set] CraftingFormula Formula
- prop [get/set] CraftView Master
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(CraftView master, CraftingFormula formula)

## CraftingFormula [struct]

- prop [get] Boolean IDValid

## CraftingFormulaCollection [class] : UnityEngine.ScriptableObject

- prop [get] ReadOnlyCollection<CraftingFormula> Entries
- prop [get] CraftingFormulaCollection Instance (static)
- method Boolean TryGetFormula(String id, CraftingFormula& formula) (static)

## CraftingManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] CraftingManager Instance (static)
- prop [get] IEnumerable<String> UnlockedFormulaIDs (static)
- field Action<String> OnFormulaUnlocked (static)
- field Action<CraftingFormula, Item> OnItemCrafted (static)
- method UniTask<List<Item>> Craft(String id)
- method Void UnlockFormula(String formulaID) (static)

## CustomFaceData [class] : UnityEngine.ScriptableObject

- prop [get] CustomFacePartCollection Decorations
- prop [get] CustomFacePreset DefaultPreset
- prop [get] CustomFacePartCollection Eyebrows
- prop [get] CustomFacePartCollection Eyes
- prop [get] CustomFacePartCollection Foots
- prop [get] CustomFacePartCollection Hairs
- prop [get] CustomFacePartCollection Mouths
- prop [get] CustomFacePartCollection Tails
- prop [get] CustomFacePartCollection Wings

## CustomFaceInstance [class] : UnityEngine.MonoBehaviour

- method Void AddRendererToSubVisual(Renderer renderer)
- method CustomFaceSettingData ConvertToSaveData()
- method Void LoadFromData(CustomFaceSettingData saveData)
- method Void RefreshAll()
- method CustomFacePart SwitchPart(CustomFacePartTypes type, CustomFaceInstance parent, Int32 direction)

## CustomFaceLoadSaveButton [class] : UnityEngine.MonoBehaviour

- method Void Init(CustomFaceSaveLoad _master, Int32 _index, String name)
- method Void OnPointerClick(PointerEventData eventData)
- method Void SetSelection(Boolean selected)

## CustomFaceManager [class] : UnityEngine.MonoBehaviour

- method CustomFaceSettingData LoadMainCharacterSetting()
- method Void SaveSettingToMainCharacter(CustomFaceSettingData setting)

## CustomFacePart [class] : UnityEngine.MonoBehaviour

- prop [get] CustomFaceInstance Parent
- method Void SetInfo(CustomFacePartInfo info, CustomFaceInstance _parent)

## CustomFacePartCollection [class]

- prop [get] Int32 totalCount
- method Void AddNewCollection(CustomFacePart newPart)
- method Void Clear()
- method CustomFacePart GetNextOrPrevPrefab(Int32 currentID, Int32 direction)
- method CustomFacePart GetPartPrefab(Int32 id)
- method Void Sort()

## CustomFacePartTypes [enum]

- hair = 0
- eye = 1
- eyebrow = 2
- mouth = 3
- tail = 4
- foot = 5
- wing = 6

## CustomFacePartUtility [class]

- prop [get] CustomFacePart PartInstance
- method Void ChangePart(CustomFacePart newInstance)
- method Int32 GetCurrentPartID()
- method String GetCurrentPartName()
- method Void RefreshThisPart()

## CustomFaceSaveLoad [class] : UnityEngine.MonoBehaviour

- method Void LoadDefault()
- method Void SaveDataToCurrentSlot()
- method Void SetSlotAndLoad(Int32 slot)

## CustomFaceSettingData [struct]

- method String DataToJson()
- method Boolean JsonToData(String jsonData, CustomFaceSettingData& data) (static)

## CustomFaceSlider [class] : UnityEngine.MonoBehaviour

- prop [get] Single Value
- method Void Init(Single minValue, Single maxValue, CustomFaceUI _master, String nameKey)
- method Void SetMinMaxValue(Single min, Single max)
- method Void SetNameKey(String _nameKey)
- method Void SetValue(Single value)

## CustomFaceTabs [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)
- method Void SetSelectVisual(Boolean selected)

## CustomFaceUI [class] : UnityEngine.MonoBehaviour

- prop [get] CustomFaceUI ActiveView (static)
- method Void OnDrag(PointerEventData eventData)
- method Void RandomPreset()
- method Void RefreshInfos()
- method Void SaveToMainCharacter()
- method Void SelectTab(CustomFaceTabs tab)
- method Void SetDirty()
- method Void SetFace(CustomFaceInstance face)
- method String SwitchPart(CustomFacePartTypes type, Int32 direction)

## CustomFaceUIColorPicker [class] : UnityEngine.MonoBehaviour

- prop [get] Color CurrentColor
- prop [get] IEnumerable<Color> colors
- method Void Init(CustomFaceUI _master, String titleKey)
- method Void SetColor(Color _color)

## CustomFaceUIColorPickerButton [class] : UnityEngine.MonoBehaviour

- prop [get] Color Color
- method Void Init(CustomFaceUIColorPicker _master, Color _color)
- method Void SetSelection(Boolean selected)

## CustomFaceUISwitch [class] : UnityEngine.MonoBehaviour

- method Void Init(CustomFaceUI _master, CustomFacePartTypes partType, String title)
- method Void SetName(String name)

## CutScene [class] : UnityEngine.MonoBehaviour

- method Void MarkPlayed()
- method Void Play()
- method Void PlayIfNessisary()

## CutScene.PlayTiming [enum]

- Start = 0
- OnTriggerEnter = 2
- Manual = 3

## DamageInfo [struct]

- method Void AddElementFactor(ElementTypes _type, Single _factor)
- method String GenerateDescription()

## DamageReceiver [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean IsDead
- prop [get] Boolean IsMainCharacter
- prop [get] Teams Team
- method Boolean AddBuff(Buff buffPfb, CharacterMainControl fromWho)
- method Boolean Hurt(DamageInfo damageInfo)
- method Void OnDead(DamageInfo dmgInfo)

## DamageTypes [enum]

- normal = 0
- realDamage = 2

## DarkRoomFade [class] : UnityEngine.MonoBehaviour

- method Void SetRenderers(Boolean enable)
- method Void SetRenderersEnable(Boolean enable) (static)
- method Void StartFade()

## DebugUISetup [class] : UnityEngine.MonoBehaviour

- method Void Setup()

## Debugging.InstantiateTiming [class] : UnityEngine.MonoBehaviour

- method Void InstantiatePrefab()

## Debugging.InventorySaveLoad [class] : UnityEngine.MonoBehaviour

- method Void BeginLoad()
- method UniTask Load()
- method Void Save()

## DecomposeDatabase [class] : UnityEngine.ScriptableObject

- prop [get] DecomposeDatabase Instance (static)
- method Boolean CanDecompose(Int32 itemTypeID) (static)
- method Boolean CanDecompose(Item item) (static)
- method UniTask<Boolean> Decompose(Item item, Int32 count) (static)
- method DecomposeFormula GetDecomposeFormula(Int32 itemTypeID) (static)
- method DecomposeFormula GetFormula(Int32 itemTypeID)
- method Void RebuildDictionary()
- method Void SetData(List<DecomposeFormula> formulas)

## DecomposeSlider [class] : UnityEngine.MonoBehaviour

- prop [get/set] Int32 Value
- method Void SetMinMax(Int32 min, Int32 max)

## DeleteSaveDataButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)

## DevCam [class] : UnityEngine.MonoBehaviour

- field Boolean devCamOn (static)

## DialogueBubbleProxy [class] : UnityEngine.MonoBehaviour

- method Void Pop()
- method Void Pop(String text, Single speed)

## Dialogues.DialogueUI [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Active (static)
- prop [get/set] DialogueUI instance (static)
- method Void Confirm()
- method UniTask DoSubtitle(SubtitlesRequestInfo info)
- method Void HideTextFadeGroup() (static)
- method Void OnPointerClick(PointerEventData eventData)

## Dialogues.DialogueUIChoice [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 Index
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)

## Dialogues.LocalizedStatement [class]

- prop [get/set] AudioClip audio
- prop [get/set] String meta
- prop [get] String text
- prop [get/set] String textKey

## Dialogues.LocalizedStatementNode [class] : NodeCanvas.DialogueTrees.DTNode

- prop [get] Boolean requireActorSelection

## Dialogues.LocalizedStatementSequence [class] : NodeCanvas.DialogueTrees.DTNode

- prop [get] Boolean requireActorSelection

## DigitInputPanel [class] : UnityEngine.MonoBehaviour

- prop [get] Int64 Value
- method Void Max()
- method Void Setup(Int64 value, Func<Int64> maxFunc)

## DisableCameraOffset [class] : OptionsProviderBase

- prop [get] String Key
- field Boolean disableCameraOffset (static)
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## Door [class] : UnityEngine.MonoBehaviour

- prop [get] InteractableBase Interact
- prop [get] Boolean IsOpen
- prop [get] Boolean NoRequireItem
- method Void Close()
- method Void ForceSetClosed(Boolean _closed, Boolean triggerEvent)
- method Void Open()
- method Void SetParts(List<DoorTransformInfo> transforms)
- method Void Switch()

## DragHandler [class] : UnityEngine.MonoBehaviour

- method Void OnDrag(PointerEventData eventData)

## Duckov.Achievements.AchievementDatabase [class] : UnityEngine.ScriptableObject

- prop [get] AchievementDatabase Instance (static)
- method Boolean TryGetAchievementData(String id, Achievement& achievement) (static)

## Duckov.Achievements.AchievementDatabase.Achievement [class]

- prop [get] String Description
- prop [get/set] String DescriptionKey
- prop [get] String DisplayName
- prop [get/set] String id
- prop [get/set] String overrideDescriptionKey
- prop [get/set] String overrideDisplayNameKey

## Duckov.Achievements.AchievementManager [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean CanUnlockAchievement (static)
- prop [get] AchievementManager Instance (static)
- prop [get] List<String> UnlockedAchievements
- method Boolean IsIDValid(String id) (static)
- method Void Unlock(String id)
- method Void UnlockEndowmentAchievement(EndowmentIndex index) (static)

## Duckov.Achievements.StatisticsManager [class] : UnityEngine.MonoBehaviour

- method Void Add(String key, Int64 value) (static)

## Duckov.Aquariums.Aquarium [class] : UnityEngine.MonoBehaviour

- method Void Loot()

## Duckov.Aquariums.IAquariumContent [interface]

- method Void Setup(Aquarium master)

## Duckov.AudioManager [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Initialized (static)
- prop [get] AudioManager Instance (static)
- prop [get] Boolean IsStingerPlaying (static)
- prop [get] Boolean PlayingBGM (static)
- field String path_building_built (static)
- field String path_gun_unload (static)
- field String path_reload_fmt_soundkey (static)
- field String path_shoot_fmt_gunkey (static)
- field String path_stinger_fmt_key (static)
- field String path_task_finished (static)
- method Bus GetBus(String name) (static)
- method String GetState(String stateGroup) (static)
- method Nullable<EventInstance> PlayBGM(String name) (static)
- method Nullable<EventInstance> PlayCustomBGM(String filePath, Boolean loop) (static)
- method Void PlayPutItemSFX(Item item, Boolean pickup) (static)
- method Void PlayStringer(String key) (static)
- method Nullable<EventInstance> Post(String eventName) (static)
- method Nullable<EventInstance> Post(String eventName, GameObject gameObject) (static)
- method Nullable<EventInstance> Post(String eventName, Vector3 position) (static)
- method Nullable<EventInstance> PostCustomSFX(String filePath, GameObject gameObject, Boolean loop) (static)
- method Void PostHitMarker(Boolean crit) (static)
- method Void PostKillMarker(Boolean crit) (static)
- method Void SetState(String stateGroup, String state) (static)
- method Void SetVoiceType(GameObject gameObject, VoiceType voiceType) (static)
- method Void StopAll(GameObject gameObject, STOP_MODE mode) (static)
- method Void StopBGM() (static)
- method Boolean TryCreateEventInstance(String eventPath, EventInstance& eventInstance) (static)

## Duckov.AudioManager.Bus [class]

- prop [get] Boolean Dirty
- prop [get/set] Boolean Mute
- prop [get] String Name
- prop [get/set] Single Volume
- method Void Apply()

## Duckov.AudioManager.FootStepMaterialType [enum]

- organic = 0
- mech = 1
- danger = 2
- noSound = 3
- horse = 4

## Duckov.AudioManager.VoiceType [enum]

- Duck = 0
- Robot = 1
- Wolf = 2
- Chicken = 3
- Crow = 4
- Eagle = 5
- coalball = 6

## Duckov.AudioObject [class] : UnityEngine.MonoBehaviour

- prop [get/set] VoiceType VoiceType
- method Nullable<EventInstance> Post(String eventName, Boolean doRelease)
- method Nullable<EventInstance> PostCustomSFX(String filePath, Boolean doRelease, Boolean loop)
- method Nullable<EventInstance> PostFile(String eventPath, String filePath, Boolean doRelease)
- method Nullable<EventInstance> PostQuak(String soundKey)
- method Void Stop(String eventName, STOP_MODE mode)

## Duckov.Bitcoins.BitcoinMiner [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean CreatingCoin
- prop [get] Double HoursPerCoin
- prop [get/set] Boolean Initialized
- prop [get/set] BitcoinMiner Instance (static)
- prop [get] Boolean IsInventoryFull
- prop [get] Item Item
- prop [get/set] Boolean Loading
- prop [get] Single NormalizedProgress
- prop [get] Double Performance
- prop [get] TimeSpan RemainingTime
- prop [get] TimeSpan TimePerCoin
- prop [get] Double WorkPerSecond
- field String SaveKey (static)

## Duckov.BlackMarkets.BlackMarket [class] : UnityEngine.MonoBehaviour

- prop [get] ReadOnlyCollection<DemandSupplyEntry> Demands
- prop [get/set] BlackMarket Instance (static)
- prop [get] Int32 MaxRefreshChance
- prop [get/set] Int32 RefreshChance
- prop [get] TimeSpan RemainingTimeBeforeRefresh
- prop [get] ReadOnlyCollection<DemandSupplyEntry> Supplies
- method UniTask<Boolean> Buy(DemandSupplyEntry entry)
- method Void NotifyMaxRefreshChanceChanged() (static)
- method Void PayAndRegenerate()
- method UniTask<Boolean> Sell(DemandSupplyEntry entry)

## Duckov.BlackMarkets.BlackMarket.DemandSupplyEntry [class]

- prop [get] Cost BuyCost
- prop [get] String ItemDisplayName
- prop [get] Int32 ItemID
- prop [get] Int32 Remaining
- prop [get] Cost SellCost
- prop [get] Int32 TotalPrice

## Duckov.BlackMarkets.BlackMarket.OnRequestMaxRefreshChanceEventContext [class]

- prop [get] Int32 Value
- method Void Add(Int32 count)

## Duckov.BlackMarkets.BlackMarket.OnRequestRefreshTimeFactorEventContext [class]

- prop [get] Single Value
- method Void Add(Single count)

## Duckov.BlackMarkets.UI.BlackMarketView [class] : Duckov.UI.View

- prop [get] BlackMarketView Instance (static)
- prop [get/set] BlackMarket Target
- method Void Show(Mode mode) (static)

## Duckov.BlackMarkets.UI.BlackMarketView.Mode [enum]

- None = 0
- Demand = 1
- Supply = 2

## Duckov.BlackMarkets.UI.DemandPanel [class] : UnityEngine.MonoBehaviour

- prop [get/set] BlackMarket Target

## Duckov.BlackMarkets.UI.DemandPanel_Entry [class] : UnityEngine.MonoBehaviour

- prop [get/set] DemandSupplyEntry Target
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnDealButtonClicked()

## Duckov.BlackMarkets.UI.SupplyPanel [class] : UnityEngine.MonoBehaviour

- prop [get/set] BlackMarket Target

## Duckov.BlackMarkets.UI.SupplyPanel_Entry [class] : UnityEngine.MonoBehaviour

- prop [get/set] DemandSupplyEntry Target
- method Void NotifyPooled()
- method Void NotifyReleased()

## Duckov.Buffs.Buff [class] : UnityEngine.MonoBehaviour

- prop [get] CharacterMainControl Character
- prop [get/set] Int32 CurrentLayers
- prop [get] Single CurrentLifeTime
- prop [get] String Description
- prop [get] Boolean DisplayInExtraView
- prop [get] String DisplayName
- prop [get] String DisplayNameKey
- prop [get] BuffExclusiveTags ExclusiveTag
- prop [get] Int32 ExclusiveTagPriority
- prop [get] Boolean Hide
- prop [get/set] Int32 ID
- prop [get] Sprite Icon
- prop [get] Boolean IsOutOfTime
- prop [get] Boolean LimitedLifeTime
- prop [get] Int32 MaxLayers
- prop [get] Single RemainingTime
- prop [get] Single TotalLifeTime

## Duckov.Buffs.Buff.BuffExclusiveTags [enum]

- NotExclusive = 0
- Bleeding = 1
- Starve = 2
- Thirsty = 3
- Weight = 4
- Poison = 5
- Pain = 6
- Electric = 7
- Burning = 8
- Space = 9
- StormProtection = 10
- Nauseous = 11
- Stun = 12
- Ghost = 13
- Freeze = 14
- Tagilla = 15
- BlueBall = 16
- RedBall = 17

## Duckov.Buffs.CharacterBuffManager [class] : UnityEngine.MonoBehaviour

- prop [get] ReadOnlyCollection<Buff> Buffs
- prop [get] CharacterMainControl Master
- method Void AddBuff(Buff buffPrefab, CharacterMainControl fromWho, Int32 overrideWeaponID)
- method Buff GetBuffByTag(BuffExclusiveTags tag)
- method Boolean HasBuff(Int32 buffID)
- method Void RemoveBuff(Int32 buffID, Boolean removeOneLayer)
- method Void RemoveBuff(Buff toRemove, Boolean oneLayer)
- method Void RemoveBuffsByTag(BuffExclusiveTags buffTag, Boolean removeOneLayer)

## Duckov.Buildings.Building [class] : UnityEngine.MonoBehaviour

- prop [get] String Description
- prop [get/set] String DescriptionKey
- prop [get] Vector2Int Dimensions
- prop [get] String DisplayName
- prop [get/set] String DisplayNameKey
- prop [get] Int32 GUID
- prop [get] String ID
- method Void DrawGizmos()
- method String GetDisplayName(String id) (static)
- method Vector3 GetOffset(BuildingRotation rotation)

## Duckov.Buildings.BuildingArea [class] : UnityEngine.MonoBehaviour

- prop [get] BuildingAreaData AreaData
- prop [get] String AreaID
- prop [get] Vector2Int LowerLeftCorner
- prop [get] Plane Plane
- prop [get] Vector2Int Size
- method Vector2Int CursorToCoord(Vector3 point, Vector2Int dimensions, BuildingRotation rotation)
- method Void DrawGizmos()
- method Boolean IsPlacementWithinRange(Vector2Int dimensions, BuildingRotation rotation, Vector2Int coord)
- method Void RepaintAll()

## Duckov.Buildings.BuildingBuyAndPlaceResults [enum]

- Succeed = 0
- NoReferences = 1
- InvalidBuildingInfo = 2
- PaymentFailure = 3
- ReachedAmountLimit = 4

## Duckov.Buildings.BuildingDataCollection [class] : UnityEngine.ScriptableObject

- prop [get] ReadOnlyCollection<BuildingInfo> Infos
- prop [get] BuildingDataCollection Instance (static)

## Duckov.Buildings.BuildingInfo [struct]

- prop [get] Int32 CurrentAmount
- prop [get] String Description
- prop [get] String DescriptionKey
- prop [get] Vector2Int Dimensions
- prop [get] String DisplayName
- prop [get] String DisplayNameKey
- prop [get] Building Prefab
- prop [get] Boolean ReachedAmountLimit
- prop [get] Int32 TokenAmount
- prop [get] Boolean Valid
- method String GetDisplayName(String id) (static)

## Duckov.Buildings.BuildingManager [class] : UnityEngine.MonoBehaviour

- prop [get] List<BuildingAreaData> Areas
- prop [get/set] BuildingManager Instance (static)
- method Boolean Any(Func<BuildingData, Boolean> predicate) (static)
- method Boolean Any(String id, Boolean includeTokens) (static)
- method BuildingAreaData GetArea(String id)
- method Int32 GetBuildingAmount(String id) (static)
- method BuildingInfo GetBuildingInfo(String id) (static)
- method Vector2Int[] GetOccupyingCoords(Vector2Int dimensions, BuildingRotation rotations, Vector2Int coord) (static)
- method BuildingAreaData GetOrCreateArea(String id)
- method Int32 GetTokenAmount(String id)

## Duckov.Buildings.BuildingManager.BuildingAreaData [class]

- prop [get] String AreaID
- prop [get] List<BuildingData> Buildings
- method Boolean Add(String buildingID, BuildingRotation rotation, Vector2Int coord, Int32 guid)
- method Boolean Any(String buildingID)
- method Boolean Collide(Vector2Int dimensions, BuildingRotation rotation, Vector2Int coord)
- method HashSet<Vector2Int> GetAllOccupiedCoords()
- method BuildingData GetBuildingAt(Vector2Int coord)
- method Boolean Remove(Int32 buildingGUID)
- method Boolean Remove(BuildingData building)

## Duckov.Buildings.BuildingManager.BuildingData [class]

- prop [get] Vector2Int Coord
- prop [get] Vector2Int Dimensions
- prop [get] Int32 GUID
- prop [get] String ID
- prop [get] BuildingInfo Info
- prop [get] BuildingRotation Rotation

## Duckov.Buildings.BuildingRotation [enum]

- Zero = 0
- Quarter = 1
- Half = 2
- ThreeQuarter = 3

## Duckov.Buildings.Showcase [class] : UnityEngine.MonoBehaviour

- prop [get] String DisplayName
- prop [get] String DisplayNameKey
- prop [get] String ID
- prop [get] Inventory Inventory
- prop [get] Item Item
- prop [get] SlotCollection Slots
- prop [get] CinemachineVirtualCamera Vcam
- method Void ShowUI()

## Duckov.Buildings.UI.BuilderView [class] : Duckov.UI.View

- prop [get] BuilderView Instance (static)
- method Void BeginDestroying()
- method Void BeginPlacing(BuildingInfo info)
- method Void OnPointerClick(PointerEventData eventData)
- method Void SetupAndShow(BuildingArea targetArea)
- method Void Show(BuildingArea target) (static)
- method Boolean TryGetPointingCoord(Vector2Int& coord, Building previewBuilding)

## Duckov.Buildings.UI.BuildingBtnEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean CostEnough
- prop [get] BuildingInfo Info

## Duckov.Buildings.UI.BuildingContextMenu [class] : UnityEngine.MonoBehaviour

- prop [get/set] Building Target
- method Void Hide()
- method Void Setup(Building target)

## Duckov.Buildings.UI.BuildingContextMenuEntry [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## Duckov.Buildings.UI.BuildingSelectionPanel [class] : UnityEngine.MonoBehaviour

- method BuildingInfo[] GetBuildingsToDisplay() (static)
- method Void Refresh()
- method Void Show()

## Duckov.Buildings.UI.GridDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] GridDisplay Instance (static)
- method Void Close() (static)
- method Void HideGrid() (static)
- method Void HidePreview() (static)
- method UniTask SetGridShowHide(Boolean show, AnimationCurve curve, Single duration) (static)
- method Void Setup(BuildingArea buildingArea)
- method Void ShowGrid() (static)

## Duckov.CheatMode [class]

- prop [get/set] Boolean Active (static)
- method Void Activate() (static)
- method Void Deactivate() (static)

## Duckov.Consoles.DCommand [class] : UnityEngine.ScriptableObject

- prop [get] String CommandWord
- method String Execute(DConsole console, String[] args)

## Duckov.Consoles.DConsole [class] : UnityEngine.MonoBehaviour

- prop [get/set] DConsole Instance (static)
- field Dictionary<String, IDCommand> commandDic (static)
- method Void Echo(String result)
- method Void EchoWarning(String message)
- method Void Process(String command)
- method Void RegisterCommand(IDCommand command) (static)

## Duckov.Consoles.Give [class] : Duckov.Consoles.DCommand

- prop [get] String CommandWord
- method String Execute(DConsole console, String[] args)

## Duckov.Consoles.IDCommand [interface]

- prop [get] String CommandWord
- method String Execute(DConsole console, String[] args)

## Duckov.CreditsUtility.CreditsDisplay [class] : UnityEngine.MonoBehaviour

- method Transform PopTransform()
- method Void PushTransform(Transform trans)

## Duckov.CreditsUtility.CreditsLexer [class]

- method IEnumerator<Token> GetEnumerator()
- method Token Next()
- method Void Reset()

## Duckov.CreditsUtility.EmptyEntry [class] : UnityEngine.MonoBehaviour

- method Void Setup(String[] args)

## Duckov.CreditsUtility.HorizontalEntry [class] : UnityEngine.MonoBehaviour

- method Void Setup(String[] args)

## Duckov.CreditsUtility.TokenType [enum]

- End = 0
- String = 1
- Instructor = 2
- EmptyLine = 3
- Comment = 4
- Invalid = -1

## Duckov.CreditsUtility.VerticalEntry [class] : UnityEngine.MonoBehaviour

- method Void SetLayoutSpacing(Single spacing)
- method Void SetPreferredWidth(Single width)
- method Void Setup(String[] args)

## Duckov.Crops.Crop [class] : UnityEngine.MonoBehaviour

- prop [get] CropData Data
- prop [get] String DisplayName
- prop [get] CropInfo Info
- prop [get] Single Progress
- prop [get] TimeSpan RemainingTime
- prop [get] Boolean Ripen
- prop [get] Boolean Watered
- method Void DestroyCrop()
- method Boolean Harvest()
- method Void Initialize(Garden garden, CropData data)
- method Void InitializeNew(Garden garden, String id, Vector2Int coord)
- method Void Water()

## Duckov.Crops.Crop.CropEvent [enum]

- Plant = 0
- Water = 1
- Ripen = 2
- Harvest = 3
- BeforeDestroy = 4

## Duckov.Crops.CropData [struct]

- prop [get] TimeSpan GrowTime
- prop [get/set] DateTime LastUpdateDateTime
- prop [get] ProductRanking Ranking

## Duckov.Crops.CropDatabase [class] : UnityEngine.ScriptableObject

- prop [get] CropDatabase Instance (static)
- method Nullable<CropInfo> GetCropInfo(String id) (static)

## Duckov.Crops.CropInfo [struct]

- prop [get] String DisplayName
- prop [get] TimeSpan GrowTime
- method Int32 GetProduct(ProductRanking ranking)

## Duckov.Crops.Garden [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean AutoWater
- prop [get] PrefabPool<CellDisplay> CellPool
- prop [get] String GardenID
- prop [get/set] Crop Item
- prop [get] String SaveKey
- prop [get/set] Vector2Int Size
- field List<IGardenAutoWaterProvider> autoWaters (static)
- field Dictionary<String, Garden> gardens (static)
- field List<IGardenSizeAdder> sizeAdders (static)
- method Vector3 CoordToLocalPosition(Vector2Int coord)
- method Vector3 CoordToWorldPosition(Vector2Int coord)
- method Boolean IsCoordOccupied(Vector2Int coord)
- method Boolean IsCoordValid(Vector2Int coord)
- method Void Load()
- method Boolean Plant(Vector2Int coord, String cropID)
- method Void Save()
- method Void SetSize(Int32 x, Int32 y)
- method Void Water(Vector2Int coord)
- method Vector2Int WorldPositionToCoord(Vector3 wPos)

## Duckov.Crops.IGardenAutoWaterProvider [interface]

- method Boolean TakeEffect(String gardenID)

## Duckov.Crops.IGardenSizeAdder [interface]

- method Vector2Int GetValue(String gardenID)

## Duckov.Crops.ProductRanking [enum]

- Poor = 0
- Normal = 1
- Good = 2

## Duckov.Crops.SeedInfo [struct]

- method String GetRandomCropID()

## Duckov.Crops.UI.GardenView [class] : Duckov.UI.View

- prop [get/set] Boolean Hovering
- prop [get/set] Vector2Int HoveringCoord
- prop [get/set] Crop HoveringCrop
- prop [get/set] GardenView Instance (static)
- prop [get/set] Int32 PlantingSeedTypeID
- prop [get/set] ItemMetaData SeedMeta
- prop [get/set] Boolean SeedSelected
- prop [get/set] Garden Target
- prop [get/set] ToolType Tool
- prop [get] String ToolDisplayName
- method CursorData GetCursorData()
- method Boolean IsBusy()
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void OnPointerMove(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)
- method Void SelectSeed(Int32 seedTypeID)
- method Void SetTool(ToolType action)
- method Void Show(Garden target) (static)

## Duckov.Crops.UI.GardenView.ToolType [enum]

- None = 0
- Plant = 1
- Harvest = 2
- Water = 3
- Destroy = 4

## Duckov.Crops.UI.GardenViewCropSelector [class] : UnityEngine.MonoBehaviour

- method Void Hide()
- method Void Show()

## Duckov.Crops.UI.GardenViewToolButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## Duckov.CursorData [class]

- prop [get] Texture2D texture

## Duckov.CursorManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] CursorManager Instance (static)
- method Void MSetCursor(CursorData data)
- method Void MSetDefaultCursor()
- method Void Register(ICursorDataProvider dataProvider) (static)
- method Boolean Unregister(ICursorDataProvider dataProvider) (static)

## Duckov.CustomOptions.CustomOptionsPanel [class] : UnityEngine.MonoBehaviour

- field Action<RectTransform> OnPanelEnabled (static)

## Duckov.CustomerService.QuestionairButton [class] : UnityEngine.MonoBehaviour

- method String GenerateQuestionair()
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.DeadBodyManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] DeadBodyManager Instance (static)

## Duckov.DeathLotteries.CardDisplay [class] : UnityEngine.MonoBehaviour

- method Void Flip()
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void OnPointerMove(PointerEventData eventData)
- method Void SetFacing(Boolean facingFront, Boolean skipAnimation)

## Duckov.DeathLotteries.DeathLottery [class] : UnityEngine.MonoBehaviour

- prop [get] OptionalCosts[] Costs
- prop [get] UInt32 CurrentDeadCharacterToken (static)
- prop [get] Status CurrentStatus
- prop [get] List<Item> ItemInstances
- prop [get] Boolean Loading
- prop [get] Int32 MaxChances
- prop [get] Int32 RemainingChances
- field Int32 MaxCandidateCount (static)
- method Void RequestUI()
- method UniTask<Boolean> Select(Int32 index, Cost payWhenSucceed)

## Duckov.DeathLotteries.DeathLottery.Status [struct]

- prop [get] Int32 SelectedCount

## Duckov.DeathLotteries.DeathLotteryCard [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 Index
- method Void NotifyFacing(Boolean uncovered)
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void Setup(DeathLotteryVIew master, Int32 index)

## Duckov.DeathLotteries.DeathLotteryVIew [class] : Duckov.UI.View

- prop [get] Int32 RemainingChances
- prop [get] DeathLottery Target

## Duckov.EXPManager [class] : UnityEngine.MonoBehaviour

- prop [get] Int64 CachedExp (static)
- prop [get/set] Int64 EXP (static)
- prop [get] EXPManager Instance (static)
- prop [get] Int32 Level (static)
- field Action<Int64> onExpChanged (static)
- field Action<Int32, Int32> onLevelChanged (static)
- method Boolean AddExp(Int32 amount) (static)
- method Object GenerateSaveData()
- method ValueTuple<Int64, Int64> GetLevelExpRange(Int32 level)
- method Int32 LevelFromExp(Int64 exp)
- method Void SetupSaveData(Object data)

## Duckov.Economy.Cost [struct]

- prop [get] Boolean Enough
- prop [get] Boolean IsFree
- prop [get] Boolean TaskPending (static)
- method Cost FromString(String costDescription) (static)
- method Boolean Pay(Boolean accountAvaliable, Boolean cashAvaliable)

## Duckov.Economy.EconomyManager [class] : UnityEngine.MonoBehaviour

- prop [get] Int64 Cash (static)
- prop [get/set] EconomyManager Instance (static)
- prop [get] String ItemUnlockNotificationTextMainFormat (static)
- prop [get] String ItemUnlockNotificationTextSubFormat (static)
- prop [get/set] Int64 Money (static)
- prop [get] ReadOnlyCollection<Int32> UnlockedItemIds
- field Int32 CashItemID (static)
- method Boolean Add(Int64 amount) (static)
- method Void ConfirmUnlock(Int32 itemTypeID) (static)
- method Object GenerateSaveData()
- method Boolean IsEnough(Cost cost, Boolean accountAvaliable, Boolean cashAvaliale) (static)
- method Boolean IsUnlocked(Int32 itemTypeID) (static)
- method Boolean IsWaitingForUnlockConfirm(Int32 itemTypeID) (static)
- method Boolean Pay(Cost cost, Boolean accountAvaliable, Boolean cashAvaliale) (static)
- method Void SetupSaveData(Object rawData)
- method Void Unlock(Int32 itemTypeID, Boolean needConfirm, Boolean showUI) (static)

## Duckov.Economy.StockShop [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean AccountAvaliable
- prop [get] Boolean Busy
- prop [get] String DisplayName
- prop [get] String MerchantID
- prop [get] TimeSpan NextRefreshETA
- prop [get] String OpinionKey
- prop [get] String PurchaseNotificationTextFormat
- prop [get] TimeSpan TimeSinceLastRefresh
- method UniTask<Boolean> Buy(Int32 itemTypeID, Int32 amount)
- method Int32 ConvertPrice(Item item, Boolean selling)
- method Object GenerateSaveData()
- method Item GetItemInstanceDirect(Int32 typeID)
- method Void SetupSaveData(Object dataRaw)
- method Void ShowUI()

## Duckov.Economy.StockShop.Entry [class]

- prop [get/set] Int32 CurrentStock
- prop [get] Boolean ForceUnlock
- prop [get] Int32 ItemTypeID
- prop [get] Int32 MaxStock
- prop [get] Single Possibility
- prop [get] Single PriceFactor
- prop [get/set] Boolean Show

## Duckov.Economy.UI.StockShopItemEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Entry Target
- method Item GetItem()
- method Boolean IsUnlocked()
- method Void OnPointerClick(PointerEventData eventData)
- method Void Punch()

## Duckov.Economy.UI.StockShopView [class] : Duckov.UI.View

- prop [get] StockShopView Instance (static)
- prop [get] StockShop Target
- method StockShopItemEntry GetSelection()
- method Boolean SetSelection(StockShopItemEntry selection)

## Duckov.Endowment.EndowmentEntry [class] : UnityEngine.MonoBehaviour

- prop [get] String Description
- prop [get] String DescriptionAndEffects
- prop [get] String DisplayName
- prop [get] Sprite Icon
- prop [get] EndowmentIndex Index
- prop [get] ModifierDescription[] Modifiers
- prop [get] String RequirementText
- prop [get] Boolean UnlockedByDefault
- method Void Activate()
- method Void Deactivate()

## Duckov.Endowment.EndowmentEntry.ModifierDescription [struct]

- prop [get] String DescriptionText

## Duckov.Endowment.EndowmentIndex [enum]

- None = 0
- Surviver = 1
- Porter = 2
- Berserker = 3
- Marksman = 4
- _Count = 5

## Duckov.Endowment.EndowmentManager [class] : UnityEngine.MonoBehaviour

- prop [get] EndowmentEntry Current (static)
- prop [get] EndowmentIndex CurrentIndex (static)
- prop [get] ReadOnlyCollection<EndowmentEntry> Entries
- prop [get] EndowmentManager Instance (static)
- prop [get/set] EndowmentIndex SelectedIndex (static)
- field Action<EndowmentIndex> OnEndowmentChanged (static)
- field Action<EndowmentIndex> OnEndowmentUnlock (static)
- method Boolean GetEndowmentUnlocked(EndowmentIndex index) (static)
- method Boolean UnlockEndowment(EndowmentIndex index) (static)

## Duckov.Endowment.UI.EndowmentSelectionEntry [class] : UnityEngine.MonoBehaviour

- prop [get] String Description
- prop [get] String DescriptionAndEffects
- prop [get] String DisplayName
- prop [get] EndowmentIndex Index
- prop [get] Boolean Locked
- prop [get/set] Boolean Selected
- prop [get/set] EndowmentEntry Target
- prop [get] Boolean Unlocked
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(EndowmentEntry target)

## Duckov.Endowment.UI.EndowmentSelectionPanel [class] : Duckov.UI.View

- prop [get/set] EndowmentSelectionEntry Selection
- method UniTask Execute()
- method Void Setup()
- method Void Show() (static)

## Duckov.GameMetaData [class] : UnityEngine.ScriptableObject

- prop [get] Boolean BloodFxOn (static)
- prop [get] GameMetaData Instance (static)
- prop [get] Boolean IsDemo
- prop [get] Boolean IsTestVersion
- prop [get/set] Platform Platform
- prop [get] VersionData Version

## Duckov.HardwareSyncingManager [class] : UnityEngine.MonoBehaviour

- method Void SetEvent(String eventName) (static)

## Duckov.ICursorDataProvider [interface]

- method CursorData GetCursorData()

## Duckov.IProgress [interface]

- method Progress GetProgress()

## Duckov.ItemBuilders.ItemBuilder [class]

- method ItemBuilder DisableStacking()
- method ItemBuilder EnableStacking(Int32 maxStackCount, Int32 stackCount)
- method ItemBuilder Icon(Sprite sprite)
- method Item Instantiate()
- method ItemBuilder Modifier(ModifierDescription description)
- method ItemBuilder New() (static)
- method ItemBuilder SetConstant(String key, Single value, Boolean display)
- method ItemBuilder SetConstant(String key, Int32 value, Boolean display)
- method ItemBuilder SetConstant(String key, Boolean value, Boolean display)
- method ItemBuilder SetConstant(String key, String value, Boolean display)
- method ItemBuilder SetVariable(String key, Single value, Boolean display)
- method ItemBuilder SetVariable(String key, Int32 value, Boolean display)
- method ItemBuilder SetVariable(String key, Boolean value, Boolean display)
- method ItemBuilder SetVariable(String key, String value, Boolean display)
- method ItemBuilder Slot(String key, List<Tag> requireTags, List<Tag> excludeTags)
- method ItemBuilder Slot(String key, Tag requireTag, Tag excludeTag)
- method ItemBuilder Stat(String key, Single value, Boolean display)
- method ItemBuilder TypeID(Int32 id)

## Duckov.ItemShortcut [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 MaxIndex (static)
- field ItemShortcut Instance (static)
- method Item Get(Int32 index) (static)
- method Boolean IsItemValid(Item item) (static)
- method Boolean Set(Int32 index, Item item) (static)

## Duckov.ItemUnlockNotification [class] : UnityEngine.MonoBehaviour

- prop [get/set] ItemUnlockNotification Instance (static)
- prop [get] String MainTextFormat
- prop [get] Boolean Showing (static)
- method Void OnPointerClick(PointerEventData eventData)
- method Void Push(Int32 itemTypeID) (static)

## Duckov.ItemUsage.AddBuff [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## Duckov.ItemUsage.DeadByChance [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## Duckov.ItemUsage.Drug [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## Duckov.ItemUsage.FoodDrink [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## Duckov.ItemUsage.RemoveBuff [class] : ItemStatsSystem.UsageBehavior

- method Boolean CanBeUsed(Item item, Object user)

## Duckov.ItemUsage.SpawnEgg [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## Duckov.LotteryBox [class] : InteractableBase

- method Void Begin()
- method UniTask Process(CancellationToken cancellationToken)

## Duckov.MasterKeys.MasterKeysManager [class] : UnityEngine.MonoBehaviour

- prop [get] List<Int32> AllPossibleKeys (static)
- prop [get] Int32 Count
- prop [get/set] MasterKeysManager Instance (static)
- method Status GetOrCreateStatus(Int32 id)
- method Status GetStatus_Local(Int32 id)
- method Boolean IsActive(Int32 id) (static)
- method Boolean IsActive_Local(Int32 id)
- method Boolean SubmitAndActivate(Item item) (static)

## Duckov.MasterKeys.UI.MasterKeysIndexEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Active
- prop [get] String Description
- prop [get] String DisplayName
- prop [get] Sprite Icon
- prop [get] Int32 ItemID
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(Int32 itemID, ISingleSelectionMenu<MasterKeysIndexEntry> menu)

## Duckov.MasterKeys.UI.MasterKeysIndexEntry.Look [struct]

- method Void ApplyTo(Graphic graphic)

## Duckov.MasterKeys.UI.MasterKeysIndexList [class] : UnityEngine.MonoBehaviour

- method MasterKeysIndexEntry GetSelection()
- method Boolean SetSelection(MasterKeysIndexEntry selection)

## Duckov.MasterKeys.UI.MasterKeysRegisterView [class] : Duckov.UI.View

- prop [get] MasterKeysRegisterView Instance (static)
- method Void Show() (static)

## Duckov.MasterKeys.UI.MasterKeysView [class] : Duckov.UI.View

- prop [get] MasterKeysView Instance (static)
- method MasterKeysIndexEntry GetSelection()
- method Boolean SetSelection(MasterKeysIndexEntry selection)

## Duckov.MiniGames.BubblePoppers.Bubble [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Int32 ColorIndex
- prop [get/set] Vector2Int Coord
- prop [get] Color DisplayColor
- prop [get/set] BubblePopper Master
- prop [get/set] Vector2 MoveDirection
- prop [get] Single Radius
- prop [get/set] Vector2 Velocity
- prop [get/set] Status status
- method Void Impact(Vector2 velocity)
- method Void NotifyDetached()
- method Void RefreshColor()

## Duckov.MiniGames.BubblePoppers.Bubble.Status [enum]

- Idle = 0
- Moving = 1
- Attached = 2
- Detached = 3
- Explode = 4

## Duckov.MiniGames.BubblePoppers.BubblePopper [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Int32 AvaliableColorCount
- prop [get] Single BubbleRadius
- prop [get] Bubble BubbleTemplate
- prop [get/set] Boolean Busy
- prop [get/set] Int32 FloorStepETA
- prop [get/set] Int32 HighLevel (static)
- prop [get/set] Int32 HighScore (static)
- prop [get] BubblePopperLayout Layout
- prop [get/set] Int32 Score
- prop [get/set] Status status
- method Void MoveBubble(Bubble bubble, Single deltaTime)
- method Void NextPallette()
- method Void SetPallette(Color[] colors)
- method CastResult SlideCast(Vector2 origin, Vector2 delta)

## Duckov.MiniGames.BubblePoppers.BubblePopper.CastResult [struct]

- prop [get] Boolean Collide

## Duckov.MiniGames.BubblePoppers.BubblePopper.Status [enum]

- Idle = 0
- Loaded = 1
- Launched = 2
- Settled = 3
- GameOver = 4

## Duckov.MiniGames.BubblePoppers.BubblePopperLayout [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Vector2 XPositionBorder
- field Single YOffsetFactor (static)
- method Vector2 CoordToLocalPosition(Vector2Int coord)
- method Vector2Int[] GetAllNeighbourCoords(Vector2Int center, Boolean includeCenter)
- method List<Vector2Int> GetAllPassingCoords(Vector2 localOrigin, Vector2 direction, Single length)
- method Void GizmosDrawCoord(Vector2Int coord, Single ratio)
- method Vector2Int LocalPositionToCoord(Vector2 localPosition)
- method Vector2Int WorldPositionToCoord(Vector2 position)

## Duckov.MiniGames.BubblePoppers.BubblePopperLevelDataProvider [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 TotalLevels

## Duckov.MiniGames.Examples.FPS.FPSDamageReceiver [class] : UnityEngine.MonoBehaviour

- prop [get] ParticleSystem DamageFX

## Duckov.MiniGames.Examples.FPS.FPSGun [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Single ScatterAngle

## Duckov.MiniGames.Examples.FPS.FPSGun.Pose [struct]

- method Pose Extraterpolate(Pose poseA, Pose poseB, Single t) (static)

## Duckov.MiniGames.Examples.FPS.FPSGunControl [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] FPSGun Gun
- prop [get] Single ScatterAngle

## Duckov.MiniGames.Examples.FPS.FPSHealth [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Boolean Dead
- prop [get] Int32 HP

## Duckov.MiniGames.Examples.FPS.FPSMovement [class] : ECM2.Character

- method Void AddControlYawInput(Single value)
- method Void SetGame(MiniGame game)

## Duckov.MiniGames.GamingConsole [class] : InteractableBase

- prop [get] Item Cartridge
- prop [get] String CatridgeGameID
- prop [get] Item Console
- prop [get] Slot ConsoleSlot
- prop [get] MiniGame Game
- prop [get] Boolean HasGraphicsCard
- prop [get] Item Monitor
- prop [get] Slot MonitorSlot
- prop [get] MiniGame SelectedGame
- prop [get] Boolean controllerConnected

## Duckov.MiniGames.GamingConsoleHUD [class] : Duckov.UI.View

- method Void Hide() (static)
- method Void Show() (static)

## Duckov.MiniGames.GoldMiner.GoldMiner [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] ReadOnlyCollection<GoldMinerArtifact> ArtifactPrefabs
- prop [get] Bounds Bounds
- prop [get] Single GlobalPriceFactor
- prop [get/set] Int32 HighLevel (static)
- prop [get] Hook Hook
- prop [get] Int32 Money
- prop [get/set] Boolean isBeingDestroyed
- prop [get/set] GoldMinerRunData run
- method Void Cleanup()
- method GoldMinerArtifact GetArtifactPrefab(String id)
- method Void LaunchHook()
- method Boolean UseEagleEyePotion()
- method Boolean UseStrengthPotion()

## Duckov.MiniGames.GoldMiner.GoldMinerArtifact [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Boolean AllowMultiple
- prop [get] Int32 BasePrice
- prop [get] String Description
- prop [get] String DisplayName
- prop [get] String ID
- prop [get] Sprite Icon
- prop [get] GoldMiner Master
- prop [get] Int32 Quality
- method Void Attach(GoldMiner master)
- method Void Detatch(GoldMiner master)

## Duckov.MiniGames.GoldMiner.GoldMinerEntity [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Single Speed
- prop [get] String TypeID
- prop [get/set] Int32 Value
- prop [get/set] GoldMiner master
- method Void NotifyAttached(Hook hook)
- method Void NotifyBeginRetrieving()
- method Void SetMaster(GoldMiner master)

## Duckov.MiniGames.GoldMiner.GoldMinerEntity.Size [enum]

- M = 0
- L = 1
- XL = 2
- XS = -2
- S = -1

## Duckov.MiniGames.GoldMiner.GoldMinerEntity.Tag [enum]

- None = 0
- Rock = 1
- Gold = 2
- Diamond = 3
- Mine = 4
- Chest = 5
- Pig = 6
- Cable = 7

## Duckov.MiniGames.GoldMiner.GoldMinerRunData [class]

- prop [get/set] Boolean EagleEyeActivated
- prop [get] Single GameSpeedFactor
- prop [get/set] Boolean StrengthPotionActivated
- prop [get/set] Boolean gameOver
- prop [get/set] Int32 level
- prop [get/set] Random levelRandom
- prop [get/set] Int32 seed
- prop [get/set] Random shopRandom
- prop [get/set] Single stamina
- field Int32 shopDefaultItemAmount (static)
- field Int32 shopMaxItemAmount (static)
- method Void ActivateEagleEye()
- method Void ActivateStrengthPotion()
- method GoldMinerArtifact AttachArtifactFromPrefab(GoldMinerArtifact prefab)
- method Void DeactivateEagleEye()
- method Void DeactivateStrengthPotion()
- method Boolean DetachArtifact(GoldMinerArtifact artifact)
- method Int32 GetArtifactCount(String id)
- method Boolean IsGold(GoldMinerEntity entity)
- method Boolean IsRock(GoldMinerEntity entity)

## Duckov.MiniGames.GoldMiner.GoldMinerShop [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get/set] Int32 refreshChance
- method Boolean Buy(ShopEntity entity)
- method Int32 CalculateDealPrice(ShopEntity entity, Boolean& useTicket)
- method UniTask Execute()

## Duckov.MiniGames.GoldMiner.GoldMinerShopUI [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get/set] GoldMinerShop target
- method Void Setup(GoldMinerShop shop)

## Duckov.MiniGames.GoldMiner.GoldMiner_PopText [class] : Duckov.MiniGames.MiniGameBehaviour

- method Void Pop(String content, Vector3 position)

## Duckov.MiniGames.GoldMiner.GoldMiner_PopTextEntry [class] : UnityEngine.MonoBehaviour

- method Void Setup(Vector3 pos, String text, Action<GoldMiner_PopTextEntry> releaseAction)

## Duckov.MiniGames.GoldMiner.GoldMiner_ShopItem [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 BasePrice
- prop [get] String DisplayName
- prop [get] String DisplayNameKey
- prop [get] Sprite Icon
- method Void OnBought(GoldMiner target)

## Duckov.MiniGames.GoldMiner.Hook [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Transform Axis
- prop [get/set] GoldMinerEntity GrabbingTarget
- prop [get] HookStatus Status
- method Void Launch()
- method Void Reset()
- method Void SetParameters(Single swingFreqFactor, Single emptySpeed, Single strength)
- method Void Tick(Single deltaTime)

## Duckov.MiniGames.GoldMiner.Hook.HookStatus [enum]

- Idle = 0
- Swinging = 1
- Launching = 2
- Attaching = 3
- Retrieving = 4
- Retrieved = 5

## Duckov.MiniGames.GoldMiner.LevelSettlementUI [class] : UnityEngine.MonoBehaviour

- method Void Hide()
- method Void SetTargetScore(Int32 targetScore)
- method Void Show()
- method Void Step(Int32 money, Single factor, Int32 score)
- method Void StepResolveEntity(GoldMinerEntity entity)
- method Void StepResult(Boolean clear)

## Duckov.MiniGames.GoldMiner.NavGroup [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get/set] NavGroup ActiveNavGroup (static)
- prop [get/set] Int32 NavIndex
- prop [get] Boolean active
- field Action OnNavGroupChanged (static)
- method NavEntry GetSelectedEntry()
- method Void SetAsActiveNavGroup()

## Duckov.MiniGames.GoldMiner.PassivePropDisplay [class] : UnityEngine.MonoBehaviour

- prop [get] NavEntry NavEntry
- prop [get/set] GoldMinerArtifact Target
- prop [get/set] RectTransform rectTransform

## Duckov.MiniGames.GoldMiner.ShopEntity [class]

- prop [get] String ID

## Duckov.MiniGames.GoldMiner.UI.NavEntry [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean selectionState
- method Void NotifySelectionState(Boolean value)
- method Void TrySelectThis()

## Duckov.MiniGames.Island.PressStartText [class] : UnityEngine.MonoBehaviour

- method Void NotifyPressed()
- method Void Reset()

## Duckov.MiniGames.MiniGame [class] : UnityEngine.MonoBehaviour

- prop [get] Camera Camera
- prop [get] GamingConsole Console
- prop [get] String ID
- prop [get] RenderTexture RenderTexture
- prop [get] Camera UICamera
- field Action<MiniGame, Single> onUpdateLogic (static)
- method Void ClearInput()
- method RenderTexture CreateAndSetRenderTexture(Int32 width, Int32 height)
- method Vector2 GetAxis(Int32 index)
- method Boolean GetButton(Button button)
- method Boolean GetButtonDown(Button button)
- method Boolean GetButtonUp(Button button)
- method Void SetButton(Button button, Boolean down)
- method Void SetInputAxis(Vector2 axis, Int32 index)
- method Void SetRenderTexture(RenderTexture texture)

## Duckov.MiniGames.MiniGame.Button [enum]

- None = 0
- A = 1
- B = 2
- Start = 3
- Select = 4
- Left = 5
- Right = 6
- Up = 7
- Down = 8

## Duckov.MiniGames.MiniGame.TickTiming [enum]

- Manual = 0
- Update = 1
- FixedUpdate = 2
- LateUpdate = 3

## Duckov.MiniGames.MiniGameBehaviour [class] : UnityEngine.MonoBehaviour

- prop [get] MiniGame Game
- method Void SetGame(MiniGame game)

## Duckov.MiniGames.MiniGameInputHandler [class] : UnityEngine.MonoBehaviour

- method Void ClearInput()

## Duckov.MiniGames.MouseDodger.MouseDodger [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get/set] Int32 HighScore (static)
- method UniTask GameProcess()

## Duckov.MiniGames.MouseDodger.MouseDodger.RunData [struct]

- method RunData New(Int32 hp) (static)

## Duckov.MiniGames.MouseDodger.MouseDodger.Status [enum]

- None = 0
- Title = 1
- Gameplay = 2
- GameOver = 3

## Duckov.MiniGames.MouseDodger.MouseDodgerEntity [class] : UnityEngine.MonoBehaviour

- method MouseDodgerEntity Create(Sprite sprite, Single radius, Transform parent, Action<MouseDodgerEntity, Collision2D> onEnter, Action<MouseDodgerEntity, Collision2D> onStay, Action<MouseDodgerEntity, Collision2D> onExit) (static)

## Duckov.MiniGames.SnakeForces.SnakeForce [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] List<Vector2Int> Foods
- prop [get] Part Head
- prop [get/set] Int32 HighScore (static)
- prop [get/set] Int32 Score
- prop [get] List<Part> Snake
- prop [get] Part Tail
- field String HighScoreKey (static)

## Duckov.MiniGames.SnakeForces.SnakeForce.Part [class]

- prop [get] Boolean IsHead
- prop [get] Boolean IsTail

## Duckov.MiniGames.SnakeForces.SnakePartDisplay [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get/set] SnakeDisplay Master
- prop [get/set] Part Target

## Duckov.MiniGames.Utilities.ControllerPickupAnimation [class] : UnityEngine.MonoBehaviour

- method UniTask PickUp(Transform endTransform)
- method UniTask PutDown()

## Duckov.MiniGames.VirtualCursorTarget [class] : Duckov.MiniGames.MiniGameBehaviour

- prop [get] Boolean IsHovering
- method Void OnClick()
- method Void OnCursorEnter()
- method Void OnCursorExit()

## Duckov.MiniMaps.IPointOfInterest [interface]

- prop [get] Single AreaRadius
- prop [get] Color Color
- prop [get] String DisplayName
- prop [get] Boolean HideIcon
- prop [get] Sprite Icon
- prop [get] Boolean IsArea
- prop [get] Int32 OverrideScene
- prop [get] Single ScaleFactor
- prop [get] Color ShadowColor
- prop [get] Single ShadowDistance
- method Void NotifyClicked(PointerEventData eventData)

## Duckov.MiniMaps.MapMarkerManager [class] : UnityEngine.MonoBehaviour

- prop [get] List<Sprite> Icons (static)
- prop [get/set] MapMarkerManager Instance (static)
- prop [get] Color SelectedColor (static)
- prop [get] Sprite SelectedIcon (static)
- prop [get] Int32 SelectedIconIndex (static)
- prop [get] String SelectedIconName (static)
- field Action<Color> OnColorChanged (static)
- field Action<Int32> OnIconChanged (static)
- method Void Release(MapMarkerPOI entry) (static)
- method Void Request(RuntimeData data) (static)
- method Void Request(Vector3 worldPos) (static)

## Duckov.MiniMaps.MapMarkerPOI [class] : UnityEngine.MonoBehaviour

- prop [get] Color Color
- prop [get] RuntimeData Data
- prop [get] Sprite Icon
- prop [get] Int32 OverrideScene
- prop [get] Single ScaleFactor
- prop [get] Color ShadowColor
- method Void NotifyClicked(PointerEventData eventData)
- method Void Setup(RuntimeData data)
- method Void Setup(Vector3 worldPosition, String iconName, String overrideScene, Nullable<Color> color)

## Duckov.MiniMaps.MiniMapCenter [class] : UnityEngine.MonoBehaviour

- prop [get] Single WorldSize

## Duckov.MiniMaps.MiniMapSettings [class] : UnityEngine.MonoBehaviour

- prop [get] Vector3 CombinedCenter
- prop [get] Sprite CombinedSprite
- prop [get/set] MiniMapSettings Instance (static)
- prop [get] List<IMiniMapEntry> Maps
- prop [get] Single PixelSize
- method Boolean TryGetMinimapPosition(Vector3 worldPosition, Vector3& result) (static)
- method Boolean TryGetMinimapPosition(Vector3 worldPosition, String sceneID, Vector3& result) (static)
- method Boolean TryGetWorldPosition(Vector3 minimapPosition, String sceneID, Vector3& result) (static)

## Duckov.MiniMaps.MiniMapSettings.MapEntry [class]

- prop [get] Boolean Hide
- prop [get] Boolean NoSignal
- prop [get] Vector2 Offset
- prop [get] Single PixelSize
- prop [get] String SceneID
- prop [get] SceneReference SceneReference
- prop [get] Sprite Sprite

## Duckov.MiniMaps.PointsOfInterests [class]

- prop [get] ReadOnlyCollection<MonoBehaviour> Points (static)
- method Void Register(MonoBehaviour point) (static)
- method Void Unregister(MonoBehaviour point) (static)

## Duckov.MiniMaps.SimplePointOfInterest [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single AreaRadius
- prop [get/set] Color Color
- prop [get] String DisplayName
- prop [get/set] Boolean HideIcon
- prop [get] Sprite Icon
- prop [get/set] Boolean IsArea
- prop [get] Int32 OverrideScene
- prop [get/set] Single ScaleFactor
- prop [get/set] Color ShadowColor
- prop [get/set] Single ShadowDistance
- method SimplePointOfInterest Create(Vector3 position, String sceneID, String displayName, Sprite icon, Boolean hideIcon) (static)
- method Void NotifyClicked(PointerEventData pointerEventData)
- method Void SetColor(Color color)
- method Void Setup(Sprite icon, String displayName, Boolean followActiveScene, String overrideSceneID)
- method Boolean SetupMultiSceneLocation(MultiSceneLocation location, Boolean moveToMainScene)

## Duckov.MiniMaps.UI.MiniMapDisplay [class] : UnityEngine.MonoBehaviour

- method Boolean NoSignal()
- method Void OnScroll(PointerEventData eventData)
- method Void Setup(IMiniMapDataProvider dataProvider)
- method Boolean TryConvertToWorldPosition(Vector3 displayPosition, Vector3& result)
- method Boolean TryConvertWorldToMinimap(Vector3 worldPosition, String sceneID, Vector3& result)

## Duckov.MiniMaps.UI.MiniMapDisplayEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Hide
- prop [get/set] MiniMapDisplay Master
- prop [get] String SceneID
- prop [get] SceneReference SceneReference
- method Boolean NoSignal()
- method Void OnPointerClick(PointerEventData eventData)
- method Boolean ShouldShow()

## Duckov.MiniMaps.UI.MiniMapView [class] : Duckov.UI.View

- prop [get] MiniMapView Instance (static)
- method Void CeneterPlayer()
- method Void LoadCurrent()
- method Void LoadData(PackedMapData mapData)
- method Void Show() (static)
- method Boolean TryConvertWorldToMinimapPosition(Vector3 worldPosition, Vector3& result) (static)
- method Boolean TryConvertWorldToMinimapPosition(Vector3 worldPosition, String sceneID, Vector3& result) (static)

## Duckov.MiniMaps.UI.PointOfInterestEntry [class] : UnityEngine.MonoBehaviour

- prop [get] MonoBehaviour Target
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.Modding.ModBehaviour [class] : UnityEngine.MonoBehaviour

- prop [get/set] ModInfo info
- prop [get/set] ModManager master
- method Void NotifyBeforeDeactivate()
- method Void Setup(ModManager master, ModInfo info)

## Duckov.Modding.ModInfo [struct]

- prop [get] String dllPath

## Duckov.Modding.ModManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean AllowActivatingMod (static)
- prop [get] String DefaultModFolderPath (static)
- prop [get] ModManager Instance (static)
- field Action<String, String> OnModLoadingFailed (static)
- field List<ModInfo> modInfos (static)
- method ModBehaviour ActivateMod(ModInfo info)
- method Void DeactivateMod(ModInfo info)
- method ModBehaviour GetActiveModBehaviour(ModInfo info)
- method List<String> GetCurrentActiveModList() (static)
- method Int32 GetModPriority(String name) (static)
- method Boolean IsModActive(ModInfo info, ModBehaviour& instance) (static)
- method List<String> LoadLastActiveModList() (static)
- method Boolean Reorder(Int32 fromIndex, Int32 toIndex) (static)
- method Void Rescan() (static)
- method Void ScanAndActivateMods()
- method Void SetModPriority(String name, Int32 priority) (static)
- method Boolean TryProcessModFolder(String path, ModInfo& info, Boolean isSteamItem, UInt64 publishedFileId) (static)

## Duckov.Modding.SteamWorkshopManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] SteamWorkshopManager Instance (static)
- prop [get/set] Boolean UploadSucceed
- prop [get] Single UploadingProgress (static)
- prop [get/set] UInt64 punBytesProcess (static)
- prop [get/set] UInt64 punBytesTotal (static)
- method Boolean IsOwner(ModInfo info) (static)
- method UniTask<PublishedFileId_t> RequestNewWorkshopItemID()
- method Void SendQueryDetailsRequest()
- method UniTask<Boolean> UploadWorkshopItem(String path, String changeNote)

## Duckov.Modding.UI.ModChangedWarning [class] : UnityEngine.MonoBehaviour

- method UniTask<Boolean> Check()

## Duckov.Modding.UI.ModEntry [class] : UnityEngine.MonoBehaviour

- method Void Setup(ModManagerUI master, ModInfo modInfo, Int32 index)

## Duckov.Modding.UI.ModPathButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## Duckov.NoteIndexs.Note [class]

- prop [get] String Content
- prop [get] String Title
- prop [get/set] String contentKey
- prop [get/set] String titleKey

## Duckov.NoteIndexs.NoteIndex [class] : UnityEngine.MonoBehaviour

- prop [get] NoteIndex Instance (static)
- prop [get] List<Note> Notes
- prop [get] HashSet<String> ReadNotes
- prop [get] HashSet<String> UnlockedNotes
- field Action<String> onNoteStatusChanged (static)
- method IEnumerable<String> GetAllNotes(Boolean unlockedOnly) (static)
- method Note GetNote(String key) (static)
- method Boolean GetNoteRead(String noteKey) (static)
- method Boolean GetNoteUnlocked(String noteKey) (static)
- method Note MGetNote(String key)
- method Void MSetEntryDynamic(Note note)
- method Boolean SetNoteDynamic(Note note) (static)
- method Void SetNoteRead(String noteKey) (static)
- method Void SetNoteUnlocked(String noteKey) (static)

## Duckov.NoteIndexs.NoteIndexProxy [class] : UnityEngine.MonoBehaviour

- method Void UnlockAndShowNote(String key)
- method Void UnlockNote(String key)

## Duckov.NoteIndexs.NoteInteract [class] : InteractableBase

- method Void ReName()

## Duckov.NoteIndexs.NoteStatus [enum]

- None = 0
- Unlocked = 2

## Duckov.NoteIndexs.RequireNoteIndexUnlocked [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Duckov.Options.OptionsManager [class] : UnityEngine.MonoBehaviour

- prop [get] String FilePath (static)
- prop [get/set] Single MouseSensitivity (static)
- field String FileName (static)
- method T Load(String key, T defaultValue) (static)
- method Void Save(String key, T obj) (static)

## Duckov.Options.UI.OptionsPanel [class] : UIPanel

- method OptionsPanel_TabButton GetSelection()
- method Boolean SetSelection(OptionsPanel_TabButton selection)

## Duckov.Options.UI.OptionsUIEntry_Dropdown [class] : UnityEngine.MonoBehaviour

- prop [get/set] String LabelKey
- method Void OnPointerEnter(PointerEventData eventData)

## Duckov.Options.UI.OptionsUIEntry_Slider [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single Value

## Duckov.Options.UI.OptionsUIEntry_Toggle [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Value

## Duckov.Options.UI.RuleEntry_Float [class] : UnityEngine.MonoBehaviour

- method Void RefreshValue()

## Duckov.Options.UI.RuleEntry_Int [class] : UnityEngine.MonoBehaviour

- method Void RefreshValue()

## Duckov.PerkTrees.AddGardenSize [class] : Duckov.PerkTrees.PerkBehaviour

- prop [get] String Description
- method Vector2Int GetValue(String gardenID)

## Duckov.PerkTrees.AddPlayerStorage [class] : Duckov.PerkTrees.PerkBehaviour

- prop [get] String Description

## Duckov.PerkTrees.Behaviours.ModifyCharacterStatsBase [class] : Duckov.PerkTrees.PerkBehaviour

- prop [get] String Description

## Duckov.PerkTrees.GardenAutoWater [class] : Duckov.PerkTrees.PerkBehaviour

- prop [get] String Description
- method Boolean TakeEffect(String gardenID)

## Duckov.PerkTrees.Perk [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean DefaultUnlocked
- prop [get] String Description
- prop [get] String DescriptionRaw
- prop [get] String DisplayName
- prop [get] String DisplayNameRaw
- prop [get] DisplayQuality DisplayQuality
- prop [get] Boolean EnabledInCurrentLevel
- prop [get] Sprite Icon
- prop [get] Boolean LockInDemo
- prop [get/set] PerkTree Master
- prop [get] PerkRequirement Requirement
- prop [get/set] Boolean Unlocked
- prop [get] Boolean Unlocking
- method Boolean AreAllParentsUnlocked()
- method Boolean ConfirmUnlock()
- method Boolean ForceUnlock()
- method Single GetProgress01()
- method TimeSpan GetRemainingTime()
- method Boolean SubmitItemsAndBeginUnlocking()
- method Void Validate(SelfValidationResult result)

## Duckov.PerkTrees.PerkBehaviour [class] : UnityEngine.MonoBehaviour

- prop [get] String Description

## Duckov.PerkTrees.PerkLevelLineNode [class] : Duckov.PerkTrees.PerkRelationNodeBase

- prop [get] String DisplayName
- prop [get] Int32 maxInConnections
- prop [get] Int32 maxOutConnections

## Duckov.PerkTrees.PerkRelationGraph [class] : NodeCanvas.Framework.Graph

- prop [get] Boolean allowBlackboardOverrides
- prop [get] Type baseNodeType
- prop [get] Boolean canAcceptVariableDrops
- prop [get] PlanarDirection flowDirection
- prop [get] Boolean isTree
- prop [get] Boolean requiresAgent
- prop [get] Boolean requiresPrimeNode
- method List<PerkRelationNode> GetIncomingNodes(PerkRelationNode skillTreeNode)
- method List<PerkRelationNode> GetOutgoingNodes(PerkRelationNode skillTreeNode)
- method PerkRelationNode GetRelatedNode(Perk perk)

## Duckov.PerkTrees.PerkRelationNode [class] : Duckov.PerkTrees.PerkRelationNodeBase

- method Void OnDestroy()

## Duckov.PerkTrees.PerkRelationNodeBase [class] : NodeCanvas.Framework.Node

- prop [get] Boolean allowAsPrime
- prop [get] Boolean canSelfConnect
- prop [get] Alignment2x2 commentsAlignment
- prop [get] Alignment2x2 iconAlignment
- prop [get] Int32 maxInConnections
- prop [get] Int32 maxOutConnections
- prop [get] Type outConnectionType

## Duckov.PerkTrees.PerkRequirement [class]

- prop [get] TimeSpan RequireTime

## Duckov.PerkTrees.PerkTree [class] : UnityEngine.MonoBehaviour

- prop [get] String DisplayName
- prop [get] Boolean EnabledInCurrentLevel
- prop [get] Boolean Horizontal
- prop [get] String ID
- prop [get] ReadOnlyCollection<Perk> Perks
- prop [get] PerkTreeRelationGraphOwner RelationGraphOwner
- method Object GenerateSaveData()
- method Void Load()
- method Void ReapplyPerks()
- method Void Save()
- method Void SetupSaveData(Object data)

## Duckov.PerkTrees.PerkTreeRelationGraphOwner [class] : NodeCanvas.Framework.GraphOwner<PerkRelationGraph>

- prop [get] PerkRelationGraph RelationGraph
- method List<Perk> GetRequiredNodes(Perk node)

## Duckov.Platform [enum]

- Unknown = 0
- Steam = 1
- Epic = 2
- WeGame = 3
- Apple = 4
- UnityEditor = 5
- bilibili = 6

## Duckov.PlatformInfo [class]

- prop [get/set] Func<String> GetDisplayNameFunc (static)
- prop [get/set] Func<String> GetIDFunc (static)
- prop [get/set] Platform Platform (static)
- method String GetDisplayName() (static)
- method String GetID() (static)

## Duckov.Progress [struct]

- prop [get] Single progress

## Duckov.Quests.Condition [class] : UnityEngine.MonoBehaviour

- prop [get] String DisplayText
- method Boolean Evaluate()

## Duckov.Quests.ConditionExtensions [class]

- method Boolean Satisfied(IEnumerable<Condition> conditions) (static)

## Duckov.Quests.Conditions.RequireDemo [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Duckov.Quests.Conditions.RequireFormulaUnlocked [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Duckov.Quests.Conditions.RequireGameobjectsActived [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Duckov.Quests.Conditions.RequireHasFished [class] : Duckov.Quests.Condition

- method Boolean Evaluate()
- method Boolean GetHasFished() (static)
- method Void SetHasFished() (static)

## Duckov.Quests.Conditions.RequirePerkUnlocked [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## Duckov.Quests.Conditions.RequireQuestsActive [class] : Duckov.Quests.Condition

- prop [get] Int32[] RequiredQuestIDs
- method Boolean Evaluate()

## Duckov.Quests.Conditions.RequireQuestsActiveOrFinished [class] : Duckov.Quests.Condition

- prop [get] Int32[] RequiredQuestIDs
- method Boolean Evaluate()

## Duckov.Quests.Conditions.RequireQuestsFinished [class] : Duckov.Quests.Condition

- prop [get] Int32[] RequiredQuestIDs
- method Boolean Evaluate()

## Duckov.Quests.MapElementForTask [class] : UnityEngine.MonoBehaviour

- method Void DespawnAll()
- method Void DespawnPoint(SimplePointOfInterest point)
- method Void SetVisibility(Boolean _visable)

## Duckov.Quests.Quest [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Active
- prop [get/set] Boolean Complete
- prop [get] String Description
- prop [get/set] String DescriptionRaw
- prop [get] String DisplayName
- prop [get/set] String DisplayNameRaw
- prop [get] Object FinishedTaskCount
- prop [get/set] Int32 ID
- prop [get] Boolean LockInDemo
- prop [get/set] Boolean NeedInspection
- prop [get] ReadOnlyCollection<Condition> Prerequisits
- prop [get/set] QuestGiverID QuestGiverID
- prop [get] Int32 RequireLevel
- prop [get] SceneReference RequireScene
- prop [get] SceneInfoEntry RequireSceneInfo
- prop [get] Int32 RequiredItemCount
- prop [get] Int32 RequiredItemID
- prop [get] ReadOnlyCollection<Reward> Rewards
- prop [get] Boolean SceneRequirementSatisfied
- prop [get] List<Task> Tasks
- method Boolean AreTasksFinished()
- method Int32 Compare(Quest x, Quest y, SortingMode sortingMode, Boolean invert) (static)
- method Void ForceComplete()
- method Object GenerateSaveData()
- method Void Initialize()
- method Boolean MeetsPrerequisit()
- method Void OnValidate()
- method Void SetupSaveData(Object obj)
- method Boolean TryComplete()

## Duckov.Quests.Quest.SortingMode [enum]

- Default = 0
- ID = 1
- Giver = 2
- Location = 3

## Duckov.Quests.QuestCollection [class] : UnityEngine.ScriptableObject

- prop [get] Int32 Count
- prop [get] QuestCollection Instance (static)
- prop [get] Boolean IsReadOnly
- prop [get/set] Quest Item
- method Void Add(Quest item)
- method Void Clear()
- method Void Collect()
- method Boolean Contains(Quest item)
- method Void CopyTo(Quest[] array, Int32 arrayIndex)
- method Quest Get(Int32 id)
- method IEnumerator<Quest> GetEnumerator()
- method Int32 IndexOf(Quest item)
- method Void Insert(Int32 index, Quest item)
- method Boolean Remove(Quest item)
- method Void RemoveAt(Int32 index)
- method Void Validate(SelfValidationResult result)

## Duckov.Quests.QuestGiver [class] : InteractableBase

- prop [get] QuestGiverID ID
- method Void ActivateQuest(Quest quest)

## Duckov.Quests.QuestGiverID [enum]

- None = 0
- Jeff = 1
- Xavier = 2
- Orange = 3
- Bob = 4
- Mud = 5
- DemoTrophy = 6
- Ming = 7
- Fo = 8
- SnowPMC = 9
- Alex = 10
- GunFire = 11

## Duckov.Quests.QuestManager [class] : UnityEngine.MonoBehaviour

- prop [get] List<Quest> ActiveQuests
- prop [get] Boolean AnyQuestNeedsInspection (static)
- prop [get] List<Int32> EverInspectedQuest
- prop [get] List<Quest> HistoryQuests
- prop [get] QuestManager Instance (static)
- prop [get] Boolean NeedInspection
- prop [get] String TaskFinishNotificationFormat
- field Action<Quest, Task> OnTaskFinishedEvent (static)
- method Void ActivateQuest(Int32 id, Nullable<QuestGiverID> overrideQuestGiverID)
- method Boolean AnyActiveQuestNeedsInspection(QuestGiverID giverID) (static)
- method Object GenerateSaveData()
- method IEnumerable<Quest> GetAllQuestsByQuestGiverID(QuestGiverID questGiverID)
- method IEnumerable<Int32> GetAllRequiredItems() (static)
- method Void SetupSaveData(Object dataObj)

## Duckov.Quests.QuestTask_BubblePopper_Level [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.QuestTask_GoldMiner_Level [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.QuestTask_MouseDodger_Score [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.QuestTask_Snake_Score [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.QuestTask_UnlockBeacon [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Relations.QuestRelationGraph [class] : NodeCanvas.Framework.Graph

- prop [get] Boolean allowBlackboardOverrides
- prop [get] Type baseNodeType
- prop [get] Boolean canAcceptVariableDrops
- prop [get] PlanarDirection flowDirection
- prop [get] Boolean isTree
- prop [get] Boolean requiresAgent
- prop [get] Boolean requiresPrimeNode
- field Int32 selectedQuestID (static)
- method QuestRelationNode GetNode(Int32 questID)
- method List<Int32> GetRequiredIDs(Int32 targetID)

## Duckov.Quests.Relations.QuestRelationNode [class] : Duckov.Quests.Relations.QuestRelationNodeBase

- method List<Int32> GetChildren()
- method List<Int32> GetParents()

## Duckov.Quests.Relations.QuestRelationNodeBase [class] : NodeCanvas.Framework.Node

- prop [get] Boolean allowAsPrime
- prop [get] Boolean canSelfConnect
- prop [get] Alignment2x2 commentsAlignment
- prop [get] Alignment2x2 iconAlignment
- prop [get] Int32 maxInConnections
- prop [get] Int32 maxOutConnections
- prop [get] Type outConnectionType

## Duckov.Quests.Relations.QuestRelationProxyNode [class] : Duckov.Quests.Relations.QuestRelationNodeBase

- prop [get] Int32 maxInConnections

## Duckov.Quests.Reward [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean AutoClaim
- prop [get] Boolean Claimable
- prop [get] Boolean Claimed
- prop [get] Boolean Claiming
- prop [get] String Description
- prop [get/set] Int32 ID
- prop [get] Sprite Icon
- prop [get/set] Quest Master
- method Void Claim()
- method Object GenerateSaveData()
- method Void NotifyReload(Quest questInstance)
- method Void OnClaim()
- method Void OnMasterStatusChanged(Quest quest)
- method Void SetupSaveData(Object data)
- method Void Validate(SelfValidationResult result)

## Duckov.Quests.RewardItem [class] : Duckov.Quests.Reward

- prop [get] Boolean Claimed
- prop [get] Boolean Claiming
- prop [get] String Description
- prop [get] Sprite Icon
- method Object GenerateSaveData()
- method Void OnClaim()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Rewards.QuestReward_EXP [class] : Duckov.Quests.Reward

- prop [get] Int32 Amount
- prop [get] Boolean AutoClaim
- prop [get] Boolean Claimed
- prop [get] String Description
- method Object GenerateSaveData()
- method Void OnClaim()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Rewards.QuestReward_Money [class] : Duckov.Quests.Reward

- prop [get] Int32 Amount
- prop [get] Boolean AutoClaim
- prop [get] Boolean Claimed
- prop [get] String Description
- method Object GenerateSaveData()
- method Void OnClaim()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Rewards.QuestReward_UnlockStockItem [class] : Duckov.Quests.Reward

- prop [get] Boolean AutoClaim
- prop [get] Boolean Claimed
- prop [get] String Description
- prop [get] Sprite Icon
- prop [get] Int32 UnlockItem
- method Object GenerateSaveData()
- method Void NotifyReload(Quest questInstance)
- method Void OnClaim()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Task [class] : UnityEngine.MonoBehaviour

- prop [get] String Description
- prop [get] String[] ExtraDescriptsions
- prop [get/set] Int32 ID
- prop [get] Sprite Icon
- prop [get] String InteractText
- prop [get] Boolean Interactable
- prop [get/set] Quest Master
- prop [get] Boolean NeedInspection
- prop [get] Boolean PossibleValidInteraction
- method Object GenerateSaveData()
- method Void Interact()
- method Boolean IsFinished()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_CheckSaveData [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] String SaveDataKey
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_ConstructBuilding [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_Evacuate [class] : Duckov.Quests.Task

- prop [get] String Description
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_KillCount [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] String[] ExtraDescriptsions
- prop [get] SceneReference RequireScene
- prop [get] SceneInfoEntry RequireSceneInfo
- prop [get] Boolean SceneRequirementSatisfied
- prop [get] String requireSceneName
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_ReachLocation [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] String DescriptionFormat
- prop [get] String TargetLocationDisplayName
- prop [get] String descriptionFormatkey
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_SubmitMoney [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] String DescriptionFormat
- prop [get] String InteractText
- prop [get] Boolean Interactable
- prop [get] Boolean PossibleValidInteraction
- method Object GenerateSaveData()
- method Void Interact()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_TaskEvent [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] String EventKey
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_UnlockPerk [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] Sprite Icon
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.QuestTask_UseItem [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] Sprite Icon
- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Duckov.Quests.Tasks.SubmitItems [class] : Duckov.Quests.Task

- prop [get] String Description
- prop [get] Sprite Icon
- prop [get] String InteractText
- prop [get] Boolean Interactable
- prop [get] Int32 ItemTypeID
- prop [get] Boolean NeedInspection
- prop [get] Boolean PossibleValidInteraction
- method Object GenerateSaveData()
- method Void Interact()
- method Void SetupSaveData(Object data)
- method Void Submit(Item item)

## Duckov.Quests.UI.IQuestSortable [interface]

- prop [get/set] Boolean SortRevert
- prop [get/set] SortingMode SortingMode

## Duckov.Quests.UI.QuestCompletePanel [class] : UnityEngine.MonoBehaviour

- prop [get] Quest Target
- method UniTask Show(Quest quest)
- method Void Skip()

## Duckov.Quests.UI.QuestEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Selected
- prop [get] Quest Target
- method Void NotifyPooled()
- method Void NotifyRefresh()
- method Void NotifyReleased()
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.Quests.UI.QuestGiverTabButton [class] : UnityEngine.MonoBehaviour

- prop [get] QuestStatus Status

## Duckov.Quests.UI.QuestGiverTabs [class] : UnityEngine.MonoBehaviour

- method QuestGiverTabButton GetSelection()
- method QuestStatus GetStatus()
- method Boolean SetSelection(QuestGiverTabButton selection)

## Duckov.Quests.UI.QuestGiverView [class] : Duckov.UI.View

- prop [get] String BtnText_AcceptQuest
- prop [get] String BtnText_CompleteQuest
- prop [get] QuestGiverView Instance (static)
- prop [get/set] Boolean SortRevert
- prop [get/set] SortingMode SortingMode
- method QuestEntry GetSelection()
- method Boolean SetSelection(QuestEntry selection)
- method Void Setup(QuestGiver target)

## Duckov.Quests.UI.QuestSortButton [class] : UnityEngine.MonoBehaviour

- prop [get] SortingMode SortingMode
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.Quests.UI.QuestStatus [enum]

- None = 0
- Avaliable = 2
- Active = 4
- Finished = 8

## Duckov.Quests.UI.QuestView [class] : Duckov.UI.View

- prop [get] QuestView Instance (static)
- prop [get] Quest SelectedQuest
- prop [get] IList<Quest> ShowingContent
- prop [get] ShowContent ShowingContentType
- prop [get/set] Boolean SortRevert
- prop [get/set] SortingMode SortingMode
- method QuestEntry GetSelection()
- method Boolean SetSelection(QuestEntry selection)
- method Void SetShowingContent(ShowContent flags)
- method Void Setup()
- method Void Show() (static)
- method Void ShowActiveQuests()
- method Void ShowHistoryQuests()

## Duckov.Quests.UI.QuestView.ShowContent [enum]

- Active = 1
- History = 2

## Duckov.Quests.UI.QuestViewDetails [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Interactable
- prop [get] Quest Target

## Duckov.Quests.UI.RewardEntry [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Interactable
- method Void NotifyPooled()
- method Void NotifyReleased()

## Duckov.Quests.UI.TaskEntry [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Interactable
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.RichPresenceManager [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean isPlaying
- field Action<RichPresenceManager> OnInstanceChanged (static)

## Duckov.Rules.GameRulesManager [class] : UnityEngine.MonoBehaviour

- prop [get] Ruleset Current (static)
- prop [get] GameRulesManager Instance (static)
- prop [get/set] RuleIndex SelectedRuleIndex (static)
- method RuleIndex GetRuleIndexOfSaveSlot(Int32 slot) (static)
- method Void NotifyRuleChanged() (static)

## Duckov.Rules.RuleIndex [enum]

- Standard = 0
- Custom = 1
- Easy = 11
- ExtraEasy = 12
- StandardChallenge = 13
- Hard = 21
- ExtraHard = 22
- Rage = 23

## Duckov.Rules.Ruleset [class]

- prop [get] Boolean AdvancedDebuffMode
- prop [get] Single DamageFactor_ToPlayer
- prop [get] String Description
- prop [get] String DisplayName
- prop [get] Single EnemyAttackTimeFactor
- prop [get] Single EnemyAttackTimeSpaceFactor
- prop [get] Single EnemyHealthFactor
- prop [get] Single EnemyReactionTimeFactor
- prop [get] Boolean FogOfWar
- prop [get] Single RecoilMultiplier
- prop [get] Int32 SaveDeadbodyCount
- prop [get] Boolean SpawnDeadBody

## Duckov.Rules.RulesetFile [class] : UnityEngine.ScriptableObject

- prop [get] Ruleset Data

## Duckov.Rules.UI.DifficultySelection [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean CustomDifficultyMarker (static)
- prop [get/set] DifficultySelection_Entry HoveringEntry
- prop [get/set] DifficultySelection_Entry SelectedEntry
- prop [get] RuleIndex SelectedRuleIndex
- method UniTask Execute()
- method Boolean GetRageUnlocked(Boolean isFirstSelect)
- method Void UnlockRage() (static)

## Duckov.Rules.UI.DifficultySelection.SettingEntry [struct]

- prop [get] String Description
- prop [get] String Title

## Duckov.Rules.UI.DifficultySelection_Entry [class] : UnityEngine.MonoBehaviour

- prop [get/set] DifficultySelection Master
- prop [get/set] SettingEntry Setting
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)

## Duckov.Scenes.MultiSceneCore [class] : UnityEngine.MonoBehaviour

- prop [get] Nullable<Scene> ActiveSubScene (static)
- prop [get] String ActiveSubSceneID (static)
- prop [get] String DisplayName
- prop [get] String DisplaynameRaw
- prop [get/set] MultiSceneCore Instance (static)
- prop [get] Boolean IsLoading
- prop [get] Nullable<Scene> MainScene (static)
- prop [get] String MainSceneID (static)
- prop [get] SceneInfoEntry SceneInfo
- prop [get] List<SubSceneEntry> SubScenes
- method Void BeginLoadSubScene(SceneReference reference)
- method Void CacheLocations()
- method Void CacheTeleporters()
- method Transform GetSetActiveWithSceneParent(Int32 sceneBuildIndex)
- method SubSceneEntry GetSubSceneInfo()
- method Boolean GetVisited(String sceneID) (static)
- method UniTask<Boolean> LoadAndTeleport(MultiSceneLocation location)
- method UniTask<Boolean> LoadAndTeleport(String sceneID, Vector3 position, Boolean subSceneLocation)
- method Void MoveToActiveWithScene(GameObject go) (static)
- method Void MoveToActiveWithScene(GameObject go, Int32 sceneBuildIndex) (static)
- method Void MoveToMainScene(GameObject gameObject) (static)
- method Void PlayStinger()
- method Void SetVisited(String sceneID) (static)

## Duckov.Scenes.MultiSceneLocation [struct]

- prop [get] String DisplayName
- prop [get/set] String LocationName
- prop [get/set] Transform LocationTransform
- prop [get] SceneReference Scene
- prop [get/set] String SceneID
- method Transform GetLocationTransform()
- method Boolean TryGetLocationPosition(Vector3& result)

## Duckov.Scenes.MultiSceneTeleporter [class] : InteractableBase

- prop [get] MultiSceneLocation Target
- method Void DoTeleport()

## Duckov.Scenes.SceneLocationsProvider [class] : UnityEngine.MonoBehaviour

- prop [get] ReadOnlyCollection<SceneLocationsProvider> ActiveProviders (static)
- method List<ValueTuple<String, Vector3, GameObject>> GetAllPathsAndItsPosition()
- method Transform GetLocation(String path)
- method Transform GetLocation(SceneReference scene, String name) (static)
- method Transform GetLocation(Int32 sceneBuildIndex, String name) (static)
- method Transform GetLocation(String sceneID, String name) (static)
- method SceneLocationsProvider GetProviderOfScene(SceneReference sceneReference) (static)
- method SceneLocationsProvider GetProviderOfScene(Scene scene) (static)
- method Boolean TryGetPath(Transform value, String& path)

## Duckov.Scenes.SubSceneEntry [class]

- prop [get] String AmbientSound
- prop [get] String DisplayName
- prop [get] SceneInfoEntry Info
- prop [get] Boolean IsInDoor
- prop [get] SceneReference SceneReference

## Duckov.Scenes.SubSceneEntry.Location [class]

- prop [get] String DisplayName
- prop [get/set] String DisplayNameRaw

## Duckov.Sounds.SoundDisplay [class] : UnityEngine.MonoBehaviour

- prop [get] AISound CurrentSount
- prop [get] Single Value

## Duckov.Splines.Bevel [class]

- method Vector3[] Evaluate(Vector3 cur, Vector3 prev, Vector3 next, Int32 step, Single offset, Vector3& o, Vector3& axis, Single protectionOffset, Boolean useProtectionOffset, Single clipDistance) (static)

## Duckov.Splines.BeveledLineShape [class] : ShapeProvider

- prop [get] List<Vector3> points
- method OrientedPoint[] GenerateShape()

## Duckov.StrongNotification [class] : UnityEngine.MonoBehaviour

- prop [get/set] StrongNotification Instance (static)
- prop [get] Boolean Showing (static)
- method Void OnPointerClick(PointerEventData eventData)
- method Void Push(StrongNotificationContent content) (static)
- method Void Push(String mainText, String subText) (static)

## Duckov.Tasks.Credits [class] : UnityEngine.MonoBehaviour

- method Void Begin()
- method Boolean IsComplete()
- method Boolean IsPending()
- method Void Skip()

## Duckov.Tasks.ITaskBehaviour [interface]

- method Void Begin()
- method Boolean IsComplete()
- method Boolean IsPending()
- method Void Skip()

## Duckov.Tasks.ParallelTask [class] : UnityEngine.MonoBehaviour

- method Void Begin()
- method Boolean IsComplete()
- method Boolean IsPending()

## Duckov.Tasks.PlayTimelineTask [class] : UnityEngine.MonoBehaviour

- method Void Begin()
- method Boolean IsComplete()
- method Boolean IsPending()
- method Void Skip()

## Duckov.Tasks.TaskList [class] : UnityEngine.MonoBehaviour

- method Void Begin()
- method Boolean IsComplete()
- method Boolean IsPending()
- method Void Skip()

## Duckov.Tips.TipsDisplay [class] : UnityEngine.MonoBehaviour

- method Void Display(String tipID)
- method Void DisplayRandom()

## Duckov.UI.Animations.ButtonAnimation [class] : UnityEngine.MonoBehaviour

- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)

## Duckov.UI.Animations.FadeElement [class] : UnityEngine.MonoBehaviour

- prop [get] UniTask ActiveTask
- prop [get/set] Boolean IsFading
- method UniTask Hide()
- method UniTask Show(Single delay)
- method Void SkipHide()

## Duckov.UI.Animations.FadeGroup [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean IsFading
- prop [get] Boolean IsHidden
- prop [get] Boolean IsHidingInProgress
- prop [get] Boolean IsShowingInProgress
- prop [get] Boolean IsShown
- method Void Hide()
- method UniTask HideAndReturnTask()
- method UniTask HideTask()
- method Void Show()
- method UniTask ShowAndReturnTask()
- method UniTask ShowTask()
- method Void SkipHide()
- method Void Toggle()

## Duckov.UI.Animations.LooperClock [class] : UnityEngine.MonoBehaviour

- prop [get] Single t

## Duckov.UI.Animations.MaterialPropertyFade [class] : Duckov.UI.Animations.FadeElement

- prop [get/set] Single Duration
- prop [get/set] AnimationCurve HideCurve
- prop [get/set] AnimationCurve ShowCurve

## Duckov.UI.Animations.ToggleAnimation [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Status
- method Void SetToggle(Boolean value)

## Duckov.UI.BarDisplay [class] : UnityEngine.MonoBehaviour

- method Void SetValue(Int32 current, Int32 max, Int32 min)
- method Void SetValue(Single current, Single max, String format, Single min)
- method Void Setup(String labelText, Color color, Int32 current, Int32 max, Int32 min)
- method Void Setup(String labelText, Color color, Single current, Single max, String format, Single min)
- method Void SetupLook(String labelText, Color color)

## Duckov.UI.BlackScreen [class] : UnityEngine.MonoBehaviour

- prop [get] BlackScreen Instance (static)
- method UniTask HideAndReturnTask(AnimationCurve animationCurve, Single circleFade, Single duration) (static)
- method UniTask ShowAndReturnTask(AnimationCurve animationCurve, Single circleFade, Single duration) (static)

## Duckov.UI.BuffDetailsOverlay [class] : UnityEngine.MonoBehaviour

- prop [get] Buff Target
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(Buff target)
- method Void Setup(BuffsDisplayEntry target)
- method Void Show()

## Duckov.UI.BuffsDisplay [class] : UnityEngine.MonoBehaviour

- method Void ReleaseEntry(BuffsDisplayEntry entry)

## Duckov.UI.BuffsDisplayEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Image Icon
- prop [get] Buff Target
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnPointerClick(PointerEventData eventData)
- method Void Release()
- method Void Setup(BuffsDisplay master, Buff target)

## Duckov.UI.CloseViewOnPointerClick [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## Duckov.UI.ClosureView [class] : Duckov.UI.View

- prop [get] ClosureView Instance (static)
- field IClosureView OverrideClosureView (static)
- method UniTask ShowAndReturnTask(Single duration) (static)
- method UniTask ShowAndReturnTask(DamageInfo dmgInfo, Single duration) (static)

## Duckov.UI.CopyTextButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## Duckov.UI.DebugKontextMenuInvoker [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)
- method Void Show(Vector2 point)

## Duckov.UI.DialogueBubbles.DialogueBubble [class] : UnityEngine.MonoBehaviour

- prop [get] Transform Target
- method Void Interact()
- method Void OnPointerClick(PointerEventData eventData)
- method UniTask Show(String text, Transform target, Single yOffset, Boolean needInteraction, Boolean skippable, Single speed, Single duration)
- method UniTask ShowTask(String text, Transform target, Single yOffset, Boolean needInteraction, Boolean skippable, Single speed, Single duration)

## Duckov.UI.DialogueBubbles.DialogueBubblesManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] DialogueBubblesManager Instance (static)
- method Void OnPointerClick(PointerEventData eventData)
- method UniTask Show(String text, Transform target, Single yOffset, Boolean needInteraction, Boolean skippable, Single speed, Single duration) (static)

## Duckov.UI.ExtraBuffViewEntry [class] : UnityEngine.MonoBehaviour

- prop [get/set] ExtraBuffView Master
- prop [get/set] Buff target
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)

## Duckov.UI.ExtraBuffViewHoverPanel [class] : UnityEngine.MonoBehaviour

- method Void SkipHide()
- method Void SkipShow()

## Duckov.UI.FormulasIndexEntry [class] : UnityEngine.MonoBehaviour

- prop [get] CraftingFormula Formula
- prop [get] Boolean Valid
- method Void OnPointerClick(PointerEventData eventData)
- method Void Refresh()

## Duckov.UI.FormulasIndexView [class] : Duckov.UI.View

- prop [get] FormulasIndexView Instance (static)
- method FormulasIndexEntry GetSelection()
- method Boolean SetSelection(FormulasIndexEntry selection)
- method Void Show() (static)

## Duckov.UI.FormulasRegisterView [class] : Duckov.UI.View

- prop [get] FormulasRegisterView Instance (static)
- method String GetFormulaID(Item item) (static)
- method Void Show(ICollection<Tag> requireTags) (static)

## Duckov.UI.GameplayUIManager [class] : UnityEngine.MonoBehaviour

- prop [get] View ActiveView
- prop [get] GameplayUIManager Instance (static)
- prop [get] PrefabPool<InventoryEntry> InventoryEntryPool
- prop [get] PrefabPool<ItemDisplay> ItemDisplayPool
- prop [get] PrefabPool<SlotDisplay> SlotDisplayPool
- prop [get] SplitDialogue SplitDialogue
- method T GetViewInstance() (static)
- method UniTask ReverseTemporaryHide() (static)
- method UniTask TemporaryHide() (static)

## Duckov.UI.GenericButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)

## Duckov.UI.HealthBar [class] : UnityEngine.MonoBehaviour

- prop [get/set] Health target
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void RefreshOffset()
- method Void Setup(Health target, Nullable<DamageInfo> damage, Action releaseAction)

## Duckov.UI.HealthBarManager [class] : UnityEngine.MonoBehaviour

- prop [get] HealthBarManager Instance (static)
- method Void RequestHealthBar(Health health, Nullable<DamageInfo> damage) (static)

## Duckov.UI.IClosureView [interface]

- method UniTask Task(Boolean evacuated)

## Duckov.UI.ITooltipsProvider [interface]

- method String GetTooltipsText()

## Duckov.UI.Inventories.PagesControl [class] : UnityEngine.MonoBehaviour

- method Void Setup(InventoryDisplay target)

## Duckov.UI.InventoryDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Editable
- prop [get] Func<Item, Boolean> Func_CanOperate
- prop [get] Func<Item, Boolean> Func_ShouldHighlight
- prop [get] Int32 MaxPage
- prop [get/set] Boolean Movable
- prop [get] Int32 SelectedPage
- prop [get/set] Boolean ShowOperationButtons
- prop [get/set] Boolean ShowSortButton
- prop [get/set] Inventory Target
- prop [get] Boolean UsePages
- method Void DisableItem(Item item)
- method Boolean IsShortcut(Int32 index)
- method Void NextPage()
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void PreviousPage()
- method Void SetFilter(Func<Item, Boolean> filter)
- method Void SetPage(Int32 page)
- method Void Setup(Inventory target, Func<Item, Boolean> funcShouldHighLight, Func<Item, Boolean> funcCanOperate, Boolean movable, Func<Item, Boolean> filter)

## Duckov.UI.InventoryEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean CanOperate
- prop [get] Item Content
- prop [get/set] Boolean Disabled
- prop [get] Boolean Editable
- prop [get] Int32 Index
- prop [get] Item Item
- prop [get/set] InventoryDisplay Master
- prop [get] Boolean Movable
- prop [get] PrefabPool<InventoryEntry> Pool (static)
- prop [get] Boolean ShouldHighlight
- field Single doubleClickTimeThreshold (static)
- method InventoryEntry Get() (static)
- method Item GetItem()
- method Boolean IsEditable()
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnDrag(PointerEventData eventData)
- method Void OnDrop(PointerEventData eventData)
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void Release(InventoryEntry item) (static)
- method Void Setup(InventoryDisplay master, Int32 index, Boolean disabled)
- method Void ToggleLock()

## Duckov.UI.InventoryEntryTradingPriceDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Selling

## Duckov.UI.InventoryView [class] : Duckov.UI.View

- method Void Hide() (static)
- method Void Show() (static)

## Duckov.UI.IslandClosureView [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)
- method UniTask Task(Boolean evacuated)

## Duckov.UI.ItemCustomizeSelectionView [class] : Duckov.UI.View

- prop [get] ItemCustomizeSelectionView Instance (static)
- method Void Hide() (static)
- method Void Show() (static)

## Duckov.UI.ItemCustomizeView [class] : Duckov.UI.View

- prop [get] ItemCustomizeView Instance (static)
- prop [get] Item Target
- method Void DebugSetup(Item target, Inventory inventory1, Inventory inventory2)
- method SlotDisplay GetSelection()
- method Boolean SetSelection(SlotDisplay selection)
- method Void Setup(Item target, List<Inventory> avaliableInventories)

## Duckov.UI.ItemDecomposeView [class] : Duckov.UI.View

- prop [get] ItemDecomposeView Instance (static)

## Duckov.UI.ItemDetailsDisplay [class] : UnityEngine.MonoBehaviour

- prop [get] ItemSlotCollectionDisplay SlotCollectionDisplay
- prop [get] Item Target

## Duckov.UI.ItemDetailsPanel [class] : Duckov.UI.ManagedUIElement

- method Void Open(Item target, ManagedUIElement source)
- method Void Show(Item target, ManagedUIElement source) (static)

## Duckov.UI.ItemDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean CanDrop
- prop [get/set] Boolean CanLockSort
- prop [get] Boolean CanSetShortcut
- prop [get] Boolean CanSplit
- prop [get] Boolean CanUse
- prop [get/set] Boolean Editable
- prop [get/set] Boolean IsStockshopSample
- prop [get/set] Boolean Movable
- prop [get] PrefabPool<ItemDisplay> Pool (static)
- prop [get] Boolean Selected
- prop [get/set] Boolean ShowOperationButtons
- prop [get/set] Item Target
- field Single doubleClickTimeThreshold (static)
- method ItemDisplay Get() (static)
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void NotifySelected()
- method Void NotifyUnselected()
- method Void OnDrop(PointerEventData eventData)
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)
- method Void Punch()
- method Void Release(ItemDisplay item) (static)
- method Void Setup(Item target)

## Duckov.UI.ItemEffectEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void Setup(Effect target)

## Duckov.UI.ItemHoveringUI [class] : UnityEngine.MonoBehaviour

- prop [get/set] Int32 DisplayingItemID (static)
- prop [get/set] ItemHoveringUI Instance (static)
- prop [get] RectTransform LayoutParent
- prop [get] Boolean Shown (static)

## Duckov.UI.ItemModifierEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()

## Duckov.UI.ItemOperationMenu [class] : Duckov.UI.ManagedUIElement

- prop [get/set] ItemOperationMenu Instance (static)
- method Void OnPointerClick(PointerEventData eventData)
- method Void Show(ItemDisplay id) (static)

## Duckov.UI.ItemPicker [class] : UnityEngine.MonoBehaviour

- prop [get/set] ItemPicker Instance (static)
- prop [get] Boolean Picking
- method Void Cancel()
- method Void ConfirmPick(Item item)
- method UniTask<Item> Pick(ICollection<Item> candidates) (static)

## Duckov.UI.ItemPickerEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void Setup(ItemPicker master, Item item)

## Duckov.UI.ItemRepairView [class] : Duckov.UI.View

- prop [get] ItemRepairView Instance (static)
- method Int32 CalculateRepairPrice(List<Item> itemsToRepair)
- method List<Item> GetAllEquippedItems()
- method Void Hide() (static)
- method Void RepairItems(List<Item> itemsToRepair)
- method Void Show() (static)

## Duckov.UI.ItemShortcutButton [class] : UnityEngine.MonoBehaviour

- prop [get/set] CharacterMainControl Character
- prop [get/set] Int32 Index
- prop [get/set] Inventory Inventory
- prop [get/set] ItemShortcutPanel Master
- prop [get/set] Item TargetItem
- method Void AnimateDenial()
- method Void AnimateDenial(Int32 index) (static)
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.UI.ItemShortcutEditorEntry [class] : UnityEngine.MonoBehaviour

- method Item GetItem()
- method Boolean IsEditable()
- method Void OnDrag(PointerEventData eventData)
- method Void OnDrop(PointerEventData eventData)
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)

## Duckov.UI.ItemShortcutPanel [class] : UnityEngine.MonoBehaviour

- prop [get/set] CharacterMainControl Character
- prop [get/set] Inventory Target

## Duckov.UI.ItemSlotCollectionDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean ContentSelectable
- prop [get/set] Boolean Editable
- prop [get/set] Boolean Movable
- prop [get] Boolean ShowOperationMenu
- prop [get/set] Item Target
- method Void Setup(Item target, Boolean movable)

## Duckov.UI.ItemStatEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()

## Duckov.UI.ItemUIUtilities [class]

- prop [get] Boolean IsGunSelected (static)
- prop [get] Item SelectedItem (static)
- prop [get] String SelectedItemCaliber (static)
- prop [get/set] ItemDisplay SelectedItemDisplay (static)
- prop [get] ItemDisplay SelectedItemDisplayRaw (static)
- method String GetPropertiesDisplayText(Item item) (static)
- method List<ValueTuple<String, String, Polarity>> GetPropertyValueTextPair(Item item) (static)
- method Void NotifyPutItem(Item item, Boolean pickup) (static)
- method Void RaiseOrphan(Item orphan) (static)
- method Void Select(ItemDisplay itemDisplay) (static)

## Duckov.UI.ItemVariableEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()

## Duckov.UI.KontextMenu [class] : UnityEngine.MonoBehaviour

- method Void Hide(Object target) (static)
- method Void InstanceHide()
- method Void InstanceShow(Object target, RectTransform targetRectTransform, KontextMenuDataEntry[] entries)
- method Void InstanceShow(Object target, Vector2 screenPoint, KontextMenuDataEntry[] entries)
- method Void Show(Object target, RectTransform watchRectTransform, KontextMenuDataEntry[] entries) (static)
- method Void Show(Object target, Vector2 position, KontextMenuDataEntry[] entries) (static)

## Duckov.UI.KontextMenuDataEntry [class]

- method Void Invoke()

## Duckov.UI.KontextMenuEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(KontextMenu menu, Int32 index, KontextMenuDataEntry data)

## Duckov.UI.LootView [class] : Duckov.UI.View

- prop [get] LootView Instance (static)
- prop [get] Inventory TargetInventory
- method Boolean HasInventoryEverBeenLooted(Inventory inventory) (static)
- method Void LootItem(Item item) (static)
- method Void Show()

## Duckov.UI.MainMenu.SaveSlotSelectionMenu [class] : UnityEngine.MonoBehaviour

- method Void Finish()

## Duckov.UI.ManagedUIElement [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean open
- method Void Close()
- method Void Open(ManagedUIElement parent)

## Duckov.UI.MapSelectionEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 BeaconIndex
- prop [get] Boolean ConditionsSatisfied
- prop [get] Cost Cost
- prop [get] Sprite FullScreenImage
- prop [get] String SceneID
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(MapSelectionView master)

## Duckov.UI.MapSelectionView [class] : Duckov.UI.View

- prop [get] MapSelectionView Instance (static)

## Duckov.UI.NoteIndexView [class] : Duckov.UI.View

- method Void DoOpen()
- method Void SetDisplayTargetNote(String noteKey)
- method Void ShowNote(String noteKey, Boolean unlock) (static)

## Duckov.UI.NoteIndexView_Entry [class] : UnityEngine.MonoBehaviour

- prop [get] String key
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.UI.NotificationText [class] : UnityEngine.MonoBehaviour

- method Void Push(String text) (static)

## Duckov.UI.PerkDetails [class] : UnityEngine.MonoBehaviour

- method Void Setup(Perk perk, Boolean editable)

## Duckov.UI.PerkEntry [class] : UnityEngine.MonoBehaviour

- prop [get] RectTransform RectTransform
- prop [get] Perk Target
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnPointerClick(PointerEventData eventData)
- method Void Setup(PerkTreeView master, Perk target)

## Duckov.UI.PerkLineEntry [class] : UnityEngine.MonoBehaviour

- prop [get] RectTransform RectTransform

## Duckov.UI.PerkTreeView [class] : Duckov.UI.View

- prop [get] PerkTreeView Instance (static)
- method PerkEntry GetSelection()
- method Void Hide()
- method Boolean SetSelection(PerkEntry selection)
- method Void Show(PerkTree target) (static)

## Duckov.UI.PlayerStatsView [class] : Duckov.UI.View

- prop [get] PlayerStatsView Instance (static)

## Duckov.UI.PunchReceiver [class] : UnityEngine.MonoBehaviour

- method Void Punch()

## Duckov.UI.RequireItemEntry [class] : UnityEngine.MonoBehaviour

- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void Setup(RequireItemEntry target)

## Duckov.UI.SavesRestore.SavesBackupRestorePanel [class] : UnityEngine.MonoBehaviour

- method Void Cancel()
- method Void Close()
- method Void Confirm()
- method Void Open(Int32 savesSlot)

## Duckov.UI.SavesRestore.SavesBackupRestorePanelEntry [class] : UnityEngine.MonoBehaviour

- prop [get] BackupInfo Info
- method Void OnPointerClick(PointerEventData eventData)

## Duckov.UI.ScrollViewMaxHeight [class] : UnityEngine.EventSystems.UIBehaviour

- prop [get] Single flexibleHeight
- prop [get] Single flexibleWidth
- prop [get] Int32 layoutPriority
- prop [get] Single minHeight
- prop [get] Single minWidth
- prop [get] Single preferredHeight
- prop [get] Single preferredWidth
- method Void CalculateLayoutInputHorizontal()
- method Void CalculateLayoutInputVertical()

## Duckov.UI.ShowcaseView [class] : Duckov.UI.View

- method Void Show(Showcase target) (static)

## Duckov.UI.SleepView [class] : Duckov.UI.View

- prop [get] SleepView Instance (static)
- field Action OnAfterSleep (static)
- method Void Show() (static)

## Duckov.UI.SliderWithTextField [class] : UnityEngine.MonoBehaviour

- prop [get/set] String LabelKey
- prop [get/set] Single Value
- method Single GetValue()
- method Void SetValue(Single value)
- method Void SetValueWithoutNotify(Single value)

## Duckov.UI.SlotDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean ContentSelectable
- prop [get/set] Boolean Editable
- prop [get/set] Boolean Movable
- prop [get] PrefabPool<SlotDisplay> Pool (static)
- prop [get/set] Boolean ShowOperationMenu
- prop [get/set] Slot Target
- method Void DenialPunch()
- method SlotDisplay Get() (static)
- method Item GetItem()
- method Boolean IsEditable()
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void OnDrag(PointerEventData eventData)
- method Void OnDrop(PointerEventData eventData)
- method Void OnPointerClick(PointerEventData eventData)
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void Punch()
- method Void Release(SlotDisplay item) (static)
- method Void Setup(Slot target)

## Duckov.UI.SlotDisplayOperationContext.Operation [enum]

- None = 0
- Equip = 1
- Unequip = 2
- Deny = 3

## Duckov.UI.SlotIndicator [class] : UnityEngine.MonoBehaviour

- prop [get/set] Slot Target
- method Void NotifyPooled()
- method Void NotifyReleased()
- method Void Setup(Slot target)

## Duckov.UI.StatInfoDatabase [class] : UnityEngine.ScriptableObject

- prop [get] StatInfoDatabase Instance (static)
- method Entry Get(String statName) (static)
- method Polarity GetPolarity(String statName) (static)

## Duckov.UI.StatInfoDatabase.Entry [struct]

- prop [get] String DisplayFormat

## Duckov.UI.StorageDock [class] : Duckov.UI.View

- prop [get] StorageDock Instance (static)
- method Void GotoPage(Int32 page)
- method Void NextPage()
- method Void PrevPage()

## Duckov.UI.StorageDockEntry [class] : UnityEngine.MonoBehaviour

- method Void Setup(Int32 index, ItemTreeData item)

## Duckov.UI.TagsDisplay [class] : UnityEngine.MonoBehaviour

- method Void Setup(Item item)

## Duckov.UI.TagsDisplayEntry [class] : UnityEngine.MonoBehaviour

- method String GetTooltipsText()
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void Setup(Tag tag)

## Duckov.UI.Tooltips [class] : UnityEngine.MonoBehaviour

- prop [get/set] ITooltipsProvider CurrentProvider (static)
- method Void NotifyEnterTooltipsProvider(ITooltipsProvider provider) (static)
- method Void NotifyExitTooltipsProvider(ITooltipsProvider provider) (static)

## Duckov.UI.TooltipsProvider [class] : UnityEngine.MonoBehaviour

- method String GetTooltipsText()
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)

## Duckov.UI.TradingUIUtilities [class]

- prop [get/set] IMerchant ActiveMerchant (static)

## Duckov.UI.UIPrefabsReference [class] : UnityEngine.ScriptableObject

- prop [get] Button Button
- prop [get] InventoryEntry InventoryEntry
- prop [get] ItemDisplay ItemDisplay
- prop [get] ScrollRect ScrollRect
- prop [get] SlotDisplay SlotDisplay
- prop [get] SlotIndicator SlotIndicator

## Duckov.UI.UsageUtilitiesDisplay [class] : UnityEngine.MonoBehaviour

- prop [get/set] UsageUtilities Target
- method Void Setup(Item item)

## Duckov.UI.UsageUtilitiesDisplay_Entry [class] : UnityEngine.MonoBehaviour

- prop [get/set] UsageBehavior Target

## Duckov.UI.View [class] : Duckov.UI.ManagedUIElement

- prop [get/set] View ActiveView (static)
- method Void OnCancel(UIInputEventData eventData)
- method Void OnConfirm(UIInputEventData eventData)
- method Void OnNavigate(UIInputEventData eventData)

## Duckov.UI.WeaponButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## Duckov.Utilities.CommonVariables [class] : UnityEngine.MonoBehaviour

- prop [get] CustomDataCollection Data
- method Boolean GetBool(String key, Boolean defaultValue) (static)
- method Boolean GetBool(Int32 hash, Boolean defaultValue) (static)
- method Single GetFloat(String key, Single defaultValue) (static)
- method Single GetFloat(Int32 hash, Single defaultValue) (static)
- method Int32 GetInt(String key, Int32 defaultValue) (static)
- method Int32 GetInt(Int32 hash, Int32 defaultValue) (static)
- method String GetString(String key, String defaultValue) (static)
- method String GetString(Int32 hash, String defaultValue) (static)
- method Void SetBool(String key, Boolean value) (static)
- method Void SetFloat(String key, Single value) (static)
- method Void SetInt(String key, Int32 value) (static)
- method Void SetString(String key, String value) (static)

## Duckov.Utilities.FishSpawner [class] : UnityEngine.MonoBehaviour

- method Void CalculateChances()
- method Int32[] Search(ItemFilter filter) (static)
- method UniTask<Item> Spawn(Int32 baitID, Single luck)

## Duckov.Utilities.GameplayDataSettings [class] : UnityEngine.ScriptableObject

- prop [get] AchievementDatabase AchievementDatabase (static)
- prop [get] BuffsData Buffs (static)
- prop [get] BuildingDataCollection BuildingDataCollection (static)
- prop [get] CharacterRandomPresets CharacterRandomPresetData (static)
- prop [get] CraftingFormulaCollection CraftingFormulas (static)
- prop [get] CropDatabase CropDatabase (static)
- prop [get] CustomFaceData CustomFaceData (static)
- prop [get] DecomposeDatabase DecomposeDatabase (static)
- prop [get] PerkTreeIDList DefaultEnabledPerkTrees (static)
- prop [get] DialogData DialogDatas (static)
- prop [get] EconomyData Economy (static)
- prop [get] InputActionAsset InputActions (static)
- prop [get] ItemAssetsData ItemAssets (static)
- prop [get] LayersData Layers (static)
- prop [get] LootingData Looting (static)
- prop [get] PrefabsData Prefabs (static)
- prop [get] QuestCollection QuestCollection (static)
- prop [get] QuestRelationGraph QuestRelation (static)
- prop [get] QuestsData Quests (static)
- prop [get] SceneManagementData SceneManagement (static)
- prop [get] StatInfoDatabase StatInfo (static)
- prop [get] StockShopDatabase StockshopDatabase (static)
- prop [get] StringListsData StringLists (static)
- prop [get] TagsData Tags (static)
- prop [get] UIPrefabsReference UIPrefabs (static)
- prop [get] UIStyleData UIStyle (static)

## Duckov.Utilities.GameplayDataSettings.BuffsData [class]

- prop [get] Buff BaseBuff
- prop [get] Buff BleedSBuff
- prop [get] Buff BoneCrackBuff
- prop [get] Buff Burn
- prop [get] Buff Cold
- prop [get] Buff Electric
- prop [get] Buff Hypoxia
- prop [get] Buff Pain
- prop [get] Buff Poison
- prop [get] Buff Space
- prop [get] Buff Starve
- prop [get] Buff SuperCold
- prop [get] Buff Thirsty
- prop [get] Buff UnlimitBleedBuff
- prop [get] Buff Warm
- prop [get] Buff Weight_Heavy
- prop [get] Buff Weight_Light
- prop [get] Buff Weight_Overweight
- prop [get] Buff Weight_SuperHeavy
- prop [get] Buff WoundBuff
- method String GetBuffDisplayName(Int32 id)

## Duckov.Utilities.GameplayDataSettings.DialogData [class]

- method String GetColdDialog()
- method String GetSuperColdDialog()

## Duckov.Utilities.GameplayDataSettings.EconomyData [class]

- prop [get] ReadOnlyCollection<Int32> UnlockedItemByDefault

## Duckov.Utilities.GameplayDataSettings.ItemAssetsData [class]

- prop [get] Int32 CashItemTypeID
- prop [get] Int32 DefaultCharacterItemTypeID

## Duckov.Utilities.GameplayDataSettings.LayersData [class]

- method Boolean IsLayerInLayerMask(Int32 layer, LayerMask layerMask) (static)

## Duckov.Utilities.GameplayDataSettings.LootingData [class]

- method Single GetInspectingTime(Item item) (static)
- method Single MGetInspectingTime(Item item)

## Duckov.Utilities.GameplayDataSettings.PrefabsData [class]

- prop [get] GameObject AlertFxPrefab
- prop [get] GameObject BlockBulletFx
- prop [get] GameObject BuildingBlockAreaMesh
- prop [get] GameObject BulletBackFx
- prop [get] GameObject BulletHitObsticleFx
- prop [get] CharacterMainControl CharacterPrefab
- prop [get] Projectile DefaultBullet
- prop [get] CharacterModel DefaultCharacterModel
- prop [get] DuckovItemAgent HandheldAgentPrefab
- prop [get] HeadCollider HeadCollider
- prop [get] InteractMarker InteractMarker
- prop [get] ItemGraphicInfo ItemGraphic_Sprite
- prop [get] GameObject KazooUi
- prop [get] LevelManager LevelManagerPrefab
- prop [get] InteractableLootbox LootBoxPrefab
- prop [get] InteractableLootbox LootBoxPrefab_Tomb
- prop [get] BulletBlocker MeleeBulletBlocker
- prop [get] DuckovItemAgent PickupAgentNoRendererPrefab
- prop [get] DuckovItemAgent PickupAgentPrefab
- prop [get] GameObject QuestMarker
- prop [get] GameObject QuestMarkerPOI
- prop [get] ParticleSystem SwimWaveFx
- prop [get] UIInputManager UIInputManagerPrefab

## Duckov.Utilities.GameplayDataSettings.QuestsData [class]

- prop [get] QuestCollection QuestCollection
- prop [get] QuestRelationGraph QuestRelation
- method String GetDisplayName(QuestGiverID id)
- method QuestGiverInfo GetInfo(QuestGiverID id)

## Duckov.Utilities.GameplayDataSettings.QuestsData.QuestGiverInfo [class]

- prop [get] String DisplayName

## Duckov.Utilities.GameplayDataSettings.SceneManagementData [class]

- prop [get] SceneReference BaseScene
- prop [get] SceneReference EvacuateScreenScene
- prop [get] SceneReference FailLoadingScreenScene
- prop [get] SceneReference MainMenuScene
- prop [get] SceneReference PrologueScene
- prop [get] SceneInfoCollection SceneInfoCollection

## Duckov.Utilities.GameplayDataSettings.SpritesData [class]

- method Sprite GetSprite(String key)

## Duckov.Utilities.GameplayDataSettings.StringListsData [class]

- field StringList ItemAgentKeys (static)
- field StringList SlotTypes (static)
- field StringList StatKeys (static)

## Duckov.Utilities.GameplayDataSettings.TagsData [class]

- prop [get] Tag AdvancedDebuffMode
- prop [get] ReadOnlyCollection<Tag> AllTags
- prop [get] Tag Armor
- prop [get] Tag Backpack
- prop [get] Tag Bait
- prop [get] Tag Bullet
- prop [get] Tag Character
- prop [get] Tag DestroyOnLootBox
- prop [get] Tag DontDropOnDeadInSlot
- prop [get] Tag Gun
- prop [get] Tag Helmat
- prop [get] Tag LockInDemoTag
- prop [get] Tag Special

## Duckov.Utilities.GameplayDataSettings.UIStyleData [class]

- prop [get] Sprite BossCharacterIcon
- prop [get] Sprite CritPopSprite
- prop [get] Sprite DefaultTeleporterIcon
- prop [get] Sprite EleteCharacterIcon
- prop [get] Sprite FallbackItemIcon
- prop [get] Sprite MerchantCharacterIcon
- prop [get] Sprite PetCharacterIcon
- prop [get] Sprite PmcCharacterIcon
- prop [get] Single TeleporterIconScale
- prop [get] TextMeshProUGUI TemplateTextUGUI
- method Void ApplyDisplayQualityShadow(DisplayQuality displayQuality, TrueShadow target)
- method DisplayQualityLook GetDisplayQualityLook(DisplayQuality q)
- method DisplayElementDamagePopTextLook GetElementDamagePopTextLook(ElementTypes elementType)
- method ValueTuple<Single, Color, Boolean> GetShadowOffsetAndColorOfQuality(DisplayQuality displayQuality)

## Duckov.Utilities.GameplayDataSettings.UIStyleData.DisplayQualityLook [class]

- method Void Apply(TrueShadow trueShadow)

## Duckov.Utilities.LootBoxLoader [class] : UnityEngine.MonoBehaviour

- prop [get] List<Int32> FixedItems
- method Void CalculateChances()
- method Int32[] Search(ItemFilter filter) (static)
- method UniTask Setup()
- method Void StartSetup()

## Duckov.Utilities.LootSpawner [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean RandomButNotFromPool
- prop [get] Boolean RandomFromPool
- method Void CalculateChances()
- method Int32[] Search(ItemFilter filter) (static)
- method UniTask Setup()

## Duckov.Utilities.SetActiveByPlayerDistance [class] : UnityEngine.MonoBehaviour

- prop [get] Single Distance
- prop [get/set] SetActiveByPlayerDistance Instance (static)
- method Void Register(GameObject gameObject, Int32 sceneBuildIndex) (static)
- method Void Register(GameObject gameObject, Scene scene) (static)
- method Boolean Unregister(GameObject gameObject, Int32 sceneBuildIndex) (static)
- method Void Unregister(GameObject gameObject, Scene scene) (static)

## Duckov.VersionData [struct]

- method String ToString()

## Duckov.Weathers.Precipitation [class]

- prop [get] Single CloudyThreshold
- prop [get] Single RainyThreshold
- method Single Get()
- method Single Get(TimeSpan dayAndTime)
- method Vector2 GetPerlinNoiseCoord(TimeSpan dayAndTime)
- method Boolean IsCloudy()
- method Boolean IsCloudy(TimeSpan dayAndTime)
- method Boolean IsRainy()
- method Boolean IsRainy(TimeSpan dayAndTime)

## Duckov.Weathers.Seasons [enum]

- spring = 0
- summer = 1
- autumn = 2
- winter = 3

## Duckov.Weathers.Storm [class]

- method Single GetSleepPercent(TimeSpan dayAndTime)
- method TimeSpan GetStormETA(TimeSpan dayAndTime)
- method TimeSpan GetStormIIOverETA(TimeSpan dayAndTime)
- method TimeSpan GetStormIOverETA(TimeSpan dayAndTime)
- method Int32 GetStormLevel(TimeSpan dayAndTime)
- method Single GetStormRemainPercent(TimeSpan dayAndTime)

## Duckov.Weathers.Weather [enum]

- Sunny = 0
- Cloudy = 1
- Rainy = 2
- Stormy_I = 3
- Stormy_II = 4
- Snow = 22

## Duckov.Weathers.WeatherManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean ForceWeather
- prop [get/set] Weather ForceWeatherValue
- prop [get/set] WeatherManager Instance (static)
- prop [get] Seasons Season (static)
- prop [get] Storm Storm
- method Weather GetWeather() (static)
- method Weather GetWeather(TimeSpan dayAndTime) (static)
- method Void SetForceWeather(Boolean forceWeather, Weather value) (static)

## Duckov.WyvrnManager [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Initialized (static)
- prop [get/set] WyvrnManager Instance (static)
- method Void SetEvent(String eventName)
- method IEnumerator Start()

## DuckovDialogueActor [class] : UnityEngine.MonoBehaviour

- prop [get] String ID
- prop [get] String NameKey
- prop [get] Vector3 Offset
- prop [get] Color dialogueColor
- prop [get] Vector3 dialoguePosition
- prop [get] Texture2D portrait
- prop [get] Sprite portraitSprite
- method DuckovDialogueActor Get(String id) (static)
- method Void Register(DuckovDialogueActor actor) (static)
- method Void Unregister(DuckovDialogueActor actor) (static)

## DuckovItemAgent [class] : ItemStatsSystem.ItemAgent

- prop [get] CharacterMainControl Holder
- prop [get] List<Transform> Sockets
- prop [get] IAgentUsable UsableInterface
- method Void AddSocket(Transform socket)
- method CharacterMainControl GetHolder()
- method Transform GetSocket(String socketName, Boolean createNew)
- method Void SetHolder(CharacterMainControl _holder)

## DuckovResolution [struct]

- method Boolean CheckRotioFit(DuckovResolution newRes, DuckovResolution defaultRes)
- method Boolean Equals(Object obj)
- method String ToString()

## Duckvo.Beacons.BeaconManager [class] : UnityEngine.MonoBehaviour

- prop [get/set] BeaconManager Instance (static)
- field Action<String, Int32> OnBeaconUnlocked (static)
- method Boolean GetBeaconUnlocked(String id, Int32 index) (static)
- method Void Load()
- method Void Save()
- method Void UnlockBeacon(String id, Int32 index) (static)

## DummyFish [class] : UnityEngine.MonoBehaviour

- method Void Setup(Aquarium master)

## EdgeLightEntry [class] : UnityEngine.MonoBehaviour

- method Void SetEnabled(Boolean enabled) (static)

## EdgeLightSettings [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## Egg [class] : UnityEngine.MonoBehaviour

- method Void Init(Vector3 spawnPosition, Vector3 spawnVelocity, CharacterMainControl _fromCharacter, CharacterRandomPreset preset, Single _life)

## ElementFactorFilter [class] : ItemStatsSystem.EffectFilter

- prop [get] String DisplayName

## ElementFactorFilter.ElementFactorFilterTypes [enum]

- GreaterThan = 0
- LessThan = 1

## ElementTypes [enum]

- physics = 0
- fire = 1
- poison = 2
- electricity = 3
- space = 4
- ghost = 5
- ice = 6

## Ending [class]

- field Int32 endingIndex (static)

## EndingControl [class] : UnityEngine.MonoBehaviour

- method Void SetEndingIndex()

## EvacuationCountdownUI [class] : UnityEngine.MonoBehaviour

- prop [get] EvacuationCountdownUI Instance (static)
- method Void Release(CountDownArea target) (static)
- method Void Request(CountDownArea target) (static)

## EvacuationCountdownUIProxy [class] : UnityEngine.MonoBehaviour

- method Void Release(CountDownArea target)
- method Void Request(CountDownArea target)

## EventReports.BDSManager [class] : UnityEngine.MonoBehaviour

- field Action<String, String> OnReportCustomEvent (static)

## EventReports.BDSManager.PlayerInfo [struct]

- method PlayerInfo GetCurrent() (static)
- method String GetCurrentJson() (static)
- method String ToJson()

## ExitCreator [class] : UnityEngine.MonoBehaviour

- method Void Spawn()

## ExplosionFxTypes [enum]

- normal = 0
- flash = 1
- fire = 2
- ice = 3
- custom = 4

## ExplosionManager [class] : UnityEngine.MonoBehaviour

- method Void CreateExplosion(Vector3 center, Single radius, DamageInfo dmgInfo, ExplosionFxTypes fxType, Single shakeStrength, Boolean canHurtSelf)

## ExplosionProxy [class] : UnityEngine.MonoBehaviour

- method Void ExplodeImmediately()
- method Void Stop()
- method Void Trigger()

## FX.PopText [class] : UnityEngine.MonoBehaviour

- field PopText instance (static)
- method Void InstancePop(String text, Vector3 worldPosition, Color color, Single size, Sprite sprite)
- method Void Pop(String text, Vector3 worldPosition, Color color, Single size, Sprite sprite) (static)
- method Void Recycle(PopTextEntity entry)

## FX.PopTextEntity [class] : UnityEngine.MonoBehaviour

- prop [get/set] Color Color
- prop [get] Color EndColor
- prop [get] TextMeshPro Tmp
- prop [get] Single timeSinceSpawn
- method Void SetupContent(String text, Sprite sprite)

## FXPool [class] : UnityEngine.MonoBehaviour

- prop [get/set] FXPool Instance (static)
- method ParticleSystem Play(ParticleSystem prefab, Vector3 postion, Quaternion rotation) (static)
- method ParticleSystem Play(ParticleSystem prefab, Vector3 postion, Quaternion rotation, Color color) (static)

## FadeGroupButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## FillWaterAndFood [class] : UnityEngine.MonoBehaviour

- method Void Fill()

## Fishing.FishingPool [class] : UnityEngine.ScriptableObject

- method Int32 GetRandom(WeightModifications[] modifications)

## Fishing.UI.BaitSelectPanel [class] : UnityEngine.MonoBehaviour

- method BaitSelectPanelEntry GetSelection()
- method Boolean SetSelection(BaitSelectPanelEntry selection)

## Fishing.UI.BaitSelectPanelEntry [class] : UnityEngine.MonoBehaviour

- prop [get] Item Target
- method Void OnPointerClick(PointerEventData eventData)

## FishingRod [class] : UnityEngine.MonoBehaviour

- prop [get] Item Bait
- method Boolean UseBait()

## FloatOnWaterVisual [class] : UnityEngine.MonoBehaviour

- method Void CaptureInitialPose()
- method Void ResetToInitialPose()

## FmodEventTester [class] : UnityEngine.MonoBehaviour

- method Void PlayEvent()

## FormulasDetailsDisplay [class] : UnityEngine.MonoBehaviour

- method Void Setup(Nullable<CraftingFormula> formula)

## FrameRateSetting [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## FreeCameraController [class] : UnityEngine.MonoBehaviour

- method Void SetRotation(Quaternion rotation)

## FsrSetting [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## FullScreenOptions [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## FxAction.Sockets [enum]

- root = 0
- helmat = 1
- armor = 2

## GameCamera [class] : UnityEngine.MonoBehaviour

- prop [get] GameCamera Instance (static)
- field Action<GameCamera, CharacterMainControl> OnCameraPosUpdate (static)
- method Void ForceSyncPos()
- method Void FreezeCamera(Single time)
- method Boolean IsOffScreen(Vector3 woorldPos)
- method Void SetTarget(CharacterMainControl newCharacter)
- method Void UpdateFov(Single deltaTime)
- method Void UpdatePosition(Single deltaTime)

## GameCamera.CameraAimingTypes [enum]

- normal = 0
- bounds = 1

## GameClock [class] : UnityEngine.MonoBehaviour

- prop [get] Int64 Day (static)
- prop [get] Int32 Hour (static)
- prop [get/set] GameClock Instance (static)
- prop [get] Int32 Milliseconds (static)
- prop [get] Int32 Minut (static)
- prop [get] TimeSpan Now (static)
- prop [get] Int32 Seconds (static)
- prop [get] TimeSpan TimeOfDay (static)
- method TimeSpan GetRealTimePlayedOfSaveSlot(Int32 saveSlot) (static)
- method Void StepTimeTil(TimeSpan time)

## GameManager [class] : UnityEngine.MonoBehaviour

- prop [get] AchievementManager AchievementManager (static)
- prop [get] AudioManager AudioManager (static)
- prop [get] BlackScreen BlackScreen (static)
- prop [get] Boolean BloodFxOn (static)
- prop [get] GameRulesManager DifficultyManager (static)
- prop [get] EventSystem EventSystem (static)
- prop [get] GameManager Instance (static)
- prop [get] PlayerInput MainPlayerInput (static)
- prop [get] ModManager ModManager (static)
- prop [get] NightVisionVisual NightVision (static)
- prop [get] NoteIndex NoteIndex (static)
- prop [get] PauseMenu PauseMenu (static)
- prop [get] Boolean Paused (static)
- prop [get] SceneLoader SceneLoader (static)
- prop [get] TimeScaleManager TimeScaleManager (static)
- prop [get] UIInputManager UiInputManager (static)
- field Boolean newBoot (static)
- method Void TimeTravelDetected() (static)

## GamingConsoleView [class] : Duckov.UI.View

- prop [get] GamingConsoleView Instance (static)

## GrassOptions [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## Grenade [class] : UnityEngine.MonoBehaviour

- method Void BindAgent(ItemAgent _agent)
- method Void Launch(Vector3 startPoint, Vector3 velocity, CharacterMainControl fromCharacter, Boolean canHurtSelf)
- method Void SetWeaponIdInfo(Int32 typeId)
- method Void StickTo(Transform parent)

## GunSettingExpend_StickBoomOnHurt [class] : UnityEngine.MonoBehaviour

- method Void OnHurtEnemy(DamageReceiver dmgReceiver, DamageInfo dmgInfo)

## HBAOSettings [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## HUDManager [class] : UnityEngine.MonoBehaviour

- method Void RegisterHideToken(Object obj) (static)
- method Void UnregisterHideToken(Object obj) (static)

## HalfObsticle [class] : UnityEngine.MonoBehaviour

- method Void Dead(DamageInfo dmgInfo)
- method Void OnTriggerEnter(Collider other)
- method Void OnTriggerExit(Collider other)

## HandheldAnimationType [enum]

- normal = 1
- gun = 2
- meleeWeapon = 3
- bow = 4

## HandheldSocketTypes [enum]

- normalHandheld = 1
- meleeWeapon = 2
- leftHandSocket = 3

## HeadCollider [class] : UnityEngine.MonoBehaviour

- method Void Init(CharacterMainControl _character)

## Health [class] : UnityEngine.MonoBehaviour

- prop [get] Single BodyArmor
- prop [get/set] Boolean CanDieIfNotRaidMap
- prop [get/set] Single CurrentHealth
- prop [get] Single HeadArmor
- prop [get] Boolean Hidden
- prop [get] Boolean Invincible
- prop [get] Boolean IsDead
- prop [get] Boolean IsMainCharacterHealth
- prop [get] Single MaxHealth
- prop [get/set] Boolean showHealthBar
- method Void AddBuff(Buff buffPfb, CharacterMainControl fromWho, Int32 overrideFromWeaponID)
- method Void AddHealth(Single healthValue)
- method UniTask DestroyOnDelay()
- method Single ElementFactor(ElementTypes type)
- method Boolean Hurt(DamageInfo damageInfo)
- method Void Init()
- method Void RequestHealthBar()
- method Void SetHealth(Single healthValue)
- method Void SetInvincible(Boolean value)
- method Void SetItemAndCharacter(Item _item, CharacterMainControl _character)
- method CharacterMainControl TryGetCharacter()

## HealthBar_DamageBar [class] : UnityEngine.MonoBehaviour

- method UniTask Animate(Single damageBarPostion, Single damageBarWidth, Action onComplete)

## HealthSimpleBase [class] : UnityEngine.MonoBehaviour

- prop [get] Single HealthValue

## HurtVisual [class] : UnityEngine.MonoBehaviour

- prop [get] GameObject DeadFx
- prop [get] GameObject HitFx
- field Int32 hurtHash (static)
- method Void ForceHurt()
- method Void SetHealth(Health _health)
- method Void SetRenderers(List<Renderer> _renderers)

## HurtVisualSettings [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## IAgentUsable [interface]

- method Boolean BeginUse()

## IInitializedQueryHandler [interface]

- method Boolean HasInitialized()

## IItemDragSource [interface]

- method Item GetItem()
- method Boolean IsEditable()

## IItemMetaDataProvider [interface]

- method ItemMetaData GetMetaData()

## IMerchant [interface]

- method Int32 ConvertPrice(Item item, Boolean selling)

## IMiniMapDataProvider [interface]

- prop [get] Vector3 CombinedCenter
- prop [get] Sprite CombinedSprite
- prop [get] List<IMiniMapEntry> Maps
- prop [get] Single PixelSize

## IMiniMapEntry [interface]

- prop [get] Boolean Hide
- prop [get] Boolean NoSignal
- prop [get] Vector2 Offset
- prop [get] Single PixelSize
- prop [get] String SceneID
- prop [get] Sprite Sprite

## INeedInspection [interface]

- prop [get/set] Boolean NeedInspection

## IOnPointsChanged [interface]

- method Void OnPointsChanged()

## ISingleSelectionMenu<EntryType> [interface]

- method EntryType GetSelection()
- method Boolean SetSelection(EntryType selection)

## InputManager [class] : UnityEngine.MonoBehaviour

- prop [get] Vector2 AimScreenPoint
- prop [get] Transform AimTarget
- prop [get] Boolean AimingEnemyHead
- prop [get] CharacterMainControl ControllingCharacter
- prop [get] Boolean InputActived (static)
- prop [get] Vector3 InputAimPoint
- prop [get] InputDevices InputDevice (static)
- prop [get] Vector2 MousePos
- prop [get] Vector2 MoveAxisInput
- prop [get] Boolean TriggerInput
- prop [get] Vector3 WorldMoveInput
- field Int32 MeleeWeaponSlotHash (static)
- field Action OnInteractButtonDown (static)
- field Int32 PrimaryWeaponSlotHash (static)
- field Int32 SecondaryWeaponSlotHash (static)
- field Boolean useRunInputBuffer (static)
- method Void ActiveInput(GameObject source) (static)
- method Void AddRecoil(ItemAgent_Gun gun)
- method Boolean CancleSkill()
- method Void Dash()
- method Void DisableInput(GameObject source) (static)
- method Void Interact()
- method Void PutAway()
- method Void Quack()
- method Void RefreshCharacter(CharacterMainControl newCharacter)
- method Void ReleaseCharacterSkill()
- method Void ReleaseItemSkill()
- method Void SetAdsInput(Boolean ads)
- method Void SetAimInputUsingJoystick(Vector2 _joystickAxisInput)
- method Void SetAimInputUsingMouse(Vector2 mouseDelta)
- method Void SetAimType(AimTypes aimType)
- method Void SetInputDevice(InputDevices _inputDevice) (static)
- method Void SetMousePosition(Vector2 mousePosition)
- method Void SetMoveInput(Vector2 axisInput)
- method Void SetRunInput(Boolean run)
- method Void SetSwitchBulletTypeInput(Int32 dir)
- method Void SetSwitchInteractInput(Int32 dir)
- method Void SetSwitchWeaponInput(Int32 dir)
- method Void SetTrigger(Boolean trigger, Boolean triggerThisFrame, Boolean releaseThisFrame)
- method Void StartCharacterSkillAim()
- method Void StartItemSkillAim()
- method Void StopAction()
- method Void SwitchItemAgent(Int32 index)
- method Void ToggleNightVision()
- method Void ToggleView()

## InputManager.InputDevices [enum]

- mouseKeyboard = 0
- touch = 1

## InputRebinder [class] : UnityEngine.MonoBehaviour

- field Action OnBindingChanged (static)
- field Action<InputAction> OnRebindBegin (static)
- field Action<InputAction> OnRebindComplete (static)
- method Void Clear() (static)
- method Void ClearRebind(String name) (static)
- method Void Load() (static)
- method Void Rebind()
- method UniTask<Boolean> RebindAsync(String name, Int32 index, String[] excludes, Boolean save) (static)
- method Void Save() (static)

## InteractMarker [class] : UnityEngine.MonoBehaviour

- method Void MarkAsUsed()

## InteractSelectionHUD [class] : UnityEngine.MonoBehaviour

- prop [get] InteractableBase InteractTarget
- method Void SetInteractable(InteractableBase _interactable, Boolean _hasUpDown)
- method Void SetSelection(Boolean _select)

## Interact_CustomFace [class] : InteractableBase

- method Void CopyToClipboard()
- method Void PastyDataAndApply()

## InteractableBase [class] : UnityEngine.MonoBehaviour

- prop [get/set] String InteractName
- prop [get] Single InteractTime
- prop [get] Boolean Interacting
- prop [get/set] Boolean MarkerActive
- method Boolean CheckInteractable()
- method Void FinishInteract(CharacterMainControl _interactCharacter)
- method String GetInteractName()
- method InteractableBase GetInteractableInGroup(Int32 index)
- method List<InteractableBase> GetInteractableList()
- method Progress GetProgress()
- method String GetRequiredItemName()
- method Sprite GetRequireditemIcon()
- method Void InteractWithMainCharacter()
- method Void InternalStopInteract()
- method Void RefreshInteract()
- method Void SetMarkerUsed()
- method Boolean StartInteract(CharacterMainControl _interactCharacter)
- method Void StopInteract()
- method ValueTuple<Boolean, Item> TryGetRequiredItem(CharacterMainControl fromCharacter)
- method Void UpdateInteract(CharacterMainControl _interactCharacter, Single deltaTime)

## InteractableBase.WhenToUseRequireItemTypes [enum]

- None = 0
- OnFinshed = 1
- OnTimeOut = 2
- OnStartInteract = 3

## InteractableLootbox [class] : InteractableBase

- prop [get] Dictionary<Int32, Inventory> Inventories (static)
- prop [get] Inventory Inventory
- prop [get] Transform LootBoxInventoriesParent (static)
- prop [get] Boolean Looted
- prop [get] InteractableLootbox Prefab (static)
- prop [get] Boolean ShowSortButton
- prop [get] Boolean UsePages
- method Void CheckHideIfEmpty()
- method InteractableLootbox CreateFromItem(Item item, Vector3 position, Quaternion rotation, Boolean moveToMainScene, InteractableLootbox prefab, Boolean filterDontDropOnDead) (static)
- method Inventory GetOrCreateInventory(InteractableLootbox lootBox) (static)

## InteractableLootbox.LootBoxStates [enum]

- closed = 0
- openning = 1
- looting = 2

## InteractablePickup [class] : InteractableBase

- prop [get] DuckovItemAgent ItemAgent
- method Void CreateGraphic()
- method Void OnInit()
- method Void Throw(Vector3 direction, Single randomAngle)

## InventoryFilterDisplay [class] : UnityEngine.MonoBehaviour

- method InventoryFilterDisplayEntry GetSelection()
- method Boolean SetSelection(InventoryFilterDisplayEntry selection)
- method Void Setup(InventoryDisplay target)

## InventoryFilterDisplayEntry [class] : UnityEngine.MonoBehaviour

- prop [get/set] FilterEntry Filter
- method Void OnPointerClick(PointerEventData eventData)

## InventoryFilterProvider.FilterEntry [struct]

- prop [get] String DisplayName
- method Func<Item, Boolean> GetFunction()

## InvisibleTeleporter [class] : UnityEngine.MonoBehaviour

- method Void DrawGizmos()
- method Void Teleport()

## ItemAgentHolder [class] : UnityEngine.MonoBehaviour

- prop [get] ItemAgent_Gun CurrentHoldGun
- prop [get] DuckovItemAgent CurrentHoldItemAgent
- prop [get] ItemAgent_MeleeWeapon CurrentHoldMeleeWeapon
- prop [get] Transform CurrentUsingSocket
- prop [get] ItemSetting_Skill Skill
- method DuckovItemAgent ChangeHoldItem(Item item)
- method Void SetTrigger(Boolean trigger, Boolean triggerThisFrame, Boolean releaseThisFrame)

## ItemAgent_Gun [class] : DuckovItemAgent

- prop [get] Single ADSAimDistanceFactor
- prop [get] Single AdsSpeed
- prop [get] Single AdsValue
- prop [get] Single AdsWalkSpeedMultiplier
- prop [get] Single ArmorBreak
- prop [get] Single ArmorPiercing
- prop [get] Single BuffChance
- prop [get] Single BulletArmorBreakGain
- prop [get] Single BulletArmorPiercingGain
- prop [get] Single BulletBleedChance
- prop [get] Single BulletBuffChanceMultiplier
- prop [get] Int32 BulletCount
- prop [get] Single BulletCritDamageFactorGain
- prop [get] Single BulletDamageMultiplier
- prop [get] Single BulletDistance
- prop [get] Boolean BulletEmpty
- prop [get] Single BulletExplosionDamage
- prop [get] Single BulletExplosionRange
- prop [get] Item BulletItem
- prop [get] Single BulletSpeed
- prop [get] Int32 BurstCount
- prop [get] Int32 Capacity
- prop [get] Single CharacterDamageMultiplier
- prop [get] Single CharacterGunCritDamageGain
- prop [get] Single CharacterGunCritRateGain
- prop [get] Single CharacterRecoilControl
- prop [get] Single CharacterReloadSpeedGain
- prop [get] Single CharacterScatterMultiplier
- prop [get] Single CharacterShootSpeedMultiplier
- prop [get] Single ControlMindTime
- prop [get] ControlMindTypes ControlMindType
- prop [get] Single CritDamageFactor
- prop [get] Single CritRate
- prop [get] Single CurrentScatter
- prop [get] Single Damage
- prop [get] Single DamageFactorToZombie
- prop [get] Single DefaultScatter
- prop [get] Single DmgOverDistance
- prop [get] Single Durability
- prop [get] Single ExplosionDamageMultiplier
- prop [get] ItemSetting_Gun GunItemSetting
- prop [get] GunStates GunState
- prop [get] Boolean IsInAds
- prop [get] Single MaxDurability
- prop [get] Single MaxScatter
- prop [get] Single MinScatter
- prop [get] Single MoveSpeedMultiplier
- prop [get] Int32 Penetrate
- prop [get] Single RecoilHMax
- prop [get] Single RecoilHMin
- prop [get] Single RecoilRecover
- prop [get] Single RecoilRecoverTime
- prop [get] Single RecoilScaleH
- prop [get] Single RecoilScaleV
- prop [get] Single RecoilTime
- prop [get] Single RecoilVMax
- prop [get] Single RecoilVMin
- prop [get] Single ReloadTime
- prop [get] Single ScatterGrow
- prop [get] Single ScatterRecover
- prop [get] Single ShootSpeed
- prop [get] Single ShootSpeedGainByShootMax
- prop [get] Single ShootSpeedGainEachShoot
- prop [get] Single ShotAngle
- prop [get] Int32 ShotCount
- prop [get] Boolean Silenced
- prop [get] Single SoundRange
- prop [get] Single StateTimer
- prop [get] Single TraceAbility
- prop [get] CharacterMainControl TraceTarget
- prop [get] Single bulletCritRateGain
- prop [get] Single bulletDurabilityCost
- prop [get] Single burstCoolTime
- prop [get] Single burstShotTimeSpace
- prop [get] Single durabilityPercent
- prop [get] Transform muzzle
- method Boolean BeginReload()
- method ItemAgent_Gun BuildAgent(GameObject modelPfb) (static)
- method Void CancleReload()
- method Boolean CharacterReload(Item prefererdBullet)
- method ADSAimMarker GetAimMarkerPfb()
- method Int32 GetBulletCountInInventory()
- method Progress GetReloadProgress()
- method Boolean IsFull()
- method Boolean IsReloading()
- method Void OverrideShootPoint(Vector3 newTargetPoint)
- method Void SetMuzzleSocketLocalPos(Vector3 pos)
- method Void SetTecSocketLocalPosition(Vector3 pos)
- method Void SetTrigger(Boolean trigger, Boolean _triggerThisFrame, Boolean _releaseThisFrame)
- method Void UpdateStates()

## ItemAgent_Gun.GunStates [enum]

- shootCooling = 0
- ready = 1
- fire = 2
- burstEachShotCooling = 3
- empty = 4
- reloading = 5

## ItemAgent_Kazoo [class] : DuckovItemAgent

- method Void OnTriggerUpdate(Boolean trigger, Boolean triggerThisFrame, Boolean releaseThisFrame)

## ItemAgent_MeleeWeapon [class] : DuckovItemAgent

- prop [get] Single ArmorPiercing
- prop [get] Single AttackRange
- prop [get] Single AttackSpeed
- prop [get] Single BleedChance
- prop [get] Single BlockBullet
- prop [get] Single CharacterCritDamageGain
- prop [get] Single CharacterCritRateGain
- prop [get] Single CharacterDamageMultiplier
- prop [get] Single CritDamageFactor
- prop [get] Single CritRate
- prop [get] Single Damage
- prop [get] Single DamageFactorToZombie
- prop [get] Single DealDamageTime
- prop [get] Single MoveSpeedMultiplier
- prop [get] String SoundKey
- prop [get] Single StaminaCost
- method Boolean AttackableTargetInRange()
- method Void CheckAndDealDamage()

## ItemAmountDisplay [class] : UnityEngine.MonoBehaviour

- prop [get] ItemMetaData MetaData
- prop [get] Int32 TypeID
- method ItemMetaData GetMetaData()
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void Setup(Int32 itemTypeID, Int64 amount)

## ItemExtensions [class]

- field Int32 HandheldHash (static)
- field Int32 PickupHash (static)
- method Void ConsumeItemsOfAmount(IEnumerable<Item> itemsToBeConsumed, Int32 amount) (static)
- method ItemAgent CreateHandheldAgent(Item itemInstance) (static)
- method Void Drop(Item item, CharacterMainControl character, Boolean createRigidbody) (static)
- method DuckovItemAgent Drop(Item item, Vector3 pos, Boolean createRigidbody, Vector3 dropDirection, Single randomAngle) (static)
- method UniTask<List<Item>> GetItemsOfAmount(Inventory inventory, Int32 itemTypeID, Int32 amount) (static)
- method Boolean TryFindItemsOfAmount(IEnumerable<Inventory> inventories, Int32 itemTypeID, Int32 requiredAmount, List`1& result) (static)

## ItemGraphicInfo_Gun [class] : ItemGraphicInfo

- method Void SetSockets(List<Transform> _sockets)

## ItemMetaDisplay [class] : UnityEngine.MonoBehaviour

- method ItemMetaData GetMetaData()
- method Void OnPointerEnter(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void Setup(Int32 typeID)
- method Void Setup(ItemMetaData data)

## ItemPickerDebug [class] : UnityEngine.MonoBehaviour

- method Void PickPlayerInventoryAndLog()

## ItemRepair_RepairAllPanel [class] : UnityEngine.MonoBehaviour

- method Void Setup(ItemRepairView master)

## ItemSettingBase [class] : UnityEngine.MonoBehaviour

- prop [get] Item Item
- method Void Awake()
- method Void OnInit()
- method Void SetMarkerParam(Item selfItem)
- method Void Start()

## ItemSetting_Accessory [class] : ItemSettingBase

- method Void OnInit()
- method Void SetMarkerParam(Item selfItem)

## ItemSetting_Bullet [class] : ItemSettingBase

- method Void SetMarkerParam(Item selfItem)

## ItemSetting_Formula [class] : ItemSettingBase

- method Void SetMarkerParam(Item selfItem)

## ItemSetting_GPU [class] : ItemSettingBase

- method Void SetMarkerParam(Item selfItem)

## ItemSetting_Gun [class] : ItemSettingBase

- prop [get] Int32 BulletCount
- prop [get] Int32 Capacity
- prop [get] String CurrentBulletName
- prop [get] Boolean LoadBulletsSuccess
- prop [get] Boolean LoadingBullets
- prop [get] Int32 OverrideTriggerMode
- prop [get/set] Item PreferdBulletsToLoad
- prop [get] Int32 TargetBulletID
- prop [get] TriggerModes currentTriggerMode
- method Boolean AutoSetTypeInInventory(Inventory inventory)
- method Int32 GetBulletCount()
- method Int32 GetBulletCountofTypeInInventory(Int32 bulletItemTypeID, Inventory inventory)
- method Dictionary<Int32, BulletTypeInfo> GetBulletTypesInInventory(Inventory inventory)
- method Item GetCurrentLoadedBullet()
- method Boolean IsFull()
- method Boolean IsValidBullet(Item newBulletItem)
- method UniTaskVoid LoadBulletsFromInventory(Inventory inventory)
- method Boolean LoadSpecificBullet(Item newBulletItem)
- method Void SetMarkerParam(Item selfItem)
- method Void SetTargetBulletType(Item bulletItem)
- method Void SetTargetBulletType(Int32 typeID)
- method Void Start()
- method Void TakeOutAllBullets()
- method Void TriggerOnHurtEnemyEvent(DamageReceiver dmgReceiver, DamageInfo damageInfo)
- method Void TriggerOnShootEvent()
- method Void UseABullet()

## ItemSetting_Gun.ReloadModes [enum]

- fullMag = 0
- singleBullet = 1

## ItemSetting_Gun.TriggerModes [enum]

- auto = 0
- semi = 1
- bolt = 2

## ItemSetting_MeleeWeapon [class] : ItemSettingBase

- method Void SetMarkerParam(Item selfItem)
- method Void Start()

## ItemSetting_NightVision [class] : ItemSettingBase

- method Void OnInit()
- method Void SetMarkerParam(Item selfItem)
- method Void ToggleNightVison()

## ItemSetting_Skill [class] : ItemSettingBase

- method Void OnInit()
- method Void SetMarkerParam(Item selfItem)

## ItemSetting_Skill.OnReleaseAction [enum]

- none = 0
- reduceCount = 1

## ItemTest [class] : UnityEngine.MonoBehaviour

- method Void DestroyInstances()
- method Void DoInstantiate()
- method Void EquipSword()
- method Void UequipSword()

## ItemUtilities [class]

- method Boolean AddAndMerge(Inventory inventory, Item item, Int32 preferedFirstPosition) (static)
- method UniTask<Boolean> Decompose(Item item, Int32 count) (static)
- method List<Item> FindAllBelongsToPlayer(Predicate<Item> predicate) (static)
- method UniTask<Item> GenerateBullet(Item gunItem) (static)
- method CharacterMainControl GetCharacterMainControl(Item item) (static)
- method Int32 GetItemCount(Int32 typeID) (static)
- method Single GetRepairLossRatio(Item item) (static)
- method Boolean IsInPlayerCharacter(Item item) (static)
- method Boolean IsInPlayerStorage(Item item) (static)
- method Boolean IsRegistered(Item item) (static)
- method Void SendToPlayer(Item item, Boolean dontMerge, Boolean sendToStorage) (static)
- method Boolean SendToPlayerCharacter(Item item, Boolean dontMerge) (static)
- method Boolean SendToPlayerCharacterInventory(Item item, Boolean dontMerge) (static)
- method Void SendToPlayerStorage(Item item, Boolean directToBuffer) (static)
- method Boolean TryPlug(Item main, Item part, Boolean emptyOnly, Inventory backupInventory, Int32 preferredFirstIndex) (static)

## ItemWishlist [class] : UnityEngine.MonoBehaviour

- prop [get/set] ItemWishlist Instance (static)
- method Void AddToWishList(Int32 itemTypeID) (static)
- method WishlistInfo GetWishlistInfo(Int32 itemTypeID) (static)
- method Boolean IsBuildingRequired(Int32 itemTypeID)
- method Boolean IsManuallyWishlisted(Int32 itemTypeID)
- method Boolean IsQuestRequired(Int32 itemTypeID)
- method Boolean RemoveFromWishlist(Int32 itemTypeID) (static)

## KunEvents [class] : UnityEngine.MonoBehaviour

- method Void Check()

## LabelAndValue [class] : UnityEngine.MonoBehaviour

- method Void Setup(String label, String value, Polarity valuePolarity)

## LanguageOptionsProvider [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## LevelConfig [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean AccountAvailable
- prop [get] LevelConfig Instance (static)
- prop [get] Boolean IsBaseLevel (static)
- prop [get] Boolean IsRaidMap (static)
- prop [get] Boolean LockRule
- prop [get] Single LootBoxQualityLowPercent
- prop [get] Single LootboxItemCountMultiplier
- prop [get] Int32 MaxExitCount (static)
- prop [get] Int32 MinExitCount (static)
- prop [get] Boolean NeedInspectDeadLootBox
- prop [get] RulesetFile Rule
- prop [get] Boolean SaveCharacter (static)
- prop [get] Boolean SavePet (static)
- prop [get] Seasons Season (static)
- prop [get] Boolean SpawnTomb (static)

## LevelManager [class] : UnityEngine.MonoBehaviour

- prop [get] AIMainBrain AIMainBrain
- prop [get] Boolean AfterInit (static)
- prop [get] BulletPool BulletPool
- prop [get] CharacterCreator CharacterCreator
- prop [get] CharacterMainControl ControllingCharacter
- prop [get] CustomFaceManager CustomFaceManager
- prop [get] ExitCreator ExitCreator
- prop [get] ExplosionManager ExplosionManager
- prop [get] FogOfWarManager FogOfWarManager
- prop [get] GameCamera GameCamera
- prop [get] InputManager InputManager
- prop [get] LevelManager Instance (static)
- prop [get] Boolean IsBaseLevel
- prop [get] Boolean IsRaidMap
- prop [get] Boolean LevelInited (static)
- prop [get] Boolean LevelInitializing (static)
- prop [get/set] String LevelInitializingComment (static)
- prop [get] Single LevelTime
- prop [get] Dictionary<Int32, Inventory> LootBoxInventories (static)
- prop [get] Transform LootBoxInventoriesParent (static)
- prop [get] CharacterMainControl MainCharacter
- prop [get] CharacterMainControl PetCharacter
- prop [get] PetProxy PetProxy
- prop [get] Ruleset Rule (static)
- prop [get] TimeOfDayController TimeOfDayController
- field String MainCharacterHealthSaveKey (static)
- field String MainCharacterItemSaveKey (static)
- field Action<CharacterMainControl> OnControllingCharacterChanged (static)
- field Single enemySpawnCountFactor (static)
- field Boolean forceBossSpawn (static)
- field Int32 loadLevelBeaconIndex (static)
- method LevelInfo GetCurrentLevelInfo() (static)
- method Void NotifyEvacuated(EvacuationInfo info)
- method Void NotifySaveBeforeLoadScene(Boolean saveToFile)
- method Void RefreshMainCharacterFace()
- method Void RegisterWaitForInitialization(T toWait) (static)
- method Void SetControllingCharacter(CharacterMainControl character)
- method Void TestTeleport()
- method Boolean UnregisterWaitForInitialization(T obj) (static)

## LevelManagerProxy [class] : UnityEngine.MonoBehaviour

- method Void NotifyEvacuated()

## LongPressButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerExit(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)

## LoopSoundWithObject [class] : UnityEngine.MonoBehaviour

- method Void Stop()

## MainMenu [class] : UnityEngine.MonoBehaviour

- field Action OnMainMenuAwake (static)
- field Action OnMainMenuDestroy (static)

## MapMarkerPanelButton [class] : UnityEngine.MonoBehaviour

- prop [get] Image Image
- method Void Setup(UnityAction action, Boolean selected)

## MiningMachineCardDisplay [class] : UnityEngine.MonoBehaviour

- method Void SetVisualActive(Boolean active, CardTypes cardType)

## MiningMachineCardDisplay.CardTypes [enum]

- normal = 0
- potato = 1

## ModUploadPanel [class] : UnityEngine.MonoBehaviour

- method UniTask Execute(ModInfo info)

## MoveDirectionOptions [class] : OptionsProviderBase

- prop [get] String Key
- prop [get] Boolean MoveViaCharacterDirection (static)
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## MoveRing [class] : UnityEngine.MonoBehaviour

- method Void LateUpdate()
- method Void SetMove(Vector3 direction, Single value)
- method Void SetRunning(Boolean running)
- method Void SetThreshold(Single threshold)

## Movement [class] : UnityEngine.MonoBehaviour

- prop [get] Vector3 CurrentMoveDirectionXZ
- prop [get] Boolean IsOnGround
- prop [get] Vector3 MoveInput
- prop [get/set] Boolean MovementEnabled
- prop [get] Boolean Moving
- prop [get] Boolean Running
- prop [get] Boolean StandStill
- prop [get] Vector3 TargetVelocity
- prop [get] Single TurnAngleThisFrame
- prop [get] Vector3 Velocity
- prop [get] Single aimTurnSpeed
- prop [get] Single originRunSpeed
- prop [get] Single originWalkSpeed
- prop [get] Transform rotationRoot
- prop [get] Single runAcc
- prop [get] Single runSpeed
- prop [get] Single turnSpeed
- prop [get] Single walkAcc
- prop [get] Single walkSpeed
- method Void ForceSetAimDirectionToAimPoint()
- method Void ForceSetPosition(Vector3 Pos)
- method Void ForceTurnTo(Vector3 direction)
- method Vector2 GetLocalMoveDirectionAnimationValue()
- method Single GetMoveAnimationValue()
- method Void SetAimDirection(Vector3 _aimDirection)
- method Void SetAimDirectionToTarget(Vector3 targetPoint, Transform aimHandler)
- method Void SetForceMoveVelocity(Vector3 _forceMoveVelocity)
- method Void SetGravityFactor(Single factor)
- method Void SetMoveInput(Vector3 _moveInput)
- method Void SetPushCharacter(Boolean push)
- method Void SetYVelocity(Single _yVelocity)
- method Void UpdateMovement()

## MultiCirceExtrudeShape [class] : ShapeProvider

- method OrientedPoint[] GenerateShape()

## MultiInteraction [class] : UnityEngine.MonoBehaviour

- prop [get] ReadOnlyCollection<InteractableBase> Interactables

## MultiInteractionMenu [class] : UnityEngine.MonoBehaviour

- prop [get/set] MultiInteractionMenu Instance (static)
- prop [get] MultiInteraction Target
- method UniTask Hide()
- method UniTask SetupAndShow(MultiInteraction target)

## MultipleBezierShape [class] : ShapeProvider

- method OrientedPoint[] GenerateShape()

## NightVisionVisual [class] : UnityEngine.MonoBehaviour

- method Void Awake()
- method Void Refresh()

## NodeCanvas.DialogueTrees.LocalizedMultipleChoiceNode [class] : NodeCanvas.DialogueTrees.DTNode

- prop [get] Int32 maxOutConnections
- prop [get] Boolean requireActorSelection

## NodeCanvas.Tasks.Actions.Dash.DashDirectionModes [enum]

- random = 0
- targetTransform = 1

## NodeCanvas.Tasks.Actions.PostSound.VoiceSounds [enum]

- normal = 0
- surprise = 1
- death = 2

## NodeCanvas.Tasks.Actions.ReleaseItemSkillIfHas [class] : NodeCanvas.Framework.ActionTask<AICharacterController>

- prop [get] Single checkTimeSpace

## NodeCanvas.Tasks.Actions.TryToReloadIfEmpty [class] : NodeCanvas.Framework.ActionTask<AICharacterController>

- prop [get] String SoundKey

## NotHurtForSeconds [class] : ItemStatsSystem.EffectFilter

- prop [get] String DisplayName

## NotificationProxy [class] : UnityEngine.MonoBehaviour

- method Void Notify()

## OcclusionFadeManager [class] : UnityEngine.MonoBehaviour

- prop [get] OcclusionFadeManager Instance (static)
- prop [get] Single startFadeHeight
- field Boolean FadeEnabled (static)
- method Material GetMaskedMaterial(Material mat)

## OcclusionFadeObject [class] : UnityEngine.MonoBehaviour

- method Void OnEnter()
- method Void OnLeave()

## OcclusionFadeTrigger [class] : UnityEngine.MonoBehaviour

- method Void Enter()
- method Void Leave()

## OcclusionFadeTypes [enum]

- Fade = 0
- ShadowOnly = 1

## OnTriggerEnterEvent [class] : UnityEngine.MonoBehaviour

- method Void Init()

## OpenSaveFolder [class] : UnityEngine.MonoBehaviour

- method Void OpenFolder()

## OptionsPanel_TabButton [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## OptionsProviderBase [class] : UnityEngine.MonoBehaviour

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## OverrideDeathSceneRouting [class] : UnityEngine.MonoBehaviour

- prop [get/set] OverrideDeathSceneRouting Instance (static)
- method String GetSceneID()

## PackedMapData [class] : UnityEngine.ScriptableObject

- prop [get] Vector3 CombinedCenter
- prop [get] Sprite CombinedSprite
- prop [get] List<IMiniMapEntry> Maps
- prop [get] Single PixelSize

## PackedMapData.Entry [class]

- prop [get] Boolean Hide
- prop [get] Boolean NoSignal
- prop [get] Vector2 Offset
- prop [get] Single PixelSize
- prop [get] String SceneID
- prop [get] Sprite Sprite

## PasswordMachine [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 maxNum
- method Void Confirm()
- method Void DeleteNum()
- method Void InputNum(Int32 num)

## PauseMenu [class] : UIPanel

- prop [get] PauseMenu Instance (static)
- prop [get] Boolean Shown
- method Void Hide() (static)
- method Void Show() (static)
- method Void Toggle() (static)

## PerkTreeManager [class] : UnityEngine.MonoBehaviour

- prop [get] PerkTreeManager Instance (static)
- method PerkTree GetPerkTree(String id) (static)

## PetAI [class] : UnityEngine.MonoBehaviour

- method Void SetMaster(CharacterMainControl _master)
- method Void SetStandBy(Boolean _standBy, Vector3 pos)

## PetHouse [class] : UnityEngine.MonoBehaviour

- prop [get] PetHouse Instance (static)

## PetProxy [class] : UnityEngine.MonoBehaviour

- prop [get] PetProxy Instance (static)
- prop [get] Inventory Inventory
- prop [get] Boolean InventoryEmpty (static)
- prop [get] Inventory PetInventory (static)
- method Void DestroyItemInBase()

## PipeDecoration [class] : UnityEngine.MonoBehaviour

- method Void OnValidate()

## PipeHelperFunctions [class]

- method Vector2[] GenerateUV2(OrientedPoint[] points) (static)
- method Single GetTotalLength(OrientedPoint[] points) (static)
- method Void RecalculateNormals(OrientedPoint[]& points) (static)
- method Void RecalculateUvs(OrientedPoint[]& points, Single factor, Single offset) (static)
- method OrientedPoint[] RemoveDuplicates(OrientedPoint[] points, Single threshold) (static)
- method Void RotatePoints(OrientedPoint[]& points, Single offset, Single step) (static)

## PipeRenderer [class] : UnityEngine.MonoBehaviour

- prop [get] OrientedPoint[] extrudeShape
- prop [get] OrientedPoint[] splineShape
- method Mesh GeneratePipeMesh(OrientedPoint[] extrudeShape, OrientedPoint[] splineShape, Color vertexColor, Single uvTwist, Single extrudeShapeScale, AnimationCurve extrudeShapeScaleCurve, Single sectionLength, Boolean caps, Boolean recalculateNormal, Boolean revertFaces) (static)
- method Vector3 GetPositionByOffset(Single offset, Quaternion& rotation)
- method Single GetTotalLength()

## PlayHurtEventProxy [class] : UnityEngine.MonoBehaviour

- method Void Play(Boolean crit)

## PlayerHurtVisual [class] : UnityEngine.MonoBehaviour

- field Boolean hurtVisualOn (static)

## PlayerPositionBackupManager [class] : UnityEngine.MonoBehaviour

- method Void BackupCurrentPos()
- method Void SetPlayerToBackupPos()
- method Void StartRecover() (static)

## PlayerPositionBackupProxy [class] : UnityEngine.MonoBehaviour

- method Void StartRecoverInteract()

## PlayerStorage [class] : UnityEngine.MonoBehaviour

- prop [get] Int32 DefaultCapacity
- prop [get] List<ItemTreeData> IncomingItemBuffer (static)
- prop [get/set] PlayerStorage Instance (static)
- prop [get] InteractableLootbox InteractableLootBox
- prop [get] Inventory Inventory (static)
- prop [get/set] Boolean Loading (static)
- prop [get/set] Boolean TakingItem (static)
- method Boolean HasInitialized()
- method Boolean IsAccessableAndNotFull() (static)
- method Void NotifyCapacityDirty() (static)
- method Void Push(Item item, Boolean toBufferDirectly) (static)
- method Int32 RecalculateStorageCapacity() (static)
- method UniTask TakeBufferItem(Int32 index) (static)

## PlayerStorageBuffer [class] : UnityEngine.MonoBehaviour

- prop [get] List<ItemTreeData> Buffer (static)
- prop [get/set] PlayerStorageBuffer Instance (static)
- method Void LoadBuffer() (static)
- method Void SaveBuffer() (static)

## PointerDownUpEvents [class] : UnityEngine.MonoBehaviour

- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)

## Points [class] : UnityEngine.MonoBehaviour

- method Vector3 GetPoint(Int32 index)
- method Vector3 GetRandomPoint()
- method List<Vector3> GetRandomPoints(Int32 count, Single minDistance, Single maxDistance)
- method Void RemoveAllPoints()
- method Void SendPointsChangedMessage()
- method Void SetYtoZero()

## PostAudioEventOnEnter [class] : UnityEngine.StateMachineBehaviour

- method Void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, Int32 layerIndex)

## PreUnlockPerkTree [class] : UnityEngine.MonoBehaviour

- method Void Start()

## PrefabLineGenrator [class] : UnityEngine.MonoBehaviour

- method Void OnPointsChanged()

## PrefabLineGenrator.GapInfo [struct]

- prop [get] Single EndDistance
- method Void Normalize()
- method Boolean Overlaps(Single segmentStartDistance, Single segmentEndDistance)

## PrefabLineGenrator.SapwnInfo [struct]

- method GameObject GetRandomPrefab()
- method Boolean HasPrefab()

## Projectile [class] : UnityEngine.MonoBehaviour

- field Action<Vector3> OnBulletFlyByCharacter (static)
- method Void Init()
- method Void Init(ProjectileContext _context)
- method Void SetPool(ObjectPool<Projectile> _pool)

## QuestRequiredItem [class] : UnityEngine.MonoBehaviour

- method Void Set(Int32 itemTypeID, Int32 count)

## Radar [class] : UnityEngine.MonoBehaviour

- prop [get/set] Transform Target (static)
- prop [get/set] Vector3 TargetPosition

## RaidUtilities [class]

- prop [get/set] RaidInfo CurrentRaid (static)
- method Void NewRaid() (static)
- method Void NotifyDead() (static)
- method Void NotifyEnd() (static)

## RandomCharacterSpawner [class] : CharacterSpawnerComponentBase

- field String currentGizmosTag (static)
- method Void Init(CharacterSpawnerRoot root)
- method Void StartSpawn()

## RandomItemGenerateDescription [struct]

- method UniTask<List<Item>> Generate(Int32 count)

## RectTransformExtensions [class]

- method Camera GetUICamera() (static)
- method Boolean MatchWorldPosition(RectTransform rectTransform, Vector3 worldPosition, Vector3 worldSpaceOffset) (static)

## RequireBeaconUnlocked [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## RequireEnemyKilled [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## RequireInLevelDataBool [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## RequireSaveDataBool [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## RequireSeasons [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## RequireWeathers [class] : Duckov.Quests.Condition

- method Boolean Evaluate()

## ResModes [enum]

- Source = 0
- HalfRes = 1
- R720p = 2
- R480p = 3

## ResolutionOptions [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## ResolutionSetter [class] : UnityEngine.MonoBehaviour

- prop [get] DuckovResolution MaxResolution (static)
- field String Key_Resolution (static)
- field String Key_ScreenMode (static)
- field Boolean currentFullScreen (static)
- method Resolution GetResByHeight(Int32 height, DuckovResolution maxRes) (static)
- method DuckovResolution[] GetResolutions() (static)
- method String[] GetScreenModes() (static)
- method String ScreenModeToName(screenModes mode) (static)
- method Void UpdateResolutionAndScreenMode() (static)

## ResolutionSetter.screenModes [enum]

- FullScreen = 0
- Window = 1

## RogueClosureManager [class] : UnityEngine.MonoBehaviour

- prop [get] RogueClosureManager Instance (static)
- method Void OnDestroy()
- method Void ReportKilled(Boolean isBoss)

## RoundCornerRectExtrudeShape [class] : ShapeProvider

- method OrientedPoint[] GenerateShape()

## RoundCornerSquareExtrudeShape [class] : ShapeProvider

- method OrientedPoint[] GenerateShape()

## RuleEntry_Bool [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## RunInputOptions [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## SaveDataBoolProxy [class] : UnityEngine.MonoBehaviour

- method Void Save()

## Saves.ISaveDataProvider [interface]

- method Object GenerateSaveData()
- method Void SetupSaveData(Object data)

## Saves.ItemSavesUtilities [class]

- method UniTask LoadInventory(String key, Inventory inventoryInstance) (static)
- method UniTask<Item> LoadItem(String key) (static)
- method UniTask<Item> LoadLastDeadCharacterItem() (static)
- method Void Save(Item item, String key) (static)
- method Void Save(Inventory inventory, String key) (static)
- method Void SaveAsLastDeadCharacter(Item item) (static)

## Saves.SavesSystem [class]

- prop [get] String CurrentFilePath (static)
- prop [get/set] Int32 CurrentSlot (static)
- prop [get] String GlobalSaveDataFileName (static)
- prop [get] String GlobalSaveDataFilePath (static)
- prop [get] Boolean IsSaving (static)
- prop [get/set] Boolean RestoreFailureMarker (static)
- prop [get] String SavesFolder (static)
- method Void CollectSaveData() (static)
- method Void DeleteCurrentSave() (static)
- method BackupInfo[] GetBackupList() (static)
- method BackupInfo[] GetBackupList(Int32 slot) (static)
- method BackupInfo[] GetBackupList(String mainPath, Int32 slot) (static)
- method String GetFilePath(Int32 slot) (static)
- method String GetFullPathToSavesFolder() (static)
- method String GetSaveFileName(Int32 slot) (static)
- method DateTime GetSaveTimeLocal(Int32 slot)
- method DateTime GetSaveTimeUTC(Int32 slot)
- method Boolean IsOldGame() (static)
- method Boolean IsOldGame(Int32 index) (static)
- method Boolean IsOldSave(Int32 index) (static)
- method Boolean KeyExisits(String realKey) (static)
- method Boolean KeyExisits(String prefix, String key) (static)
- method Boolean KeyExisits(String realKey, Int32 slotIndex) (static)
- method T Load(String realKey) (static)
- method T Load(String prefix, String key) (static)
- method T Load(String realKey, Int32 slotIndex) (static)
- method T LoadGlobal(String key, T defaultValue) (static)
- method Void RestoreIndexedBackup(Int32 slot, Int32 index) (static)
- method Void Save(String realKey, T value) (static)
- method Void Save(String prefix, String key, T value) (static)
- method Void SaveFile(Boolean writeSaveTime) (static)
- method Void SaveGlobal(String key, T value) (static)
- method Void SetFile(Int32 index) (static)
- method Void UpgradeSaveFileAssemblyInfo(String path) (static)

## Saves.SavesSystem.BackupInfo [struct]

- prop [get] DateTime Time
- prop [get] Boolean TimeValid

## SavesCounter [class]

- field Action<String, Int32> OnKillCountChanged (static)
- method Int32 AddCount(String countKey) (static)
- method Int32 AddKillCount(String key) (static)
- method Int32 GetCount(String countKey) (static)
- method Int32 GetKillCount(String key) (static)

## SceneInfoCollection [class] : UnityEngine.ScriptableObject

- prop [get] List<SceneInfoEntry> Entries (static)
- field String BaseSceneID (static)
- method String GetSceneID(SceneReference sceneRef) (static)
- method String GetSceneID(Int32 buildIndex) (static)
- method SceneInfoEntry GetSceneInfo(String sceneID) (static)
- method String InstanceGetSceneID(Int32 buildIndex)
- method SceneInfoEntry InstanceGetSceneInfo(String id)

## SceneInfoEntry [class]

- prop [get] Int32 BuildIndex
- prop [get] String Description
- prop [get] String DisplayName
- prop [get] String DisplayNameRaw
- prop [get] String ID
- prop [get] Boolean IsLoaded
- prop [get] SceneReference SceneReference

## SceneLoader [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean HideTips (static)
- prop [get] SceneLoader Instance (static)
- prop [get/set] Boolean IsSceneLoading (static)
- prop [get/set] String LoadingComment (static)
- method UniTask LoadBaseScene(SceneReference overrideCurtainScene, Boolean doCircleFade)
- method Void LoadMainMenu(Boolean circleFade) (static)
- method UniTask LoadScene(String sceneID, MultiSceneLocation location, SceneReference overrideCurtainScene, Boolean clickToConinue, Boolean notifyEvacuation, Boolean doCircleFade, Boolean saveToFile, Boolean hideTips)
- method UniTask LoadScene(String sceneID, SceneReference overrideCurtainScene, Boolean clickToConinue, Boolean notifyEvacuation, Boolean doCircleFade, Boolean useLocation, MultiSceneLocation location, Boolean saveToFile, Boolean hideTips)
- method UniTask LoadScene(SceneReference sceneReference, SceneReference overrideCurtainScene, Boolean clickToConinue, Boolean notifyEvacuation, Boolean doCircleFade, Boolean useLocation, MultiSceneLocation location, Boolean saveToFile, Boolean hideTips)
- method Void NotifyPointerClick(PointerEventData eventData)

## SceneLoaderProxy [class] : UnityEngine.MonoBehaviour

- method Void LoadMainMenu()
- method Void LoadScene()

## ScrollViewEventReceiver [class] : UnityEngine.MonoBehaviour

- method Void OnScroll(PointerEventData eventData)

## ScrollWheelBehaviour [class]

- prop [get/set] Behaviour CurrentBehaviour (static)
- method String GetDisplayName(Behaviour behaviour) (static)

## ScrollWheelBehaviour.Behaviour [enum]

- AmmoAndInteract = 0
- Weapon = 1

## ScrollWheelOptionsProvider [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## SetActiveByCondition [class] : UnityEngine.MonoBehaviour

- method Void Set()

## SetInLevelDataBoolProxy [class] : UnityEngine.MonoBehaviour

- method Void SetTo(Boolean target)
- method Void SetToTarget()

## SfxOnClick [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## ShadowSetting [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## ShapeProvider [class] : UnityEngine.MonoBehaviour

- method OrientedPoint[] GenerateShape()

## ShapesSkillLine [class] : UnityEngine.MonoBehaviour

- method Void DrawLine()

## ShowLocationInMap [class] : UnityEngine.MonoBehaviour

- prop [get] String DisplayName
- prop [get] String DisplayNameRaw

## ShowcaseAltar [class] : UnityEngine.MonoBehaviour

- prop [get] String ID
- field Dictionary<String, ShowcaseAltar> instances (static)
- method ShowcaseAltar GetInstance(String id)

## SimpleTeleporter [class] : InteractableBase

- prop [get] Transform TeleportPoint

## SimpleTeleporter.TransitionTypes [enum]

- volumeFx = 0
- blackScreen = 1

## SimpleTeleporterSpawner [class] : UnityEngine.MonoBehaviour

- method Void StartCreate()

## SingleCrosshair [class] : UnityEngine.MonoBehaviour

- method Void UpdateScatter(Single _scatter)

## SkillBase [class] : UnityEngine.MonoBehaviour

- prop [get] Single LastReleaseTime
- prop [get] SkillContext SkillContext
- method Void OnRelease()
- method Void ReleaseSkill(SkillReleaseContext releaseContext, CharacterMainControl from)

## SkillProjectileLineHUD [class] : UnityEngine.MonoBehaviour

- method Boolean UpdateLine(Vector3 start, Vector3 target, Single verticleSpeed, Vector3& hitPoint)

## SkillRangeHUD [class] : UnityEngine.MonoBehaviour

- method Void SetProgress(Single progress)
- method Void SetRange(Single range)

## SkillTypes [enum]

- itemSkill = 0
- characterSkill = 1

## Skill_Grenade [class] : SkillBase

- method Vector3 CalculateVelocity(Vector3 start, Vector3 target, Single verticleSpeed)
- method Void OnRelease()

## Skill_Shoot [class] : SkillBase

- method Void OnRelease()

## SlotItemSwapper [class] : InteractableBase

- method Void Swap(CharacterMainControl character)
- method Void Swap(SlotCollection slotsA, SlotCollection slotsB) (static)

## Soda.DebugView [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean EdgeLightActive
- prop [get] DebugView Instance
- method Void CreateItem()
- method Void SetInputDevice(Int32 type)
- method Void SetRes(Int32 resModeIndex)
- method Void SetRes(ResModes mode)
- method Void SetTexture(Int32 texModeIndex)
- method Void SetTexture(TextureModes mode)
- method Void ToggleAO()
- method Void ToggleBloom()
- method Void ToggleDof()
- method Void ToggleEdgeLight()
- method Void ToggleInvincible()
- method Void ToggleReporter()

## SodaCraft.CameraArmControl [class] : UnityEngine.Rendering.VolumeComponent

- method Boolean IsActive()
- method Void Override(VolumeComponent state, Single interpFactor)

## SodaCraft.EdgeLight [class] : UnityEngine.Rendering.VolumeComponent

- method Boolean IsActive()
- method Boolean IsTileCompatible()
- method Void Override(VolumeComponent state, Single interpFactor)

## SodaCraft.LightControl [class] : UnityEngine.Rendering.VolumeComponent

- method Boolean IsActive()
- method Boolean IsTileCompatible()
- method Void Override(VolumeComponent state, Single interpFactor)

## SodaCraft.SunFogTD [class] : UnityEngine.Rendering.VolumeComponent

- method Boolean IsActive()
- method Boolean IsTileCompatible()
- method Void Override(VolumeComponent state, Single interpFactor)

## SodaCraft.TimeOfDayPost [class] : UnityEngine.Rendering.VolumeComponent

- method Boolean IsActive()
- method Boolean IsTileCompatible()
- method Void Override(VolumeComponent state, Single interpFactor)

## SodaPointLight [class] : UnityEngine.MonoBehaviour

- prop [get/set] Single FallOff
- prop [get/set] Single Hardness
- prop [get/set] Color LightColor

## Soda_Joysticks [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Holding
- prop [get] Vector2 InputValue
- method Void CancleTouch()
- method Void OnDisable()
- method Void OnDrag(PointerEventData eventData)
- method Void OnPointerDown(PointerEventData eventData)
- method Void OnPointerUp(PointerEventData eventData)

## SoftShadowOptions [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## SoulCollector [class] : UnityEngine.MonoBehaviour

- method Void AddCube()

## SoulCube [class] : UnityEngine.MonoBehaviour

- method Void Init(SoulCollector collectorTarget)

## SoundTypes [enum]

- unknowNoise = 0
- combatSound = 1
- grenadeDropSound = 2
- alert = 3

## SpaceShipInstaller [class] : UnityEngine.MonoBehaviour

- method Void Install()

## SpawnControlCharacter [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## SpawnPaperBoxAction.Sockets [enum]

- root = 0
- helmat = 1
- armor = 2

## SplitDialogue [class] : UnityEngine.MonoBehaviour

- prop [get] SplitDialogue Instance (static)
- method Void Cancel()
- method Void OnPointerClick(PointerEventData eventData)
- method Void SetupAndShow(Item item) (static)
- method Void SetupAndShow(Item item, Inventory destinationInventory, Int32 destinationIndex) (static)

## SteamManager [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Initialized (static)
- field Int32 AppID_Int (static)
- field Boolean SteamEnabled (static)

## StockShopDatabase [class] : UnityEngine.ScriptableObject

- prop [get] StockShopDatabase Instance (static)
- method MerchantProfile GetMerchantProfile(String merchantID)

## StrJson [class]

- method StrJson Add(String key, String value)
- method StrJson Create(String[] contentPairs) (static)
- method String ToString()

## SunFogEntry [class] : UnityEngine.MonoBehaviour

- method Void SetEnabled(Boolean enabled) (static)

## SunFogSettings [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## TagUtilities [class]

- method Tag TagFromString(String name) (static)

## TakeOutWeapon.weaponTypes [enum]

- gun = 0
- melee = -1

## TaskEvent [class]

- method Void EmitTaskEvent(String taskEventKey) (static)

## TaskEventEmitter [class] : UnityEngine.MonoBehaviour

- method Void EmitEvent()
- method Void SetKey(String key)

## Team [class]

- method Boolean IsEnemy(Teams selfTeam, Teams targetTeam) (static)

## Teams [enum]

- player = 0
- scav = 1
- usec = 3
- bear = 4
- middle = 5
- lab = 6
- all = 7
- wolf = 8

## TeleportBeacon [class] : UnityEngine.MonoBehaviour

- method Void ActivateBeacon()

## TestSetControl [class] : UnityEngine.MonoBehaviour

- method Void FinishControl()
- method Void SetControl()

## TextureModes [enum]

- High = 0
- Middle = 1
- Low = 2
- VeryLow = 3

## TimeOfDayAlert [class] : UnityEngine.MonoBehaviour

- method Void EnterAlertTrigger() (static)
- method Void LeaveAlertTrigger() (static)

## TimeOfDayAlertTriggerProxy [class] : UnityEngine.MonoBehaviour

- method Void OnEnter()
- method Void OnLeave()

## TimeOfDayConfig [class] : UnityEngine.MonoBehaviour

- method TimeOfDayEntry GetCurrentEntry(Weather weather)
- method Void InvokeDebug()

## TimeOfDayController [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean AtNight
- prop [get] TimeOfDayPhase CurrentPhase
- prop [get] Weather CurrentWeather
- prop [get] TimeOfDayController Instance (static)
- prop [get] Single Time
- field Single NightSenseRangeFactor (static)
- field Single NightViewAngleFactor (static)
- field Single NightViewDistanceFactor (static)
- field Single coldLevel (static)
- field Single heatLevel (static)
- method String GetTimePhaseNameByPhaseTag(TimePhaseTags phaseTag) (static)
- method String GetWeatherNameByWeather(Weather weather) (static)

## TimeOfDayEntry [class] : UnityEngine.MonoBehaviour

- method TimeOfDayPhase GetPhase(TimePhaseTags timePhaseTags)

## TimeOfDayVolumeControl [class] : UnityEngine.MonoBehaviour

- prop [get] VolumeProfile BufferTargetProfile
- prop [get] VolumeProfile CurrentProfile
- method Void ForceSetProfile(VolumeProfile profile)
- method Void SetTargetProfile(VolumeProfile profile)

## TimePhaseTags [enum]

- day = 0
- dawn = 1
- night = 2

## TimeScaleManager [class] : UnityEngine.MonoBehaviour

- field Single devCambulletTimeScale (static)
- method Void EnterBulletTime(Single time)

## Title [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## UI.Menu [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean Focused
- method Void Cancel()
- method Void Confirm()
- method MenuItem GetSelected()
- method T GetSelected()
- method Void Navigate(Vector2 direction)
- method Void Select(MenuItem toSelect)
- method Void SelectDefault()

## UI.MenuItem [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean IsSelected
- prop [get/set] Menu Master
- prop [get/set] Boolean Selectable
- method Void Select()

## UIButtonRevertBinding [class] : UnityEngine.MonoBehaviour

- method Void OnBtnClick()

## UIInputEventData [class]

- prop [get] Boolean Used
- method Void Use()

## UIInputManager [class] : UnityEngine.MonoBehaviour

- prop [get] Boolean Alt (static)
- prop [get] Boolean Ctrl (static)
- prop [get] UIInputManager Instance (static)
- prop [get] Vector2 MouseDelta (static)
- prop [get] Vector2 Point (static)
- prop [get] Boolean Shift (static)
- prop [get] Boolean WasClickedThisFrame (static)
- method Ray GetPointRay() (static)

## UIPanel [class] : UnityEngine.MonoBehaviour

- method Void Close()
- method Void OpenChild(UIPanel childPanel)

## UIPanelButton_OpenChildPanel [class] : UnityEngine.MonoBehaviour

- method Void OnPointerClick(PointerEventData eventData)

## UIShadowOptions [class] : OptionsProviderBase

- prop [get/set] Boolean Active (static)
- prop [get] String ActiveText
- prop [get] String InactiveText
- prop [get] String Key
- method Void Apply() (static)
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)

## UnityEngine.Splines.SplineFlatExtrude [class] : UnityEngine.MonoBehaviour

- prop [get/set] SplineContainer Container
- prop [get/set] Single Height
- prop [get/set] Int32 ProfileSeg
- prop [get/set] Vector2 Range
- prop [get/set] Int32 RebuildFrequency
- prop [get/set] Boolean RebuildOnSplineChange
- prop [get/set] Single SegmentsPerUnit
- prop [get] Spline Spline
- prop [get] IReadOnlyList<Spline> Splines
- prop [get/set] Single Width
- prop [get] SplineContainer container
- prop [get] Boolean rebuildOnSplineChange
- method Void Rebuild()

## UnityEngine.Splines.SplineProfileExtrude [class] : UnityEngine.MonoBehaviour

- prop [get/set] SplineContainer Container
- prop [get/set] Single Height
- prop [get] Int32 ProfileSeg
- prop [get/set] Vector2 Range
- prop [get/set] Int32 RebuildFrequency
- prop [get/set] Boolean RebuildOnSplineChange
- prop [get/set] Single SegmentsPerUnit
- prop [get] Spline Spline
- prop [get] IReadOnlyList<Spline> Splines
- prop [get/set] Single Width
- prop [get] SplineContainer container
- prop [get] Boolean rebuildOnSplineChange
- method Void Rebuild()

## UnityEngine.Splines.SplineSidewalkExtrude [class] : UnityEngine.MonoBehaviour

- prop [get/set] SplineContainer Container
- prop [get/set] Single Height
- prop [get/set] Vector2 Range
- prop [get/set] Int32 RebuildFrequency
- prop [get/set] Boolean RebuildOnSplineChange
- prop [get/set] Single SegmentsPerUnit
- prop [get] Spline Spline
- prop [get] IReadOnlyList<Spline> Splines
- prop [get/set] Single Width
- prop [get] SplineContainer container
- prop [get] Boolean rebuildOnSplineChange
- method Void Rebuild()

## UnityEngine.Splines.SplineSidewalkExtrude.Sides [enum]

- None = 0
- Left = 1
- Right = 2
- Both = 3

## UnlockStockShopItem [class] : Duckov.PerkTrees.PerkBehaviour

- prop [get] String Description

## UseToCreateItem [class] : ItemStatsSystem.UsageBehavior

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)

## ViewTabs [class] : UnityEngine.MonoBehaviour

- method Void Hide()
- method Void Show()

## ViewsProxy [class] : UnityEngine.MonoBehaviour

- method Void ShowATMView()
- method Void ShowBitcoinView()
- method Void ShowBlackMarket_Demands()
- method Void ShowBlackMarket_Supplies()
- method Void ShowDecomposeView()
- method Void ShowEndowmentView()
- method Void ShowFormulasIndexView()
- method Void ShowGamingConsoleView(GamingConsole console)
- method Void ShowGardenView(Garden garnden)
- method Void ShowInventoryView()
- method Void ShowKeyView()
- method Void ShowMapSelectionView()
- method Void ShowMapView()
- method Void ShowPlayerStats()
- method Void ShowQuestView()
- method Void ShowRepairView()
- method Void ShowSleepView()
- method Void ShowStorageDock()

## WaveCharacterSpawner [class] : CharacterSpawnerComponentBase

- field String currentGizmosTag (static)
- method Void Init(CharacterSpawnerRoot root)
- method Void StartSpawn()

## WishListProxy [class] : UnityEngine.MonoBehaviour

- method Void ShowStorePage()

## Zone [class] : UnityEngine.MonoBehaviour

- prop [get] HashSet<Health> Healths

## vSyncSetting [class] : OptionsProviderBase

- prop [get] String Key
- method String GetCurrentOption()
- method String[] GetOptions()
- method Void Set(Int32 index)


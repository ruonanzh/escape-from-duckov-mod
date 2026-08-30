# ItemStatsSystem.dll

52 public types (game-owned; 0 third-party filtered)

## ItemGraphicInfo [class] : UnityEngine.MonoBehaviour

- prop [get] Item ItemRefrence
- field ItemGraphicInfo spriteGraphicPfb (static)
- method ItemGraphicInfo CreateAGraphic(Item item, Transform parent, Boolean snapGround, Boolean useSpriteIfNoGraphic) (static)
- method Transform FindDeepChild(Transform parent, String childName)
- method Void Setup(Item item)
- method Void SnapGroundPointToParent()

## ItemStatsSystem.BoolFilter [class] : ItemStatsSystem.EffectFilter

- prop [get] String DisplayName

## ItemStatsSystem.Data.InventoryData [class]

- method InventoryData FromInventory(Inventory inventory) (static)
- method UniTask LoadIntoInventory(InventoryData data, Inventory inventoryInstance) (static)

## ItemStatsSystem.Data.ItemTreeData [class]

- prop [get] DataEntry RootData
- prop [get] Int32 RootTypeID
- method ItemTreeData FromItem(Item item) (static)
- method DataEntry GetEntry(Int32 instanceID)
- method UniTask<Item> InstantiateAsync(ItemTreeData data) (static)
- method String ToString()

## ItemStatsSystem.Data.ItemTreeData.DataEntry [class]

- prop [get] Int32 StackCount
- prop [get] String TypeName

## ItemStatsSystem.DisplayQuality [enum]

- None = 0
- White = 1
- Green = 2
- Blue = 3
- Purple = 4
- Orange = 5
- Red = 6
- Q7 = 7
- Q8 = 8

## ItemStatsSystem.Effect [class] : UnityEngine.MonoBehaviour

- prop [get] ReadOnlyCollection<EffectAction> Actions
- prop [get] String Description
- prop [get] Boolean Display
- prop [get] ReadOnlyCollection<EffectFilter> Filters
- prop [get] Item Item
- prop [get] ReadOnlyCollection<EffectTrigger> Triggers
- method String GetDisplayString()
- method Void SetItem(Item targetItem)
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.EffectAction [class] : ItemStatsSystem.EffectComponent

- prop [get] String DisplayName
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.EffectComponent [class] : UnityEngine.MonoBehaviour

- prop [get] String DisplayName
- prop [get/set] Effect Master
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.EffectFilter [class] : ItemStatsSystem.EffectComponent

- prop [get] String DisplayName
- method Boolean Evaluate(EffectTriggerEventContext context)
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.EffectTrigger [class] : ItemStatsSystem.EffectComponent

- prop [get] String DisplayName
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.Inventory [class] : UnityEngine.MonoBehaviour

- prop [get/set] Boolean AcceptSticky
- prop [get/set] Item AttachedToItem
- prop [get] Single CachedWeight
- prop [get] Int32 Capacity
- prop [get] List<Item> Content
- prop [get] String DisplayName
- prop [get/set] String DisplayNameKey
- prop [get] Item Item
- prop [get/set] Boolean Loading
- prop [get/set] Boolean NeedInspection
- method Boolean AddAt(Item item, Int32 atPosition)
- method Boolean AddItem(Item item)
- method Void DestroyAllContent()
- method Item Find(Int32 typeID)
- method List<Item> FindAll(Predicate<Item> match)
- method IEnumerator<Item> GetEnumerator()
- method Int32 GetFirstEmptyPosition(Int32 preferedFirstPosition)
- method Int32 GetIndex(Item item)
- method Item GetItemAt(Int32 position)
- method Int32 GetItemCount()
- method Int32 GetLastItemPosition()
- method Boolean IsEmpty()
- method Boolean IsIndexLocked(Int32 index)
- method Void LockIndex(Int32 index)
- method Void RecalculateWeight()
- method Boolean RemoveAt(Int32 position, Item& removedItem)
- method Boolean RemoveItem(Item item)
- method Void SetCapacity(Int32 capacity)
- method Void Sort()
- method Void Sort(Comparison<Item> comparison)
- method Void ToggleLockIndex(Int32 index)
- method Void UnlockIndex(Int32 index)
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.Item [class] : UnityEngine.MonoBehaviour

- prop [get] ItemAgent ActiveAgent
- prop [get] ItemAgentUtilities AgentUtilities
- prop [get] Boolean CanBeSold
- prop [get] Boolean CanDrop
- prop [get] CustomDataCollection Constants
- prop [get] String Description
- prop [get] String DescriptionRaw
- prop [get] String DisplayName
- prop [get/set] String DisplayNameRaw
- prop [get/set] DisplayQuality DisplayQuality
- prop [get/set] Single Durability
- prop [get/set] Single DurabilityLoss
- prop [get] List<Effect> Effects
- prop [get/set] String FromInfoKey
- prop [get] Boolean HasHandHeldAgent
- prop [get/set] Sprite Icon
- prop [get] Inventory InInventory
- prop [get/set] Boolean Inspected
- prop [get/set] Boolean Inspecting
- prop [get/set] Inventory Inventory
- prop [get] Boolean IsBeingDestroyed
- prop [get] Boolean IsCharacter
- prop [get] ItemGraphicInfo ItemGraphic
- prop [get/set] Single MaxDurability
- prop [get] Single MaxDurabilityWithLoss
- prop [get/set] Int32 MaxStackCount
- prop [get] ModifierDescriptionCollection Modifiers
- prop [get] Boolean NeedInspection
- prop [get/set] Int32 Order
- prop [get] Item ParentItem
- prop [get] Object ParentObject
- prop [get] Slot PluggedIntoSlot
- prop [get/set] Int32 Quality
- prop [get] Boolean Repairable
- prop [get] Single SelfWeight
- prop [get] SlotCollection Slots
- prop [get] String SoundKey
- prop [get/set] Int32 StackCount
- prop [get] Boolean Stackable
- prop [get] StatCollection Stats
- prop [get] Boolean Sticky
- prop [get] TagCollection Tags
- prop [get] Single TotalWeight
- prop [get/set] Int32 TypeID
- prop [get] Single UnitSelfWeight
- prop [get] UsageUtilities UsageUtilities
- prop [get] Boolean UseDurability
- prop [get] Single UseTime
- prop [get/set] Int32 Value
- prop [get] CustomDataCollection Variables
- method Void AddEffect(Effect instance)
- method Boolean AddModifier(String statKey, Modifier modifier)
- method Void AddUsageUtilitiesComponent()
- method Void Combine(Item incomingItem)
- method Void CombineInto(Item otherItem)
- method Item CreateInstance()
- method Void CreateInventoryComponent()
- method Void CreateModifiersComponent()
- method Void CreateSlotsComponent()
- method Void CreateStatsComponent()
- method Void Detach()
- method Boolean GetBool(String key, Boolean defaultResult)
- method Boolean GetBool(Int32 hash, Boolean defaultResult)
- method Item GetCharacterItem()
- method Single GetFloat(String key, Single defaultResult)
- method Single GetFloat(Int32 hash, Single defaultResult)
- method Int32 GetInt(String key, Int32 defaultResult)
- method Int32 GetInt(Int32 hash, Int32 defaultResult)
- method Stat GetStat(Int32 hash)
- method Stat GetStat(String key)
- method Stat GetStat(Item item, Int32 hash) (static)
- method Single GetStatValue(Int32 hash)
- method Single GetStatValue(Item item, Int32 hash) (static)
- method String GetString(String key, String defaultResult)
- method String GetString(Int32 hash, String defaultResult)
- method Int32 GetTotalRawValue()
- method CustomData GetVariableEntry(String variableKey)
- method CustomData GetVariableEntry(Int32 hash)
- method Void Initialize()
- method Boolean IsInCharacterSlot()
- method Boolean IsUsable(Object user)
- method Void MarkDestroyed()
- method Single RecalculateTotalWeight()
- method Int32 RemoveAllModifiersFrom(Object endowmentEntry)
- method Void SetBool(Int32 hash, Boolean value)
- method Void SetBool(String key, Boolean value, Boolean createNewIfNotExist)
- method Void SetFloat(Int32 hash, Single value)
- method Void SetFloat(String key, Single value, Boolean createNewIfNotExist)
- method Void SetInt(Int32 hash, Int32 value)
- method Void SetInt(String key, Int32 value, Boolean createNewIfNotExist)
- method Void SetString(Int32 hash, String value)
- method Void SetString(String key, String value, Boolean createNewIfNotExist)
- method Void SetTypeID(Int32 id)
- method UniTask<Item> Split(Int32 count)
- method String ToString()
- method Void Use(Object user, Boolean inBaseLevel)
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.ItemAgent [class] : UnityEngine.MonoBehaviour

- prop [get] AgentTypes AgentType
- prop [get] Item Item
- method Void Initialize(Item item, AgentTypes _agentType)

## ItemStatsSystem.ItemAgent.AgentTypes [enum]

- normal = 0
- pickUp = 1
- handheld = 2
- equipment = 3

## ItemStatsSystem.ItemAgentUtilities [class]

- prop [get] ItemAgent ActiveAgent
- prop [get] ItemAgent Item
- prop [get] ItemAgent Item
- prop [get] Item Master
- method Void BindNewAgent(ItemAgent agent, AgentTypes agentType)
- method ItemAgent CreateAgent(ItemAgent prefab, AgentTypes agentType)
- method ItemAgent GetPrefab(Int32 hash)
- method ItemAgent GetPrefab(String key)
- method Void Initialize(Item master)
- method Void ReleaseActiveAgent()

## ItemStatsSystem.ItemAssetsCollection [class] : UnityEngine.ScriptableObject

- prop [get] ItemAssetsCollection Instance (static)
- prop [get] Int32 NextTypeID
- method Boolean AddDynamicEntry(Item prefab) (static)
- method Void Collect()
- method Int32[] GetAllTypeIds(ItemFilter filter) (static)
- method ItemMetaData GetMetaData(Int32 typeID) (static)
- method Item GetPrefab(Int32 typeID) (static)
- method UniTask<Item> InstantiateAsync(Int32 typeID) (static)
- method UniTask<Item> InstantiateAsync_Local(Int32 typeID)
- method Item InstantiateFallbackItem(Int32 typeID) (static)
- method Item InstantiateSync(Int32 typeID) (static)
- method Void RefreshMeta()
- method Boolean RemoveDynamicEntry(Item prefab) (static)
- method Int32[] Search(ItemFilter filter) (static)
- method Boolean TryGetDynamicEntry(Int32 typeID, DynamicEntry& entry) (static)
- method Int32 TryGetIDByName(String name, Boolean ignoreCase) (static)
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.ItemAssetsCollection.DynamicEntry [class]

- prop [get] ItemMetaData MetaData

## ItemStatsSystem.ItemAssetsCollection.Entry [class]

- method Void RefreshMetaData()
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.ItemComponent [class] : UnityEngine.MonoBehaviour

- prop [get/set] Item Master
- method Void Validate(SelfValidationResult result)

## ItemStatsSystem.ItemFilter [struct]

- method Int32 GetHashCode()
- method String ToString()

## ItemStatsSystem.ItemMetaData [struct]

- prop [get] String Catagory
- prop [get] String Description
- prop [get] String DisplayName
- prop [get] String DisplayNameKey
- prop [get] String Name

## ItemStatsSystem.ItemTreeExtensions [class]

- method Void DestroyTree(Item item) (static)
- method Void DestroyTreeImmediate(Item item) (static)
- method List<Item> GetAllChildren(Item item, Boolean includingGrandChildren, Boolean excludeSelf) (static)
- method List<Item> GetAllConnected(Item item) (static)
- method List<Item> GetAllParents(Item item, Boolean excludeSelf) (static)
- method Item GetRoot(Item item) (static)

## ItemStatsSystem.ItemUsedTrigger [class] : ItemStatsSystem.EffectTrigger

- prop [get] String DisplayName

## ItemStatsSystem.Items.Slot [class]

- prop [get] Item Content
- prop [get] String DisplayName
- prop [get] Boolean ForbidItemsWithSameID
- prop [get] String Key
- prop [get] Item Master
- prop [get/set] Sprite SlotIcon
- method Boolean CanPlug(Item item)
- method Void ForceInvokeSlotContentChangedEvent()
- method Void Initialize(SlotCollection collection)
- method Boolean Plug(Item otherItem, Item& unpluggedItem)
- method Item Unplug()

## ItemStatsSystem.Items.SlotCollection [class] : ItemStatsSystem.ItemComponent

- prop [get] Int32 Count
- prop [get] Boolean IsReadOnly
- prop [get] Slot Item
- prop [get] Slot Item
- method Void Add(Slot item)
- method Void Clear()
- method Boolean Contains(Slot item)
- method Void CopyTo(Slot[] array, Int32 arrayIndex)
- method IEnumerator<Slot> GetEnumerator()
- method Slot GetSlot(Int32 hash)
- method Slot GetSlot(String key)
- method Slot GetSlotByIndex(Int32 index)
- method Boolean Remove(Slot item)
- method Void TryInitialize()

## ItemStatsSystem.LogItemNameAction [class] : ItemStatsSystem.EffectAction

- prop [get] String DisplayName

## ItemStatsSystem.LogWhenUsed [class] : ItemStatsSystem.UsageBehavior

- method Boolean CanBeUsed(Item item, Object user)

## ItemStatsSystem.ModifierDescription [class]

- prop [get] Boolean Display
- prop [get] String DisplayName
- prop [get] Boolean IsOverrideOrder
- prop [get] String Key
- prop [get] Int32 Order
- prop [get] ModifierType Type
- prop [get] Single Value
- method Modifier CreateModifier(Object source)
- method ModifierDescription FromString(String str) (static)
- method String GetDisplayValueString(String format)
- method Item GetTargetItem()
- method Void ReapplyModifier(ModifierDescriptionCollection collection)
- method String ToString()

## ItemStatsSystem.ModifierDescriptionCollection [class] : ItemStatsSystem.ItemComponent

- prop [get] Int32 Count
- prop [get] Boolean IsReadOnly
- prop [get/set] Boolean ModifierEnable
- method Void Add(ModifierDescription item)
- method Void Clear()
- method Boolean Contains(ModifierDescription item)
- method Void CopyTo(ModifierDescription[] array, Int32 arrayIndex)
- method ModifierDescription Find(Predicate<ModifierDescription> predicate)
- method IEnumerator<ModifierDescription> GetEnumerator()
- method Void ReapplyModifiers()
- method Boolean Remove(ModifierDescription item)

## ItemStatsSystem.ModifierTarget [enum]

- Self = 0
- Parent = 1
- Character = 2

## ItemStatsSystem.Polarity [enum]

- Neutral = 0
- Positive = 1
- Negative = -1

## ItemStatsSystem.Stat [class]

- prop [get/set] Single BaseValue
- prop [get] Boolean Display
- prop [get] String DisplayName
- prop [get] String DisplayNameKey
- prop [get] String Key
- prop [get] Item Master
- prop [get] List<Modifier> Modifiers
- prop [get] Single Value
- method Void AddModifier(Modifier modifier)
- method Int32 RemoveAllModifiersFromSource(Object source)
- method Boolean RemoveModifier(Modifier modifier)

## ItemStatsSystem.StatCollection [class] : ItemStatsSystem.ItemComponent

- prop [get] Int32 Count
- prop [get] Boolean IsReadOnly
- prop [get] Stat Item
- prop [get] Stat Item
- method Void Add(Stat item)
- method Void Clear()
- method Boolean Contains(Stat item)
- method Void CopyTo(Stat[] array, Int32 arrayIndex)
- method IEnumerator<Stat> GetEnumerator()
- method Stat GetStat(Int32 hash)
- method Stat GetStat(String key)
- method Boolean Remove(Stat item)

## ItemStatsSystem.Stats.Modifier [class]

- prop [get] Int32 Order
- prop [get] Object Source
- prop [get] ModifierType Type
- prop [get/set] Single Value
- field Comparison<Modifier> OrderComparison (static)
- method Void NotifyAddedToStat(Stat stat)
- method Void RemoveFromTarget()
- method String ToString()

## ItemStatsSystem.Stats.ModifierType [enum]

- Add = 0
- PercentageAdd = 100
- PercentageMultiply = 200

## ItemStatsSystem.TickTrigger [class] : ItemStatsSystem.EffectTrigger

- prop [get] String DisplayName
- method Void OnUpdate()

## ItemStatsSystem.UpdateTrigger [class] : ItemStatsSystem.EffectTrigger

- prop [get] String DisplayName

## ItemStatsSystem.UsageBehavior [class] : UnityEngine.MonoBehaviour

- prop [get] DisplaySettingsData DisplaySettings
- method Boolean CanBeUsed(Item item, Object user)
- method Void Use(Item item, Object user)

## ItemStatsSystem.UsageBehavior.DisplaySettingsData [struct]

- prop [get] String Description

## ItemStatsSystem.UsageUtilities [class] : ItemStatsSystem.ItemComponent

- prop [get] Single UseTime
- method Boolean IsUsable(Item item, Object user)
- method Void Use(Item item, Object user, Boolean inBaseLevel)


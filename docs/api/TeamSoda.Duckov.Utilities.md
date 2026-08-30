# TeamSoda.Duckov.Utilities.dll

20 public types (game-owned; 0 third-party filtered)

## Duckov.Utilities.CustomData [class]

- prop [get] CustomDataType DataType
- prop [get/set] Boolean Display
- prop [get] String DisplayName
- prop [get] String Key
- method Boolean GetBool()
- method Single GetFloat()
- method Int32 GetInt()
- method Byte[] GetRawCopied()
- method String GetString()
- method String GetValueDisplayString(String format)
- method Void SetBool(Boolean value)
- method Void SetFloat(Single value)
- method Void SetInt(Int32 value)
- method Void SetRaw(Byte[] value)
- method Void SetString(String value)

## Duckov.Utilities.CustomDataCollection [class]

- prop [get] Int32 Count
- prop [get] Boolean IsReadOnly
- method Void Add(CustomData item)
- method Void Clear()
- method Boolean Contains(CustomData item)
- method Void CopyTo(CustomData[] array, Int32 arrayIndex)
- method Boolean GetBool(String key, Boolean defaultResult)
- method Boolean GetBool(Int32 hash, Boolean defaultResult)
- method CustomData GetEntry(Int32 hash)
- method CustomData GetEntry(String key)
- method IEnumerator<CustomData> GetEnumerator()
- method Single GetFloat(String key, Single defaultResult)
- method Single GetFloat(Int32 hash, Single defaultResult)
- method Int32 GetInt(String key, Int32 defaultResult)
- method Int32 GetInt(Int32 hash, Int32 defaultResult)
- method Byte[] GetRawCopied(String key, Byte[] defaultResult)
- method Byte[] GetRawCopied(Int32 hash, Byte[] defaultResult)
- method String GetString(String key, String defaultResult)
- method String GetString(Int32 hash, String defaultResult)
- method Boolean Remove(CustomData item)
- method Void Set(Int32 hash, Single value)
- method Void Set(Int32 hash, Int32 value)
- method Void Set(Int32 hash, Boolean value)
- method Void Set(Int32 hash, String value)
- method Void Set(String key, Single value, Boolean createNewIfNotExist)
- method Void Set(String key, Int32 value, Boolean createNewIfNotExist)
- method Void Set(String key, Boolean value, Boolean createNewIfNotExist)
- method Void Set(String key, String value, Boolean createNewIfNotExist)
- method Void SetBool(Int32 hash, Boolean value)
- method Void SetBool(String key, Boolean value, Boolean createNewIfNotExist)
- method Void SetDisplay(String key, Boolean display)
- method Void SetFloat(Int32 hash, Single value)
- method Void SetFloat(String key, Single value, Boolean createNewIfNotExist)
- method Void SetInt(Int32 hash, Int32 value)
- method Void SetInt(String key, Int32 value, Boolean createNewIfNotExist)
- method Void SetRaw(Int32 hash, CustomDataType type, Byte[] bytes)
- method Void SetRaw(String key, CustomDataType type, Byte[] bytes, Boolean createNewIfNotExist, Boolean display)
- method Void SetString(Int32 hash, String value)
- method Void SetString(String key, String value, Boolean createNewIfNotExist)

## Duckov.Utilities.CustomDataType [enum]

- Raw = 0
- Float = 1
- Int = 2
- Bool = 3
- String = 4

## Duckov.Utilities.DuckovUtilitiesSettings [class] : UnityEngine.ScriptableObject

- prop [get] ColorsData Colors (static)

## Duckov.Utilities.DuckovUtilitiesSettings.ColorsData [class]

- prop [get] Color EffectAction
- prop [get] Color EffectFilter
- prop [get] Color EffectTrigger

## Duckov.Utilities.IPercentRefreshable [interface]

- method Void RefreshPercent()

## Duckov.Utilities.IPoolable [interface]

- method Void NotifyPooled()
- method Void NotifyReleased()

## Duckov.Utilities.PrefabPool<T> [class]

- prop [get] ReadOnlyCollection<T> ActiveEntries
- method T Find(Predicate<T> predicate)
- method T Get(Transform setParent)
- method Void Release(T item)
- method Void ReleaseAll()
- method Int32 ReleaseAll(Predicate<T> predicate)

## Duckov.Utilities.RandomContainer<T> [class]

- prop [get] Int32 Count
- method Void AddEntry(T _value, Single _weight)
- method RandomContainer<String> FromString(String str) (static)
- method T GetRandom(Single lowPercent)
- method T GetRandom(Random overrideRandom, Single lowPercent)
- method T GetRandom(Random overrideRandom, Func<T, Boolean> predicator, Single lowPercent)
- method List<T> GetRandomMultiple(Int32 count, Boolean repeatable)
- method Void RefreshPercent()

## Duckov.Utilities.RandomUtilities [class]

- method T GetRandom(IList<T> list) (static)
- method T GetRandom(T[] array) (static)
- method T GetRandom(IList<T> list, Random rng) (static)
- method T[] GetRandomSubSet(IList<T> list, Int32 amount) (static)
- method T GetRandomWeighted(IList<T> list, Func<T, Single> weightFunction, Single lowPercent) (static)
- method Void RandomizeOrder(List<T> list) (static)

## Duckov.Utilities.StringList [class] : UnityEngine.ScriptableObject

- prop [get] ReadOnlyCollection<String> Strings
- method IEnumerator<String> GetEnumerator()

## Duckov.Utilities.StringLists [class] : UnityEngine.ScriptableObject

- prop [get] StringList ItemAgentKeys (static)
- prop [get] StringList SlotNames (static)
- prop [get] StringList StatKeys (static)

## Duckov.Utilities.Tag [class] : UnityEngine.ScriptableObject

- prop [get] Color Color
- prop [get] String Description
- prop [get] String DisplayName
- prop [get] Int32 Hash
- prop [get] Int32 Priority
- prop [get] Boolean Show
- prop [get] Boolean ShowDescription
- method Boolean Match(Tag tag, String name) (static)
- method String ToString()

## Duckov.Utilities.TagCollection [class]

- prop [get] Int32 Count
- prop [get] Boolean IsReadOnly
- method Void Add(Tag item)
- method Boolean Check(ICollection<Tag> requireTags, ICollection<Tag> excludeTags)
- method Void Clear()
- method Boolean Contains(Tag item)
- method Boolean Contains(String tagName)
- method Void CopyTo(Tag[] array, Int32 arrayIndex)
- method Tag Get(Int32 index)
- method IEnumerator<Tag> GetEnumerator()
- method Boolean Remove(Tag item)

## Duckov.Utilities.TrasnformExtensions [class]

- method Void DestroyAllChildren(Transform transform) (static)

## Duckov.Utilities.UpdatableInvoker [class] : UnityEngine.MonoBehaviour

- prop [get] UpdatableInvoker Instance (static)
- method Void Register(IUpdatable updatable) (static)
- method Boolean Unregister(IUpdatable updatable) (static)

## Duckov.Utilities.Updatables.IUpdatable [interface]

- method Void OnUpdate()


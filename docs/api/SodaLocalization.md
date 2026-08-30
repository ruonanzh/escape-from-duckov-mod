# SodaLocalization.dll

10 public types (game-owned; 0 third-party filtered)

## LocalizationDataModel [class]

- prop [get] String DisplayName
- method String GetPlainText(String key)

## SodaCraft.Localizations.CSVUtilities [class]

- method List<List<String>> ReadCSV(String csvContent) (static)
- method Void SaveAsCSV(List<List<String>> table, String path) (static)
- method String ToCSVString(List<List<String>> table) (static)

## SodaCraft.Localizations.LanguageSettings [class]

- prop [get] SystemLanguage Language
- prop [get] ILocalizationProvider Provider
- prop [get] String path
- method LanguageSettings CreateFromPath(String path) (static)
- method String GetDisplayName()
- method String GetPlainText(String key)

## SodaCraft.Localizations.LocalizationDatabase [class] : UnityEngine.ScriptableObject

- prop [get] LocalizationDatabase Instance (static)
- method LanguageSettings GetEntry(SystemLanguage language)
- method String[] GetLanguageDisplayNameList()

## SodaCraft.Localizations.LocalizationManager [class]

- prop [get/set] SystemLanguage CurrentLanguage (static)
- prop [get] String CurrentLanguageDisplayName (static)
- prop [get/set] LocalizationDataModel DataModel (static)
- prop [get] LocalizationDataModel FallbackDataModel (static)
- prop [get/set] Boolean Initialized (static)
- field Dictionary<String, String> overrideTexts (static)
- method String GetDisplayName(SystemLanguage language) (static)
- method String GetPlainText(String key) (static)
- method Boolean RemoveOverrideText(String key) (static)
- method Void SetLanguage(SystemLanguage language) (static)
- method Void SetLanguage(String name) (static)
- method Void SetLanguage(Int32 index) (static)
- method Void SetOverrideText(String key, String value) (static)
- method String ToPlainText(String key) (static)
- method Boolean TryGetOverrideText(String key, String& value) (static)

## SodaCraft.Localizations.TextLocalizor [class] : UnityEngine.MonoBehaviour

- prop [get/set] String Key


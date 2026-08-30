# TeamSoda.MiniLocalizor.dll

3 public types (game-owned; 0 third-party filtered)

## CSVFileLocalizor [class]

- prop [get] SystemLanguage Language
- prop [get] String Path
- method Void BuildDictionary()
- method String Get(String key)
- method DataEntry GetEntry(String key)
- method Boolean HasKey(String key)

## MiniLocalizor.DataEntry [class]

- prop [get/set] String key
- prop [get/set] String sheet
- prop [get/set] String value
- prop [get/set] String version
- method Boolean IsNewerThan(String version)

## MiniLocalizor.ILocalizationProvider [interface]

- method String Get(String key)


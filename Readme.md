# Okta User Export Tool

This tool exports Okta directory data (users, groups, apps) to CSV files. It targets `.NET Framework 4.8` and is a console utility that supports robust paging, throttling, retry/backoff, and flexible configuration.

## Summary of recent changes
- `Common.cs` enhanced with:
  - HTTP throttling and robust retry logic (`GetAsyncWithThrottling`, `ThrottleAsync`, `ComputeBackoff`, `GetRetryAfterDelay`).
  - Polly-based `RetryPolicy` configured from `App.config`.
  - New throttle settings: `InterRequestDelayMs` and `HttpMaxRetries`.
  - Date placeholder replacement in filenames: `%Today%`, `%DateTime%`, `%DateTimeStamp%`.
  - CSV escaping via `Common.CsvEscape(object)`.
  - A thread-safe map `UserToGroupIds` (`ConcurrentDictionary<string, List<string>>`) to collect user→group relationships during exports.
  - Utilities to aggregate paged responses: `GetAllPagesAsJArrayAsync`.
- `OktaConnect` support: a path/filename to a JSON config (parsed with `Common.Utils.JsonFileReader`) so `OktaUrl` and `OktaApiKey` can be read from a JSON file instead of only environment variables / App.config.
- Improved handling and guidance for JSON tokens that may be arrays/objects (use `JsonFileReader` or flatten before casting to string).

## Requirements
- .NET Framework 4.8
- Visual Studio 2022 (or compatible)
- Dependencies:
  - `Newtonsoft.Json` (Json.NET)
  - `Polly` (for retry policy)
  - `Common.Utils.dll` — provides `JsonFileReader` and `File_Utils.SetFile` helpers (must be referenced and copied to output)

## App.config (appSettings) — keys now used
- `OktaUrl` — fallback Okta base URL
- `OktaApiKey` — fallback Okta API token
- `OktaConnect` — optional path to JSON file containing `OktaUrl` and `OktaApiKey` (if present, JSON values take precedence)
- `OutputFileName` — users CSV file name (supports date placeholders)
- `OutputUsersGroupsFileName` — users↔groups CSV file name
- `OutputGroupsOnlyFileName` — groups CSV file name
- `OutputUsersAppsFileName` — users↔apps CSV file name
- `OutputAppsOnlyFileName` — apps CSV file name
- `OutputJsonFileName` — raw JSON extract filename
- `OutputFolderPath` — optional output folder
- `PollyRetryCount` — number of Polly retries (default from code)
- `PollyRetryBaseDelayMs` — base delay for Polly backoff (ms)
- `InterRequestDelayMs` — minimum inter-request delay used for throttling (ms)
- `HttpMaxRetries` — maximum attempts for `GetAsyncWithThrottling` (used for 429/5xx)

Example snippet:
````````
<appSettings>
  <add key="OktaUrl" value="https://yourdomain.okta.com"/>
  <add key="OktaApiKey" value="your_api_key"/>
  <add key="OktaConnect" value="path\to\okta_config.json"/>
  <add key="OutputFileName" value="OktaUsers_%DateTime%.csv"/>
  <add key="OutputUsersGroupsFileName" value="OktaUsersGroups.csv"/>
  <add key="OutputGroupsOnlyFileName" value="OktaGroups.csv"/>
  <add key="OutputUsersAppsFileName" value="OktaUsersApps.csv"/>
  <add key="OutputAppsOnlyFileName" value="OktaApps.csv"/>
  <add key="OutputJsonFileName" value="OktaData.json"/>
  <add key="OutputFolderPath" value="C:\Exports"/>
  <add key="PollyRetryCount" value="5"/>
  <add key="PollyRetryBaseDelayMs" value="200"/>
  <add key="InterRequestDelayMs" value="100"/>
  <add key="HttpMaxRetries" value="3"/>
</appSettings>
````````

## Building
1. Open the solution in Visual Studio 2022.
2. Ensure `Common.Utils.dll` is referenced (or the `Common.Utils` project is added to the solution).
3. Restore NuGet packages (e.g., Newtonsoft.Json, Polly).
4. Build the solution (targeting .NET Framework 4.8).

## Running
- From Visual Studio: run the console project.
- From the command line: run `Okta.Tools.UserExporter.exe` from the folder containing the executable and `Common.Utils.dll`.
- Outputs are written to `OutputFileName` and/or `OutputGroupsOnlyFileName` as configured.

## Troubleshooting
- Error `Can not convert Array to String`:
  - Cause: The code attempts `(string)jtoken` while the token is a `JArray` (Okta sometimes returns arrays for fields like `objectClass`).
  - Fixes:
    - Update code to flatten `JArray`/`JObject` before converting to string (for example join array elements with `;` or call `token.ToString(Formatting.None)`).
    - Use `Common.CsvEscape` when writing values; ensure you don't cast a `JToken` to `string` directly.
- Missing `Common.Utils.dll`:
  - Ensure the DLL is referenced in the project and copied to the output folder.
  - If you don't have the DLL, locate or rebuild the `Common.Utils` project and add it to the solution.

## Notes and troubleshooting
- If you see `Can not convert Array to String`, a JSON property returned by Okta (for example `objectClass`) is an array. Use `Common.Utils.JsonFileReader` or flatten `JToken` values before converting to string to avoid direct `(string)jtoken` casts.
- Ensure `Common.Utils.dll` is referenced and `Copy Local = True` so it exists next to `Okta.Tools.UserExporter.exe`.
- The `UserToGroupIds` `ConcurrentDictionary` is available to collect user→group mappings; consumers should populate it in the export flow when enumerating group membership.
- CSV fields are escaped with `Common.CsvEscape` which quotes fields, removes newlines, and doubles embedded quotes.

## Next steps / recommended improvements
- Add a consistent `JToken` flattening helper in `Common` or use `JsonFileReader` for all JSON token conversions.
- Add CLI switches to select export mode and override file paths at runtime.
- Consider streaming large exports to reduce memory pressure.


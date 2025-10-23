# Okta User Export Tool

This tool exports Okta directory data to CSV files. It can export full user profiles (including custom UD attributes) and group listings. The project targets `.NET Framework 4.8`.

## Overview
- Exports Okta users and groups to CSV.
- Handles paged Okta API responses using `Common.GetAllPagesAsJArrayAsync`.
- Escapes CSV fields using `Common.CsvEscape`.
- Adds a `GroupsOnly` mode that writes group id, name and other group fields to CSV.

## What's changed
- CSV escaping centralized in `Common.CsvEscape(object)` (it quotes, removes newlines, doubles quotes).
- Paging aggregated via `Common.GetAllPagesAsJArrayAsync(HttpClient, string)`.
- New `GroupsOnly` export implemented in `GroupsOnly.cs`.
- Note: some Okta fields (for example `objectClass`) may be arrays/objects in the JSON response. The code casts tokens to string in some places; if you encounter errors like `Can not convert Array to String` you should flatten `JToken` values before casting (or use a small helper that converts `JArray`/`JObject` to a delimited string or compact JSON).

## Requirements
- .NET Framework 4.8 (project is configured for this TFM).
- Visual Studio 2022 (or compatible) to build and edit.
- Newtonsoft.Json (Json.NET) package (used for `JArray`/`JObject`).
- `Common.Utils.dll` — REQUIRED:
  - Provides helper utilities referenced by the project (for example `File_Utils.SetFile` and other helpers contained in `Common`).
  - Must be referenced at compile-time and present at runtime (either in the project's reference list or copied next to the executable).

## Obtaining/Installing `Common.Utils.dll`
- If you have source for `Common.Utils`, build it targeting .NET Framework 4.8 and add a project or assembly reference.
- To add the assembly in Visual Studio:
  1. Right-click the project -> `Add` -> `Reference...`.
  2. Choose `Browse` and select `Common.Utils.dll`.
  3. Ensure `Copy Local` is `True` (so the DLL is placed next to the executable).
- Alternatively, place `Common.Utils.dll` in the same folder as `Okta.Tools.UserExporter.exe` before running.

## Configuration
Edit `app.config` (or `Okta.Tools.UserExporter.exe.config`) `<appSettings>` with the following keys:

- `OktaUrl` — Base URL of your Okta org (including `https://`). Examples:
  - `https://acme.okta.com`
  - `https://acme.okta-emea.com`
  - `https://acmedev.oktapreview.com`
- `OktaApiKey` — API token from Admin → Security → API.
- `OutputFileName` — Optional. Filename for user export (only the name; file is written to the running folder). If omitted a default name like `OktaUsers_yyyyMMdd-hhmmss.csv` is used.
- `OutputGroupsOnlyFileName` — Optional. Filename for the groups-only export.

Sample snippet:

````````
<appSettings>
  <add key="OktaUrl" value="https://yourdomain.okta.com"/>
  <add key="OktaApiKey" value="your_api_key"/>
  <add key="OutputFileName" value="OktaUsers.csv"/>
  <add key="OutputGroupsOnlyFileName" value="OktaGroups.csv"/>
</appSettings>
````````

## Building
1. Open the solution in Visual Studio 2022.
2. Ensure `Common.Utils.dll` is referenced (or the `Common.Utils` project is added to the solution).
3. Restore NuGet packages (e.g., Newtonsoft.Json).
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

## Further improvements
- Add a `JToken`-to-string helper to consistently flatten arrays/objects prior to CSV escaping.
- Stream large exports to avoid high memory usage.
- Consider adding command-line options to select mode (users vs groups), file paths, and paging size.


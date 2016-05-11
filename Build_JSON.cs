using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Okta.Tools.UserExporter
{
    public static class Build_JSON
    {
        // Entry point to build the JSON summary after all CSV exports are done
        public static async Task<int> BuildJsonMain()
        {
            try
            {
                var jsonPath = CreateEmptyJson();

                // Add a section for each CSV that exists
                TryAddCsvSection(jsonPath, "Users", Common.OutputFileName);
                TryAddCsvSection(jsonPath, "UsersGroups", Common.OutputUsersGroupsFileName);
                TryAddCsvSection(jsonPath, "GroupsOnly", Common.OutputGroupsOnlyFileName);
                TryAddCsvSection(jsonPath, "UsersApps", Common.OutputUsersAppsFileName);
                TryAddCsvSection(jsonPath, "AppsOnly", Common.OutputAppsOnlyFileName);

                Console.WriteLine("JSON summary file written to {0}", jsonPath);
                await Task.CompletedTask;
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Build_JSON error: {0}", ex);
                return 1;
            }
        }

        // Creates (or truncates) the JSON file in the same directory as the CSV outputs
        public static string CreateEmptyJson()
        {
            var jsonPath = Common.SetFile(Common.OutputJsonFileName);
            
            // Initialize with an empty root object
            var root = new JObject
            {
                ["generatedUtc"] = DateTime.UtcNow.ToString("o"),
                ["files"] = new JArray()
            };

            File.WriteAllText(jsonPath, root.ToString());
            return jsonPath;
        }

        // Checks if a CSV exists for the provided file name (relative to the CSV output directory)
        public static bool CsvCreated(string csvFileName, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(csvFileName)) return false;

            var dir = Path.GetDirectoryName(Common.FilePath);
            if (string.IsNullOrWhiteSpace(dir)) dir = Directory.GetCurrentDirectory();

            fullPath = Path.Combine(dir, csvFileName);
            return File.Exists(fullPath);
        }

        // Adds a file section for a CSV file if it exists
        public static void AddFileSection(string jsonPath, string name, string csvFullPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(csvFullPath)) return;

            var text = File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : null;
            var root = !string.IsNullOrWhiteSpace(text) ? JObject.Parse(text) : new JObject();

            var files = root["files"] as JArray;
            if (files == null)
            {
                files = new JArray();
                root["files"] = files;
            }

            var fi = new FileInfo(csvFullPath);
            var item = new JObject
            {
                ["name"] = name ?? Path.GetFileNameWithoutExtension(csvFullPath),
                ["fileName"] = fi.Name,
                ["path"] = csvFullPath,
                ["sizeBytes"] = fi.Exists ? fi.Length : 0,
                ["createdUtc"] = fi.Exists ? fi.CreationTimeUtc.ToString("o") : string.Empty
            };

            files.Add(item);
            File.WriteAllText(jsonPath, root.ToString());
        }

        private static void TryAddCsvSection(string jsonPath, string name, string csvFileName)
        {
            if (CsvCreated(csvFileName, out var full))
            {
                AddFileSection(jsonPath, name, full);
            }
        }
    }
}

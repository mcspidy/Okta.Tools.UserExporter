using System;
using Newtonsoft.Json.Linq; // only needed if you use ReadJsonFile's return type
using Common.Utils;

class JsonFileReaderUsage
{
    static void UsageMain()
    {
        string filePath = "config.json";

        // 1) Simple safe lookup (returns default on missing/error)
        string name = JsonFileReader.GetValue(filePath, "profile.name", "unknown");
        Console.WriteLine($"profile.name = {name}");

        // 2) Try-get pattern (inspect success separately)
        if (JsonFileReader.TryGetValue(filePath, "roles", out string roles, defaultValue: "none"))
        {
            // If roles was a JSON array like ["admin","user"] -> roles == "admin;user"
            Console.WriteLine($"roles = {roles}");
        }
        else
        {
            Console.WriteLine("roles not found or error reading file");
        }

        // 3) Get entire document flattened
        string fullDoc = JsonFileReader.GetValue(filePath, "", "{}");
        Console.WriteLine($"full document (flattened) = {fullDoc}");

        // 4) Direct parse when you need a JToken to walk/manipulate
        try
        {
            JToken root = JsonFileReader.ReadJsonFile(filePath);
            var id = root.SelectToken("meta.id")?.ToString() ?? string.Empty;
            Console.WriteLine($"meta.id = {id}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read JSON: {ex.Message}");
        }
    }
}
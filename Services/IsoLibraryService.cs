using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ventoy_Builder.Models;

namespace Ventoy_Builder.Services
{
    public class IsoLibraryService
    {
        private readonly string _libraryPath =
            Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                "iso_library.json");

        public List<IsoLibraryItem> LoadLibrary()
        {
            if (!File.Exists(_libraryPath))
                return new List<IsoLibraryItem>();

            string json = File.ReadAllText(_libraryPath);

            return JsonSerializer.Deserialize<List<IsoLibraryItem>>(json)
                   ?? new List<IsoLibraryItem>();
        }

        public void SaveLibrary(List<IsoLibraryItem> items)
        {
            string json = JsonSerializer.Serialize(
                items,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(_libraryPath, json);
        }
    }
}
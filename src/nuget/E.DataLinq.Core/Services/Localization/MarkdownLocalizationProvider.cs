using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace E.DataLinq.Core.Services.Localization
{
    public class MarkdownLocalizationProvider
    {
        private readonly Dictionary<string, MarkdownLocalizer> _localizers = new();

        public MarkdownLocalizationProvider(string resourceDirectory)
        {
            foreach (var file in Directory.GetFiles(resourceDirectory, "*.md"))
            {
                var language = Path.GetFileNameWithoutExtension(file); // "de", "en"
                var translations = ParseMarkdownFile(file);
                _localizers[language] = new MarkdownLocalizer(translations);
            }
        }

        public MarkdownLocalizer GetLocalizer(string language)
        {
            if (_localizers.TryGetValue(language, out var localizer))
                return localizer;

            throw new ArgumentException(
                $"Language '{language}' not found. Available: {string.Join(", ", _localizers.Keys)}");
        }

        public IEnumerable<string> AvailableLanguages => _localizers.Keys;

        private static Dictionary<string, string> ParseMarkdownFile(string filePath)
        {
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                var trimmed = line.Trim();

                // Match lines like "#key: value"
                if (!trimmed.StartsWith('#') || !trimmed.Contains(':'))
                    continue;

                var colonIndex = trimmed.IndexOf(':');
                var key = trimmed[1..colonIndex].Trim();   // Remove '#' prefix
                var value = trimmed[(colonIndex + 1)..].Trim();

                if (!string.IsNullOrEmpty(key))
                    translations[key] = value;
            }

            return translations;
        }
    }
}

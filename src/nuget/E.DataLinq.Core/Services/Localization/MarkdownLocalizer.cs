using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Services.Localization
{
    public class MarkdownLocalizer
    {
        private readonly Dictionary<string, string> _translations;

        public MarkdownLocalizer(Dictionary<string, string> translations)
        {
            _translations = translations;
        }

        public Dictionary<string, string> All => _translations;

        public string this[string key] =>
            _translations.TryGetValue(key, out var value) ? value : $"[{key}]";

        public string Get(string key, string? fallback = null) =>
            _translations.TryGetValue(key, out var value) ? value : fallback ?? $"[{key}]";
    }
}

using System.Collections.Generic;

namespace SpaceSim.UI.Localization
{
    /// <summary>
    /// Temporary centralized string provider for UI labels.
    /// All user-facing strings are routed through this class
    /// so they can be replaced with a proper localization system later.
    /// Current language: Russian.
    /// </summary>
    public static class UIStrings
    {
        private static readonly Dictionary<string, string> _strings = new Dictionary<string, string>
        {
            // Object list panel.
            ["panel.object_list.title"] = "\u041e\u0431\u044a\u0435\u043a\u0442\u044b",

            // Details panel.
            ["panel.details.title"] = "\u0421\u0432\u043e\u0439\u0441\u0442\u0432\u0430",
            ["panel.details.name"] = "\u041d\u0430\u0437\u0432\u0430\u043d\u0438\u0435",
            ["panel.details.type"] = "\u0422\u0438\u043f",
            ["panel.details.radius"] = "\u0420\u0430\u0434\u0438\u0443\u0441",
            ["panel.details.parent"] = "\u0420\u043e\u0434\u0438\u0442\u0435\u043b\u044c",
            ["panel.details.none"] = "\u2014",
            ["panel.details.no_selection"] = "\u041d\u0438\u0447\u0435\u0433\u043e \u043d\u0435 \u0432\u044b\u0431\u0440\u0430\u043d\u043e",

            // Body type names.
            ["bodytype.Star"] = "\u0417\u0432\u0435\u0437\u0434\u0430",
            ["bodytype.Planet"] = "\u041f\u043b\u0430\u043d\u0435\u0442\u0430",
            ["bodytype.Moon"] = "\u0421\u043f\u0443\u0442\u043d\u0438\u043a",
            ["bodytype.Asteroid"] = "\u0410\u0441\u0442\u0435\u0440\u043e\u0438\u0434",
            ["bodytype.Station"] = "\u0421\u0442\u0430\u043d\u0446\u0438\u044f",
            ["bodytype.Ship"] = "\u041a\u043e\u0440\u0430\u0431\u043b\u044c",
            ["bodytype.SurfaceSite"] = "\u041d\u0430\u0437\u0435\u043c\u043d\u044b\u0439 \u043e\u0431\u044a\u0435\u043a\u0442",

            // Time controls.
            ["time.pause"] = "\u041f\u0430\u0443\u0437\u0430",
            ["time.resume"] = "\u041f\u0440\u043e\u0434\u043e\u043b\u0436\u0438\u0442\u044c",
            ["time.status.paused"] = "\u041f\u0430\u0443\u0437\u0430"
        };

        /// <summary>
        /// Get a localized string by key. Returns key itself if not found.
        /// </summary>
        public static string Get(string key)
        {
            return _strings.TryGetValue(key, out var value) ? value : key;
        }

        /// <summary>
        /// Get a localized body type name.
        /// </summary>
        public static string GetBodyTypeName(string bodyType)
        {
            return Get($"bodytype.{bodyType}");
        }
    }
}

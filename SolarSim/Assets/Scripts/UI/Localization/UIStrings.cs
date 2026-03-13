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
            ["panel.details.role"] = "\u0420\u043e\u043b\u044c",
            ["panel.details.ship_class"] = "\u041a\u043b\u0430\u0441\u0441",
            ["panel.details.state"] = "\u0421\u043e\u0441\u0442\u043e\u044f\u043d\u0438\u0435",
            ["panel.details.destination"] = "\u041a\u0443\u0440\u0441",
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

            // Ship role names.
            ["shiprole.Player"] = "\u0418\u0433\u0440\u043e\u043a",
            ["shiprole.Trader"] = "\u0422\u043e\u0440\u0433\u043e\u0432\u0435\u0446",
            ["shiprole.Patrol"] = "\u041f\u0430\u0442\u0440\u0443\u043b\u044c",
            ["shiprole.Civilian"] = "\u0413\u0440\u0430\u0436\u0434\u0430\u043d\u0441\u043a\u0438\u0439",

            // Ship state names.
            ["shipstate.Idle"] = "\u041e\u0436\u0438\u0434\u0430\u043d\u0438\u0435",
            ["shipstate.Orbiting"] = "\u041d\u0430 \u043e\u0440\u0431\u0438\u0442\u0435",
            ["shipstate.Travelling"] = "\u0412 \u043f\u043e\u043b\u0451\u0442\u0435",
            ["shipstate.Arrived"] = "\u041f\u0440\u0438\u0431\u044b\u043b",

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

        /// <summary>
        /// Get a localized ship role name.
        /// </summary>
        public static string GetShipRoleName(string role)
        {
            return Get($"shiprole.{role}");
        }

        /// <summary>
        /// Get a localized ship state name.
        /// </summary>
        public static string GetShipStateName(string state)
        {
            return Get($"shipstate.{state}");
        }
    }
}

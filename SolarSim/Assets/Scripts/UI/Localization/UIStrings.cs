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
            ["panel.object_list.ships"] = "\u041a\u043e\u0440\u0430\u0431\u043b\u0438",
            ["panel.object_list.collapse_all"] = "\u25B6\u25B6",
            ["panel.object_list.expand_all"] = "\u25BC\u25BC",

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
            ["panel.details.soi_body"] = "\u0417\u043e\u043d\u0430 \u0432\u043b.",
            ["panel.details.none"] = "\u2014",
            ["panel.details.no_selection"] = "\u041d\u0438\u0447\u0435\u0433\u043e \u043d\u0435 \u0432\u044b\u0431\u0440\u0430\u043d\u043e",
            ["panel.details.station_kind"] = "\u0422\u0438\u043f \u0441\u0442.",
            ["panel.details.attachment"] = "\u041a\u0440\u0435\u043f\u043b\u0435\u043d\u0438\u0435",
            ["panel.details.soi_radius"] = "\u0420\u0430\u0434\u0438\u0443\u0441 SOI",
            ["panel.details.more_btn"] = "\u0414\u0435\u0442\u0430\u043b\u044c\u043d\u0435\u0435",

            // Docking details.
            ["panel.details.docked_at"] = "\u041f\u0440\u0438\u0441\u0442\u044b\u043a.",
            ["panel.details.docking_port"] = "\u041f\u043e\u0440\u0442",
            ["panel.details.docking_ports"] = "\u041f\u043e\u0440\u0442\u044b",
            ["panel.details.ports_occupied"] = "\u0417\u0430\u043d\u044f\u0442\u043e",

            // Economy / Cargo details.
            ["panel.details.cargo"] = "\u0413\u0440\u0443\u0437",
            ["panel.details.cargo_empty"] = "\u041f\u0443\u0441\u0442\u043e",
            ["panel.details.storage"] = "\u0421\u043a\u043b\u0430\u0434",
            ["panel.details.storage_empty"] = "\u041f\u0443\u0441\u0442\u043e",

            // Production details.
            ["panel.details.production"] = "\u041f\u0440\u043e\u0438\u0437\u0432.",
            ["panel.details.production_none"] = "\u041d\u0435\u0442",
            ["panel.details.production_stalled"] = "\u0421\u0442\u043e\u043f",
            ["panel.details.production_input"] = "\u0412\u0445\u043e\u0434",
            ["panel.details.production_output"] = "\u0412\u044b\u0445\u043e\u0434",
            ["panel.details.production_cycle"] = "\u0426\u0438\u043a\u043b",
            ["panel.details.production_need"] = "\u041d\u0443\u0436\u043d\u043e",

            // Demand / Surplus details.
            ["panel.details.demand"] = "\u0421\u043f\u0440\u043e\u0441",
            ["panel.details.surplus"] = "\u0418\u0437\u0431\u044b\u0442\u043e\u043a",
            ["panel.details.demand_none"] = "\u041d\u0435\u0442",
            ["panel.details.surplus_none"] = "\u041d\u0435\u0442",
            ["panel.details.trade_job"] = "\u0417\u0430\u0434\u0430\u043d\u0438\u0435",
            ["panel.details.trade_job_none"] = "\u041d\u0435\u0442",

            // Demand level labels.
            ["demand.high"] = "\u0412\u044b\u0441\u043e\u043a\u0438\u0439",
            ["demand.medium"] = "\u0421\u0440\u0435\u0434\u043d\u0438\u0439",
            ["demand.low"] = "\u041d\u0438\u0437\u043a\u0438\u0439",

            // Resource type names (full).
            ["resource.Food"] = "\u041f\u0440\u043e\u0434\u043e\u0432\u043e\u043b\u044c\u0441\u0442\u0432\u0438\u0435",
            ["resource.Metals"] = "\u041c\u0435\u0442\u0430\u043b\u043b\u044b",
            ["resource.Fuel"] = "\u0422\u043e\u043f\u043b\u0438\u0432\u043e",
            ["resource.Electronics"] = "\u042d\u043b\u0435\u043a\u0442\u0440\u043e\u043d\u0438\u043a\u0430",

            // Resource type names (short).
            ["resource_short.Food"] = "\u0415\u0434\u0430",
            ["resource_short.Metals"] = "\u041c\u0435\u0442.",
            ["resource_short.Fuel"] = "\u0422\u043e\u043f\u043b.",
            ["resource_short.Electronics"] = "\u042d\u043b\u0435\u043a.",

            // Body type names.
            ["bodytype.Star"] = "\u0417\u0432\u0435\u0437\u0434\u0430",
            ["bodytype.Planet"] = "\u041f\u043b\u0430\u043d\u0435\u0442\u0430",
            ["bodytype.Moon"] = "\u0421\u043f\u0443\u0442\u043d\u0438\u043a",
            ["bodytype.Asteroid"] = "\u0410\u0441\u0442\u0435\u0440\u043e\u0438\u0434",
            ["bodytype.Station"] = "\u0421\u0442\u0430\u043d\u0446\u0438\u044f",
            ["bodytype.Ship"] = "\u041a\u043e\u0440\u0430\u0431\u043b\u044c",
            ["bodytype.SurfaceSite"] = "\u041d\u0430\u0437\u0435\u043c\u043d\u044b\u0439 \u043e\u0431\u044a\u0435\u043a\u0442",

            // Station kind names.
            ["stationkind.Orbital"] = "\u041e\u0440\u0431\u0438\u0442\u0430\u043b\u044c\u043d\u0430\u044f",
            ["stationkind.Surface"] = "\u041d\u0430\u0437\u0435\u043c\u043d\u0430\u044f",

            // Attachment mode names.
            ["attachment.None"] = "\u041d\u0435\u0442",
            ["attachment.Orbit"] = "\u041e\u0440\u0431\u0438\u0442\u0430",
            ["attachment.Surface"] = "\u041f\u043e\u0432\u0435\u0440\u0445\u043d\u043e\u0441\u0442\u044c",
            ["attachment.LocalSpace"] = "\u041b\u043e\u043a\u0430\u043b\u044c\u043d\u043e\u0435",

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
            ["shipstate.ApproachingStation"] = "\u0421\u0431\u043b\u0438\u0436\u0435\u043d\u0438\u0435",
            ["shipstate.Docking"] = "\u0421\u0442\u044b\u043a\u043e\u0432\u043a\u0430",
            ["shipstate.Docked"] = "\u041f\u0440\u0438\u0441\u0442\u044b\u043a\u043e\u0432\u0430\u043d",

            // Trade job phase names.
            ["tradephase.GoingToSource"] = "\u041a \u0438\u0441\u0442\u043e\u0447\u043d\u0438\u043a\u0443",
            ["tradephase.LoadingAtSource"] = "\u0417\u0430\u0433\u0440\u0443\u0437\u043a\u0430",
            ["tradephase.GoingToDestination"] = "\u041a \u043f\u043e\u043b\u0443\u0447\u0430\u0442\u0435\u043b\u044e",
            ["tradephase.UnloadingAtDestination"] = "\u0420\u0430\u0437\u0433\u0440\u0443\u0437\u043a\u0430",

            // Time controls.
            ["time.pause"] = "\u041f\u0430\u0443\u0437\u0430",
            ["time.resume"] = "\u041f\u0440\u043e\u0434\u043e\u043b\u0436\u0438\u0442\u044c",
            ["time.status.paused"] = "\u041f\u0430\u0443\u0437\u0430",

            // Modal tabs.
            ["modal.tab.details"] = "\u0414\u0435\u0442\u0430\u043b\u0438",
            ["modal.tab.market"] = "\u0420\u044b\u043d\u043e\u043a",
            ["modal.tab.missions"] = "\u0417\u0430\u0434\u0430\u043d\u0438\u044f",
            ["modal.tab.modules"] = "\u041c\u043e\u0434\u0443\u043b\u0438",
            ["modal.tab.hangar"] = "\u0410\u043d\u0433\u0430\u0440",

            // Modal section headers.
            ["modal.section.identity"] = "\u0418\u0434\u0435\u043d\u0442\u0438\u0444\u0438\u043a\u0430\u0446\u0438\u044f",
            ["modal.section.physical"] = "\u0424\u0438\u0437\u0438\u0447\u0435\u0441\u043a\u0438\u0435 \u043f\u0430\u0440\u0430\u043c\u0435\u0442\u0440\u044b",
            ["modal.section.orbit"] = "\u041e\u0440\u0431\u0438\u0442\u0430",
            ["modal.section.docking"] = "\u0421\u0442\u044b\u043a\u043e\u0432\u043a\u0430",
            ["modal.section.economy"] = "\u042d\u043a\u043e\u043d\u043e\u043c\u0438\u043a\u0430",
            ["modal.section.production"] = "\u041f\u0440\u043e\u0438\u0437\u0432\u043e\u0434\u0441\u0442\u0432\u043e",
            ["modal.section.demand"] = "\u0421\u043f\u0440\u043e\u0441 / \u0418\u0437\u0431\u044b\u0442\u043e\u043a",

            // Modal detail labels.
            ["modal.details.orbit_radius"] = "\u0420\u0430\u0434\u0438\u0443\u0441 \u043e\u0440\u0431\u0438\u0442\u044b",
            ["modal.details.orbit_period"] = "\u041f\u0435\u0440\u0438\u043e\u0434",

            // Modal placeholders.
            ["modal.placeholder.market"] = "\u0420\u044b\u043d\u043e\u043a \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d",
            ["modal.placeholder.missions"] = "\u0417\u0430\u0434\u0430\u043d\u0438\u044f \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u044b",
            ["modal.placeholder.modules"] = "\u041c\u043e\u0434\u0443\u043b\u0438 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u044b",
            ["modal.placeholder.hangar"] = "\u0410\u043d\u0433\u0430\u0440 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d"
        };

        public static string Get(string key)
        {
            return _strings.TryGetValue(key, out var value) ? value : key;
        }

        public static string GetBodyTypeName(string bodyType) => Get($"bodytype.{bodyType}");
        public static string GetShipRoleName(string role) => Get($"shiprole.{role}");
        public static string GetShipStateName(string state) => Get($"shipstate.{state}");
        public static string GetStationKindName(string kind) => Get($"stationkind.{kind}");
        public static string GetAttachmentModeName(string mode) => Get($"attachment.{mode}");
        public static string GetResourceName(string resourceType) => Get($"resource.{resourceType}");

        /// <summary>
        /// Get short (abbreviated) resource name for compact UI display.
        /// </summary>
        public static string GetResourceShortName(string resourceType) => Get($"resource_short.{resourceType}");

        /// <summary>
        /// Get localized trade job phase name.
        /// </summary>
        public static string GetTradeJobPhaseName(string phase) => Get($"tradephase.{phase}");

        /// <summary>
        /// Get a demand level label based on score value.
        /// </summary>
        public static string GetDemandLevelLabel(double score)
        {
            if (score >= 60.0) return Get("demand.high");
            if (score >= 25.0) return Get("demand.medium");
            return Get("demand.low");
        }
    }
}

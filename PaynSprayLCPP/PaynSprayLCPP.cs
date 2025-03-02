using GTA;
using GTA.Math;
using GTA.UI;


// Main Class
public class Main : Script
{
    // Define a static readonly dictionary
    public static readonly Dictionary<string, object> metadata = new Dictionary<string, object>
    {
        {"name",      "Pay n' Spray LCPP"},
        {"developer", "votrinhan88"},
        {"version",   "1.1"},
        {"iniPath",   @"scripts\PaynSprayLCPP.ini"}
    };
    private static readonly Dictionary<string, Dictionary<string, object>> defaultSettingsDict = new Dictionary<string, Dictionary<string, object>>
    {
        {
            "SETTINGS", new Dictionary<string, object>
            {
                {"verbose",  Verbosity.WARNING},
                {"Interval", 1000},
            }
        },
        {
            "PARAMETERS", new Dictionary<string, object>
            {
                {"distance",       10.0f},
                {"costMultiplier", 1.0f},
            }
        },
        {
            "BASE_COST", new Dictionary<string, object>
            {
                {"Boats",           30000},
                {"Commercial",       4000},
                {"Compacts",         1500},
                {"Coupes",           3000},
                {"Cycles",            200},
                {"Emergency",       10000},
                {"Helicopters",     50000},
                {"Industrial",       5000},
                {"Military",        30000},
                {"Motorcycles",      1500},
                {"Muscle",           3000},
                {"OffRoad",          2500},
                {"OpenWheel",       20000},
                {"Planes",          50000},
                {"Sedans",           2000},
                {"Service",          2500},
                {"Sports",           5000},
                {"SportsClassics",   4000},
                {"Super",           10000},
                {"SUVs",             3000},
                {"Trailers",        10000},
                {"Trains",          30000},
                {"Utility",          3000},
                {"Vans",             2000},
            }
        }
    };
    private static readonly Vector3[] locationsShops = new Vector3[5] {
        new Vector3(4880.41f, -1716.87f, 19.84f),
        new Vector3(4674.51f, -2889.68f,  6.02f),
        new Vector3(6246.37f, -3555.75f, 20.89f),
        new Vector3(4040.26f, -2078.96f, 16.57f),
        new Vector3(3887.98f, -2981.69f, 10.33f)
    };
    private static readonly string[] namesShops = new string[5] {
        "~b~Pay n' Spray~w~ at Frankfort Avenue, Northwood, Algonquin",
        "~b~Pay n' Spray~w~ at West Way, Purgatory, Algonquin",
        "~b~Pay n' Spray~w~ at Gibson Street, Outlook, Broker",
        "~b~Axel's Pay 'N' Spray~w~ at Panhandle Road, Leftwood, Alderney",
        "~b~Axel's Pay 'N' Spray~w~ at Roebuck Road & Hardtack Avenue, Port Tudor, Alderney",
    };

    private ScriptSettings settings;
    private bool isNearShop = false;
    private int idxNearestShop;

    private bool isPromptedHonk = false;
    private bool isFixed = false;

    // Core class
    public Main()
    {
        DevUtils.EnsureSettingsFile(
            (string)metadata["iniPath"],
            defaultSettingsDict,
            (int)defaultSettingsDict["SETTINGS"]["verbose"]
        );
        settings = DevUtils.LoadSettings(
            (string)metadata["iniPath"],
            defaultSettingsDict,
            (int)defaultSettingsDict["SETTINGS"]["verbose"]
        );
        settings.Save();
        if (settings.GetValue("SETTINGS", "verbose", 0) >= Verbosity.INFO) {
            Notification.PostTicker($"~b~{metadata["name"]} ~g~{metadata["version"]}~w~ has been loaded.", true);
        }

        Tick += OnTick;
        Interval = settings.GetValue("SETTINGS", "Interval", (int)defaultSettingsDict["SETTINGS"]["Interval"]);
    }

    // Code is ran every new frame in-game.
    private void OnTick(object sender, EventArgs e)
    {
        // Get the player's current vehcile
        Vehicle playerVehicle = Game.Player.Character.CurrentVehicle;
        if (playerVehicle != null)
        {
            float distance;
            (idxNearestShop, distance) = getClosestShop();
            isNearShop = distance < settings.GetValue("PARAMETERS", "distance", (float)defaultSettingsDict["PARAMETERS"]["distance"]);
            
            if (isNearShop == true) {
                if (settings.GetValue("SETTINGS", "verbose", 0) >= Verbosity.DEBUG) {Notification.PostTicker($"Pay n' Spray {idxNearestShop} nearby.", false);}
                
                if (isFixed == false) {
                    var cost = ComputeCost(playerVehicle);
                    
                    if (isPromptedHonk == false) {
                        Notification.PostTicker($"Welcome to {namesShops[idxNearestShop]}. Hold ~y~Honk~w~ to pay n' spray for ~g~${cost}~W~.", true);
                        isPromptedHonk = true;
                    }
                    
                    if (Game.IsControlPressed(GTA.Control.VehicleHorn)) {
                        ServiceFixVehicle(playerVehicle, cost);
                    }
                }
            }
            else {
                isPromptedHonk = false;
                isFixed = false;
            }
        }
    }
    
    private (int, float) getClosestShop()
    {
        int closestIndex = 0;
        float closestDistance = 9999999.0f;
        foreach (Vector3 location in locationsShops)
        {
            float distance = World.GetDistance(Game.Player.Character.Position, location);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = Array.IndexOf(locationsShops, location);
            }
        }
        return (closestIndex, closestDistance);
    }

    private void ServiceFixVehicle(Vehicle vehicle, int cost) {
        int money = Game.Player.Money;

        if (money >= cost) {
            vehicle.Repair();
            vehicle.Wash();
            Game.Player.Money -= cost;
            isFixed = true;
            Notification.PostTicker($"Sprayed n' payed ~g~${cost}~W~.", true);
        } else {
            Notification.PostTicker($"You don't have enough ~g~${cost}~W~.", true);
        }
    }

    private int ComputeCost(Vehicle vehicle) {
        var vehicleClass = vehicle.ClassType.ToString();
        float baseCost = (float)settings.GetValue("BASE_COST", vehicleClass, (int)defaultSettingsDict["BASE_COST"][vehicleClass]);
        float healthFactor = 1 - (vehicle.HealthFloat/vehicle.MaxHealthFloat);
        float costMultiplier = (float)settings.GetValue("PARAMETERS", "costMultiplier", (float)defaultSettingsDict["PARAMETERS"]["costMultiplier"]);

        int cost = (int)(baseCost*healthFactor*costMultiplier);
        if (settings.GetValue("SETTINGS", "verbose", 0) >= Verbosity.DEBUG) {
            Notification.PostTicker($"costMultiplier: {costMultiplier}, healthFactor: {healthFactor}, baseCost: {baseCost} -> cost {cost}", true);
        }
        return cost;
    }
}
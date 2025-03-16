using System.Drawing;
using GTA;
using GTA.Math;
using GTA.UI;

public class PaynSprayLCPP : Script
{
    // CONSTRUCTOR /////////////////////////////////////////////////////////////
    public static readonly Dictionary<string, object> metadata = new Dictionary<string, object>
    {
        {"name",      "Pay n' Spray LCPP"},
        {"developer", "votrinhan88"},
        {"version",   "1.2"},
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
                {"distance",       15.0f},
                {"costPaint",      0.1f},
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
    private Dictionary<string, Dictionary<string, object>> settings = new Dictionary<string, Dictionary<string, object>>();

    public PaynSprayLCPP()
    {
        DevUtils.EnsureSettingsFile(
            (string)metadata["iniPath"],
            defaultSettingsDict,
            (int)defaultSettingsDict["SETTINGS"]["verbose"]
        );
        ScriptSettings loadedsettings = DevUtils.LoadSettings(
            (string)metadata["iniPath"],
            defaultSettingsDict,
            (int)defaultSettingsDict["SETTINGS"]["verbose"]
        );
        loadedsettings.Save();
        InitSettings(loadedsettings);

        Tick += OnTick;
        Interval = (int)this.settings["SETTINGS"]["Interval"];
    }

    private Dictionary<string, Dictionary<string, object>> InitSettings(ScriptSettings scriptSettings)
    {
        foreach (string sectionName in scriptSettings.GetAllSectionNames())
        {
            this.settings.Add(sectionName, new Dictionary<string, object>());
            foreach (string keyName in scriptSettings.GetAllKeyNames(sectionName))
            {   
                Type type = defaultSettingsDict[sectionName][keyName].GetType();
                this.settings[sectionName].Add(keyName, Convert.ChangeType(scriptSettings.GetValue(sectionName, keyName, defaultSettingsDict[sectionName][keyName]), type));
            }
        }

        if ((int)this.settings["SETTINGS"]["verbose"] >= Verbosity.INFO)
        {
            Notification.PostTicker($"~b~{metadata["name"]} ~g~{metadata["version"]}~w~ has been loaded.", true);
        }
        return settings;
    }


    // VARIABLES ///////////////////////////////////////////////////////////////
    private static Ped player => Game.Player.Character;
    private ModState modState = ModState.None;
    private int idxClosestShop = -1;
    private int cost = 0;
    private FeedPost? feedPostCostUpdated;
    private FeedPost? feedPostNoMoney;
    private enum ModState : int
    {
        Unknown = -1,
        None = 0,
        Entered = 1,
        ReadyForService = 2,
        Left = 3,
    }

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

    private void OnTick(object sender, EventArgs e)
    {
        ShowDebugInfo();

        switch (modState)
        {
            case ModState.Unknown:
                this.modState = ResetMod();
                break;
            case ModState.None:
                this.modState = CheckNearbyShop();
                break;
            case ModState.Entered:
                this.modState = PromptPaynSpray();
                break;
            case ModState.ReadyForService:
                this.modState = ServicePaynSpray();
                break;
            case ModState.Left:
                this.modState = ResetMod();
                break;
        }
    }
    
    private void ShowDebugInfo()
    {
        string subtitle = $"modState: {this.modState}";
        GTA.UI.Screen.ShowSubtitle(subtitle, (int)this.settings["SETTINGS"]["Interval"]);
    }

    private ModState ResetMod()
    {
        this.idxClosestShop = -1;
        this.cost = 0;
        this.feedPostCostUpdated = null;
        this.feedPostNoMoney = null;
        return ModState.None;
    }

    private ModState CheckNearbyShop()
    {
        // Check: modState == ModState.None
        if (this.modState != ModState.None) { return ModState.None; }

        // Check: Player is in a vehicle 
        Vehicle vehicle = player.CurrentVehicle;
        if (vehicle == null) { return ModState.None; }

        float distance;
        (this.idxClosestShop, distance) = GetClosestShop();
        if (distance > (float)this.settings["PARAMETERS"]["distance"])
        {
            return ModState.None;
        }

        return ModState.Entered;
    }

    private ModState PromptPaynSpray()
    {
        // Check: modState == ModState.Nearby
        if (this.modState != ModState.Entered) { return ModState.None; }

        // Check: Player is in a vehicle
        Vehicle vehicle = player.CurrentVehicle;
        if (vehicle == null) { return ModState.None; }

        (this.cost, bool onlyPaint) = ComputeCost(vehicle);
        Notification.PostTicker(
            (
                $"Welcome to {namesShops[this.idxClosestShop]}. "
                + $"Hold ~y~Honk~w~ to pay n' spray for ~g~${this.cost}~W~."
            ),
            true
        );
        return ModState.ReadyForService;
    }

    private ModState ServicePaynSpray() {
        // Check: modState == ModState.Nearby
        if (this.modState != ModState.ReadyForService) { return ModState.None; }
        
        // Check: Player is in a vehicle
        Vehicle vehicle = player.CurrentVehicle;
        if (vehicle == null) { return ModState.None; }

        // Check:
        if ((player.Position - locationsShops[this.idxClosestShop]).Length() > (float)this.settings["PARAMETERS"]["distance"])
        {
            Notification.PostTicker($"Thank you for visiting {namesShops[this.idxClosestShop]}", true);
            return ModState.Left;
        }

        // Check: Cost can be updated
        (int costNew, bool onlyPaint) = ComputeCost(vehicle);
        if (costNew != this.cost)
        {
            this.cost = costNew;
            if (this.feedPostCostUpdated != null)
            {
                this.feedPostCostUpdated.Delete();
            }

            this.feedPostCostUpdated = Notification.PostTicker($"Hold ~y~Honk~w~ to pay n' respray for ~g~${this.cost}~W~.", true);
        }

        // Check: Horn pressed for any action
        if (!Game.IsControlPressed(Control.VehicleHorn)) { return ModState.ReadyForService; }

        // Check: Player has enough money
        int money = Game.Player.Money;
        if (money < this.cost)
        {
            if (this.feedPostNoMoney == null)
            {
                this.feedPostNoMoney = Notification.PostTicker($"Come back with ~g~${cost}~W~.", true);
            }
            return ModState.ReadyForService;
        }

        // Fix and wash
        vehicle.Repair();
        vehicle.Wash();

        // Change color
        int colorComb = GetDifferentRandom(0, vehicle.Mods.ColorCombinationCount, vehicle.Mods.ColorCombination);
        if ((int)settings["SETTINGS"]["verbose"] >= Verbosity.DEBUG)
        {
            Notification.PostTicker($"ColorCombination: {colorComb} in [0..{vehicle.Mods.ColorCombinationCount+1})", true);
        }
        vehicle.Mods.ColorCombination = colorComb;
        
        // Smoke effect
        // List of particle effects: https://gist.githubusercontent.com/alexguirre/af70f0122957f005a5c12bef2618a786/raw/899e93c5611ba58138c56873bb6f56664a776af4/Particles%2520Effects%2520Dump.txt
        Color colorSmoke = vehicle.Mods.CustomPrimaryColor;
        if ((int)settings["SETTINGS"]["verbose"] >= Verbosity.DEBUG) { Notification.PostTicker($"colorSmoke: {colorSmoke}.", true);}

        World.RemoveAllParticleEffectsInRange(vehicle.Position, 5.0f);
        ParticleEffect particleEffect = World.CreateParticleEffect(
            new ParticleEffectAsset("scr_paintnspray"),
            "scr_respray_smoke",
            vehicle.Position
        );
        // No idea why but changing smoke color doesn't work on the first try everytime
        try {
            particleEffect.Color = colorSmoke;
        } catch (Exception e) {
            if ((int)settings["SETTINGS"]["verbose"] >= Verbosity.DEBUG) { Notification.PostTicker($"particleEffect caught {e}.", true);}
        }

        // Sound effect
        // List of audio/sound names: https://github.com/DurtyFree/gta-v-data-dumps/blob/master/soundNames.json
        if (!onlyPaint) {
            Audio.PlaySoundFromEntityAndForget(vehicle, "Engine_Rev", "Lowrider_Super_Mod_Garage_Sounds");
        }

        Game.Player.Money -= this.cost;
        Notification.PostTicker($"Sprayed n' payed ~g~${this.cost}~W~.", true);
        return ModState.ReadyForService;
    }


    private (int, float) GetClosestShop()
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
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


    private (int, bool) ComputeCost(Vehicle vehicle) {
        string vehicleClass = vehicle.ClassType.ToString();
        int baseCost = (int)this.settings["BASE_COST"][vehicleClass];
        float healthFactor = 1 - (vehicle.HealthFloat/vehicle.MaxHealthFloat);
        float costMultiplier = (float)this.settings["PARAMETERS"]["costMultiplier"];

        int costRepair = (int)Convert.ToInt32(baseCost*healthFactor*(float)this.settings["PARAMETERS"]["costMultiplier"]);
        int costPaint = (int)Convert.ToInt32(baseCost*(float)this.settings["PARAMETERS"]["costPaint"]);

        int cost = costRepair + costPaint;
        if ((int)this.settings["SETTINGS"]["verbose"] >= Verbosity.DEBUG)
        {
            Notification.PostTicker(
                (
                    $"healthFactor: {(float)Math.Round(healthFactor, 2)}, "
                    + $"baseCost: {baseCost}, "
                    + $"costMultiplier: {(float)Math.Round(costMultiplier, 2)}, "
                    + $"-> repair {costRepair} + paint {costPaint} = {cost}"
                ),
                true
            );
        }

        bool onlyPaint = (costRepair == 0);
        return (cost, onlyPaint);
    }


    private static int GetDifferentRandom(int min, int max, int current)
    {
        int newValue;
        do
        {
            newValue = new Random().Next(min, max+1);
        }
        while (newValue == current);

        return newValue;
    }
}
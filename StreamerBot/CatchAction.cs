/*
-------------------------------------------------------

PokéHunter SB
Version 1.0.0

Created by OleKoderNo
GitHub: https://github.com/OleKoderNo
Twitch: https://twitch.tv/KapteinOle

A persistent Pokémon collection system for Streamer.bot.

-------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

public class CPHInline
{
    // ---------------- EASY CONFIGURATION ----------------
    // Change these values to rebalance your own installation.
    private const int ShinyOdds = 250;

    private const string DataFolderName = "PokemonCatchData";
    private const string PokemonFileName = "pokemon.json";
    private const string CollectionFileName = "collections.json";
    private const string MutexName = @"Local\StreamerBotPokemonCollection";
    private static readonly Random Rng = new Random();
    private static readonly object RngLock = new object();

    private static readonly string[] PokemonTypes =
    {
        "normal", "fire", "water", "electric", "grass", "ice",
        "fighting", "poison", "ground", "flying", "psychic",
        "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy"
    };

    private static readonly List<BstTier> BstTiers = new List<BstTier>
    {
        new BstTier(0, 349, 35),
        new BstTier(350, 399, 25),
        new BstTier(400, 449, 18),
        new BstTier(450, 499, 12),
        new BstTier(500, 549, 7),
        new BstTier(550, 9999, 3)
    };

    private static readonly List<RarityRoll> RarityGroups = new List<RarityRoll>
    {
        new RarityRoll("common", 70),
        new RarityRoll("rare", 25),
        new RarityRoll("ultra-rare", 5)
    };

    public bool Execute()
    {
        string userId;
        string userName;

        CPH.TryGetArg("userId", out userId);
        CPH.TryGetArg("userName", out userName);

        if (string.IsNullOrWhiteSpace(userId))
        {
            CPH.LogError("Pokémon catch failed: no userId was supplied.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = "UnknownViewer";
        }

        try
        {
            using (Mutex dataMutex = new Mutex(false, MutexName))
            {
                bool lockTaken = false;

                try
                {
                    lockTaken = dataMutex.WaitOne(TimeSpan.FromSeconds(15));

                    if (!lockTaken)
                    {
                        CPH.LogError("Pokémon catch failed: timed out waiting for the data file.");
                        CPH.SendMessage("The Pokémon system is busy. Please try again.", true);
                        return false;
                    }

                    // Creates collections.json automatically on the first run.
                    EnsureCollectionDatabaseExists();

                    PokemonDatabase pokemonDatabase = LoadPokemonDatabase();

                    if (pokemonDatabase.Pokemon == null || pokemonDatabase.Pokemon.Count == 0)
                    {
                        throw new Exception("pokemon.json contains no Pokémon.");
                    }

                    CollectionDatabase collectionDatabase = LoadCollectionDatabase();
                    ViewerCollection viewer = GetOrCreateViewer(collectionDatabase, userId, userName);

                    int totalAttempts = 1 + Math.Max(0, viewer.LossStreak);
                    EncounterResult lastEncounter = null;

                    for (int attempt = 1; attempt <= totalAttempts; attempt++)
                    {
                        EncounterResult encounter = GenerateEncounter(pokemonDatabase.Pokemon);
                        lastEncounter = encounter;

                        string collectionKey = BuildCollectionKey(encounter.Pokemon.Key, encounter.IsShiny);

                        if (viewer.Catches.ContainsKey(collectionKey))
                        {
                            continue;
                        }

                        viewer.Catches[collectionKey] = new CatchRecord
                        {
                            PokemonKey = encounter.Pokemon.Key,
                            PokemonName = encounter.Pokemon.Name,
                            Shiny = encounter.IsShiny,
                            CaughtAtUtc = DateTime.UtcNow
                        };

                        viewer.UserName = userName;
                        viewer.LossStreak = 0;
                        viewer.LastUpdatedUtc = DateTime.UtcNow;

                        SaveCollectionDatabase(collectionDatabase);

                        int normalCaught = 0;
                        int shinyCaught = 0;

                        foreach (CatchRecord record in viewer.Catches.Values)
                        {
                            if (record == null)
                            {
                                continue;
                            }

                            if (record.Shiny)
                            {
                                shinyCaught++;
                            }
                            else
                            {
                                normalCaught++;
                            }
                        }

                        CPH.SendMessage(
                            BuildSuccessMessage(
                                userName,
                                encounter,
                                normalCaught,
                                shinyCaught,
                                pokemonDatabase.Pokemon.Count,
                                attempt,
                                totalAttempts
                            ),
                            true
                        );

                        return true;
                    }

                    viewer.UserName = userName;
                    viewer.LossStreak++;
                    viewer.LastUpdatedUtc = DateTime.UtcNow;

                    SaveCollectionDatabase(collectionDatabase);

                    string duplicateName = lastEncounter == null
                        ? "a duplicate"
                        : (lastEncounter.IsShiny ? "shiny " : "") + lastEncounter.Pokemon.Name;

                    CPH.SendMessage(
                        userName + " only found Pokémon already owned. " +
                        "The final roll was " + duplicateName + ". " +
                        "Loss streak: " + viewer.LossStreak + ". " +
                        "The next redeem gets " + (viewer.LossStreak + 1) + " attempts.",
                        true
                    );

                    return true;
                }
                finally
                {
                    if (lockTaken)
                    {
                        dataMutex.ReleaseMutex();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            CPH.LogError("Pokémon catch error: " + ex);
            CPH.SendMessage("The Pokémon catch could not be saved. Check the Streamer.bot log.", true);
            return false;
        }
    }

    private EncounterResult GenerateEncounter(List<PokemonData> allPokemon)
    {
        bool isShiny = NextInt(ShinyOdds) == 0;

        BstTier bstTier = WeightedPick(
            BstTiers,
            delegate(BstTier tier) { return tier.Weight; }
        );

        RarityRoll rarity = WeightedPick(
            RarityGroups,
            delegate(RarityRoll group) { return group.Weight; }
        );

        string selectedType = PokemonTypes[NextInt(PokemonTypes.Length)];
        List<PokemonData> candidates = new List<PokemonData>();

        foreach (PokemonData pokemon in allPokemon)
        {
            if (pokemon.Bst >= bstTier.Minimum &&
                pokemon.Bst <= bstTier.Maximum &&
                string.Equals(pokemon.Rarity, rarity.Name, StringComparison.OrdinalIgnoreCase) &&
                HasType(pokemon, selectedType))
            {
                candidates.Add(pokemon);
            }
        }

        if (candidates.Count == 0)
        {
            foreach (PokemonData pokemon in allPokemon)
            {
                if (pokemon.Bst >= bstTier.Minimum &&
                    pokemon.Bst <= bstTier.Maximum &&
                    HasType(pokemon, selectedType))
                {
                    candidates.Add(pokemon);
                }
            }
        }

        if (candidates.Count == 0)
        {
            foreach (PokemonData pokemon in allPokemon)
            {
                if (string.Equals(pokemon.Rarity, rarity.Name, StringComparison.OrdinalIgnoreCase) &&
                    HasType(pokemon, selectedType))
                {
                    candidates.Add(pokemon);
                }
            }
        }

        if (candidates.Count == 0)
        {
            foreach (PokemonData pokemon in allPokemon)
            {
                if (HasType(pokemon, selectedType))
                {
                    candidates.Add(pokemon);
                }
            }
        }

        if (candidates.Count == 0)
        {
            candidates = allPokemon;
        }

        PokemonData selectedPokemon = WeightedPick(
            candidates,
            delegate(PokemonData pokemon)
            {
                return Math.Max(1, pokemon.SelectionWeight);
            }
        );

        return new EncounterResult
        {
            Pokemon = selectedPokemon,
            IsShiny = isShiny,
            RolledType = selectedType,
            RolledRarity = rarity.Name,
            RolledBstMinimum = bstTier.Minimum,
            RolledBstMaximum = bstTier.Maximum
        };
    }

    private bool HasType(PokemonData pokemon, string selectedType)
    {
        if (pokemon.Types == null)
        {
            return false;
        }

        foreach (string type in pokemon.Types)
        {
            if (string.Equals(type, selectedType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string BuildSuccessMessage(
        string userName,
        EncounterResult encounter,
        int normalCaught,
        int shinyCaught,
        int totalPokemon,
        int attempt,
        int totalAttempts
    )
    {
        string attemptText = totalAttempts > 1
            ? " Found on attempt " + attempt + "/" + totalAttempts + "."
            : "";

        if (encounter.IsShiny)
        {
            return "✨ " + userName + " caught a SHINY " +
                encounter.Pokemon.Name + "! " +
                normalCaught + "/" + totalPokemon + " Pokémon and " +
                shinyCaught + "/" + totalPokemon + " shinies." + attemptText;
        }

        return userName + " caught " + encounter.Pokemon.Name + "! " +
            normalCaught + "/" + totalPokemon + " Pokémon and " +
            shinyCaught + "/" + totalPokemon + " shinies." + attemptText;
    }

    private ViewerCollection GetOrCreateViewer(
        CollectionDatabase database,
        string userId,
        string userName
    )
    {
        if (database.Viewers == null)
        {
            database.Viewers = new Dictionary<string, ViewerCollection>();
        }

        ViewerCollection viewer;

        if (!database.Viewers.TryGetValue(userId, out viewer) || viewer == null)
        {
            viewer = new ViewerCollection
            {
                UserId = userId,
                UserName = userName,
                LossStreak = 0,
                Catches = new Dictionary<string, CatchRecord>(),
                LastUpdatedUtc = DateTime.UtcNow
            };

            database.Viewers[userId] = viewer;
        }

        if (viewer.Catches == null)
        {
            viewer.Catches = new Dictionary<string, CatchRecord>();
        }

        return viewer;
    }

    private void EnsureCollectionDatabaseExists()
    {
        string path = GetDataPath(CollectionFileName);

        if (File.Exists(path))
        {
            return;
        }

        CollectionDatabase emptyDatabase = new CollectionDatabase
        {
            SchemaVersion = 1,
            ProjectName = "Streamer.bot Pokémon Catch System",
            Developer = "OleKoderNo",
            TwitchChannel = "KapteinOle",
            CreatedAtUtc = DateTime.UtcNow,
            Viewers = new Dictionary<string, ViewerCollection>()
        };

        SaveCollectionDatabase(emptyDatabase);
        CPH.LogInfo("Created Pokémon collection database: " + path);
    }

    private PokemonDatabase LoadPokemonDatabase()
    {
        string path = GetDataPath(PokemonFileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Run Import-PokemonData.ps1 first. pokemon.json was not found.",
                path
            );
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        PokemonDatabase database = JsonConvert.DeserializeObject<PokemonDatabase>(json);

        if (database == null)
        {
            throw new Exception("pokemon.json is invalid.");
        }

        return database;
    }

    private CollectionDatabase LoadCollectionDatabase()
    {
        string path = GetDataPath(CollectionFileName);

        if (!File.Exists(path))
        {
            return new CollectionDatabase
            {
                SchemaVersion = 1,
                ProjectName = "Streamer.bot Pokémon Catch System",
                Developer = "OleKoderNo",
                TwitchChannel = "KapteinOle",
                CreatedAtUtc = DateTime.UtcNow,
                Viewers = new Dictionary<string, ViewerCollection>()
            };
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        CollectionDatabase database = JsonConvert.DeserializeObject<CollectionDatabase>(json);

        if (database == null)
        {
            throw new Exception("collections.json is invalid.");
        }

        if (database.Viewers == null)
        {
            database.Viewers = new Dictionary<string, ViewerCollection>();
        }

        return database;
    }

    private void SaveCollectionDatabase(CollectionDatabase database)
    {
        if (string.IsNullOrWhiteSpace(database.ProjectName))
        {
            database.ProjectName = "Streamer.bot Pokémon Catch System";
        }

        if (string.IsNullOrWhiteSpace(database.Developer))
        {
            database.Developer = "OleKoderNo";
        }

        if (string.IsNullOrWhiteSpace(database.TwitchChannel))
        {
            database.TwitchChannel = "KapteinOle";
        }

        if (database.CreatedAtUtc == default(DateTime))
        {
            database.CreatedAtUtc = DateTime.UtcNow;
        }

        string dataFolder = GetDataFolder();
        string path = Path.Combine(dataFolder, CollectionFileName);
        string temporaryPath = path + ".tmp";
        string backupFolder = Path.Combine(dataFolder, "Backups");

        Directory.CreateDirectory(dataFolder);
        Directory.CreateDirectory(backupFolder);

        string json = JsonConvert.SerializeObject(database, Formatting.Indented);
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

        if (File.Exists(path))
        {
            string backupPath = Path.Combine(
                backupFolder,
                "collections-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json"
            );

            File.Replace(temporaryPath, path, backupPath);
            PruneBackups(backupFolder, 20);
        }
        else
        {
            File.Move(temporaryPath, path);
        }
    }

    private void PruneBackups(string backupFolder, int keepCount)
    {
        FileInfo[] backups = new DirectoryInfo(backupFolder).GetFiles("collections-*.json");

        Array.Sort(
            backups,
            delegate(FileInfo left, FileInfo right)
            {
                return right.CreationTimeUtc.CompareTo(left.CreationTimeUtc);
            }
        );

        for (int index = keepCount; index < backups.Length; index++)
        {
            try
            {
                backups[index].Delete();
            }
            catch
            {
            }
        }
    }

    private string GetDataFolder()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private string GetDataPath(string fileName)
    {
        return Path.Combine(GetDataFolder(), fileName);
    }

    private string BuildCollectionKey(string pokemonKey, bool shiny)
    {
        return pokemonKey + "|" + (shiny ? "shiny" : "normal");
    }

    private int NextInt(int maximumExclusive)
    {
        lock (RngLock)
        {
            return Rng.Next(maximumExclusive);
        }
    }

    private T WeightedPick<T>(IList<T> options, Func<T, int> getWeight)
    {
        if (options == null || options.Count == 0)
        {
            throw new Exception("Cannot select from an empty list.");
        }

        int totalWeight = 0;

        foreach (T option in options)
        {
            totalWeight += Math.Max(0, getWeight(option));
        }

        if (totalWeight <= 0)
        {
            return options[NextInt(options.Count)];
        }

        int roll;

        lock (RngLock)
        {
            roll = Rng.Next(1, totalWeight + 1);
        }

        int runningTotal = 0;

        foreach (T option in options)
        {
            runningTotal += Math.Max(0, getWeight(option));

            if (roll <= runningTotal)
            {
                return option;
            }
        }

        return options[options.Count - 1];
    }
}

public class PokemonDatabase
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonProperty("generatedAtUtc")]
    public DateTime GeneratedAtUtc { get; set; }

    [JsonProperty("pokemon")]
    public List<PokemonData> Pokemon { get; set; }
}

public class PokemonData
{
    [JsonProperty("key")]
    public string Key { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("species")]
    public string Species { get; set; }

    [JsonProperty("form")]
    public string Form { get; set; }

    [JsonProperty("region")]
    public string Region { get; set; }

    [JsonProperty("pokedexNumber")]
    public int PokedexNumber { get; set; }

    [JsonProperty("bst")]
    public int Bst { get; set; }

    [JsonProperty("types")]
    public List<string> Types { get; set; }

    [JsonProperty("rarity")]
    public string Rarity { get; set; }

    [JsonProperty("selectionWeight")]
    public int SelectionWeight { get; set; }

    [JsonProperty("captureRate")]
    public int CaptureRate { get; set; }

    [JsonProperty("isLegendary")]
    public bool IsLegendary { get; set; }

    [JsonProperty("isMythical")]
    public bool IsMythical { get; set; }
}

public class CollectionDatabase
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonProperty("projectName")]
    public string ProjectName { get; set; }

    [JsonProperty("developer")]
    public string Developer { get; set; }

    [JsonProperty("twitchChannel")]
    public string TwitchChannel { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonProperty("viewers")]
    public Dictionary<string, ViewerCollection> Viewers { get; set; }
}

public class ViewerCollection
{
    [JsonProperty("userId")]
    public string UserId { get; set; }

    [JsonProperty("userName")]
    public string UserName { get; set; }

    [JsonProperty("lossStreak")]
    public int LossStreak { get; set; }

    [JsonProperty("lastUpdatedUtc")]
    public DateTime LastUpdatedUtc { get; set; }

    [JsonProperty("catches")]
    public Dictionary<string, CatchRecord> Catches { get; set; }
}

public class CatchRecord
{
    [JsonProperty("pokemonKey")]
    public string PokemonKey { get; set; }

    [JsonProperty("pokemonName")]
    public string PokemonName { get; set; }

    [JsonProperty("shiny")]
    public bool Shiny { get; set; }

    [JsonProperty("caughtAtUtc")]
    public DateTime CaughtAtUtc { get; set; }
}

public class EncounterResult
{
    public PokemonData Pokemon { get; set; }
    public bool IsShiny { get; set; }
    public string RolledType { get; set; }
    public string RolledRarity { get; set; }
    public int RolledBstMinimum { get; set; }
    public int RolledBstMaximum { get; set; }
}

public class BstTier
{
    public BstTier(int minimum, int maximum, int weight)
    {
        Minimum = minimum;
        Maximum = maximum;
        Weight = weight;
    }

    public int Minimum { get; private set; }
    public int Maximum { get; private set; }
    public int Weight { get; private set; }
}

public class RarityRoll
{
    public RarityRoll(string name, int weight)
    {
        Name = name;
        Weight = weight;
    }

    public string Name { get; private set; }
    public int Weight { get; private set; }
}

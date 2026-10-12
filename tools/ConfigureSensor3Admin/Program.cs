using System.Text;
using System.Text.Json.Nodes;
using Sensor3.Distribution;

if (args.Length != 1 || Console.IsInputRedirected)
{
    Console.Error.WriteLine("Kör interaktivt med sökvägen till IIS-webbplatsens appsettings.Production.json som argument.");
    return 1;
}
try
{
    var path = Path.GetFullPath(args[0]);
    if (!File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "Sensor3.Dashboard.dll"))) throw new InvalidOperationException("Välj Sensor3 Dashboardens publiceringskatalog.");
    var settings = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new JsonObject();
    var distribution = settings["Distribution"] as JsonObject ?? new JsonObject();
    if (distribution["AdminPasswordHash"] is JsonValue existing && existing.TryGetValue<string>(out var hash) && !string.IsNullOrWhiteSpace(hash))
        throw new InvalidOperationException("Ett administratörskonto finns redan. Verktyget ersätter inte befintliga inloggningsuppgifter.");
    Console.WriteLine("Konfigurera Sensor3-administratören. Lösenordet sparas bara som PBKDF2-hash.");
    Console.Write("Användarnamn: ");
    var name = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 64) throw new ArgumentException("Ange ett användarnamn med 1–64 tecken.");
    var password = ReadPassword("Lösenord (minst 16 tecken): ");
    if (password != ReadPassword("Upprepa lösenord: ")) throw new ArgumentException("Lösenorden skiljer sig.");
    distribution["AdminUsername"] = name;
    distribution["AdminPasswordHash"] = AdminCredentials.Hash(password);
    password = "";
    settings["Distribution"] = distribution.DeepClone();
    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
        File.WriteAllText(temporary, settings.ToJsonString(new() { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
    var config = Path.Combine(Path.GetDirectoryName(path)!, "web.config");
    if (File.Exists(config)) File.SetLastWriteTimeUtc(config, DateTime.UtcNow);
    Console.WriteLine("Klart. Öppna https://prototyp.qsys.se/devices och logga in med kontot du just angav.");
    return 0;
}
catch (Exception exception) { Console.Error.WriteLine(exception.Message); return 1; }

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException("Avbruten.");
        if (key.Key == ConsoleKey.Backspace) { if (value.Length != 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar) && value.Length < 1024) value.Append(key.KeyChar);
    }
}

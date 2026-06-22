using System.Text.Json;
using TSD.API.Remoting;
using TSD.API.Remoting.Structure;

string command = args.Length > 0 ? args[0] : "";

if (string.IsNullOrEmpty(command))
{
    Console.WriteLine(JsonSerializer.Serialize(new { error = "No command provided" }));
    return;
}

try
{
    var tsdInstances = await ApplicationFactory.GetRunningApplicationsAsync();
    if (!tsdInstances.Any())
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error = "TSD is not running" }));
        return;
    }

    var tsd = tsdInstances.First();
    var document = await tsd.GetDocumentAsync();
    if (document == null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error = "No document open in TSD" }));
        return;
    }

    var model = await document.GetModelAsync();
    if (model == null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error = "No model found" }));
        return;
    }

    if (command == "list_members")
    {
        var members = await model.GetMembersAsync(null);
        var result = members.Select(m => new
        {
            name = m.Name,
            type = InferMemberType(m.Name)
        });
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else if (command == "list_members_with_sections")
    {
        var members = await model.GetMembersAsync(null);
        var result = new List<object>();
        foreach (var m in members)
        {
            string section = "Unknown";
            try
            {
                var spans = await m.GetSpanAsync(null);
                var firstSpan = spans?.FirstOrDefault();
                if (firstSpan != null)
                {
                    section = firstSpan.
                }
            }
            catch { }
            result.Add(new
            {
                name = m.Name,
                type = InferMemberType(m.Name),
                section
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else if (command == "get_design_summary")
    {
        var members = await model.GetMembersAsync(null);
        var total = members.Count();
        var byType = members.GroupBy(m => InferMemberType(m.Name))
                            .Select(g => new { type = g.Key, count = g.Count() });
        Console.WriteLine(JsonSerializer.Serialize(new { total_members = total, by_type = byType }));
    }
    else if (command == "get_validation_errors")
    {
        var validation = await model.GetValidationDataAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { validation_summary = validation?.ToString() ?? "No validation data" }));
    }
    else
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error = $"Unknown command: {command}" }));
    }
}
catch (Exception ex)
{
    Console.WriteLine(JsonSerializer.Serialize(new { error = ex.Message }));
}

static string InferMemberType(string name)
{
    if (name.StartsWith("BR")) return "Brace";
    if (name.StartsWith("CBase")) return "Column Base Plate";
    if (name.StartsWith("C")) return "Column";
    if (name.StartsWith("B")) return "Beam";
    return "Unknown";
}
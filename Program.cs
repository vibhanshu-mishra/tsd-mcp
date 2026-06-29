using System.Reflection;
using System.Text.Json;
using TSD.API.Remoting;

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
            string? section = null;
            string? sectionType = null;
            string? materialType = null;

            try
            {
                var spans = await m.GetSpanAsync(null);
                var firstSpan = spans?.FirstOrDefault();

                if (firstSpan != null)
                {
                    var physicalSection = GetPhysicalSection(firstSpan);

                    section = GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? physicalSection?.ToString();

                    sectionType = GetPropertyValue(physicalSection, "SectionType");
                    materialType = GetPropertyValue(physicalSection, "MaterialType");
                }
            }
            catch
            {
                section = null;
            }

            result.Add(new
            {
                name = m.Name,
                type = InferMemberType(m.Name),
                section = section ?? "Unknown",
                section_type = sectionType,
                material_type = materialType
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else if (command == "get_design_summary")
    {
        var members = await model.GetMembersAsync(null);

        var total = members.Count();

        var byType = members
            .GroupBy(m => InferMemberType(m.Name))
            .Select(g => new
            {
                type = g.Key,
                count = g.Count()
            });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_members = total,
            by_type = byType
        }));
    }
    else if (command == "get_validation_errors")
    {
        var validation = await model.GetValidationDataAsync();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            validation_summary = validation?.ToString() ?? "No validation data"
        }));
    }
    else if (command == "get_model_overview")
    {
        var members = await model.GetMembersAsync(null);
        var memberDetails = new List<dynamic>();

        foreach (var m in members)
        {
            string type = InferMemberType(m.Name);
            string section = "Unknown";
            string materialType = "Unknown";

            try
            {
                var spans = await m.GetSpanAsync(null);
                var firstSpan = spans?.FirstOrDefault();

                if (firstSpan != null)
                {
                    var physicalSection = GetPhysicalSection(firstSpan);

                    section = GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    materialType = GetPropertyValue(physicalSection, "MaterialType")
                        ?? "Unknown";
                }
            }
            catch
            {
                section = "Unknown";
                materialType = "Unknown";
            }

            memberDetails.Add(new
            {
                name = m.Name,
                type,
                section,
                material_type = materialType
            });
        }

        var overview = new
        {
            total_members = memberDetails.Count,

            by_type = memberDetails
                .GroupBy(x => x.type)
                .Select(g => new
                {
                    type = g.Key,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count),

            by_section = memberDetails
                .GroupBy(x => x.section)
                .Select(g => new
                {
                    section = g.Key,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count),

            by_material = memberDetails
                .GroupBy(x => x.material_type)
                .Select(g => new
                {
                    material_type = g.Key,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count)
        };

        Console.WriteLine(JsonSerializer.Serialize(overview));
    }
    else if (command == "get_top_utilized_members")
    {
        var members = await model.GetMembersAsync(null);
        var results = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var checkResults = span.GetType()
                        .GetProperty("CheckResults")?
                        .GetValue(span);

                    var valueEnumerable = checkResults?
                        .GetType()
                        .GetProperty("Value")?
                        .GetValue(checkResults) as System.Collections.IEnumerable;

                    if (valueEnumerable == null)
                        continue;

                    foreach (var item in valueEnumerable)
                    {
                        var checkType = item.GetType()
                            .GetProperty("Key")?
                            .GetValue(item)?
                            .ToString();

                        var valueWrapper = item.GetType()
                            .GetProperty("Value")?
                            .GetValue(item);

                        var checkResult = valueWrapper?
                            .GetType()
                            .GetProperty("Value")?
                            .GetValue(valueWrapper);

                        if (checkResult == null)
                            continue;

                        var statusWrapper = checkResult.GetType()
                            .GetProperty("CheckStatus")?
                            .GetValue(checkResult);

                        var ratioWrapper = checkResult.GetType()
                            .GetProperty("UtilizationRatio")?
                            .GetValue(checkResult);

                        var status = statusWrapper?
                            .GetType()
                            .GetProperty("Value")?
                            .GetValue(statusWrapper)?
                            .ToString();

                        var ratioObj = ratioWrapper?
                            .GetType()
                            .GetProperty("Value")?
                            .GetValue(ratioWrapper);

                        double ratio = 0;

                        if (ratioObj != null)
                            ratio = Convert.ToDouble(ratioObj);

                        string section = "Unknown";

                        try
                        {
                            var physicalSection = GetPhysicalSection(span);

                            section =
                                GetPropertyValue(physicalSection, "LongName")
                                ?? GetPropertyValue(physicalSection, "ShortName")
                                ?? "Unknown";
                        }
                        catch
                        {
                        }

                        results.Add(new
                        {
                            member = m.Name,
                            type = InferMemberType(m.Name),
                            section,
                            check_type = checkType,
                            status,
                            utilization_ratio = ratio
                        });
                    }
                }
            }
            catch
            {
            }
        }

        var top = results
            .OrderByDescending(x =>
                (double)x.GetType()
                    .GetProperty("utilization_ratio")!
                    .GetValue(x)!)
            .Take(25);

        Console.WriteLine(JsonSerializer.Serialize(top));
    }
    else if (command == "get_failing_members")
    {
        var members = await model.GetMembersAsync(null);
        var results = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var checkResults = span.GetType()
                        .GetProperty("CheckResults")?
                        .GetValue(span);

                    var valueEnumerable = checkResults?
                        .GetType()
                        .GetProperty("Value")?
                        .GetValue(checkResults) as System.Collections.IEnumerable;

                    if (valueEnumerable == null)
                        continue;

                    foreach (var item in valueEnumerable)
                    {
                        var checkType = item.GetType()
                            .GetProperty("Key")?
                            .GetValue(item)?
                            .ToString();

                        var valueWrapper = item.GetType()
                            .GetProperty("Value")?
                            .GetValue(item);

                        var checkResult = valueWrapper?
                            .GetType()
                            .GetProperty("Value")?
                            .GetValue(valueWrapper);

                        if (checkResult == null)
                            continue;

                        var statusWrapper = checkResult.GetType()
                            .GetProperty("CheckStatus")?
                            .GetValue(checkResult);

                        var ratioWrapper = checkResult.GetType()
                            .GetProperty("UtilizationRatio")?
                            .GetValue(checkResult);

                        var status = statusWrapper?
                            .GetType()
                            .GetProperty("Value")?
                            .GetValue(statusWrapper)?
                            .ToString();

                        var ratioObj = ratioWrapper?
                            .GetType()
                            .GetProperty("Value")?
                            .GetValue(ratioWrapper);

                        double ratio = ratioObj != null ? Convert.ToDouble(ratioObj) : 0;

                        if (ratio < 1.0)
                            continue;

                        string section = "Unknown";

                        try
                        {
                            var physicalSection = GetPhysicalSection(span);

                            section =
                                GetPropertyValue(physicalSection, "LongName")
                                ?? GetPropertyValue(physicalSection, "ShortName")
                                ?? "Unknown";
                        }
                        catch
                        {
                        }

                        results.Add(new
                        {
                            member = m.Name,
                            type = InferMemberType(m.Name),
                            section,
                            check_type = checkType,
                            status,
                            utilization_ratio = ratio
                        });
                    }
                }
            }
            catch
            {
            }
        }

        var failing = results
            .OrderByDescending(x =>
                (double)x.GetType()
                    .GetProperty("utilization_ratio")!
                    .GetValue(x)!);

        Console.WriteLine(JsonSerializer.Serialize(failing));
    }
    else if (command == "get_members_near_limit")
    {
        var members = await model.GetMembersAsync(null);
        var results = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var checkResults = span.GetType().GetProperty("CheckResults")?.GetValue(span);
                    var valueEnumerable = checkResults?.GetType().GetProperty("Value")?.GetValue(checkResults) as System.Collections.IEnumerable;

                    if (valueEnumerable == null)
                        continue;

                    foreach (var item in valueEnumerable)
                    {
                        var checkType = item.GetType().GetProperty("Key")?.GetValue(item)?.ToString();
                        var valueWrapper = item.GetType().GetProperty("Value")?.GetValue(item);
                        var checkResult = valueWrapper?.GetType().GetProperty("Value")?.GetValue(valueWrapper);

                        if (checkResult == null)
                            continue;

                        var statusWrapper = checkResult.GetType().GetProperty("CheckStatus")?.GetValue(checkResult);
                        var ratioWrapper = checkResult.GetType().GetProperty("UtilizationRatio")?.GetValue(checkResult);

                        var status = statusWrapper?.GetType().GetProperty("Value")?.GetValue(statusWrapper)?.ToString();
                        var ratioObj = ratioWrapper?.GetType().GetProperty("Value")?.GetValue(ratioWrapper);

                        double ratio = ratioObj != null ? Convert.ToDouble(ratioObj) : 0;

                        if (ratio < 0.90 || ratio >= 1.0)
                            continue;

                        string section = "Unknown";

                        try
                        {
                            var physicalSection = GetPhysicalSection(span);

                            section =
                                GetPropertyValue(physicalSection, "LongName")
                                ?? GetPropertyValue(physicalSection, "ShortName")
                                ?? "Unknown";
                        }
                        catch { }

                        results.Add(new
                        {
                            member = m.Name,
                            type = InferMemberType(m.Name),
                            section,
                            check_type = checkType,
                            status,
                            utilization_ratio = ratio
                        });
                    }
                }
            }
            catch { }
        }

        var nearLimit = results
            .OrderByDescending(x =>
                (double)x.GetType()
                    .GetProperty("utilization_ratio")!
                    .GetValue(x)!);

        Console.WriteLine(JsonSerializer.Serialize(nearLimit));
    }
    else if (command == "get_member_details")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = "Member name required" }));
            return;
        }

        string targetName = args[1];

        var members = await model.GetMembersAsync(null);
        var member = members.FirstOrDefault(m =>
            string.Equals(m.Name, targetName, StringComparison.OrdinalIgnoreCase));

        if (member == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = $"Member not found: {targetName}" }));
            return;
        }

        var spans = await member.GetSpanAsync(null);
        var spanResults = new List<object>();

        foreach (var span in spans)
        {
            string section = "Unknown";
            string sectionType = "Unknown";
            string materialType = "Unknown";

            try
            {
                var physicalSection = GetPhysicalSection(span);

                section =
                    GetPropertyValue(physicalSection, "LongName")
                    ?? GetPropertyValue(physicalSection, "ShortName")
                    ?? "Unknown";

                sectionType = GetPropertyValue(physicalSection, "SectionType") ?? "Unknown";
                materialType = GetPropertyValue(physicalSection, "MaterialType") ?? "Unknown";
            }
            catch { }

            var checks = new List<object>();

            try
            {
                var checkResults = span.GetType().GetProperty("CheckResults")?.GetValue(span);
                var valueEnumerable = checkResults?.GetType().GetProperty("Value")?.GetValue(checkResults) as System.Collections.IEnumerable;

                if (valueEnumerable != null)
                {
                    foreach (var item in valueEnumerable)
                    {
                        var checkType = item.GetType().GetProperty("Key")?.GetValue(item)?.ToString();
                        var valueWrapper = item.GetType().GetProperty("Value")?.GetValue(item);
                        var checkResult = valueWrapper?.GetType().GetProperty("Value")?.GetValue(valueWrapper);

                        if (checkResult == null)
                            continue;

                        var statusWrapper = checkResult.GetType().GetProperty("CheckStatus")?.GetValue(checkResult);
                        var ratioWrapper = checkResult.GetType().GetProperty("UtilizationRatio")?.GetValue(checkResult);

                        var status = statusWrapper?.GetType().GetProperty("Value")?.GetValue(statusWrapper)?.ToString();
                        var ratioObj = ratioWrapper?.GetType().GetProperty("Value")?.GetValue(ratioWrapper);

                        double ratio = ratioObj != null ? Convert.ToDouble(ratioObj) : 0;

                        checks.Add(new
                        {
                            check_type = checkType,
                            status,
                            utilization_ratio = ratio
                        });
                    }
                }
            }
            catch { }

            spanResults.Add(new
            {
                span = span.Name,
                section,
                section_type = sectionType,
                material_type = materialType,
                checks
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = member.Name,
            type = InferMemberType(member.Name),
            spans = spanResults
        }));
    }
    else if (command == "get_members_by_section")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = "Section name required" }));
            return;
        }

        string targetSection = args[1];
        var members = await model.GetMembersAsync(null);
        var results = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string section =
                        GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    if (!string.Equals(section, targetSection, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string sectionType = GetPropertyValue(physicalSection, "SectionType") ?? "Unknown";
                    string materialType = GetPropertyValue(physicalSection, "MaterialType") ?? "Unknown";

                    results.Add(new
                    {
                        member = m.Name,
                        type = InferMemberType(m.Name),
                        span = span.Name,
                        section,
                        section_type = sectionType,
                        material_type = materialType
                    });
                }
            }
            catch
            {
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            section = targetSection,
            count = results.Count,
            members = results
        }));
    }
    else if (command == "get_members_by_type")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = "Member type required" }));
            return;
        }

        string targetType = args[1];
        var members = await model.GetMembersAsync(null);
        var results = new List<object>();

        foreach (var m in members)
        {
            string type = InferMemberType(m.Name);

            if (!string.Equals(type, targetType, StringComparison.OrdinalIgnoreCase))
                continue;

            string section = "Unknown";
            string sectionType = "Unknown";
            string materialType = "Unknown";

            try
            {
                var spans = await m.GetSpanAsync(null);
                var firstSpan = spans?.FirstOrDefault();

                if (firstSpan != null)
                {
                    var physicalSection = GetPhysicalSection(firstSpan);

                    section =
                        GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    sectionType = GetPropertyValue(physicalSection, "SectionType") ?? "Unknown";
                    materialType = GetPropertyValue(physicalSection, "MaterialType") ?? "Unknown";
                }
            }
            catch { }

            results.Add(new
            {
                member = m.Name,
                type,
                section,
                section_type = sectionType,
                material_type = materialType
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = targetType,
            count = results.Count,
            members = results
        }));
    }
    else if (command == "get_design_status_summary")
    {
        var members = await model.GetMembersAsync(null);
        var statuses = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var checkResults = span.GetType().GetProperty("CheckResults")?.GetValue(span);
                    var valueEnumerable = checkResults?.GetType().GetProperty("Value")?.GetValue(checkResults) as System.Collections.IEnumerable;

                    if (valueEnumerable == null)
                        continue;

                    foreach (var item in valueEnumerable)
                    {
                        var checkType = item.GetType().GetProperty("Key")?.GetValue(item)?.ToString();
                        var valueWrapper = item.GetType().GetProperty("Value")?.GetValue(item);
                        var checkResult = valueWrapper?.GetType().GetProperty("Value")?.GetValue(valueWrapper);

                        if (checkResult == null)
                            continue;

                        var statusWrapper = checkResult.GetType().GetProperty("CheckStatus")?.GetValue(checkResult);
                        var ratioWrapper = checkResult.GetType().GetProperty("UtilizationRatio")?.GetValue(checkResult);

                        var status = statusWrapper?.GetType().GetProperty("Value")?.GetValue(statusWrapper)?.ToString() ?? "Unknown";
                        var ratioObj = ratioWrapper?.GetType().GetProperty("Value")?.GetValue(ratioWrapper);
                        double ratio = ratioObj != null ? Convert.ToDouble(ratioObj) : 0;

                        statuses.Add(new
                        {
                            member = m.Name,
                            type = InferMemberType(m.Name),
                            check_type = checkType,
                            status,
                            utilization_ratio = ratio
                        });
                    }
                }
            }
            catch { }
        }

        var byStatus = statuses
            .GroupBy(x => x.GetType().GetProperty("status")!.GetValue(x)!.ToString())
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .OrderBy(x => x.status);

        var byCheckType = statuses
            .GroupBy(x => x.GetType().GetProperty("check_type")!.GetValue(x)?.ToString() ?? "Unknown")
            .Select(g => new
            {
                check_type = g.Key,
                count = g.Count()
            })
            .OrderBy(x => x.check_type);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_checks = statuses.Count,
            by_status = byStatus,
            by_check_type = byCheckType
        }));
    }
    else if (command == "get_steel_takeoff")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        var members = await model.GetMembersAsync(null);
        var rows = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string section =
                        GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    string sectionType =
                        GetPropertyValue(physicalSection, "SectionType")
                        ?? "Unknown";

                    string materialType =
                        GetPropertyValue(physicalSection, "MaterialType")
                        ?? "Unknown";

                    string massString =
                        GetPropertyValue(physicalSection, "Mass")
                        ?? "0";

                    double mass = Convert.ToDouble(massString);
                    double weightPerFt = mass * TsdMassToPlf;

                    double lengthMm = span.Length.Value;
                    double lengthFt = lengthMm / MmPerFt;

                    double spanWeightLb = lengthFt * weightPerFt;

                    rows.Add(new
                    {
                        member = m.Name,
                        member_type = InferMemberType(m.Name),
                        span = span.Name,
                        section,
                        section_type = sectionType,
                        material_type = materialType,
                        length_ft = lengthFt,
                        weight_per_ft = weightPerFt,
                        total_weight_lb = spanWeightLb
                    });
                }
            }
            catch
            {
            }
        }

        var takeoff = rows
            .GroupBy(x => new
            {
                material_type = x.GetType().GetProperty("material_type")!.GetValue(x)?.ToString(),
                section = x.GetType().GetProperty("section")!.GetValue(x)?.ToString(),
                section_type = x.GetType().GetProperty("section_type")!.GetValue(x)?.ToString(),
                member_type = x.GetType().GetProperty("member_type")!.GetValue(x)?.ToString(),
                weight_per_ft = Convert.ToDouble(
                    x.GetType().GetProperty("weight_per_ft")!.GetValue(x))
            })
            .Select(g => new
            {
                material_type = g.Key.material_type,
                section = g.Key.section,
                section_type = g.Key.section_type,
                member_type = g.Key.member_type,

                member_count = g.Count(),

                total_length_ft = Math.Round(
                    g.Sum(x => Convert.ToDouble(
                        x.GetType().GetProperty("length_ft")!.GetValue(x))), 2),

                weight_per_ft = Math.Round(g.Key.weight_per_ft, 2),

                total_weight_lb = Math.Round(
                    g.Sum(x => Convert.ToDouble(
                        x.GetType().GetProperty("total_weight_lb")!.GetValue(x))), 1),

                total_weight_tons = Math.Round(
                    g.Sum(x => Convert.ToDouble(
                        x.GetType().GetProperty("total_weight_lb")!.GetValue(x))) / 2000.0, 2)
            })
            .OrderByDescending(x => x.total_weight_lb)
            .ToList();

        double grandTotalWeightLb = rows.Sum(x =>
            Convert.ToDouble(
                x.GetType().GetProperty("total_weight_lb")!.GetValue(x)));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_spans = rows.Count,
            total_weight_lb = Math.Round(grandTotalWeightLb, 1),
            total_weight_tons = Math.Round(grandTotalWeightLb / 2000.0, 2),
            takeoff
        }));
    }
    else if (command == "get_takeoff_by_member_type")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        var members = await model.GetMembersAsync(null);
        var rows = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string materialType =
                        GetPropertyValue(physicalSection, "MaterialType")
                        ?? "Unknown";

                    string massString =
                        GetPropertyValue(physicalSection, "Mass")
                        ?? "0";

                    double mass = Convert.ToDouble(massString);
                    double weightPerFt = mass * TsdMassToPlf;

                    double lengthFt = span.Length.Value / MmPerFt;
                    double totalWeightLb = lengthFt * weightPerFt;

                    rows.Add(new
                    {
                        member = m.Name,
                        member_type = InferMemberType(m.Name),
                        material_type = materialType,
                        length_ft = lengthFt,
                        total_weight_lb = totalWeightLb
                    });
                }
            }
            catch
            {
            }
        }

        var summary = rows
            .GroupBy(x => x.GetType().GetProperty("member_type")!.GetValue(x)?.ToString())
            .Select(g => new
            {
                member_type = g.Key,
                span_count = g.Count(),
                total_length_ft = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("length_ft")!.GetValue(x))), 2),
                total_weight_lb = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))), 1),
                total_weight_tons = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))) / 2000.0, 2)
            })
            .OrderByDescending(x => x.total_weight_lb)
            .ToList();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_spans = rows.Count,
            total_weight_lb = Math.Round(rows.Sum(x =>
                Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))), 1),
            total_weight_tons = Math.Round(rows.Sum(x =>
                Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))) / 2000.0, 2),
            by_member_type = summary
        }));
    }
    else if (command == "get_heaviest_sections")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        var members = await model.GetMembersAsync(null);
        var rows = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string section =
                        GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    string sectionType =
                        GetPropertyValue(physicalSection, "SectionType")
                        ?? "Unknown";

                    string materialType =
                        GetPropertyValue(physicalSection, "MaterialType")
                        ?? "Unknown";

                    string massString =
                        GetPropertyValue(physicalSection, "Mass")
                        ?? "0";

                    double mass = Convert.ToDouble(massString);
                    double weightPerFt = mass * TsdMassToPlf;

                    double lengthFt = span.Length.Value / MmPerFt;
                    double totalWeightLb = lengthFt * weightPerFt;

                    rows.Add(new
                    {
                        member = m.Name,
                        member_type = InferMemberType(m.Name),
                        span = span.Name,
                        section,
                        section_type = sectionType,
                        material_type = materialType,
                        length_ft = lengthFt,
                        weight_per_ft = weightPerFt,
                        total_weight_lb = totalWeightLb
                    });
                }
            }
            catch
            {
            }
        }

        var heaviestSections = rows
            .GroupBy(x => new
            {
                section = x.GetType().GetProperty("section")!.GetValue(x)?.ToString(),
                section_type = x.GetType().GetProperty("section_type")!.GetValue(x)?.ToString(),
                material_type = x.GetType().GetProperty("material_type")!.GetValue(x)?.ToString()
            })
            .Select(g => new
            {
                section = g.Key.section,
                section_type = g.Key.section_type,
                material_type = g.Key.material_type,
                span_count = g.Count(),
                total_length_ft = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("length_ft")!.GetValue(x))), 2),
                weight_per_ft = Math.Round(g.Average(x =>
                    Convert.ToDouble(x.GetType().GetProperty("weight_per_ft")!.GetValue(x))), 2),
                total_weight_lb = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))), 1),
                total_weight_tons = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))) / 2000.0, 2)
            })
            .OrderByDescending(x => x.total_weight_lb)
            .Take(25)
            .ToList();

        double grandTotalWeightLb = rows.Sum(x =>
            Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x)));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_spans = rows.Count,
            total_weight_lb = Math.Round(grandTotalWeightLb, 1),
            total_weight_tons = Math.Round(grandTotalWeightLb / 2000.0, 2),
            heaviest_sections = heaviestSections
        }));
    }
    else if (command == "get_takeoff_by_section_type")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        var members = await model.GetMembersAsync(null);
        var rows = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string sectionType =
                        GetPropertyValue(physicalSection, "SectionType")
                        ?? "Unknown";

                    string materialType =
                        GetPropertyValue(physicalSection, "MaterialType")
                        ?? "Unknown";

                    string massString =
                        GetPropertyValue(physicalSection, "Mass")
                        ?? "0";

                    double mass = Convert.ToDouble(massString);
                    double weightPerFt = mass * TsdMassToPlf;

                    double lengthFt = span.Length.Value / MmPerFt;
                    double totalWeightLb = lengthFt * weightPerFt;

                    rows.Add(new
                    {
                        member = m.Name,
                        section_type = sectionType,
                        material_type = materialType,
                        length_ft = lengthFt,
                        total_weight_lb = totalWeightLb
                    });
                }
            }
            catch { }
        }

        var summary = rows
            .GroupBy(x => new
            {
                section_type = x.GetType().GetProperty("section_type")!.GetValue(x)?.ToString(),
                material_type = x.GetType().GetProperty("material_type")!.GetValue(x)?.ToString()
            })
            .Select(g => new
            {
                section_type = g.Key.section_type,
                material_type = g.Key.material_type,
                span_count = g.Count(),
                total_length_ft = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("length_ft")!.GetValue(x))), 2),
                total_weight_lb = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))), 1),
                total_weight_tons = Math.Round(g.Sum(x =>
                    Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))) / 2000.0, 2)
            })
            .OrderByDescending(x => x.total_weight_lb)
            .ToList();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_spans = rows.Count,
            total_weight_lb = Math.Round(rows.Sum(x =>
                Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))), 1),
            total_weight_tons = Math.Round(rows.Sum(x =>
                Convert.ToDouble(x.GetType().GetProperty("total_weight_lb")!.GetValue(x))) / 2000.0, 2),
            by_section_type = summary
        }));
    }
    else if (command == "get_model_cost_estimate")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = "Cost per ton required" }));
            return;
        }

        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        double costPerTon = Convert.ToDouble(args[1]);

        var members = await model.GetMembersAsync(null);
        double totalWeightLb = 0;

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string massString =
                        GetPropertyValue(physicalSection, "Mass")
                        ?? "0";

                    double mass = Convert.ToDouble(massString);
                    double weightPerFt = mass * TsdMassToPlf;
                    double lengthFt = span.Length.Value / MmPerFt;

                    totalWeightLb += lengthFt * weightPerFt;
                }
            }
            catch { }
        }

        double totalWeightTons = totalWeightLb / 2000.0;
        double estimatedCost = totalWeightTons * costPerTon;

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_weight_lb = Math.Round(totalWeightLb, 1),
            total_weight_tons = Math.Round(totalWeightTons, 2),
            cost_per_ton = costPerTon,
            estimated_material_cost = Math.Round(estimatedCost, 2)
        }));
    }
    else if (command == "debug_model_properties")
    {
        var props = model.GetType().GetProperties()
            .Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName
            });

        Console.WriteLine(JsonSerializer.Serialize(props));
    }
    else if (command == "get_official_material_quantities")
    {
        var review = model.TabularResultsAccessor.Review;

        var materialType = TSD.API.Remoting.Materials.MaterialType.Steel;

        var quantities = await review.GetMaterialQuantitiesAsync(
            materialType,
            "",
            null,
            default
        );

        double massKg = quantities.Mass ?? 0;
        double massLb = massKg * 2.20462262185;
        double massTons = massLb / 2000.0;

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            material_type = "Steel",
            count = quantities.Count,
            connectors_count = quantities.ConnectorsCount,

            mass_kg = Math.Round(massKg, 2),
            mass_lb = Math.Round(massLb, 1),
            mass_tons = Math.Round(massTons, 2),

            mass_ignoring_holes_kg = Math.Round(quantities.MassIgnoringHoles ?? 0, 2),

            surface_area = quantities.SurfaceArea,
            surface_area_ignoring_holes = quantities.SurfaceAreaIgnoringHoles,
            volume = quantities.Volume,

            reinforcement_mass = quantities.ReinforcementMass,
            reinforcement_density = quantities.ReinforcementDensity,
            embodied_carbon_mass = quantities.EmbodiedCarbonMass
        }));
    }
    else if (command == "get_load_combinations")
    {
        var combos = await model.GetCombinationsAsync(null, default);

        var results = new List<object>();

        foreach (var combo in combos)
        {
            object? GetWrappedValue(object obj, string propertyName)
            {
                try
                {
                    var wrapper = obj.GetType().GetProperty(propertyName)?.GetValue(obj);
                    return wrapper?.GetType().GetProperty("Value")?.GetValue(wrapper);
                }
                catch
                {
                    return null;
                }
            }

            results.Add(new
            {
                name = combo.Name,
                id = combo.Id,
                reference_index = combo.ReferenceIndex,
                index = combo.Index,

                combination_class = GetWrappedValue(combo, "CombinationClass")?.ToString(),
                combination_speciality = GetWrappedValue(combo, "CombinationSpeciality")?.ToString(),
                factoring_type = GetWrappedValue(combo, "FactoringType")?.ToString(),

                is_active = GetWrappedValue(combo, "IsActive"),
                is_strength = GetWrappedValue(combo, "IsStrength"),
                is_service = GetWrappedValue(combo, "IsService"),
                applies_live_load_reductions = GetWrappedValue(combo, "AppliesLiveLoadReductions")
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            total_combinations = results.Count,
            combinations = results
        }));
    }
    else if (command == "get_analysis_status")
    {
        var selectedAnalysisType = await model.GetSelectedAnalysisTypeAsync(default);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_analysis_type = selectedAnalysisType.ToString(),
            tsd_running = true,
            model_open = true,
            status_note = "TSD is running and a model is open. Detailed analysis/design condition properties are not exposed by this API object."
        }));
    }
    else if (command == "get_solver_warnings")
    {
        var selectedAnalysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var errors = await model.GetSolverErrorsAsync(
            new[] { selectedAnalysisType },
            default
        );

        object? GetWrappedValue(object obj, string propertyName)
        {
            try
            {
                var wrapper = obj.GetType().GetProperty(propertyName)?.GetValue(obj);
                return wrapper?.GetType().GetProperty("Value")?.GetValue(wrapper);
            }
            catch
            {
                return null;
            }
        }

        object? GetWrapperProperty(object obj, string propertyName, string wrapperPropertyName)
        {
            try
            {
                var wrapper = obj.GetType().GetProperty(propertyName)?.GetValue(obj);
                return wrapper?.GetType().GetProperty(wrapperPropertyName)?.GetValue(wrapper);
            }
            catch
            {
                return null;
            }
        }

        var results = errors.Select(e => new
        {
            level = GetWrappedValue(e, "ErrorLevel")?.ToString(),
            text = GetWrappedValue(e, "Text")?.ToString(),
            description = GetWrappedValue(e, "Description")?.ToString(),
            related_entity = GetWrappedValue(e, "RelatedEntity")?.ToString(),

            text_available = GetWrapperProperty(e, "Text", "IsApplicable"),
            description_available = GetWrapperProperty(e, "Description", "IsApplicable"),
            related_entity_available = GetWrapperProperty(e, "RelatedEntity", "IsApplicable")
        }).ToList();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_analysis_type = selectedAnalysisType.ToString(),
            total_solver_items = results.Count,
            solver_items = results
        }));
    }
    else if (command == "get_member_forces")
    {
        if (args.Length < 3)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Usage: get_member_forces <member_name> <combination_reference_index_or_name>"
            }));
            return;
        }

        string memberName = args[1];
        string comboInput = args[2];

        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);
        var combos = await model.GetCombinationsAsync(null, default);

        object? GetWrappedValue(object obj, string propertyName)
        {
            try
            {
                var wrapper = obj.GetType().GetProperty(propertyName)?.GetValue(obj);
                return wrapper?.GetType().GetProperty("Value")?.GetValue(wrapper);
            }
            catch
            {
                return null;
            }
        }

        bool TryGetReferenceIndex(string input, out int referenceIndex)
        {
            referenceIndex = 0;

            if (int.TryParse(input, out referenceIndex))
                return true;

            var firstToken = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (firstToken != null && int.TryParse(firstToken, out referenceIndex))
                return true;

            return false;
        }

        object? combo = null;

        if (TryGetReferenceIndex(comboInput, out int comboReferenceIndex))
        {
            combo = combos.FirstOrDefault(c => c.ReferenceIndex == comboReferenceIndex);
        }

        if (combo == null)
        {
            combo = combos.FirstOrDefault(c =>
                string.Equals(c.Name, comboInput, StringComparison.OrdinalIgnoreCase));
        }

        if (combo == null)
        {
            combo = combos.FirstOrDefault(c =>
                c.Name.Contains(comboInput, StringComparison.OrdinalIgnoreCase));
        }

        if (combo == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = $"No load combination found for '{comboInput}'",
                suggestion = "Use get_tsd_load_combinations to find a valid reference index or combination name."
            }));
            return;
        }

        string comboName = combo.GetType().GetProperty("Name")?.GetValue(combo)?.ToString() ?? "";
        Guid comboId = (Guid)(combo.GetType().GetProperty("Id")?.GetValue(combo) ?? Guid.Empty);
        int comboReferenceIndexResolved = Convert.ToInt32(
            combo.GetType().GetProperty("ReferenceIndex")?.GetValue(combo) ?? 0
        );

        var forceSets = await model.TabularResultsAccessor.Analysis
            .GetLineElementEndForcesSetsAsync(
                comboId,
                analysisType,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                null,
                null,
                default);

        var forceSet = forceSets.FirstOrDefault();

        if (forceSet == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                member = memberName,
                requested_combination = comboInput,
                resolved_combination = comboName,
                combination_id = comboId,
                combination_reference_index = comboReferenceIndexResolved,
                analysis_type = analysisType.ToString(),
                force_count = 0,
                forces = new List<object>()
            }));
            return;
        }

        var forces = forceSet.LineElementData
            .Where(f => string.Equals(f.LineElementName, memberName, StringComparison.OrdinalIgnoreCase))
            .Select(f => new
            {
                member = f.LineElementName,
                span = f.LineElementEdgeName,
                position_mm = Math.Round(f.Position, 3),
                position_ft = Math.Round(f.Position / 304.8, 3),

                shear_major = Math.Round(f.ShearMajor, 3),
                shear_minor = Math.Round(f.ShearMinor, 3),
                moment_major = Math.Round(f.MomentMajor, 3),
                moment_minor = Math.Round(f.MomentMinor, 3),
                axial_force = Math.Round(f.AxialForce, 3),
                torsion = Math.Round(f.Torsion, 3),
                deflection_major = Math.Round(f.DeflectionMajor, 6),
                deflection_minor = Math.Round(f.DeflectionMinor, 6)
            })
            .ToList();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = memberName,
            requested_combination = comboInput,
            resolved_combination = comboName,
            combination_id = comboId,
            combination_reference_index = comboReferenceIndexResolved,
            combination_class = GetWrappedValue(combo, "CombinationClass")?.ToString(),
            factoring_type = GetWrappedValue(combo, "FactoringType")?.ToString(),
            is_active = GetWrappedValue(combo, "IsActive"),
            is_strength = GetWrappedValue(combo, "IsStrength"),
            is_service = GetWrappedValue(combo, "IsService"),
            analysis_type = analysisType.ToString(),
            force_count = forces.Count,
            forces
        }));
    }
    else if (command == "get_tsd_member_force_envelope")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Usage: get_member_force_envelope <member_name> [active_strength_combinations|all]"
            }));
            return;
        }

        string memberName = args[1];
        string mode = args.Length >= 3 ? args[2].ToLower() : "active_strength_combinations";

        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);
        var combos = await model.GetCombinationsAsync(null, default);

        object? GetWrappedValue(object obj, string propertyName)
        {
            try
            {
                var wrapper = obj.GetType().GetProperty(propertyName)?.GetValue(obj);
                return wrapper?.GetType().GetProperty("Value")?.GetValue(wrapper);
            }
            catch
            {
                return null;
            }
        }

        bool GetBoolWrapped(object obj, string propertyName)
        {
            var value = GetWrappedValue(obj, propertyName);
            return value is bool b && b;
        }

        string GetEndLabel(double positionFt)
        {
            return positionFt <= 0.01 ? "Start" : "End";
        }

        object EmptyResult()
        {
            return new
            {
                max_positive = (object?)null,
                max_negative = (object?)null,
                governing = (object?)null
            };
        }

        var selectedCombos = combos.Where(c =>
        {
            if (mode == "all") return true;

            bool isActive = GetBoolWrapped(c, "IsActive");
            bool isStrength = GetBoolWrapped(c, "IsStrength");

            return isActive && isStrength;
        }).ToList();

        var records = new List<dynamic>();

        foreach (var combo in selectedCombos)
        {
            try
            {
                var forceSets = await model.TabularResultsAccessor.Analysis
                    .GetLineElementEndForcesSetsAsync(
                        combo.Id,
                        analysisType,
                        false,
                        TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                        null,
                        null,
                        default);

                var forceSet = forceSets.FirstOrDefault();
                if (forceSet == null) continue;

                foreach (var f in forceSet.LineElementData
                    .Where(x => string.Equals(x.LineElementName, memberName, StringComparison.OrdinalIgnoreCase)))
                {
                    double positionFt = f.Position / 304.8;

                    records.Add(new
                    {
                        combo_name = combo.Name,
                        combo_id = combo.Id,
                        reference_index = combo.ReferenceIndex,
                        position_ft = Math.Round(positionFt, 3),
                        end = GetEndLabel(positionFt),

                        axial_force = f.AxialForce,
                        shear_major = f.ShearMajor,
                        shear_minor = f.ShearMinor,
                        moment_major = f.MomentMajor,
                        moment_minor = f.MomentMinor,
                        torsion = f.Torsion,
                        deflection_major = f.DeflectionMajor,
                        deflection_minor = f.DeflectionMinor
                    });
                }
            }
            catch
            {
                // skip combinations that do not return force data
            }
        }

        object BuildEnvelope(string componentName, Func<dynamic, double> selector, string significancePositive, string significanceNegative)
        {
            if (!records.Any())
                return EmptyResult();

            var positives = records.Where(r => selector(r) > 0).ToList();
            var negatives = records.Where(r => selector(r) < 0).ToList();

            dynamic? maxPositive = positives.Any()
                ? positives.OrderByDescending(r => selector(r)).First()
                : null;

            dynamic? maxNegative = negatives.Any()
                ? negatives.OrderBy(r => selector(r)).First()
                : null;

            var governingRecord = records
                .OrderByDescending(r => Math.Abs(selector(r)))
                .First();

            double governingValue = selector(governingRecord);
            string significance = governingValue >= 0 ? significancePositive : significanceNegative;

            object? Format(dynamic? r, bool absolute = false)
            {
                if (r == null) return null;

                double value = selector(r);

                return new
                {
                    value = Math.Round(absolute ? Math.Abs(value) : value, 3),
                    combination = r.combo_name,
                    reference_index = r.reference_index,
                    position_ft = r.position_ft,
                    end = r.end
                };
            }

            return new
            {
                max_positive = Format(maxPositive),
                max_negative = Format(maxNegative),
                governing = new
                {
                    value = Math.Round(Math.Abs(governingValue), 3),
                    raw_value = Math.Round(governingValue, 3),
                    combination = governingRecord.combo_name,
                    reference_index = governingRecord.reference_index,
                    position_ft = governingRecord.position_ft,
                    end = governingRecord.end,
                    engineering_significance = significance
                }
            };
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = memberName,
            analysis_type = analysisType.ToString(),

            mode,

            combination_filter = mode == "all"
                ? "All load combinations"
                : "Active strength load combinations only",

            combinations_filter = selectedCombos.Count,
            force_records_found = records.Count,

            performance_note = mode == "all"
                ? "Mode 'all' checks every load combination and may take significantly longer on large models."
                : null,

            axial_force = BuildEnvelope(
                "axial_force",
                r => r.axial_force,
                "Maximum tension governs.",
                "Maximum compression governs."
            ),

            shear_major = BuildEnvelope(
                "shear_major",
                r => r.shear_major,
                "Governing major-axis shear. Review member shear capacity and connection shear demand.",
                "Governing major-axis shear. Review member shear capacity and connection shear demand."
            ),

            shear_minor = BuildEnvelope(
                "shear_minor",
                r => r.shear_minor,
                "Governing weak-axis shear.",
                "Governing weak-axis shear."
            ),

            moment_major = BuildEnvelope(
                "moment_major",
                r => r.moment_major,
                "Governing strong-axis bending moment. Check flexural capacity and lateral stability.",
                "Governing strong-axis bending moment. Check flexural capacity and lateral stability."
            ),

            moment_minor = BuildEnvelope(
                "moment_minor",
                r => r.moment_minor,
                "Governing weak-axis bending. Check biaxial bending effects.",
                "Governing weak-axis bending. Check biaxial bending effects."
            ),

            torsion = BuildEnvelope(
                "torsion",
                r => r.torsion,
                "Governing torsional demand. Review torsional restraint and connection detailing.",
                "Governing torsional demand. Review torsional restraint and connection detailing."
            ),

            deflection_major = BuildEnvelope(
                "deflection_major",
                r => r.deflection_major,
                "Maximum major-axis deflection. Compare against serviceability criteria.",
                "Maximum major-axis deflection. Compare against serviceability criteria."
            ),

            deflection_minor = BuildEnvelope(
                "deflection_minor",
                r => r.deflection_minor,
                "Maximum minor-axis deflection. Compare against serviceability criteria.",
                "Maximum minor-axis deflection. Compare against serviceability criteria."
            )
        }));
    }
    else if (command == "debug_line_element_end_force_object")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);

        var combo = combos.First(c => c.ReferenceIndex == 18);

        var forceSets = await model.TabularResultsAccessor.Analysis
            .GetLineElementEndForcesSetsAsync(
                combo.Id,
                analysisType,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                null,
                null,
                default);

        var force = forceSets
            .First()
            .LineElementData
            .First(f => f.LineElementName == "B4869");

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = force.GetType().FullName,

            properties = force.GetType().GetProperties()
                .Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName,
                    value = SafeGetProperty(p, force)
                }),

            methods = force.GetType().GetMethods()
                .Where(m => !m.IsSpecialName)
                .Select(m => new
                {
                    method = m.Name,
                    return_type = m.ReturnType.FullName,
                    parameters = m.GetParameters().Select(p => new
                    {
                        name = p.Name,
                        type = p.ParameterType.FullName
                    })
                })
        }));
    }
    else if (command == "debug_analysis_accessor_methods")
    {
        var analysis = model.TabularResultsAccessor.Analysis;

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = analysis.GetType().FullName,

            methods = analysis.GetType()
                .GetMethods()
                .Where(m => !m.IsSpecialName)
                .OrderBy(m => m.Name)
                .Select(m => new
                {
                    method = m.Name,
                    return_type = m.ReturnType.FullName,
                    parameters = m.GetParameters().Select(p => new
                    {
                        name = p.Name,
                        type = p.ParameterType.FullName
                    })
                })
        }));
    }
    else if (command == "search_api_types")
    {
        var asm = typeof(TSD.API.Remoting.ApplicationFactory).Assembly;

        var keywords = new[]
        {
        "Force",
        "Moment",
        "Reaction",
        "Envelope",
        "Diagram",
        "Result",
        "Station",
        "Utilization",
        "Design",
        "Analysis"
        };

        var matches = asm.GetTypes()
            .Where(t => keywords.Any(k =>
                t.FullName != null &&
                t.FullName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.FullName)
            .Select(t => t.FullName);

        Console.WriteLine(JsonSerializer.Serialize(matches));
    }
    else if (command == "debug_combination_factor_purpose_enum")
    {
        var values = Enum.GetNames(typeof(TSD.API.Remoting.Loading.CombinationItemFactorPurpose));

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            enum_name = "CombinationItemFactorPurpose",
            values
        }));
    }
    else if (command == "debug_member_forces")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First(c =>
            c.Name.Contains("LRFD") &&
            c.Name.Contains("1.2D") &&
            c.Name.Contains("1.6L"));

        var forces = await model.TabularResultsAccessor.Analysis
            .GetLineElementEndForcesSetsAsync(
                combo.Id,
                analysisType,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                null,
                null,
                default);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_combo = combo.Name,
            combo_id = combo.Id,
            analysis_type = analysisType.ToString(),
            total_force_sets = forces.Count()
        }));
    }
    else if (command == "debug_member_force_set_deep")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First(c =>
            c.Name.Contains("LRFD") &&
            c.Name.Contains("1.2D") &&
            c.Name.Contains("1.6L"));

        var forces = await model.TabularResultsAccessor.Analysis
            .GetLineElementEndForcesSetsAsync(
                combo.Id,
                analysisType,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                null,
                null,
                default);

        var force = forces.FirstOrDefault();

        object DumpObject(object? obj)
        {
            if (obj == null)
                return new { type = "null" };

            return new
            {
                type = obj.GetType().FullName,
                value = obj.ToString(),
                properties = obj.GetType().GetProperties().Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName,
                    value = SafeGetProperty(p, obj)
                }),
                methods = obj.GetType().GetMethods()
                    .Where(m => !m.IsSpecialName)
                    .Select(m => new
                    {
                        method = m.Name,
                        return_type = m.ReturnType.FullName,
                        parameters = m.GetParameters().Select(p => new
                        {
                            name = p.Name,
                            type = p.ParameterType.FullName
                        })
                    })
            };
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_combo = combo.Name,
            combo_id = combo.Id,
            analysis_type = analysisType.ToString(),
            total_force_sets = forces.Count(),
            first_force_set = DumpObject(force)
        }));
    }
    else if (command == "debug_line_element_force_data")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First(c =>
            c.Name.Contains("LRFD") &&
            c.Name.Contains("1.2D") &&
            c.Name.Contains("1.6L"));

        var forceSets = await model.TabularResultsAccessor.Analysis
            .GetLineElementEndForcesSetsAsync(
                combo.Id,
                analysisType,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                null,
                null,
                default);

        var forceSet = forceSets.First();
        var lineData = forceSet.LineElementData;

        var first = lineData.FirstOrDefault();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_combo = combo.Name,
            total_force_sets = forceSets.Count(),
            line_element_data_count = lineData.Count,
            first_line_element_force = first == null ? null : new
            {
                type = first.GetType().FullName,
                properties = first.GetType().GetProperties().Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName,
                    value = SafeGetProperty(p, first)
                })
            }
        }));
    }
    else if (command == "debug_solver_warning_deep")
    {
        var selectedAnalysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var errors = await model.GetSolverErrorsAsync(
            new[] { selectedAnalysisType },
            default
        );

        object DumpWrapper(object? wrapper)
        {
            if (wrapper == null)
                return new { wrapper_type = "null" };

            return new
            {
                wrapper_type = wrapper.GetType().FullName,
                wrapper_string = wrapper.ToString(),
                properties = wrapper.GetType().GetProperties().Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName,
                    value = SafeGetProperty(p, wrapper)
                })
            };
        }

        var results = errors.Select(e => new
        {
            error_type = e.GetType().FullName,
            error_string = e.ToString(),
            error_level = DumpWrapper(e.GetType().GetProperty("ErrorLevel")?.GetValue(e)),
            text = DumpWrapper(e.GetType().GetProperty("Text")?.GetValue(e)),
            description = DumpWrapper(e.GetType().GetProperty("Description")?.GetValue(e)),
            related_entity = DumpWrapper(e.GetType().GetProperty("RelatedEntity")?.GetValue(e))
        });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_analysis_type = selectedAnalysisType.ToString(),
            solver_items = results
        }));
    }
    else if (command == "debug_document_properties")
    {
        var props = document.GetType().GetProperties()
            .Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName
            });

        var methods = document.GetType().GetMethods()
            .Select(m => new
            {
                method = m.Name,
                return_type = m.ReturnType.FullName,
                parameters = m.GetParameters().Select(p => new
                {
                    name = p.Name,
                    type = p.ParameterType.FullName
                })
            });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            document_type = document.GetType().FullName,
            properties = props,
            methods = methods
        }));
    }
    else if (command == "debug_sessions")
    {
        var sessions = await document.GetSessionsAsync(default);

        var results = new List<object>();

        foreach (var session in sessions)
        {
            var props = session.GetType().GetProperties()
                .Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName
                });

            var methods = session.GetType().GetMethods()
                .Select(m => new
                {
                    method = m.Name,
                    return_type = m.ReturnType.FullName,
                    parameters = m.GetParameters().Select(p => new
                    {
                        name = p.Name,
                        type = p.ParameterType.FullName
                    })
                });

            results.Add(new
            {
                session_type = session.GetType().FullName,
                properties = props,
                methods = methods
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(results));
    }
    else if (command == "debug_model_methods")
    {
        var methods = model.GetType().GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => new
            {
                method = m.Name,
                return_type = m.ReturnType.FullName,
                parameters = m.GetParameters().Select(p => new
                {
                    name = p.Name,
                    type = p.ParameterType.FullName
                })
            });

        Console.WriteLine(JsonSerializer.Serialize(methods));
    }
    else if (command == "debug_load_combinations")
    {
        var combos = await model.GetCombinationsAsync(null, default);

        var result = combos.Select(c => new
        {
            type = c.GetType().FullName,
            properties = c.GetType().GetProperties().Select(p => new
            {
                property = p.Name,
                value = SafeGetProperty(p, c)
            })
        });

        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else if (command == "debug_tabular_results_accessor")
    {
        var accessor = model.TabularResultsAccessor;

        var props = accessor.GetType().GetProperties()
            .Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName
            });

        var methods = accessor.GetType().GetMethods()
            .Select(m => new
            {
                method = m.Name,
                return_type = m.ReturnType.FullName,
                parameters = m.GetParameters().Select(p => new
                {
                    name = p.Name,
                    type = p.ParameterType.FullName
                })
            });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            accessor_type = accessor.GetType().FullName,
            properties = props,
            methods = methods
        }));
    }
    else if (command == "debug_analysis_review_results")
    {
        var accessor = model.TabularResultsAccessor;

        var analysis = accessor.Analysis;
        var review = accessor.Review;

        object DumpObject(object obj)
        {
            return new
            {
                type = obj.GetType().FullName,
                properties = obj.GetType().GetProperties().Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName
                }),
                methods = obj.GetType().GetMethods().Select(m => new
                {
                    method = m.Name,
                    return_type = m.ReturnType.FullName,
                    parameters = m.GetParameters().Select(p => new
                    {
                        name = p.Name,
                        type = p.ParameterType.FullName
                    })
                })
            };
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            analysis = DumpObject(analysis),
            review = DumpObject(review)
        }));
    }
    else if (command == "debug_material_quantities")
    {
        var review = model.TabularResultsAccessor.Review;

        var materialType = TSD.API.Remoting.Materials.MaterialType.Steel;

        var quantities = await review.GetMaterialQuantitiesAsync(
            materialType,
            "",
            null,
            default
        );

        var props = quantities.GetType().GetProperties().Select(p =>
        {
            object? value = null;
            string? error = null;

            try
            {
                value = p.GetValue(quantities);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return new
            {
                property = p.Name,
                type = p.PropertyType.FullName,
                value = value?.ToString(),
                error
            };
        });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            quantities_type = quantities.GetType().FullName,
            properties = props
        }));
    }
    else if (command == "debug_solver_warnings")
    {
        var selectedAnalysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var errors = await model.GetSolverErrorsAsync(
            new[] { selectedAnalysisType },
            default
        );

        var results = errors.Select(e => new
        {
            error_type = e.GetType().FullName,
            properties = e.GetType().GetProperties().Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName,
                value = SafeGetProperty(p, e)
            })
        });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_analysis_type = selectedAnalysisType.ToString(),
            solver_items = results
        }));
    }
    else if (command == "debug_check_results")
    {
        var members = await model.GetMembersAsync(null);
        var m = members.First();

        var spans = await m.GetSpanAsync(null);
        var span = spans.First();

        var checkResultsProperty = span.GetType().GetProperty("CheckResults");
        var checkResults = checkResultsProperty?.GetValue(span);

        var props = checkResults?.GetType().GetProperties().Select(p =>
        {
            object? value = null;
            string? error = null;

            try
            {
                value = p.GetValue(checkResults);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return new
            {
                property = p.Name,
                type = p.PropertyType.FullName,
                value = value?.ToString(),
                error
            };
        });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = m.Name,
            span = span.Name,
            checkResultsType = checkResults?.GetType().FullName,
            checkResultsString = checkResults?.ToString(),
            properties = props
        }));
    }
    else if (command == "debug_section_mass")
    {
        var members = await model.GetMembersAsync(null);
        var seenSections = new HashSet<string>();
        var results = new List<object>();

        foreach (var m in members)
        {
            try
            {
                var spans = await m.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var physicalSection = GetPhysicalSection(span);

                    string section =
                        GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    if (section == "Unknown" || seenSections.Contains(section))
                        continue;

                    seenSections.Add(section);

                    string sectionType =
                        GetPropertyValue(physicalSection, "SectionType")
                        ?? "Unknown";

                    string materialType =
                        GetPropertyValue(physicalSection, "MaterialType")
                        ?? "Unknown";

                    string mass =
                        GetPropertyValue(physicalSection, "Mass")
                        ?? "Unknown";

                    string unitSystem =
                        GetPropertyValue(physicalSection, "UnitSystem")
                        ?? "Unknown";

                    var properties = physicalSection?.GetType().GetProperties().Select(p =>
                    {
                        object? value = null;
                        string? error = null;

                        try
                        {
                            value = p.GetValue(physicalSection);
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                        }

                        return new
                        {
                            property = p.Name,
                            type = p.PropertyType.Name,
                            value = value?.ToString(),
                            error
                        };
                    });

                    results.Add(new
                    {
                        section,
                        section_type = sectionType,
                        material_type = materialType,
                        unit_system = unitSystem,
                        mass,
                        properties
                    });
                }
            }
            catch
            {
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(results));
    }
    else if (command == "debug_member_length")
    {
        var member = (await model.GetMembersAsync(null)).First();

        var spans = await member.GetSpanAsync(null);

        foreach (var span in spans)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                member = member.Name,
                span = span.Name,
                length = span.Length.Value
            }));
        }
    }
    else if (command == "debug_check_result_values")
    {
        var members = await model.GetMembersAsync(null);
        var m = members.First();

        var spans = await m.GetSpanAsync(null);
        var span = spans.First();

        var checkResultsProperty = span.GetType().GetProperty("CheckResults");
        var checkResults = checkResultsProperty?.GetValue(span);

        var valueProperty = checkResults?.GetType().GetProperty("Value");
        var valueEnumerable = valueProperty?.GetValue(checkResults) as System.Collections.IEnumerable;

        var results = new List<object>();

        if (valueEnumerable != null)
        {
            foreach (var item in valueEnumerable)
            {
                var itemType = item.GetType();

                var key = itemType.GetProperty("Key")?.GetValue(item);
                var value = itemType.GetProperty("Value")?.GetValue(item);

                var wrappedValue = value?.GetType().GetProperty("Value")?.GetValue(value);

                var wrappedProps = wrappedValue?.GetType().GetProperties().Select(p =>
                {
                    object? propValue = null;
                    string? error = null;

                    try
                    {
                        propValue = p.GetValue(wrappedValue);
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }

                    return new
                    {
                        property = p.Name,
                        type = p.PropertyType.FullName,
                        value = propValue?.ToString(),
                        error
                    };
                });

                results.Add(new
                {
                    key = key?.ToString(),
                    valueWrapperType = value?.GetType().FullName,
                    wrappedValueType = wrappedValue?.GetType().FullName,
                    wrappedValueString = wrappedValue?.ToString(),
                    wrappedProperties = wrappedProps
                });
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = m.Name,
            span = span.Name,
            checkResultsType = checkResults?.GetType().FullName,
            results
        }));
    }
    else if (command == "debug_check_result_values_resolved")
    {
        var members = await model.GetMembersAsync(null);
        var m = members.First();

        var spans = await m.GetSpanAsync(null);
        var span = spans.First();

        var checkResults = span.GetType().GetProperty("CheckResults")?.GetValue(span);
        var valueEnumerable = checkResults?.GetType().GetProperty("Value")?.GetValue(checkResults) as System.Collections.IEnumerable;

        var results = new List<object>();

        if (valueEnumerable != null)
        {
            foreach (var item in valueEnumerable)
            {
                var key = item.GetType().GetProperty("Key")?.GetValue(item)?.ToString();
                var valueWrapper = item.GetType().GetProperty("Value")?.GetValue(item);
                var checkResult = valueWrapper?.GetType().GetProperty("Value")?.GetValue(valueWrapper);

                var statusWrapper = checkResult?.GetType().GetProperty("CheckStatus")?.GetValue(checkResult);
                var ratioWrapper = checkResult?.GetType().GetProperty("UtilizationRatio")?.GetValue(checkResult);

                var status = statusWrapper?.GetType().GetProperty("Value")?.GetValue(statusWrapper)?.ToString();
                var ratio = ratioWrapper?.GetType().GetProperty("Value")?.GetValue(ratioWrapper);

                results.Add(new
                {
                    check_type = key,
                    status,
                    utilization_ratio = ratio
                });
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = m.Name,
            span = span.Name,
            results
        }));
    }
    else if (command == "debug_load_combinations")
    {
        var combos = await model.GetCombinationsAsync(null, default);

        var result = combos.Select(c => new
        {
            combination_type = c.GetType().FullName,
            properties = c.GetType().GetProperties().Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName,
                value = SafeGetProperty(p, c)
            })
        });

        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else if (command == "debug_single_combination")
    {
        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First();

        var props = combo.GetType().GetProperties()
            .Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName,
                value = p.GetValue(combo)?.ToString()
            });

        Console.WriteLine(JsonSerializer.Serialize(props));
    }
    else if (command == "debug_analysis_status")
    {
        var analysisCondition = await model.GetAnalysisConditionAsync(default);
        var designCondition = await model.GetDesignConditionAsync(default);
        var selectedAnalysisType = await model.GetSelectedAnalysisTypeAsync(default);

        object DumpObject(object obj)
        {
            return new
            {
                type = obj.GetType().FullName,
                properties = obj.GetType().GetProperties().Select(p => new
                {
                    property = p.Name,
                    type = p.PropertyType.FullName,
                    value = SafeGetProperty(p, obj)
                })
            };
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_analysis_type = selectedAnalysisType.ToString(),
            analysis_condition = DumpObject(analysisCondition),
            design_condition = DumpObject(designCondition)
        }));
    }
    else if (command == "debug_physical_section")
    {
        var members = await model.GetMembersAsync(null);
        var m = members.First();

        var spans = await m.GetSpanAsync(null);
        var span = spans.First();

        var elementSectionProperty = span.GetType().GetProperty("ElementSection");
        var elementSectionWrapper = elementSectionProperty?.GetValue(span);
        var elementSection = elementSectionWrapper?.GetType().GetProperty("Value")?.GetValue(elementSectionWrapper);
        var physicalSection = GetPhysicalSection(span);

        var props = physicalSection?.GetType().GetProperties().Select(p =>
        {
            object? value = null;
            string? error = null;

            try
            {
                value = p.GetValue(physicalSection);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return new
            {
                property = p.Name,
                type = p.PropertyType.Name,
                value = value?.ToString(),
                error
            };
        });

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = m.Name,
            elementSectionType = elementSection.GetType().FullName,
            physicalSectionType = physicalSection?.GetType().FullName,
            physicalSectionString = physicalSection?.ToString(),
            section = GetPropertyValue(physicalSection, "LongName"),
            properties = props
        }));
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

static object? GetPhysicalSection(object span)
{
    var elementSectionProperty = span.GetType().GetProperty("ElementSection");
    var elementSectionWrapper = elementSectionProperty?.GetValue(span);

    var elementSectionValueProperty = elementSectionWrapper?.GetType().GetProperty("Value");
    var elementSection = elementSectionValueProperty?.GetValue(elementSectionWrapper);

    var physicalSectionProperty = elementSection?.GetType().GetProperty("PhysicalSection");
    var physicalSectionWrapper = physicalSectionProperty?.GetValue(elementSection);

    var physicalSectionValueProperty = physicalSectionWrapper?.GetType().GetProperty("Value");
    var physicalSection = physicalSectionValueProperty?.GetValue(physicalSectionWrapper);

    return physicalSection;
}

static string? GetPropertyValue(object? obj, string propertyName)
{
    if (obj == null) return null;

    return obj
        .GetType()
        .GetProperty(propertyName)?
        .GetValue(obj)?
        .ToString();
}

static string InferMemberType(string name)
{
    if (name.StartsWith("BR")) return "Brace";
    if (name.StartsWith("CBase")) return "Column Base Plate";
    if (name.StartsWith("C")) return "Column";
    if (name.StartsWith("B")) return "Beam";

    return "Unknown";
}

static object? SafeGetProperty(PropertyInfo p, object obj)
{
    try { return p.GetValue(obj)?.ToString(); }
    catch { return null; }
}

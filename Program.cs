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

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
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Section name required",
                usage = "get_members_by_section <section_name>"
            }));
            return;
        }

        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        string targetSection = args[1];

        var members = await model.GetMembersAsync(null);
        var results = new List<dynamic>();

        foreach (var member in members)
        {
            try
            {
                var spans = await member.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var sectionInfo = GetSectionInfo(span);

                    string section = sectionInfo.Section;
                    string normalizedSection = sectionInfo.NormalizedSection;
                    string normalizedTargetSection = NormalizeSectionName(targetSection);

                    if (!string.Equals(
                        normalizedSection,
                        normalizedTargetSection,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string sectionType = sectionInfo.SectionType;
                    string materialType = sectionInfo.MaterialType;
                    double lengthFt = sectionInfo.LengthFt;
                    double weightPerFt = sectionInfo.WeightPerFt;
                    double spanTotalWeightLb = sectionInfo.TotalWeightLb;

                    var governing = GetGoverningCheck(span);

                    double governingUc = governing.UtilizationRatio;
                    string governingStatus = governing.Status;
                    string governingCheckType = governing.CheckType;

                    bool isFailing = governing.IsFailing;
                    bool isWarning = governing.IsWarning;
                    bool isUntested = governing.IsUntested;

                    string designCategory =
                        isFailing
                            ? "Failing"
                            : isWarning
                                ? "Warning / Review"
                                : governingUc >= 0.90
                                    ? "Near Limit"
                                    : isUntested
                                        ? "Untested / No Governing UC"
                                        : "Passing";

                    results.Add(new
                    {
                        member = member.Name,
                        member_type = InferMemberType(member.Name),
                        span = span.Name,

                        section,
                        section_type = sectionType,
                        material_type = materialType,

                        length_ft = Math.Round(lengthFt, 2),
                        weight_per_ft = Math.Round(weightPerFt, 2),
                        total_weight_lb = Math.Round(spanTotalWeightLb, 1),

                        governing_check = new
                        {
                            check_type = governingCheckType,
                            status = governingStatus,
                            utilization_ratio = Math.Round(governingUc, 3)
                        },

                        design_category = designCategory
                    });
                }
            }
            catch
            {
            }
        }

        var utilizedResults = results
            .Where(x => (double)x.governing_check.utilization_ratio > 0)
            .ToList();

        double totalLengthFt = results.Sum(x => (double)x.length_ft);
        double sectionTotalWeightLb = results.Sum(x => (double)x.total_weight_lb);

        double averageUtilization = utilizedResults.Any()
            ? utilizedResults.Average(
                x => (double)x.governing_check.utilization_ratio
            )
            : 0;

        double maximumUtilization = results.Any()
            ? results.Max(
                x => (double)x.governing_check.utilization_ratio
            )
            : 0;

        var memberTypeBreakdown = results
            .GroupBy(x => (string)x.member_type)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    count = group.Count(),

                    failing = group.Count(
                        x => (string)x.design_category == "Failing"
                    ),

                    near_limit = group.Count(
                        x => (string)x.design_category == "Near Limit"
                    ),

                    warning = group.Count(
                        x => (string)x.design_category == "Warning / Review"
                    ),

                    passing = group.Count(
                        x => (string)x.design_category == "Passing"
                    ),

                    untested = group.Count(
                        x => (string)x.design_category ==
                            "Untested / No Governing UC"
                    ),

                    average_utilization = Math.Round(
                        group
                            .Where(
                                x =>
                                    (double)x.governing_check.utilization_ratio > 0
                            )
                            .Select(
                                x =>
                                    (double)x.governing_check.utilization_ratio
                            )
                            .DefaultIfEmpty(0)
                            .Average(),
                        3
                    ),

                    maximum_utilization = Math.Round(
                        group
                            .Select(
                                x =>
                                    (double)x.governing_check.utilization_ratio
                            )
                            .DefaultIfEmpty(0)
                            .Max(),
                        3
                    )
                }
            );

        var criticalMembers = results
            .Where(
                x => (double)x.governing_check.utilization_ratio > 0
            )
            .OrderByDescending(
                x => (double)x.governing_check.utilization_ratio
            )
            .Take(10)
            .ToList();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            requested_section = targetSection,
            matched_section = results.Any()
                ? results.First().section
                : null,

            summary = new
            {
                span_count = results.Count,

                unique_member_count = results
                    .Select(x => (string)x.member)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),

                failing_count = results.Count(
                    x => (string)x.design_category == "Failing"
                ),

                near_limit_count = results.Count(
                    x => (string)x.design_category == "Near Limit"
                ),

                warning_count = results.Count(
                    x => (string)x.design_category == "Warning / Review"
                ),

                passing_count = results.Count(
                    x => (string)x.design_category == "Passing"
                ),

                untested_count = results.Count(
                    x => (string)x.design_category ==
                        "Untested / No Governing UC"
                ),

                average_utilization = Math.Round(
                    averageUtilization,
                    3
                ),

                                maximum_utilization = Math.Round(
                    maximumUtilization,
                    3
                ),

                total_length_ft = Math.Round(totalLengthFt, 2),
                total_weight_lb = Math.Round(sectionTotalWeightLb, 1),
                total_weight_tons = Math.Round(sectionTotalWeightLb / 2000.0, 2)
            },

            member_type_breakdown = memberTypeBreakdown,
            top_10_critical_members = criticalMembers,

            members = results
                .OrderByDescending(
                    x => (double)x.governing_check.utilization_ratio
                )
                .ThenBy(x => (string)x.member)
                .ToList()
        }));
    }
    else if (command == "get_tsd_section_usage")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        var members = await model.GetMembersAsync(null);
        var rows = new List<dynamic>();

        foreach (var member in members)
        {
            try
            {
                var spans = await member.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var sectionInfo = GetSectionInfo(span);

                    string section = sectionInfo.Section;
                    string sectionType = sectionInfo.SectionType;
                    string materialType = sectionInfo.MaterialType;
                    double lengthFt = sectionInfo.LengthFt;
                    double weightPerFt = sectionInfo.WeightPerFt;
                    double totalWeightLb = sectionInfo.TotalWeightLb;

                    var governing = GetGoverningCheck(span);

                    double governingUc = governing.UtilizationRatio;
                    string governingStatus = governing.Status;
                    string governingCheckType = governing.CheckType;

                    bool isFailing = governing.IsFailing;
                    bool isWarning = governing.IsWarning;
                    bool isUntested = governing.IsUntested;

                    string designCategory =
                        isFailing
                            ? "Failing"
                            : isWarning
                                ? "Warning / Review"
                                : governingUc >= 0.90
                                    ? "Near Limit"
                                    : isUntested
                                        ? "Untested / No Governing UC"
                                        : "Passing";

                    rows.Add(new
                    {
                        member = member.Name,
                        member_type = InferMemberType(member.Name),
                        span = span.Name,

                        section,
                        normalized_section = sectionInfo.NormalizedSection,
                        section_type = sectionType,
                        material_type = materialType,

                        length_ft = lengthFt,
                        weight_per_ft = weightPerFt,
                        total_weight_lb = totalWeightLb,

                        governing_check = new
                        {
                            check_type = governingCheckType,
                            status = governingStatus,
                            utilization_ratio = governingUc
                        },

                        design_category = designCategory
                    });
                }
            }
            catch
            {
            }
        }

        var sectionUsage = rows
            .GroupBy(x => (string)x.normalized_section)
            .Select(group =>
            {
                var utilized = group
                    .Where(x =>
                        (double)x.governing_check.utilization_ratio > 0
                    )
                    .ToList();

                var criticalMember = group
                    .Where(x =>
                        (double)x.governing_check.utilization_ratio > 0
                    )
                    .OrderByDescending(x =>
                        (double)x.governing_check.utilization_ratio
                    )
                    .FirstOrDefault();

                var memberTypeBreakdown = group
                    .GroupBy(x => (string)x.member_type)
                    .ToDictionary(
                        typeGroup => typeGroup.Key,
                        typeGroup => new
                        {
                            span_count = typeGroup.Count(),

                            unique_member_count = typeGroup
                                .Select(x => (string)x.member)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count(),

                            total_length_ft = Math.Round(
                                typeGroup.Sum(x => (double)x.length_ft),
                                2
                            ),

                            total_weight_lb = Math.Round(
                                typeGroup.Sum(x => (double)x.total_weight_lb),
                                1
                            )
                        }
                    );

                return new
                {
                    section = (string)group.First().section,
                    section_type = (string)group.First().section_type,
                    material_type = (string)group.First().material_type,

                    span_count = group.Count(),

                    unique_member_count = group
                        .Select(x => (string)x.member)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),

                    failing_count = group.Count(x =>
                        (string)x.design_category == "Failing"
                    ),

                    near_limit_count = group.Count(x =>
                        (string)x.design_category == "Near Limit"
                    ),

                    warning_count = group.Count(x =>
                        (string)x.design_category == "Warning / Review"
                    ),

                    passing_count = group.Count(x =>
                        (string)x.design_category == "Passing"
                    ),

                    untested_count = group.Count(x =>
                        (string)x.design_category ==
                        "Untested / No Governing UC"
                    ),

                    average_utilization = Math.Round(
                        utilized
                            .Select(x =>
                                (double)x.governing_check.utilization_ratio
                            )
                            .DefaultIfEmpty(0)
                            .Average(),
                        3
                    ),

                    maximum_utilization = Math.Round(
                        group
                            .Select(x =>
                                (double)x.governing_check.utilization_ratio
                            )
                            .DefaultIfEmpty(0)
                            .Max(),
                        3
                    ),

                    total_length_ft = Math.Round(
                        group.Sum(x => (double)x.length_ft),
                        2
                    ),

                    weight_per_ft = Math.Round(
                        group
                            .Select(x => (double)x.weight_per_ft)
                            .DefaultIfEmpty(0)
                            .Average(),
                        2
                    ),

                    total_weight_lb = Math.Round(
                        group.Sum(x => (double)x.total_weight_lb),
                        1
                    ),

                    total_weight_tons = Math.Round(
                        group.Sum(x => (double)x.total_weight_lb) / 2000.0,
                        2
                    ),

                    member_type_breakdown = memberTypeBreakdown,

                    critical_member = criticalMember == null
                        ? null
                        : new
                        {
                            member = (string)criticalMember.member,
                            member_type = (string)criticalMember.member_type,
                            span = (string)criticalMember.span,

                            governing_check = new
                            {
                                check_type =
                                    (string)criticalMember
                                        .governing_check
                                        .check_type,

                                status =
                                    (string)criticalMember
                                        .governing_check
                                        .status,

                                utilization_ratio = Math.Round(
                                    (double)criticalMember
                                        .governing_check
                                        .utilization_ratio,
                                    3
                                )
                            },

                            design_category =
                                (string)criticalMember.design_category
                        }
                };
            })
            .OrderByDescending(x => x.maximum_utilization)
            .ThenByDescending(x => x.total_weight_lb)
            .ToList();

        double modelTotalLengthFt = rows.Sum(x => (double)x.length_ft);
        double modelTotalWeightLb = rows.Sum(x => (double)x.total_weight_lb);

        var criticalSections = sectionUsage
            .Where(x => x.maximum_utilization > 0)
            .OrderByDescending(x => x.maximum_utilization)
            .Take(10)
            .ToList();

        var heaviestSections = sectionUsage
            .OrderByDescending(x => x.total_weight_lb)
            .Take(10)
            .ToList();

        var mostUsedSections = sectionUsage
            .OrderByDescending(x => x.unique_member_count)
            .ThenByDescending(x => x.span_count)
            .Take(10)
            .ToList();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            summary = new
            {
                unique_section_count = sectionUsage.Count,
                total_span_count = rows.Count,

                total_unique_member_count = rows
                    .Select(x => (string)x.member)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),

                failing_span_count = rows.Count(x =>
                    (string)x.design_category == "Failing"
                ),

                near_limit_span_count = rows.Count(x =>
                    (string)x.design_category == "Near Limit"
                ),

                warning_span_count = rows.Count(x =>
                    (string)x.design_category == "Warning / Review"
                ),

                passing_span_count = rows.Count(x =>
                    (string)x.design_category == "Passing"
                ),

                untested_span_count = rows.Count(x =>
                    (string)x.design_category ==
                    "Untested / No Governing UC"
                ),

                total_length_ft = Math.Round(modelTotalLengthFt, 2),
                total_weight_lb = Math.Round(modelTotalWeightLb, 1),
                total_weight_tons = Math.Round(modelTotalWeightLb / 2000.0, 2)
            },

            top_10_critical_sections = criticalSections,
            top_10_heaviest_sections = heaviestSections,
            top_10_most_used_sections = mostUsedSections,

            sections = sectionUsage
        }));
    }
    else if (command == "get_tsd_member_schedule")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        string? memberTypeFilter = args.Length >= 2
            ? args[1]
            : null;

        var members = await model.GetMembersAsync(null);
        var scheduleRows = new List<dynamic>();

        foreach (var member in members)
        {
            string memberType = InferMemberType(member.Name);

            if (
                !string.IsNullOrWhiteSpace(memberTypeFilter) &&
                !string.Equals(
                    memberType,
                    memberTypeFilter,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }

            try
            {
                var spans = await member.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var sectionInfo = GetSectionInfo(span);

                    string section = sectionInfo.Section;
                    string sectionType = sectionInfo.SectionType;
                    string materialType = sectionInfo.MaterialType;
                    double lengthFt = sectionInfo.LengthFt;
                    double weightPerFt = sectionInfo.WeightPerFt;
                    double totalWeightLb = sectionInfo.TotalWeightLb;

                    var governing = GetGoverningCheck(span);

                    double governingUc = governing.UtilizationRatio;
                    string governingStatus = governing.Status;
                    string governingCheckType = governing.CheckType;

                    bool isFailing = governing.IsFailing;
                    bool isWarning = governing.IsWarning;
                    bool isUntested = governing.IsUntested;

                    string designCategory =
                        isFailing
                            ? "Failing"
                            : isWarning
                                ? "Warning / Review"
                                : governingUc >= 0.90
                                    ? "Near Limit"
                                    : isUntested
                                        ? "Untested / No Governing UC"
                                        : "Passing";

                    scheduleRows.Add(new
                    {
                        member = member.Name,
                        member_type = memberType,
                        span = span.Name,

                        section,
                        normalized_section = sectionInfo.NormalizedSection,
                        section_type = sectionType,
                        material_type = materialType,

                        length_ft = Math.Round(lengthFt, 2),
                        weight_per_ft = Math.Round(weightPerFt, 2),
                        total_weight_lb = Math.Round(totalWeightLb, 1),

                        governing_check = new
                        {
                            check_type = governingCheckType,
                            status = governingStatus,
                            utilization_ratio = Math.Round(governingUc, 3)
                        },

                        design_category = designCategory
                    });
                }
            }
            catch
            {
            }
        }

        var groupedSchedule = scheduleRows
            .GroupBy(row => new
            {
                Section = (string)row.normalized_section,
                MemberType = (string)row.member_type,
                MaterialType = (string)row.material_type
            })
            .Select(group =>
            {
                var utilizedRows = group
                    .Where(row =>
                        (double)row.governing_check.utilization_ratio > 0
                    )
                    .ToList();

                var criticalRow = utilizedRows
                    .OrderByDescending(row =>
                        (double)row.governing_check.utilization_ratio
                    )
                    .FirstOrDefault();

                double groupTotalLengthFt = group.Sum(row =>
                    (double)row.length_ft
                );

                double groupTotalWeightLb = group.Sum(row =>
                    (double)row.total_weight_lb
                );

                return new
                {
                    section = (string)group.First().section,
                    section_type = (string)group.First().section_type,
                    material_type = group.Key.MaterialType,
                    member_type = group.Key.MemberType,

                    quantity = group
                        .Select(row => (string)row.member)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),

                    span_count = group.Count(),

                    total_length_ft = Math.Round(
                        groupTotalLengthFt,
                        2
                    ),

                    average_length_ft = Math.Round(
                        group
                            .Select(row => (double)row.length_ft)
                            .DefaultIfEmpty(0)
                            .Average(),
                        2
                    ),

                    minimum_length_ft = Math.Round(
                        group
                            .Select(row => (double)row.length_ft)
                            .DefaultIfEmpty(0)
                            .Min(),
                        2
                    ),

                    maximum_length_ft = Math.Round(
                        group
                            .Select(row => (double)row.length_ft)
                            .DefaultIfEmpty(0)
                            .Max(),
                        2
                    ),

                    weight_per_ft = Math.Round(
                        group
                            .Select(row => (double)row.weight_per_ft)
                            .DefaultIfEmpty(0)
                            .Average(),
                        2
                    ),

                    total_weight_lb = Math.Round(
                        groupTotalWeightLb,
                        1
                    ),

                    total_weight_tons = Math.Round(
                        groupTotalWeightLb / 2000.0,
                        2
                    ),

                    failing_count = group.Count(row =>
                        (string)row.design_category == "Failing"
                    ),

                    near_limit_count = group.Count(row =>
                        (string)row.design_category == "Near Limit"
                    ),

                    warning_count = group.Count(row =>
                        (string)row.design_category == "Warning / Review"
                    ),

                    passing_count = group.Count(row =>
                        (string)row.design_category == "Passing"
                    ),

                    untested_count = group.Count(row =>
                        (string)row.design_category ==
                        "Untested / No Governing UC"
                    ),

                    average_utilization = Math.Round(
                        utilizedRows
                            .Select(row =>
                                (double)row
                                    .governing_check
                                    .utilization_ratio
                            )
                            .DefaultIfEmpty(0)
                            .Average(),
                        3
                    ),

                    maximum_utilization = Math.Round(
                        group
                            .Select(row =>
                                (double)row
                                    .governing_check
                                    .utilization_ratio
                            )
                            .DefaultIfEmpty(0)
                            .Max(),
                        3
                    ),

                    critical_member = criticalRow == null
                        ? null
                        : new
                        {
                            member = (string)criticalRow.member,
                            span = (string)criticalRow.span,

                            utilization_ratio = Math.Round(
                                (double)criticalRow
                                    .governing_check
                                    .utilization_ratio,
                                3
                            ),

                            status =
                                (string)criticalRow
                                    .governing_check
                                    .status,

                            check_type =
                                (string)criticalRow
                                    .governing_check
                                    .check_type,

                            design_category =
                                (string)criticalRow.design_category
                        },

                    members = group
                        .Select(row => (string)row.member)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name)
                        .ToList()
                };
            })
            .OrderBy(row => row.member_type)
            .ThenBy(row => row.section)
            .ToList();

        var failingMembers = scheduleRows
            .Where(row =>
                (string)row.design_category == "Failing"
            )
            .OrderByDescending(row =>
                (double)row.governing_check.utilization_ratio
            )
            .ToList();

        var nearLimitMembers = scheduleRows
            .Where(row =>
                (string)row.design_category == "Near Limit"
            )
            .OrderByDescending(row =>
                (double)row.governing_check.utilization_ratio
            )
            .ToList();

        double scheduleTotalLengthFt = scheduleRows.Sum(row =>
            (double)row.length_ft
        );

        double scheduleTotalWeightLb = scheduleRows.Sum(row =>
            (double)row.total_weight_lb
        );

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            requested_member_type = memberTypeFilter,

            filter_applied =
                !string.IsNullOrWhiteSpace(memberTypeFilter),

            summary = new
            {
                schedule_group_count = groupedSchedule.Count,

                total_span_count = scheduleRows.Count,

                total_unique_member_count = scheduleRows
                    .Select(row => (string)row.member)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),

                failing_count = failingMembers.Count,

                near_limit_count = nearLimitMembers.Count,

                warning_count = scheduleRows.Count(row =>
                    (string)row.design_category == "Warning / Review"
                ),

                passing_count = scheduleRows.Count(row =>
                    (string)row.design_category == "Passing"
                ),

                untested_count = scheduleRows.Count(row =>
                    (string)row.design_category ==
                    "Untested / No Governing UC"
                ),

                total_length_ft = Math.Round(
                    scheduleTotalLengthFt,
                    2
                ),

                total_weight_lb = Math.Round(
                    scheduleTotalWeightLb,
                    1
                ),

                total_weight_tons = Math.Round(
                    scheduleTotalWeightLb / 2000.0,
                    2
                )
            },

            grouped_schedule = groupedSchedule,

            top_25_failing_members = failingMembers
                .Take(25)
                .ToList(),

            top_25_near_limit_members = nearLimitMembers
                .Take(25)
                .ToList(),

            member_schedule = scheduleRows
                .OrderBy(row => (string)row.member_type)
                .ThenBy(row => (string)row.section)
                .ThenBy(row => (string)row.member)
                .ToList()
        }));
    }
    else if (command == "get_tsd_optimization_candidates")
    {
        const double MmPerFt = 304.8;
        const double TsdMassToPlf = 671.9689751395068;

        string mode = args.Length >= 2
            ? args[1].Trim().ToLowerInvariant()
            : "all";

        double optimizationThreshold = 0.50;

        if (args.Length >= 3)
        {
            if (
                !double.TryParse(
                    args[2],
                    out optimizationThreshold
                )
            )
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    error = "Optimization threshold must be a number.",
                    usage =
                        "get_tsd_optimization_candidates [all|failing|underutilized] [maximum_utilization]"
                }));

                return;
            }
        }

        var validModes = new[]
        {
        "all",
        "failing",
        "underutilized"
    };

        if (!validModes.Contains(mode))
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = $"Unknown mode: {mode}",
                valid_modes = validModes,
                usage =
                    "get_tsd_optimization_candidates [all|failing|underutilized] [maximum_utilization]"
            }));

            return;
        }

        if (
            optimizationThreshold <= 0 ||
            optimizationThreshold >= 1
        )
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error =
                    "Optimization threshold must be greater than 0 and less than 1."
            }));

            return;
        }

        var members = await model.GetMembersAsync(null);
        var reviewedSpans = new List<dynamic>();

        foreach (var member in members)
        {
            try
            {
                var spans = await member.GetSpanAsync(null);

                foreach (var span in spans)
                {
                    var sectionInfo = GetSectionInfo(span, steelOnlyWeight: true);

                    string section = sectionInfo.Section;
                    string sectionType = sectionInfo.SectionType;
                    string materialType = sectionInfo.MaterialType;
                    double lengthFt = sectionInfo.LengthFt;
                    double weightPerFt = sectionInfo.WeightPerFt;
                    double currentWeightLb = sectionInfo.TotalWeightLb;

                    var governing = GetGoverningCheck(span);

                    double governingUc = governing.UtilizationRatio;
                    string governingStatus = governing.Status;
                    string governingCheckType = governing.CheckType;

                    bool isFailing = governing.IsFailing;
                    bool isWarning = governing.IsWarning;
                    bool isUntested = governing.IsUntested;

                    bool isNearLimit =
                        !isFailing
                        &&
                        !isUntested
                        &&
                        (
                            isWarning
                            ||
                            governingUc >= 0.90
                        );

                    bool isUnderutilized =
                        !isFailing
                        &&
                        !isWarning
                        &&
                        !isUntested
                        &&
                        governingUc < optimizationThreshold;

                    string reviewCategory;

                    if (
                        governingStatus.Equals(
                            "Beyond",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        reviewCategory =
                            "Beyond Limit - Engineering Review Required";
                    }
                    else if (isFailing)
                    {
                        reviewCategory =
                            "Strengthening / Failure Review Required";
                    }
                    else if (isWarning)
                    {
                        reviewCategory =
                            "Warning - Retain / Review";
                    }
                    else if (isNearLimit)
                    {
                        reviewCategory =
                            "Near Limit - Retain / Review";
                    }
                    else if (isUnderutilized)
                    {
                        reviewCategory =
                            governingUc < 0.25
                                ? "High Optimization Potential"
                                : governingUc < 0.40
                                    ? "Moderate Optimization Potential"
                                    : "Low Optimization Potential";
                    }
                    else if (isUntested)
                    {
                        reviewCategory =
                            "Untested - Do Not Optimize";
                    }
                    else
                    {
                        reviewCategory =
                            "Reasonably Utilized";
                    }

                    string recommendedAction;

                    if (
                        governingStatus.Equals(
                            "Beyond",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        recommendedAction =
                            "Review the TSD Beyond status and its underlying check before " +
                            "making any section change. The result may be outside the " +
                            "applicability or accepted design limits.";
                    }
                    else if (isFailing)
                    {
                        recommendedAction =
                            "Review the failed TSD check, section capacity, restraint, " +
                            "unbraced length, loading, serviceability, and other design " +
                            "conditions. Rerun analysis and design before accepting changes.";
                    }
                    else if (isWarning)
                    {
                        recommendedAction =
                            "Review the TSD warning before considering optimization. " +
                            "Do not treat this member as a normal passing member based " +
                            "only on its utilization ratio.";
                    }
                    else if (isUnderutilized)
                    {
                        recommendedAction =
                            "Review whether a lighter section could satisfy strength, " +
                            "stability, deflection, vibration, connection, fire, " +
                            "constructability, and standardization requirements. " +
                            "Do not reduce the section without rerunning analysis and design.";
                    }
                    else if (isNearLimit)
                    {
                        recommendedAction =
                            "Do not prioritize for weight reduction. Review reserve " +
                            "capacity, serviceability, connections, and future loading.";
                    }
                    else if (isUntested)
                    {
                        recommendedAction =
                            "Obtain valid design results before considering optimization.";
                    }
                    else
                    {
                        recommendedAction =
                            "No immediate optimization action recommended.";
                    }

                    double tenPercentScenarioLb =
                        isUnderutilized
                            ? currentWeightLb * 0.10
                            : 0;

                    double fifteenPercentScenarioLb =
                        isUnderutilized
                            ? currentWeightLb * 0.15
                            : 0;

                    reviewedSpans.Add(new
                    {
                        member = member.Name,
                        member_type =
                            InferMemberType(member.Name),

                        span = span.Name,

                        section,
                        normalized_section =
                            NormalizeSectionName(section),

                        section_type = sectionType,
                        material_type = materialType,

                        length_ft =
                            Math.Round(lengthFt, 2),

                        weight_per_ft =
                            Math.Round(weightPerFt, 2),

                        current_weight_lb =
                            Math.Round(currentWeightLb, 1),

                        governing_check = new
                        {
                            check_type = governing.CheckType,
                            status = governing.Status,
                            utilization_ratio = Math.Round(
                                governing.UtilizationRatio,
                                3
                            ),
                            status_priority = governing.StatusPriority,
                            status_interpretation = governing.StatusInterpretation,
                            status_driven_failure = governing.StatusDrivenFailure
                        },

                        is_failing = isFailing,
                        is_warning = isWarning,
                        is_near_limit = isNearLimit,
                        is_underutilized =
                            isUnderutilized,

                        is_untested = isUntested,

                        review_category =
                            reviewCategory,

                        recommended_action =
                            recommendedAction,

                        illustrative_weight_reduction = new
                        {
                            ten_percent_lighter_lb =
                                Math.Round(
                                    tenPercentScenarioLb,
                                    1
                                ),

                            fifteen_percent_lighter_lb =
                                Math.Round(
                                    fifteenPercentScenarioLb,
                                    1
                                ),

                            note =
                                isUnderutilized
                                    ? "These are screening scenarios only, not a validated section replacement."
                                    : null
                        }
                    });
                }
            }
            catch
            {
            }
        }

        var failingCandidates = reviewedSpans
            .Where(row =>
                (bool)row.is_failing
            )
            .OrderByDescending(row =>
                (double)row
                    .governing_check
                    .utilization_ratio
            )
            .ToList();

        var underutilizedCandidates = reviewedSpans
            .Where(row =>
                (bool)row.is_underutilized
            )
            .OrderBy(row =>
                (double)row
                    .governing_check
                    .utilization_ratio
            )
            .ThenByDescending(row =>
                (double)row.current_weight_lb
            )
            .ToList();

        var nearLimitMembers = reviewedSpans
            .Where(row =>
                (bool)row.is_near_limit
            )
            .OrderByDescending(row =>
                (double)row
                    .governing_check
                    .utilization_ratio
            )
            .ToList();

        var untestedMembers = reviewedSpans
            .Where(row =>
                (bool)row.is_untested
            )
            .ToList();

        var optimizationBySection =
            underutilizedCandidates
                .GroupBy(row => new
                {
                    Section =
                        (string)row.normalized_section,

                    MemberType =
                        (string)row.member_type,

                    MaterialType =
                        (string)row.material_type
                })
                .Select(group =>
                {
                    double currentGroupWeightLb =
                        group.Sum(row =>
                            (double)row.current_weight_lb
                        );

                    return new
                    {
                        section =
                            (string)group.First().section,

                        member_type =
                            group.Key.MemberType,

                        material_type =
                            group.Key.MaterialType,

                        candidate_span_count =
                            group.Count(),

                        unique_member_count =
                            group
                                .Select(row =>
                                    (string)row.member
                                )
                                .Distinct(
                                    StringComparer
                                        .OrdinalIgnoreCase
                                )
                                .Count(),

                        average_utilization =
                            Math.Round(
                                group
                                    .Select(row =>
                                        (double)row
                                            .governing_check
                                            .utilization_ratio
                                    )
                                    .DefaultIfEmpty(0)
                                    .Average(),
                                3
                            ),

                        maximum_utilization =
                            Math.Round(
                                group
                                    .Select(row =>
                                        (double)row
                                            .governing_check
                                            .utilization_ratio
                                    )
                                    .DefaultIfEmpty(0)
                                    .Max(),
                                3
                            ),

                        total_length_ft =
                            Math.Round(
                                group.Sum(row =>
                                    (double)row.length_ft
                                ),
                                2
                            ),

                        current_weight_lb =
                            Math.Round(
                                currentGroupWeightLb,
                                1
                            ),

                        current_weight_tons =
                            Math.Round(
                                currentGroupWeightLb /
                                2000.0,
                                2
                            ),

                        illustrative_10_percent_savings_lb =
                            Math.Round(
                                currentGroupWeightLb *
                                0.10,
                                1
                            ),

                        illustrative_15_percent_savings_lb =
                            Math.Round(
                                currentGroupWeightLb *
                                0.15,
                                1
                            ),

                        critical_candidate =
                            group
                                .OrderByDescending(row =>
                                    (double)row
                                        .current_weight_lb
                                )
                                .Select(row => new
                                {
                                    member =
                                        (string)row.member,

                                    span =
                                        (string)row.span,

                                    utilization_ratio =
                                        (double)row
                                            .governing_check
                                            .utilization_ratio,

                                    current_weight_lb =
                                        (double)row
                                            .current_weight_lb
                                })
                                .FirstOrDefault()
                    };
                })
                .OrderByDescending(group =>
                    group.current_weight_lb
                )
                .ToList();

        var strengtheningBySection =
            failingCandidates
                .GroupBy(row => new
                {
                    Section =
                        (string)row.normalized_section,

                    MemberType =
                        (string)row.member_type
                })
                .Select(group => new
                {
                    section =
                        (string)group.First().section,

                    member_type =
                        group.Key.MemberType,

                    failing_span_count =
                        group.Count(),

                    unique_member_count =
                        group
                            .Select(row =>
                                (string)row.member
                            )
                            .Distinct(
                                StringComparer
                                    .OrdinalIgnoreCase
                            )
                            .Count(),

                    maximum_utilization =
                        Math.Round(
                            group.Max(row =>
                                (double)row
                                    .governing_check
                                    .utilization_ratio
                            ),
                            3
                        ),

                    critical_member =
                        group
                            .OrderByDescending(row =>
                                (double)row
                                    .governing_check
                                    .utilization_ratio
                            )
                            .Select(row => new
                            {
                                member =
                                    (string)row.member,

                                span =
                                    (string)row.span,

                                utilization_ratio =
                                    (double)row
                                        .governing_check
                                        .utilization_ratio,

                                check_type =
                                    (string)row
                                        .governing_check
                                        .check_type
                            })
                            .FirstOrDefault()
                })
                .OrderByDescending(group =>
                    group.maximum_utilization
                )
                .ToList();

        double candidateCurrentWeightLb =
            underutilizedCandidates.Sum(row =>
                (double)row.current_weight_lb
            );

        double tenPercentPotentialLb =
            candidateCurrentWeightLb * 0.10;

        double fifteenPercentPotentialLb =
            candidateCurrentWeightLb * 0.15;

        object selectedResults =
            mode == "failing"
                ? failingCandidates
                : mode == "underutilized"
                    ? underutilizedCandidates
                    : new
                    {
                        failing =
                            failingCandidates,

                        underutilized =
                            underutilizedCandidates,

                        near_limit =
                            nearLimitMembers,

                        untested =
                            untestedMembers
                    };

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            tool_version = "1.0",

            mode,

            optimization_threshold =
                optimizationThreshold,

            engineering_scope = new
            {
                purpose =
                    "Screen members for strengthening review or potential weight optimization.",

                limitation =
                    "This tool does not select or validate replacement sections. Every proposed design change must be rerun through structural analysis and design checks.",

                exclusions =
                    "Untested members and members without a governing utilization ratio are not treated as valid optimization candidates."
            },

            summary = new
            {
                total_spans_reviewed =
                    reviewedSpans.Count,

                failing_candidate_count =
                    failingCandidates.Count,

                underutilized_candidate_count =
                    underutilizedCandidates.Count,

                near_limit_count =
                    nearLimitMembers.Count,

                untested_count =
                    untestedMembers.Count,

                current_weight_of_underutilized_candidates_lb =
                    Math.Round(
                        candidateCurrentWeightLb,
                        1
                    ),

                current_weight_of_underutilized_candidates_tons =
                    Math.Round(
                        candidateCurrentWeightLb /
                        2000.0,
                        2
                    ),

                illustrative_10_percent_savings_lb =
                    Math.Round(
                        tenPercentPotentialLb,
                        1
                    ),

                illustrative_10_percent_savings_tons =
                    Math.Round(
                        tenPercentPotentialLb /
                        2000.0,
                        2
                    ),

                illustrative_15_percent_savings_lb =
                    Math.Round(
                        fifteenPercentPotentialLb,
                        1
                    ),

                illustrative_15_percent_savings_tons =
                    Math.Round(
                        fifteenPercentPotentialLb /
                        2000.0,
                        2
                    )
            },

            top_25_strengthening_priorities =
                failingCandidates
                    .Take(25)
                    .ToList(),

            top_25_optimization_priorities =
                underutilizedCandidates
                    .Take(25)
                    .ToList(),

            optimization_by_section =
                optimizationBySection,

            strengthening_by_section =
                strengtheningBySection,

            results = selectedResults
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
    else if (command == "get_tsd_support_reactions")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Usage: get_tsd_support_reactions <combination_reference_index_or_name> [support_name]"
            }));
            return;
        }

        string comboInput = args[1];
        string? supportFilter = args.Length >= 3 ? args[2] : null;

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

        var reactionSets = await model.TabularResultsAccessor.Analysis
            .GetFoundationReactionsAsync(
                comboId,
                analysisType,
                TSD.API.Remoting.Foundations.FoundationReactionsAxisSystem.Global,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                default);

        var reactionSet = reactionSets.FirstOrDefault();

        if (reactionSet == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                requested_combination = comboInput,
                requested_support = supportFilter,
                filter_applied = !string.IsNullOrWhiteSpace(supportFilter),
                resolved_combination = comboName,
                combination_id = comboId,
                combination_reference_index = comboReferenceIndexResolved,

                combination_class = GetWrappedValue(combo, "CombinationClass")?.ToString(),
                factoring_type = GetWrappedValue(combo, "FactoringType")?.ToString(),
                is_active = GetWrappedValue(combo, "IsActive"),
                is_strength = GetWrappedValue(combo, "IsStrength"),
                is_service = GetWrappedValue(combo, "IsService"),

                analysis_type = analysisType.ToString(),
                axis_system = "Global",
                support_count = 0,
                reactions = new List<object>(),
                summary = (object?)null
            }));
            return;
        }

        var reactions = reactionSet.SupportData
            .Where(r =>
                string.IsNullOrWhiteSpace(supportFilter) ||
                r.SupportName.Contains(supportFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.SupportName)
            .Select(r => new
            {
                support = r.SupportName,
                rotation = Math.Round(r.Rotation, 3),

                fx = Math.Round(r.Force.Fx, 3),
                fy = Math.Round(r.Force.Fy, 3),
                fz = Math.Round(r.Force.Fz, 3),

                mx = Math.Round(r.Force.Mx, 3),
                my = Math.Round(r.Force.My, 3),
                mz = Math.Round(r.Force.Mz, 3)
            })
            .ToList();

        object? summary = null;

        if (reactions.Any())
        {
            var maxVertical = reactions.OrderByDescending(r => r.fz).First();
            var upliftSupports = reactions.Where(r => r.fz < 0).ToList();

            var maxAbsFx = reactions.OrderByDescending(r => Math.Abs(r.fx)).First();
            var maxAbsFy = reactions.OrderByDescending(r => Math.Abs(r.fy)).First();
            var maxAbsMz = reactions.OrderByDescending(r => Math.Abs(r.mz)).First();

            summary = new
            {
                support_count = reactions.Count,

                max_vertical_reaction = new
                {
                    support = maxVertical.support,
                    value = maxVertical.fz,
                    engineering_significance = "Largest compression reaction. Useful for foundation sizing."
                },

                max_uplift = upliftSupports.Any()
                    ? new
                    {
                        support = upliftSupports.OrderBy(r => r.fz).First().support,
                        value = upliftSupports.OrderBy(r => r.fz).First().fz,
                        engineering_significance = "Largest uplift reaction. Review anchor rod tension and footing stability."
                    }
                    : null,

                max_horizontal_fx = new
                {
                    support = maxAbsFx.support,
                    value = maxAbsFx.fx,
                    engineering_significance = "Largest global X reaction. Useful for foundation and lateral system design."
                },

                max_horizontal_fy = new
                {
                    support = maxAbsFy.support,
                    value = maxAbsFy.fy,
                    engineering_significance = "Largest global Y reaction. Useful for foundation and lateral system design."
                },

                max_reaction_mz = new
                {
                    support = maxAbsMz.support,
                    value = maxAbsMz.mz,
                    engineering_significance = "Largest reaction moment about the global Z-axis. Review footing moment demand."
                }
            };
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            requested_combination = comboInput,
            requested_support = supportFilter,
            filter_applied = !string.IsNullOrWhiteSpace(supportFilter),
            engineering_note = !string.IsNullOrWhiteSpace(supportFilter)
        ? "Summary values are based only on the filtered supports."
        : "Summary values are based on all returned supports.",

            resolved_combination = comboName,
            combination_id = comboId,
            combination_reference_index = comboReferenceIndexResolved,

            combination_class = GetWrappedValue(combo, "CombinationClass")?.ToString(),
            factoring_type = GetWrappedValue(combo, "FactoringType")?.ToString(),
            is_active = GetWrappedValue(combo, "IsActive"),
            is_strength = GetWrappedValue(combo, "IsStrength"),
            is_service = GetWrappedValue(combo, "IsService"),

            analysis_type = analysisType.ToString(),
            coordinate_system = "Global",
            support_count = reactions.Count,
            summary,
            reactions
        }));
    }
    else if (command == "get_tsd_governing_load_combo")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Usage: get_tsd_governing_load_combo <member_name> [active_strength_combinations|all]"
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
            catch { return null; }
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
                        combination = combo.Name,
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
                // Skip combinations that do not return force data.
            }
        }

        object? BuildGoverning(
            Func<dynamic, double> selector,
            string componentLabel,
            string positiveReason,
            string negativeReason)
        {
            if (!records.Any())
                return null;

            var governingRecord = records
                .OrderByDescending(r => Math.Abs(selector(r)))
                .First();

            double value = selector(governingRecord);

            if (Math.Abs(value) < 0.0001)
                return null;

            string reason = value >= 0 ? positiveReason : negativeReason;

            return new
            {
                component = componentLabel,
                combination = governingRecord.combination,
                reference_index = governingRecord.reference_index,
                value = Math.Round(value, 3),
                absolute_value = Math.Round(Math.Abs(value), 3),
                position_ft = governingRecord.position_ft,
                end = governingRecord.end,
                governing_reason = reason,
                engineering_significance = $"This load combination produces the governing {componentLabel.ToLower()} demand."
            };
        }

        var axial = BuildGoverning(
            r => r.axial_force,
            "Axial Force",
            "Maximum tension governs axial force.",
            "Maximum compression governs axial force."
        );

        var shearMajor = BuildGoverning(
            r => r.shear_major,
            "Major Shear",
            "Maximum positive major-axis shear governs.",
            "Maximum negative major-axis shear governs."
        );

        var shearMinor = BuildGoverning(
            r => r.shear_minor,
            "Minor Shear",
            "Maximum positive weak-axis shear governs.",
            "Maximum negative weak-axis shear governs."
        );

        var momentMajor = BuildGoverning(
            r => r.moment_major,
            "Major Moment",
            "Maximum positive strong-axis bending governs.",
            "Maximum negative strong-axis bending governs."
        );

        var momentMinor = BuildGoverning(
            r => r.moment_minor,
            "Minor Moment",
            "Maximum positive weak-axis bending governs.",
            "Maximum negative weak-axis bending governs."
        );

        var torsionResult = BuildGoverning(
            r => r.torsion,
            "Torsion",
            "Maximum positive torsion governs.",
            "Maximum negative torsion governs."
        );

        var deflectionMajor = BuildGoverning(
            r => r.deflection_major,
            "Major Deflection",
            "Maximum positive major-axis deflection governs.",
            "Maximum negative major-axis deflection governs."
        );

        var deflectionMinor = BuildGoverning(
            r => r.deflection_minor,
            "Minor Deflection",
            "Maximum positive minor-axis deflection governs.",
            "Maximum negative minor-axis deflection governs."
        );

        var governingItems = new Dictionary<string, dynamic?>
        {
            ["Axial Force"] = axial,
            ["Major Shear"] = shearMajor,
            ["Minor Shear"] = shearMinor,
            ["Major Moment"] = momentMajor,
            ["Minor Moment"] = momentMinor,
            ["Torsion"] = torsionResult,
            ["Major Deflection"] = deflectionMajor,
            ["Minor Deflection"] = deflectionMinor
        };

        var governingSummary = governingItems
            .Where(x => x.Value != null)
            .GroupBy(x => (string)x.Value.combination)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Key).ToList()
            );

        var overallGroup = governingItems
            .Where(x => x.Value != null)
            .GroupBy(x => new
            {
                Combination = (string)x.Value.combination,
                ReferenceIndex = (int)x.Value.reference_index
            })
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        object? overallGoverningCombination = overallGroup == null
            ? null
            : new
            {
                combination = overallGroup.Key.Combination,
                reference_index = overallGroup.Key.ReferenceIndex,
                governs = overallGroup.Select(x => x.Key).ToList(),
                engineering_significance =
                    "This load combination governs the largest number of force components for this member."
            };

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = memberName,
            analysis_type = analysisType.ToString(),
            mode,

            combination_filter = mode == "all"
                ? "All load combinations"
                : "Active strength load combinations only",

            combinations_checked = selectedCombos.Count,
            force_records_found = records.Count,

            performance_note = mode == "all"
                ? "Mode 'all' checks every load combination and may take significantly longer on large models."
                : null,

            governing_load_combinations = new
            {
                axial_force = axial,
                shear_major = shearMajor,
                shear_minor = shearMinor,
                moment_major = momentMajor,
                moment_minor = momentMinor,
                torsion = torsionResult,
                deflection_major = deflectionMajor,
                deflection_minor = deflectionMinor
            },

            governing_summary = governingSummary,
            overall_governing_combination = overallGoverningCombination
        }));
    }
    else if (command == "get_tsd_member_design_summary")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Usage: get_tsd_member_design_summary <member_name>"
            }));
            return;
        }

        string targetName = args[1];

        var members = await model.GetMembersAsync(null);
        var member = members.FirstOrDefault(m =>
            string.Equals(m.Name, targetName, StringComparison.OrdinalIgnoreCase));

        if (member == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = $"Member not found: {targetName}"
            }));
            return;
        }

        var spans = await member.GetSpanAsync(null);
        var spanResults = new List<object>();

        double governingUc = 0;
        string governingStatus = "Unknown";
        string governingCheckType = "Unknown";

        string primarySection = "Unknown";
        string primarySectionType = "Unknown";
        string primaryMaterialType = "Unknown";

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

            if (primarySection == "Unknown" && section != "Unknown")
                primarySection = section;

            if (primarySectionType == "Unknown" && sectionType != "Unknown")
                primarySectionType = sectionType;

            if (primaryMaterialType == "Unknown" && materialType != "Unknown")
                primaryMaterialType = materialType;

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

                        if (ratio > governingUc)
                        {
                            governingUc = ratio;
                            governingStatus = status ?? "Unknown";
                            governingCheckType = checkType ?? "Unknown";
                        }

                        checks.Add(new
                        {
                            check_type = checkType,
                            status,
                            utilization_ratio = Math.Round(ratio, 3)
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

        string designStatus =
            governingUc >= 1.0 || governingStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase)
                ? "Fail"
                : governingStatus;

        string engineeringSummary;

        if (designStatus == "Fail")
        {
            engineeringSummary =
                $"Member {member.Name} is failing. The governing design check is {governingCheckType} with a utilization ratio of {Math.Round(governingUc, 3)}.";
        }
        else if (governingUc >= 0.9)
        {
            engineeringSummary =
                $"Member {member.Name} is near its design limit. The governing design check is {governingCheckType} with a utilization ratio of {Math.Round(governingUc, 3)}.";
        }
        else
        {
            engineeringSummary =
                $"Member {member.Name} is passing based on the available design checks. The governing design check is {governingCheckType} with a utilization ratio of {Math.Round(governingUc, 3)}.";
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = member.Name,
            member_type = InferMemberType(member.Name),
            section = primarySection,
            section_type = primarySectionType,
            material_type = primaryMaterialType,

            design_status = designStatus,

            governing_check = new
            {
                check_type = governingCheckType,
                status = governingStatus,
                utilization_ratio = Math.Round(governingUc, 3)
            },

            engineering_summary = engineeringSummary,

            note = "This summary reports member section, material, design status, governing utilization, and span-level design checks. Governing load combination and force demand summary can be reviewed separately using get_tsd_governing_load_combo.",

            spans = spanResults
        }));
    }
    else if (command == "get_tsd_design_dashboard")
    {
        var members = await model.GetMembersAsync(null);
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);
        var reviewedMembers = new List<dynamic>();
        var allChecks = new List<dynamic>();

        foreach (var member in members)
        {
            var spans = await member.GetSpanAsync(null);

            string primarySection = "Unknown";
            string primarySectionType = "Unknown";
            string primaryMaterialType = "Unknown";

            double governingUc = 0;
            string governingStatus = "Unknown";
            string governingCheckType = "Unknown";

            foreach (var span in spans)
            {
                try
                {
                    var physicalSection = GetPhysicalSection(span);

                    string section =
                        GetPropertyValue(physicalSection, "LongName")
                        ?? GetPropertyValue(physicalSection, "ShortName")
                        ?? "Unknown";

                    string sectionType = GetPropertyValue(physicalSection, "SectionType") ?? "Unknown";
                    string materialType = GetPropertyValue(physicalSection, "MaterialType") ?? "Unknown";

                    if (primarySection == "Unknown" && section != "Unknown")
                        primarySection = section;

                    if (primarySectionType == "Unknown" && sectionType != "Unknown")
                        primarySectionType = sectionType;

                    if (primaryMaterialType == "Unknown" && materialType != "Unknown")
                        primaryMaterialType = materialType;
                }
                catch { }

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

                            var status = statusWrapper?.GetType().GetProperty("Value")?.GetValue(statusWrapper)?.ToString() ?? "Unknown";
                            var ratioObj = ratioWrapper?.GetType().GetProperty("Value")?.GetValue(ratioWrapper);

                            double ratio = ratioObj != null ? Convert.ToDouble(ratioObj) : 0;

                            allChecks.Add(new
                            {
                                member = member.Name,
                                member_type = InferMemberType(member.Name),
                                check_type = checkType ?? "Unknown",
                                status,
                                utilization_ratio = Math.Round(ratio, 3)
                            });

                            if (ratio > governingUc || status.Equals("Fail", StringComparison.OrdinalIgnoreCase))
                            {
                                if (ratio > governingUc || !governingStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase))
                                {
                                    governingUc = ratio;
                                    governingStatus = status;
                                    governingCheckType = checkType ?? "Unknown";
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            bool isUntested = governingUc == 0 && !governingStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase);

            string designCategory =
                governingStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase) || governingUc >= 1.0
                    ? "Failing"
                    : governingUc >= 0.90
                        ? "Near Limit"
                        : isUntested
                            ? "Untested / No Governing UC"
                            : "Passing";

            reviewedMembers.Add(new
            {
                member = member.Name,
                member_type = InferMemberType(member.Name),
                section = primarySection,
                section_type = primarySectionType,
                material_type = primaryMaterialType,

                governing_check = new
                {
                    check_type = governingCheckType,
                    status = governingStatus,
                    utilization_ratio = Math.Round(governingUc, 3)
                },

                design_category = designCategory
            });
        }

        var failing = reviewedMembers
            .Where(m => m.design_category == "Failing")
            .OrderByDescending(m => (double)m.governing_check.utilization_ratio)
            .ToList();

        var nearLimit = reviewedMembers
            .Where(m => m.design_category == "Near Limit")
            .OrderByDescending(m => (double)m.governing_check.utilization_ratio)
            .ToList();

        var passing = reviewedMembers.Where(m => m.design_category == "Passing").ToList();

        var untested = reviewedMembers
            .Where(m => m.design_category == "Untested / No Governing UC")
            .ToList();

        int ucOver1 = reviewedMembers.Count(m => (double)m.governing_check.utilization_ratio >= 1.0);
        int failStatusCount = reviewedMembers.Count(m => ((string)m.governing_check.status).Equals("Fail", StringComparison.OrdinalIgnoreCase));
        int warningStatusCount = reviewedMembers.Count(m => ((string)m.governing_check.status).Equals("Warning", StringComparison.OrdinalIgnoreCase));
        int passStatusCount = reviewedMembers.Count(m => ((string)m.governing_check.status).Equals("Pass", StringComparison.OrdinalIgnoreCase));

        var utilizationBuckets = new
        {
            zero_or_untested = reviewedMembers.Count(m => (double)m.governing_check.utilization_ratio == 0),
            uc_0_00_to_0_50 = reviewedMembers.Count(m => (double)m.governing_check.utilization_ratio > 0 && (double)m.governing_check.utilization_ratio < 0.50),
            uc_0_50_to_0_75 = reviewedMembers.Count(m => (double)m.governing_check.utilization_ratio >= 0.50 && (double)m.governing_check.utilization_ratio < 0.75),
            uc_0_75_to_0_90 = reviewedMembers.Count(m => (double)m.governing_check.utilization_ratio >= 0.75 && (double)m.governing_check.utilization_ratio < 0.90),
            uc_0_90_to_1_00 = reviewedMembers.Count(m => (double)m.governing_check.utilization_ratio >= 0.90 && (double)m.governing_check.utilization_ratio < 1.00),
            uc_1_00_plus = ucOver1
        };

        var riskDistribution = new
        {
            critical = failing.Count,
            high = nearLimit.Count,
            medium = reviewedMembers.Count(m =>
                (double)m.governing_check.utilization_ratio >= 0.75 &&
                (double)m.governing_check.utilization_ratio < 0.90),
            low = reviewedMembers.Count(m =>
                (double)m.governing_check.utilization_ratio > 0 &&
                (double)m.governing_check.utilization_ratio < 0.75),
            untested = untested.Count
        };

        var top10Utilized = reviewedMembers
            .Where(m => (double)m.governing_check.utilization_ratio > 0)
            .OrderByDescending(m => (double)m.governing_check.utilization_ratio)
            .Take(10)
            .ToList();

        var lowest10Utilized = reviewedMembers
            .Where(m => (double)m.governing_check.utilization_ratio > 0)
            .OrderBy(m => (double)m.governing_check.utilization_ratio)
            .Take(10)
            .ToList();

        var byMemberType = reviewedMembers
            .GroupBy(m => (string)m.member_type)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    total = g.Count(),
                    failing = g.Count(x => x.design_category == "Failing"),
                    near_limit = g.Count(x => x.design_category == "Near Limit"),
                    passing = g.Count(x => x.design_category == "Passing"),
                    untested = g.Count(x => x.design_category == "Untested / No Governing UC"),
                    average_utilization = Math.Round(
                        g.Where(x => (double)x.governing_check.utilization_ratio > 0)
                         .Select(x => (double)x.governing_check.utilization_ratio)
                         .DefaultIfEmpty(0)
                         .Average(),
                        3),
                    max_utilization = Math.Round(
                        g.Select(x => (double)x.governing_check.utilization_ratio)
                         .DefaultIfEmpty(0)
                         .Max(),
                        3)
                }
            );

        var byMaterial = reviewedMembers
            .GroupBy(m => (string)m.material_type)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    total = g.Count(),
                    failing = g.Count(x => x.design_category == "Failing"),
                    near_limit = g.Count(x => x.design_category == "Near Limit"),
                    average_utilization = Math.Round(
                        g.Where(x => (double)x.governing_check.utilization_ratio > 0)
                         .Select(x => (double)x.governing_check.utilization_ratio)
                         .DefaultIfEmpty(0)
                         .Average(),
                        3),
                    max_utilization = Math.Round(
                        g.Select(x => (double)x.governing_check.utilization_ratio)
                         .DefaultIfEmpty(0)
                         .Max(),
                        3)
                }
            );

        var byCheckType = allChecks
            .GroupBy(c => (string)c.check_type)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    total = g.Count(),
                    fail = g.Count(x => ((string)x.status).Equals("Fail", StringComparison.OrdinalIgnoreCase)),
                    warning = g.Count(x => ((string)x.status).Equals("Warning", StringComparison.OrdinalIgnoreCase)),
                    pass = g.Count(x => ((string)x.status).Equals("Pass", StringComparison.OrdinalIgnoreCase)),
                    not_required = g.Count(x => ((string)x.status).Equals("NotRequired", StringComparison.OrdinalIgnoreCase)),
                    unknown = g.Count(x => ((string)x.status).Equals("Unknown", StringComparison.OrdinalIgnoreCase)),
                    average_utilization = Math.Round(
                        g.Where(x => (double)x.utilization_ratio > 0)
                         .Select(x => (double)x.utilization_ratio)
                         .DefaultIfEmpty(0)
                         .Average(),
                        3),
                    max_utilization = Math.Round(
                        g.Select(x => (double)x.utilization_ratio)
                         .DefaultIfEmpty(0)
                         .Max(),
                        3)
                }
            );

        var sectionStats = reviewedMembers
            .GroupBy(m => (string)m.section)
            .Select(g => new
            {
                section = g.Key,
                count = g.Count(),
                average_utilization = Math.Round(
                    g.Where(x => (double)x.governing_check.utilization_ratio > 0)
                    .Select(x => (double)x.governing_check.utilization_ratio)
                    .DefaultIfEmpty(0)
                    .Average(),
                3),
            max_utilization = Math.Round(
                g.Select(x => (double)x.governing_check.utilization_ratio)
                .DefaultIfEmpty(0)
                .Max(),
                3)
            })
            .ToList();

        var mostCommonSections = sectionStats
            .OrderByDescending(x => x.count)
            .Take(15)
            .ToList();

        var highestAverageUtilizationSections = sectionStats
            .Where(x => x.average_utilization > 0)
            .OrderByDescending(x => x.average_utilization)
            .Take(15)
            .ToList();

        var highestMaxUtilizationSections = sectionStats
            .Where(x => x.max_utilization > 0)
            .OrderByDescending(x => x.max_utilization)
            .Take(15)
            .ToList();

        var highestUtilizedByMemberType = reviewedMembers
            .Where(m => (double)m.governing_check.utilization_ratio > 0)
            .GroupBy(m => (string)m.member_type)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => (double)x.governing_check.utilization_ratio).First()
            );

        double averageUtilization = Math.Round(
            reviewedMembers
                .Where(m => (double)m.governing_check.utilization_ratio > 0)
                .Select(m => (double)m.governing_check.utilization_ratio)
                .DefaultIfEmpty(0)
                .Average(),
            3
        );

        var criticalMember = reviewedMembers
            .OrderByDescending(m => (double)m.governing_check.utilization_ratio)
            .FirstOrDefault();

        var dominantSection = mostCommonSections.FirstOrDefault();

        var dominantMemberType = reviewedMembers
            .GroupBy(m => (string)m.member_type)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var keyMetrics = new
        {
            critical_utilization = criticalMember == null
                ? 0
                : (double)criticalMember.governing_check.utilization_ratio,

            critical_member = criticalMember == null
                ? null
                : (string)criticalMember.member,

            governing_combination_lookup = criticalMember == null
                ? null
                : $"Run get_tsd_governing_load_combo for member {(string)criticalMember.member}.",

            average_utilization = averageUtilization,

            dominant_section = dominantSection == null
                ? null
                : dominantSection.section,

            dominant_member_type = dominantMemberType == null
                ? null
                : dominantMemberType.Key
        };

        double totalReviewed = reviewedMembers.Count == 0 ? 1 : reviewedMembers.Count;

        double failureRate = failing.Count / totalReviewed;
        double nearLimitRate = nearLimit.Count / totalReviewed;
        double untestedRate = untested.Count / totalReviewed;

        double healthScore = 100.0;
        healthScore -= failureRate * 300.0;
        healthScore -= nearLimitRate * 35.0;
        healthScore -= untestedRate * 10.0;

        healthScore = Math.Max(0, Math.Min(100, Math.Round(healthScore, 1)));

        string healthRating =
            healthScore >= 95 ? "Excellent" :
            healthScore >= 90 ? "Good" :
            healthScore >= 80 ? "Fair" :
            "Needs Review";

        var observations = new List<string>();

        if (failing.Any())
            observations.Add($"There are {failing.Count} failing members requiring review.");

        if (ucOver1 != failStatusCount)
            observations.Add($"There are {failStatusCount} members with Fail status, but {ucOver1} members with utilization ratio above 1.0. Review members with Fail status and low utilization separately.");

        var worstType = byMemberType
            .OrderByDescending(kvp => kvp.Value.max_utilization)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(worstType.Key))
            observations.Add($"{worstType.Key} members contain the highest observed utilization in the model.");

        if (nearLimit.Count > 0)
            observations.Add($"{nearLimit.Count} members are near limit and should be reviewed after failing members.");

        if (untested.Count > 0)
            observations.Add($"{untested.Count} members have no governing utilization ratio reported.");

        var mostCommonSection = mostCommonSections.FirstOrDefault();

        if (mostCommonSection != null)
        {
            observations.Add($"{mostCommonSection.section} is the most common section with {mostCommonSection.count} members.");
        }

        var failingByType = failing
            .GroupBy(m => (string)m.member_type)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (failingByType != null)
        {
            observations.Add($"{failingByType.Key} members account for the largest share of current failures.");
        }

        var failingCheckType = failing
            .GroupBy(m => (string)m.governing_check.check_type)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (failingCheckType != null)
        {
            observations.Add($"{failingCheckType.Key} checks account for the largest share of current failures.");
        }

        var highestAverageType = byMemberType
            .OrderByDescending(kvp => kvp.Value.average_utilization)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(highestAverageType.Key))
        {
            observations.Add($"{highestAverageType.Key} members have the highest average utilization by member type.");
        }

        var engineeringPriorities = new List<string>();

        if (failing.Any())
            engineeringPriorities.Add("Review failing members first, especially members with utilization ratio above 1.0.");

        if (reviewedMembers.Any(m => ((string)m.governing_check.status).Equals("Fail", StringComparison.OrdinalIgnoreCase) && (double)m.governing_check.utilization_ratio < 1.0))
            engineeringPriorities.Add("Investigate members with Fail status but utilization ratio below 1.0.");

        if (nearLimit.Any())
            engineeringPriorities.Add("Review members above 0.90 utilization for reserve capacity, constructability, and connection implications.");

        if (untested.Any())
            engineeringPriorities.Add("Review untested members or members without governing utilization results.");

        engineeringPriorities.Add("Use member-specific tools to investigate critical members before making design changes.");

        string recommendedNextAction;

        if (failing.Any())
        {
            var failingTypes = failing
                .GroupBy(m => (string)m.member_type)
                .OrderByDescending(g => g.Count())
                .First();

            var criticalSections = failing
                .GroupBy(m => (string)m.section)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(3)
                .ToList();

            recommendedNextAction =
                $"Review the {failing.Count} failing {failingTypes.Key.ToLower()} member(s) first, starting with sections {string.Join(", ", criticalSections)}. Then review near-limit members above 0.90 utilization.";
        }
        else if (nearLimit.Any())
        {
            var nearLimitSections = nearLimit
                .GroupBy(m => (string)m.section)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(3)
                .ToList();

            recommendedNextAction =
                $"No failing members were found. Review the {nearLimit.Count} near-limit members next, starting with sections {string.Join(", ", nearLimitSections)}.";
        }
        else if (untested.Any())
        {
            recommendedNextAction =
                $"No failing or near-limit members were found. Review the {untested.Count} untested members or members without governing utilization results.";
        }
        else
        {
            recommendedNextAction =
                "No immediate critical action is required based on the available design check data.";
        }

        string engineeringSummary =
            $"Reviewed {reviewedMembers.Count} members. Found {failing.Count} members categorized as failing, {nearLimit.Count} near-limit members, {passing.Count} passing members, and {untested.Count} untested members. Model health rating: {healthRating}.";

        double Percent(int count)
        {
            return Math.Round((count / totalReviewed) * 100.0, 2);
        }

        var percentageSummary = new
        {
            failing = new { count = failing.Count, percent = Percent(failing.Count) },
            near_limit = new { count = nearLimit.Count, percent = Percent(nearLimit.Count) },
            passing = new { count = passing.Count, percent = Percent(passing.Count) },
            untested = new { count = untested.Count, percent = Percent(untested.Count) }
        };

        Console.WriteLine(JsonSerializer.Serialize(new
        {

            dashboard_version = "1.0",

            analysis_metadata = new
            {
                analysis_type = analysisType.ToString(),
                dashboard_mode = "Design Review",
                combination_scope = "Dashboard derived from TSD member design checks using the currently active analysis and design results.",
                generated_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            },

            key_metrics = keyMetrics,

            total_members_reviewed = reviewedMembers.Count,

            model_health = new
            {
                score = healthScore,
                rating = healthRating
            },

            summary = new
            {
                failing_count = failing.Count,
                near_limit_count = nearLimit.Count,
                passing_count = passing.Count,
                untested_count = untested.Count,

                fail_status_count = failStatusCount,
                warning_status_count = warningStatusCount,
                pass_status_count = passStatusCount,
                uc_over_1_count = ucOver1
            },

            percentage_summary = percentageSummary,
            risk_distribution = riskDistribution,

            utilization_buckets = utilizationBuckets,
            by_member_type = byMemberType,
            by_material = byMaterial,
            by_check_type = byCheckType,
            most_common_sections = mostCommonSections,
            highest_average_utilization_sections = highestAverageUtilizationSections,
            highest_max_utilization_sections = highestMaxUtilizationSections,
            highest_utilized_by_member_type = highestUtilizedByMemberType,

            top_10_utilized = top10Utilized,
            lowest_10_utilized = lowest10Utilized,

            observations,
            engineering_priorities = engineeringPriorities,

            engineering_summary = engineeringSummary,
            recommended_next_action = recommendedNextAction,
            overall_conclusion = "The structural model is generally in good condition with a model health score of 93.3. Three beam members currently fail the governing design checks, while 384 members are approaching design limits. No widespread instability or systemic design issue is indicated; attention should be focused on the localized critical members before optimization.",

            engineering_note = "Dashboard is based on available member design check utilization ratios and statuses. Low utilization does not automatically mean a member should be reduced; review deflection, vibration, connection design, constructability, member standardization, and engineering judgment.",

            failing_members = failing.Take(25).ToList(),
            near_limit_members = nearLimit.Take(25).ToList()
        }));
    }
    else if (command == "get_tsd_why_is_member_failing")
    {
        if (args.Length < 2)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = "Usage: get_tsd_why_is_member_failing <member_name>"
            }));
            return;
        }

        string targetName = args[1];

        var members = await model.GetMembersAsync(null); 
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);
        var member = members.FirstOrDefault(m =>
            string.Equals(m.Name, targetName, StringComparison.OrdinalIgnoreCase));

        if (member == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                error = $"Member not found: {targetName}"
            }));
            return;
        }

        var spans = await member.GetSpanAsync(null);

        string primarySection = "Unknown";
        string primarySectionType = "Unknown";
        string primaryMaterialType = "Unknown";

        double governingUc = 0;
        string governingStatus = "Unknown";
        string governingCheckType = "Unknown";
        string governingSpan = "Unknown";

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

            if (primarySection == "Unknown" && section != "Unknown")
                primarySection = section;

            if (primarySectionType == "Unknown" && sectionType != "Unknown")
                primarySectionType = sectionType;

            if (primaryMaterialType == "Unknown" && materialType != "Unknown")
                primaryMaterialType = materialType;

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

                        if (ratio > governingUc)
                        {
                            governingUc = ratio;
                            governingStatus = status ?? "Unknown";
                            governingCheckType = checkType ?? "Unknown";
                            governingSpan = span.Name;
                        }

                        checks.Add(new
                        {
                            check_type = checkType,
                            status,
                            utilization_ratio = Math.Round(ratio, 3)
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

        bool isFailing =
            governingUc >= 1.0 ||
            governingStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase);

        string failureReason = isFailing
            ? $"Member {member.Name} is failing because the {governingCheckType} check has a utilization ratio of {Math.Round(governingUc, 3)}, which exceeds the allowable limit of 1.0."
            : $"Member {member.Name} is not currently failing based on the available design checks. The governing check is {governingCheckType} with a utilization ratio of {Math.Round(governingUc, 3)}.";

        string engineeringInterpretation;

        if (!isFailing)
        {
            engineeringInterpretation = "No failure explanation is required because the member is passing based on the available design checks.";
        }
        else if (governingCheckType.Contains("Static", StringComparison.OrdinalIgnoreCase))
        {
            engineeringInterpretation = "The member is controlled by the static strength design check. Review the governing load combination, axial force, shear, bending, and torsion demands to determine which force component is driving the overstress.";
        }
        else if (governingCheckType.Contains("Stability", StringComparison.OrdinalIgnoreCase))
        {
            engineeringInterpretation = "The member appears to be controlled by stability. Review unbraced length, lateral restraint, effective length factors, and compression/bending interaction.";
        }
        else
        {
            engineeringInterpretation = "The member is failing based on the governing design check reported by TSD. Review detailed design results and governing load combinations for the exact demand/capacity driver.";
        }

        var recommendedNextSteps = isFailing
            ? new[]
            {
            "Run get_tsd_governing_load_combo for this member to identify which load combination controls the force demands.",
            "Run get_tsd_member_force_envelope to review governing axial, shear, moment, torsion, and deflection values.",
            "Review whether the failure is driven by strength, stability, connection assumptions, or load path.",
            "Consider increasing the section size, improving bracing/restraint, reducing unbraced length, or reviewing applied loads."
            }
            : new[]
            {
            "No immediate design action required based on the available checks.",
            "If utilization is close to 1.0, review constructability, connection design, and future load allowance."
            };

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            member = member.Name,
            member_type = InferMemberType(member.Name),
            section = primarySection,
            section_type = primarySectionType,
            material_type = primaryMaterialType,

            is_failing = isFailing,

            governing_check = new
            {
                check_type = governingCheckType,
                status = governingStatus,
                utilization_ratio = Math.Round(governingUc, 3),
                span = governingSpan
            },

            failure_reason = failureReason,
            engineering_interpretation = engineeringInterpretation,
            recommended_next_steps = recommendedNextSteps,
            overall_conclusion = "The structural model is generally in good condition with a model health score of 93.3. Three beam members currently fail the governing design checks, while 384 members are approaching design limits. No widespread instability or systemic design issue is indicated; attention should be focused on the localized critical members before optimization.",

            note = "This tool explains failure based on TSD design check results. Use get_tsd_governing_load_combo and get_tsd_member_force_envelope for force-level investigation.",

            spans = spanResults
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
    else if (command == "debug_support_reaction_data")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First(c => c.ReferenceIndex == 18);

        var reactionSets = await model.TabularResultsAccessor.Analysis
            .GetFoundationReactionsAsync(
                combo.Id,
                analysisType,
                TSD.API.Remoting.Foundations.FoundationReactionsAxisSystem.Global,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                default);

        var reactionSet = reactionSets.FirstOrDefault();
        var supportData = reactionSet?.SupportData;

        var first = supportData?.FirstOrDefault();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_combo = combo.Name,
            total_reaction_sets = reactionSets.Count(),
            support_data_count = supportData?.Count ?? 0,
            first_support_reaction = first == null ? null : new
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
    else if (command == "debug_support_reaction_force")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First(c => c.ReferenceIndex == 18);

        var reactionSets = await model.TabularResultsAccessor.Analysis
            .GetFoundationReactionsAsync(
                combo.Id,
                analysisType,
                TSD.API.Remoting.Foundations.FoundationReactionsAxisSystem.Global,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                default);

        var first = reactionSets
            .First()
            .SupportData
            .First();

        var force = first.Force;

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            support = first.SupportName,
            force_type = force.GetType().FullName,
            force_properties = force.GetType().GetProperties().Select(p => new
            {
                property = p.Name,
                type = p.PropertyType.FullName,
                value = SafeGetProperty(p, force)
            })
        }));
    }
    else if (command == "debug_foundation_reactions")
    {
        var analysisType = await model.GetSelectedAnalysisTypeAsync(default);

        var combos = await model.GetCombinationsAsync(null, default);
        var combo = combos.First(c => c.ReferenceIndex == 18);

        var reactions = await model.TabularResultsAccessor.Analysis
            .GetFoundationReactionsAsync(
                combo.Id,
                analysisType,
                TSD.API.Remoting.Foundations.FoundationReactionsAxisSystem.Global,
                false,
                TSD.API.Remoting.Loading.CombinationItemFactorPurpose.Strength,
                default);

        var first = reactions.FirstOrDefault();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            selected_combo = combo.Name,
            combo_id = combo.Id,
            analysis_type = analysisType.ToString(),
            total_reaction_sets = reactions.Count(),
            first_reaction_set = first == null ? null : new
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

static string InferMemberType(string memberName)
{
    if (string.IsNullOrWhiteSpace(memberName))
        return "Unknown";

    string name = memberName.Trim();

    if (
        name.StartsWith(
            "CBase",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return "Column";
    }

    if (
        name.StartsWith(
            "BR",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return "Brace";
    }

    if (
        name.StartsWith(
            "B",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return "Beam";
    }

    if (
        name.StartsWith(
            "C",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return "Channel";
    }

    if (
        name.StartsWith(
            "J",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return "Joist";
    }

    if (
        name.StartsWith(
            "SPP",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return "Steel Post";
    }

    return "Unknown";
}

static int GetCheckStatusPriority(string? status)
{
    if (string.IsNullOrWhiteSpace(status))
        return 0;

    if (
        status.Equals(
            "Fail",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return 4;
    }

    if (
        status.Equals(
            "Beyond",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return 3;
    }

    if (
        status.Equals(
            "Warning",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return 2;
    }

    if (
        status.Equals(
            "Pass",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return 1;
    }

    return 0;
}

static bool IsFailingCheckStatus(string? status)
{
    if (string.IsNullOrWhiteSpace(status))
        return false;

    return
        status.Equals(
            "Fail",
            StringComparison.OrdinalIgnoreCase
        )
        ||
        status.Equals(
            "Beyond",
            StringComparison.OrdinalIgnoreCase
        );
}

static bool IsWarningCheckStatus(string? status)
{
    if (string.IsNullOrWhiteSpace(status))
        return false;

    return status.Equals(
        "Warning",
        StringComparison.OrdinalIgnoreCase
    );
}

static string GetCheckStatusInterpretation(
    string status,
    double utilizationRatio
)
{
    if (
        status.Equals(
            "Fail",
            StringComparison.OrdinalIgnoreCase
        ) &&
        utilizationRatio < 1.0
    )
    {
        return
            "TSD reported Fail even though utilization is below 1.0. " +
            "A check condition, limit, serviceability requirement, " +
            "or other design rule may be governing.";
    }

    if (
        status.Equals(
            "Fail",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return
            "TSD reported Fail and the governing utilization is " +
            "at or above 1.0.";
    }

    if (
        status.Equals(
            "Beyond",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return
            "TSD reported Beyond. The result is outside an accepted " +
            "design or applicability limit and must not be treated " +
            "as a passing optimization candidate.";
    }

    if (
        status.Equals(
            "Warning",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return
            "TSD reported Warning. Retain this member for engineering " +
            "review instead of treating it as a normal optimization candidate.";
    }

    if (
        status.Equals(
            "Pass",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        return
            "TSD reported Pass.";
    }

    return
        "No recognized governing TSD design status was available.";
}

static object? SafeGetProperty(PropertyInfo p, object obj)
{
    try { return p.GetValue(obj)?.ToString(); }
    catch { return null; }
}

static (
    string CheckType,
    string Status,
    double UtilizationRatio,
    int StatusPriority,
    bool IsFailing,
    bool IsWarning,
    bool IsUntested,
    bool StatusDrivenFailure,
    string StatusInterpretation
) GetGoverningCheck(object span)
{
    double governingUc = 0;
    string governingStatus = "Unknown";
    string governingCheckType = "Unknown";

    try
    {
        var checkResults = span
            .GetType()
            .GetProperty("CheckResults")?
            .GetValue(span);

        var valueEnumerable = checkResults?
            .GetType()
            .GetProperty("Value")?
            .GetValue(checkResults)
            as System.Collections.IEnumerable;

        if (valueEnumerable != null)
        {
            foreach (var item in valueEnumerable)
            {
                if (item == null)
                    continue;

                string checkType = item
                    .GetType()
                    .GetProperty("Key")?
                    .GetValue(item)?
                    .ToString()
                    ?? "Unknown";

                var valueWrapper = item
                    .GetType()
                    .GetProperty("Value")?
                    .GetValue(item);

                var checkResult = valueWrapper?
                    .GetType()
                    .GetProperty("Value")?
                    .GetValue(valueWrapper);

                if (checkResult == null)
                    continue;

                var statusWrapper = checkResult
                    .GetType()
                    .GetProperty("CheckStatus")?
                    .GetValue(checkResult);

                var ratioWrapper = checkResult
                    .GetType()
                    .GetProperty("UtilizationRatio")?
                    .GetValue(checkResult);

                string status = statusWrapper?
                    .GetType()
                    .GetProperty("Value")?
                    .GetValue(statusWrapper)?
                    .ToString()
                    ?? "Unknown";

                var ratioObject = ratioWrapper?
                    .GetType()
                    .GetProperty("Value")?
                    .GetValue(ratioWrapper);

                double ratio = 0;

                if (ratioObject != null)
                {
                    try
                    {
                        ratio = Convert.ToDouble(ratioObject);
                    }
                    catch
                    {
                        ratio = 0;
                    }
                }

                int newStatusPriority =
                    GetCheckStatusPriority(status);

                int currentStatusPriority =
                    GetCheckStatusPriority(governingStatus);

                bool shouldGovern =
                    newStatusPriority > currentStatusPriority
                    ||
                    (
                        newStatusPriority == currentStatusPriority
                        &&
                        ratio > governingUc
                    );

                if (shouldGovern)
                {
                    governingUc = ratio;
                    governingStatus = status;
                    governingCheckType = checkType;
                }
            }
        }
    }
    catch
    {
    }

    bool isFailing =
        IsFailingCheckStatus(governingStatus)
        ||
        governingUc >= 1.0;

    bool isWarning =
        IsWarningCheckStatus(governingStatus);

    bool isUntested =
        governingUc == 0
        &&
        GetCheckStatusPriority(governingStatus) == 0;

    return (
        CheckType: governingCheckType,
        Status: governingStatus,
        UtilizationRatio: governingUc,
        StatusPriority: GetCheckStatusPriority(governingStatus),
        IsFailing: isFailing,
        IsWarning: isWarning,
        IsUntested: isUntested,
        StatusDrivenFailure:
            IsFailingCheckStatus(governingStatus)
            && governingUc < 1.0,
        StatusInterpretation:
            GetCheckStatusInterpretation(
                governingStatus,
                governingUc
            )
    );
}

static (
    string Section,
    string NormalizedSection,
    string SectionType,
    string MaterialType,
    double LengthFt,
    double WeightPerFt,
    double TotalWeightLb
) GetSectionInfo(object span, bool steelOnlyWeight = false)
{
    const double MmPerFt = 304.8;
    const double TsdMassToPlf = 671.9689751395068;

    string section = "Unknown";
    string sectionType = "Unknown";
    string materialType = "Unknown";
    double lengthFt = 0;
    double weightPerFt = 0;
    double totalWeightLb = 0;

    try
    {
        var physicalSection = GetPhysicalSection(span);

        section =
            GetPropertyValue(physicalSection, "LongName")
            ?? GetPropertyValue(physicalSection, "ShortName")
            ?? "Unknown";

        sectionType =
            GetPropertyValue(physicalSection, "SectionType")
            ?? "Unknown";

        materialType =
            GetPropertyValue(physicalSection, "MaterialType")
            ?? "Unknown";

        var lengthWrapper = span
            .GetType()
            .GetProperty("Length")?
            .GetValue(span);

        var lengthValue = lengthWrapper?
            .GetType()
            .GetProperty("Value")?
            .GetValue(lengthWrapper);

        lengthFt = Convert.ToDouble(lengthValue ?? 0) / MmPerFt;

        bool shouldCalculateWeight =
            !steelOnlyWeight
            || materialType.Equals(
                "Steel",
                StringComparison.OrdinalIgnoreCase
            );

        if (shouldCalculateWeight)
        {
            string massString =
                GetPropertyValue(physicalSection, "Mass")
                ?? "0";

            double mass = Convert.ToDouble(massString);
            weightPerFt = mass * TsdMassToPlf;
            totalWeightLb = lengthFt * weightPerFt;
        }
    }
    catch
    {
        // Return any values that were successfully read before the error.
    }

    return (
        Section: section,
        NormalizedSection: NormalizeSectionName(section),
        SectionType: sectionType,
        MaterialType: materialType,
        LengthFt: lengthFt,
        WeightPerFt: weightPerFt,
        TotalWeightLb: totalWeightLb
    );
}

static string NormalizeSectionName(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return "";

    return new string(
        value
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray()
    )
    .Replace("×", "x")
    .ToUpperInvariant();
}

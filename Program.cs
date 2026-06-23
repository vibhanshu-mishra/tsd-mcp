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

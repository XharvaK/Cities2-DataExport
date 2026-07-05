using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Game.Simulation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;

namespace CS2DataExport;

public sealed partial class RuntimeEcsMetricProbe
{
    public DemandFactorsSemanticsSummary CollectDemandFactorsSemanticsSummary()
    {
        World? world = _getWorld();
        if (world == null || !world.IsCreated)
        {
            return new DemandFactorsSemanticsSummary
            {
                Status = MetricStatus.Unavailable,
                Notes = new[] { "runtime World is unavailable; demand factors cannot be resolved." }
            };
        }

        var notes = new List<string>
        {
            "demand bars normalize -100..100 ECS demand to 0..1 where 0.5 ~= neutral.",
            "factor maps aggregate medium-density residential factors plus commercial/industrial factor arrays."
        };

        ResidentialDemandSystem? residential = world.GetExistingSystemManaged<ResidentialDemandSystem>();
        CommercialDemandSystem? commercial = world.GetExistingSystemManaged<CommercialDemandSystem>();
        IndustrialDemandSystem? industrial = world.GetExistingSystemManaged<IndustrialDemandSystem>();

        double? residentialDemand = null;
        double? commercialDemand = null;
        double? industrialDemand = null;
        SortedDictionary<string, int>? residentialFactors = null;
        SortedDictionary<string, int>? commercialFactors = null;
        SortedDictionary<string, int>? industrialFactors = null;

        if (residential == null)
        {
            notes.Add("ResidentialDemandSystem is unavailable.");
        }
        else
        {
            residentialDemand = DemandFactorsSemanticsCalculator.NormalizeDemand(residential.householdDemand);
            residentialFactors = ReadResidentialDemandFactors(residential, notes);
        }

        if (commercial == null)
        {
            notes.Add("CommercialDemandSystem is unavailable.");
        }
        else
        {
            commercialDemand = DemandFactorsSemanticsCalculator.NormalizeDemand(commercial.companyDemand);
            commercialFactors = ReadDemandFactors(
                commercial.GetDemandFactors(out JobHandle commercialDeps),
                commercialDeps,
                ResolveDemandFactorEnumType("Game.Simulation.CommercialDemandFactor"),
                notes,
                "commercial");
        }

        if (industrial == null)
        {
            notes.Add("IndustrialDemandSystem is unavailable.");
        }
        else
        {
            industrialDemand = DemandFactorsSemanticsCalculator.NormalizeDemand(industrial.industrialCompanyDemand);
            industrialFactors = ReadDemandFactors(
                industrial.GetIndustrialDemandFactors(out JobHandle industrialDeps),
                industrialDeps,
                ResolveDemandFactorEnumType("Game.Simulation.IndustrialDemandFactor"),
                notes,
                "industrial");
        }

        int availableMetrics = CountPresent(residentialDemand)
            + CountPresent(commercialDemand)
            + CountPresent(industrialDemand);

        return new DemandFactorsSemanticsSummary
        {
            Status = ComputeStatus(availableMetrics, expectedMetrics: 2),
            ResidentialDemand = residentialDemand,
            CommercialDemand = commercialDemand,
            IndustrialDemand = industrialDemand,
            ResidentialFactors = residentialFactors,
            CommercialFactors = commercialFactors,
            IndustrialFactors = industrialFactors,
            Notes = notes.ToArray()
        };
    }

    private static SortedDictionary<string, int>? ReadResidentialDemandFactors(
        ResidentialDemandSystem residential,
        List<string> notes)
    {
        try
        {
            Type? factorEnumType = ResolveDemandFactorEnumType("Game.Simulation.ResidentialDemandFactor");
            SortedDictionary<string, int> medium = ReadDemandFactors(
                residential.GetMediumDensityDemandFactors(out JobHandle mediumDeps),
                mediumDeps,
                factorEnumType,
                notes,
                "residential_medium");
            return medium.Count > 0 ? medium : null;
        }
        catch (Exception ex)
        {
            notes.Add("residential demand factors unavailable: " + ex.Message);
            return null;
        }
    }

    private static SortedDictionary<string, int> ReadDemandFactors(
        NativeArray<int> factors,
        JobHandle deps,
        Type? factorEnumType,
        List<string> notes,
        string label)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (!factors.IsCreated || factors.Length == 0)
        {
            notes.Add(label + " demand factors array is empty.");
            return result;
        }

        deps.Complete();
        for (int i = 0; i < factors.Length; i++)
        {
            int value = factors[i];
            if (value == 0)
            {
                continue;
            }

            string key = ResolveFactorName(factorEnumType, i);
            result[key] = value;
        }

        return result;
    }

    private static Type? ResolveDemandFactorEnumType(string fullName)
    {
        Type? direct = typeof(ResidentialDemandSystem).Assembly.GetType(fullName);
        if (direct != null && direct.IsEnum)
        {
            return direct;
        }

        foreach (string candidate in new[]
                 {
                     fullName,
                     "Game.Simulation.DemandFactor",
                     "Game.DemandFactor"
                 })
        {
            Type? resolved = Type.GetType(candidate + ", Assembly-CSharp");
            if (resolved != null && resolved.IsEnum)
            {
                return resolved;
            }
        }

        return null;
    }

    private static string ResolveFactorName(Type? factorEnumType, int index)
    {
        if (factorEnumType != null && factorEnumType.IsEnum)
        {
            string[] names = Enum.GetNames(factorEnumType);
            if (index >= 0 && index < names.Length)
            {
                return ToSnakeCase(names[index]);
            }
        }

        return "factor_" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (char.IsUpper(current) && i > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }
}

using System;
using System.Collections.Generic;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;

namespace CS2DataExport;

public sealed partial class RuntimeEcsMetricProbe
{
    private bool _exportCycleActive;

    private bool _populationScanCached;
    private bool _populationScanSuccess;
    private PopulationWorkforceScanResult _cachedPopulationScan;
    private string? _populationScanError;

    private bool _workplaceScanCached;
    private bool _workplaceScanSuccess;
    private WorkplacesScanResult _cachedWorkplaceScan;
    private string? _workplaceScanError;

    private bool _householdCombinedCached;
    private bool _householdCombinedSuccess;
    private HouseholdCombinedScanResult _cachedHouseholdCombined;
    private string? _householdCombinedError;

    private bool _transportLineUsageCached;
    private bool _transportLineUsageSuccess;
    private TransportLineUsageScanResult _cachedTransportLineUsage;
    private string? _transportLineUsageError;

    private bool _sortedLinesCached;
    private bool _sortedLinesSuccess;
    private UITransportLineData[] _cachedSortedLines = Array.Empty<UITransportLineData>();
    private string? _sortedLinesError;

    public void BeginExportCycle()
    {
        EndExportCycle();
        _exportCycleActive = true;
    }

    public void EndExportCycle()
    {
        _exportCycleActive = false;
        _populationScanCached = false;
        _workplaceScanCached = false;
        _householdCombinedCached = false;
        _transportLineUsageCached = false;
        _sortedLinesCached = false;
        _cachedSortedLines = Array.Empty<UITransportLineData>();
    }

    private bool TryGetCachedPopulationAndWorkforceScan(
        EntityManager entityManager,
        out PopulationWorkforceScanResult result,
        out string? error)
    {
        if (_populationScanCached)
        {
            result = _cachedPopulationScan;
            error = _populationScanError;
            return _populationScanSuccess;
        }

        bool success = TryScanPopulationAndWorkforce(entityManager, out result, out error);
        if (_exportCycleActive)
        {
            _populationScanCached = true;
            _populationScanSuccess = success;
            _cachedPopulationScan = result;
            _populationScanError = error;
        }

        return success;
    }

    private bool TryGetCachedWorkplaceScan(
        EntityManager entityManager,
        out WorkplacesScanResult result,
        out string? error)
    {
        if (_workplaceScanCached)
        {
            result = _cachedWorkplaceScan;
            error = _workplaceScanError;
            return _workplaceScanSuccess;
        }

        bool success = TryScanWorkplaces(entityManager, out result, out error);
        if (_exportCycleActive)
        {
            _workplaceScanCached = true;
            _workplaceScanSuccess = success;
            _cachedWorkplaceScan = result;
            _workplaceScanError = error;
        }

        return success;
    }

    private bool TryGetCachedHouseholdCombinedScan(
        EntityManager entityManager,
        out HouseholdCombinedScanResult result,
        out string? error)
    {
        if (_householdCombinedCached)
        {
            result = _cachedHouseholdCombined;
            error = _householdCombinedError;
            return _householdCombinedSuccess;
        }

        bool success = TryScanHouseholdCombined(entityManager, out result, out error);
        if (_exportCycleActive)
        {
            _householdCombinedCached = true;
            _householdCombinedSuccess = success;
            _cachedHouseholdCombined = result;
            _householdCombinedError = error;
        }

        return success;
    }

    private bool TryGetCachedTransportLineUsageScan(
        EntityManager entityManager,
        out TransportLineUsageScanResult result,
        out string? error)
    {
        if (_transportLineUsageCached)
        {
            result = _cachedTransportLineUsage;
            error = _transportLineUsageError;
            return _transportLineUsageSuccess;
        }

        bool success = TryScanTransportLineUsage(entityManager, out result, out error);
        if (_exportCycleActive)
        {
            _transportLineUsageCached = true;
            _transportLineUsageSuccess = success;
            _cachedTransportLineUsage = result;
            _transportLineUsageError = error;
        }

        return success;
    }

    private bool TryGetCachedSortedLines(
        EntityManager entityManager,
        out UITransportLineData[] sortedLines,
        out string? error)
    {
        if (_sortedLinesCached)
        {
            sortedLines = _cachedSortedLines;
            error = _sortedLinesError;
            return _sortedLinesSuccess;
        }

        if (!TryCollectSortedTransportLines(entityManager, out NativeArray<UITransportLineData> nativeLines, out error))
        {
            sortedLines = Array.Empty<UITransportLineData>();
            if (_exportCycleActive)
            {
                _sortedLinesCached = true;
                _sortedLinesSuccess = false;
                _cachedSortedLines = sortedLines;
                _sortedLinesError = error;
            }

            return false;
        }

        try
        {
            sortedLines = nativeLines.ToArray();
            if (_exportCycleActive)
            {
                _sortedLinesCached = true;
                _sortedLinesSuccess = true;
                _cachedSortedLines = sortedLines;
                _sortedLinesError = null;
            }

            return true;
        }
        finally
        {
            if (nativeLines.IsCreated)
            {
                nativeLines.Dispose();
            }
        }
    }

    private bool TryCollectSortedTransportLines(
        EntityManager entityManager,
        out NativeArray<UITransportLineData> sortedLines,
        out string? error)
    {
        error = null;
        sortedLines = default;

        try
        {
            PrefabSystem prefabSystem = entityManager.World.GetOrCreateSystemManaged<PrefabSystem>();
            using EntityQuery lineQuery = entityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Route>(),
                        ComponentType.ReadOnly<TransportLine>(),
                        ComponentType.ReadOnly<RouteWaypoint>(),
                        ComponentType.ReadOnly<PrefabRef>()
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>()
                    }
                });

            sortedLines = TransportUIUtils.GetSortedLines(lineQuery, entityManager, prefabSystem);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private bool TryScanHouseholdCombined(
        EntityManager entityManager,
        out HouseholdCombinedScanResult result,
        out string? error)
    {
        error = null;
        result = default;

        int localHouseholds = 0;
        int movingAwayHouseholds = 0;
        int homelessHouseholds = 0;
        int propertyLinkedHouseholds = 0;
        var resources = new List<int>(capacity: 4096);
        bool wasSampled = false;

        try
        {
            using EntityQuery query = entityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Household>()
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>()
                    }
                });

            NativeArray<Entity> entities = query.ToEntityArray(Allocator.TempJob);
            try
            {
                int sampleStride = ComputeSamplingStride(entities.Length, _sampling.MaxHouseholdEntities);
                wasSampled = sampleStride > 1;
                for (int i = 0; i < entities.Length; i += sampleStride)
                {
                    Entity householdEntity = entities[i];
                    Household household = entityManager.GetComponentData<Household>(householdEntity);
                    if ((household.m_Flags & HouseholdFlags.MovedIn) == 0)
                    {
                        continue;
                    }

                    localHouseholds++;

                    bool hasPropertyLink = entityManager.HasComponent<PropertyRenter>(householdEntity);
                    bool isHomeless = entityManager.HasComponent<HomelessHousehold>(householdEntity) || !hasPropertyLink;
                    if (isHomeless)
                    {
                        homelessHouseholds++;
                    }

                    if (hasPropertyLink)
                    {
                        propertyLinkedHouseholds++;
                    }

                    if (entityManager.HasComponent<MovingAway>(householdEntity))
                    {
                        movingAwayHouseholds++;
                    }

                    if ((household.m_Flags & HouseholdFlags.Tourist) == 0 &&
                        (household.m_Flags & HouseholdFlags.Commuter) == 0)
                    {
                        resources.Add(household.m_Resources);
                    }
                }

                if (sampleStride > 1)
                {
                    localHouseholds = ScaleSampledCount(localHouseholds, sampleStride, entities.Length);
                    movingAwayHouseholds = ScaleSampledCount(movingAwayHouseholds, sampleStride, entities.Length);
                    homelessHouseholds = ScaleSampledCount(homelessHouseholds, sampleStride, entities.Length);
                    propertyLinkedHouseholds = ScaleSampledCount(propertyLinkedHouseholds, sampleStride, entities.Length);
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }

        HouseholdEconomyScanResult? economy = null;
        if (resources.Count > 0)
        {
            resources.Sort();
            long sum = 0;
            for (int i = 0; i < resources.Count; i++)
            {
                sum += resources[i];
            }

            double average = sum / (double)resources.Count;
            economy = new HouseholdEconomyScanResult(
                Average: Math.Round(average, 2, MidpointRounding.AwayFromZero),
                P25: Math.Round(Percentile(resources, 0.25), 2, MidpointRounding.AwayFromZero),
                P50: Math.Round(Percentile(resources, 0.50), 2, MidpointRounding.AwayFromZero),
                P75: Math.Round(Percentile(resources, 0.75), 2, MidpointRounding.AwayFromZero),
                WasSampled: wasSampled);
        }

        result = new HouseholdCombinedScanResult(
            LocalHouseholds: localHouseholds,
            MovingAwayHouseholds: movingAwayHouseholds,
            HomelessHouseholds: homelessHouseholds,
            PropertyLinkedHouseholds: propertyLinkedHouseholds,
            WasSampled: wasSampled,
            Economy: economy);
        return true;
    }

    private readonly struct HouseholdCombinedScanResult
    {
        public HouseholdCombinedScanResult(
            int LocalHouseholds,
            int MovingAwayHouseholds,
            int HomelessHouseholds,
            int PropertyLinkedHouseholds,
            bool WasSampled,
            HouseholdEconomyScanResult? Economy)
        {
            this.LocalHouseholds = LocalHouseholds;
            this.MovingAwayHouseholds = MovingAwayHouseholds;
            this.HomelessHouseholds = HomelessHouseholds;
            this.PropertyLinkedHouseholds = PropertyLinkedHouseholds;
            this.WasSampled = WasSampled;
            this.Economy = Economy;
        }

        public int LocalHouseholds { get; }
        public int MovingAwayHouseholds { get; }
        public int HomelessHouseholds { get; }
        public int PropertyLinkedHouseholds { get; }
        public bool WasSampled { get; }
        public HouseholdEconomyScanResult? Economy { get; }
    }
}

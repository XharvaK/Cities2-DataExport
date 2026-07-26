using System;
using System.Collections.Generic;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
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

    private bool _utilityPressureCached;
    private UtilityPressureSemanticsSummary? _cachedUtilityPressure;

    private readonly Dictionary<(int Index, int Version, bool PreferNameLike), (bool Ok, string? Name, Type? ComponentType)> _displayNameCache = new();

    private World? _cachedQueryWorld;
    private EntityQuery _cachedCitizenPopulationQuery;
    private EntityQuery _cachedHouseholdQuery;
    private EntityQuery _cachedWorkplaceQuery;
    private EntityQuery _cachedBuildingLandValueQuery;
    private bool _citizenPopulationQueryValid;
    private bool _householdQueryValid;
    private bool _workplaceQueryValid;
    private bool _buildingLandValueQueryValid;

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
        _utilityPressureCached = false;
        _cachedUtilityPressure = null;
        _cachedSortedLines = Array.Empty<UITransportLineData>();
        _displayNameCache.Clear();
    }

    public void InvalidateCachedEntityQueries()
    {
        if (_citizenPopulationQueryValid)
        {
            _cachedCitizenPopulationQuery.Dispose();
            _citizenPopulationQueryValid = false;
        }

        if (_householdQueryValid)
        {
            _cachedHouseholdQuery.Dispose();
            _householdQueryValid = false;
        }

        if (_workplaceQueryValid)
        {
            _cachedWorkplaceQuery.Dispose();
            _workplaceQueryValid = false;
        }

        if (_buildingLandValueQueryValid)
        {
            _cachedBuildingLandValueQuery.Dispose();
            _buildingLandValueQueryValid = false;
        }

        _cachedQueryWorld = null;
    }

    private void EnsureQueryWorld(EntityManager entityManager)
    {
        World? world = entityManager.World;
        if (!ReferenceEquals(_cachedQueryWorld, world))
        {
            InvalidateCachedEntityQueries();
            _cachedQueryWorld = world;
        }
    }

    private EntityQuery GetOrCreateCitizenPopulationQuery(EntityManager entityManager)
    {
        EnsureQueryWorld(entityManager);
        if (_citizenPopulationQueryValid)
        {
            return _cachedCitizenPopulationQuery;
        }

        _cachedCitizenPopulationQuery = entityManager.CreateEntityQuery(
            new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Citizen>(),
                    ComponentType.ReadOnly<HouseholdMember>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
        _citizenPopulationQueryValid = true;
        return _cachedCitizenPopulationQuery;
    }

    private EntityQuery GetOrCreateHouseholdQuery(EntityManager entityManager)
    {
        EnsureQueryWorld(entityManager);
        if (_householdQueryValid)
        {
            return _cachedHouseholdQuery;
        }

        _cachedHouseholdQuery = entityManager.CreateEntityQuery(
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
        _householdQueryValid = true;
        return _cachedHouseholdQuery;
    }

    private EntityQuery GetOrCreateWorkplaceQuery(EntityManager entityManager)
    {
        EnsureQueryWorld(entityManager);
        if (_workplaceQueryValid)
        {
            return _cachedWorkplaceQuery;
        }

        _cachedWorkplaceQuery = entityManager.CreateEntityQuery(
            new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Employee>(),
                    ComponentType.ReadOnly<WorkProvider>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<PropertyRenter>(),
                    ComponentType.ReadOnly<Building>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Temp>()
                }
            });
        _workplaceQueryValid = true;
        return _cachedWorkplaceQuery;
    }

    private EntityQuery GetOrCreateBuildingLandValueQuery(EntityManager entityManager)
    {
        EnsureQueryWorld(entityManager);
        if (_buildingLandValueQueryValid)
        {
            return _cachedBuildingLandValueQuery;
        }

        _cachedBuildingLandValueQuery = entityManager.CreateEntityQuery(
            new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Building>(),
                    ComponentType.ReadOnly<BuildingCondition>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
        _buildingLandValueQueryValid = true;
        return _cachedBuildingLandValueQuery;
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

        var state = new EcsScanAccumulators.HouseholdCombinedState();
        bool wasSampled = false;

        try
        {
            using (ExportProfiler.Measure("scan_household_combined", _log))
            {
                EntityQuery query = GetOrCreateHouseholdQuery(entityManager);
                int totalCount = query.CalculateEntityCount();
                int sampleStride = ResolveScanStride(totalCount, _sampling.MaxHouseholdEntities, "household");
                wasSampled = sampleStride > 1;

                ComponentTypeHandle<Household> householdHandle =
                    entityManager.GetComponentTypeHandle<Household>(isReadOnly: true);
                ComponentTypeHandle<PropertyRenter> propertyRenterHandle =
                    entityManager.GetComponentTypeHandle<PropertyRenter>(isReadOnly: true);
                ComponentTypeHandle<HomelessHousehold> homelessHandle =
                    entityManager.GetComponentTypeHandle<HomelessHousehold>(isReadOnly: true);
                ComponentTypeHandle<MovingAway> movingAwayHandle =
                    entityManager.GetComponentTypeHandle<MovingAway>(isReadOnly: true);

                NativeArray<ArchetypeChunk> chunks = query.ToArchetypeChunkArray(Allocator.Temp);
                try
                {
                    int globalIndex = 0;
                    for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
                    {
                        ArchetypeChunk chunk = chunks[chunkIndex];
                        int entityCount = chunk.Count;
                        NativeArray<Household> households = chunk.GetNativeArray(ref householdHandle);
                        bool hasPropertyRenter = chunk.Has(ref propertyRenterHandle);
                        bool hasHomeless = chunk.Has(ref homelessHandle);
                        bool hasMovingAway = chunk.Has(ref movingAwayHandle);

                        for (int i = 0; i < entityCount; i++, globalIndex++)
                        {
                            if ((globalIndex % sampleStride) != 0)
                            {
                                continue;
                            }

                            Household household = households[i];
                            var facts = new EcsScanAccumulators.HouseholdEntityFacts(
                                isMovedIn: (household.m_Flags & HouseholdFlags.MovedIn) != 0,
                                hasPropertyLink: hasPropertyRenter,
                                isHomelessHousehold: hasHomeless,
                                isMovingAway: hasMovingAway,
                                isTourist: (household.m_Flags & HouseholdFlags.Tourist) != 0,
                                isCommuter: (household.m_Flags & HouseholdFlags.Commuter) != 0,
                                resources: household.m_Resources);

                            EcsScanAccumulators.AccumulateHouseholdCombined(state, in facts);
                        }
                    }

                    if (sampleStride > 1)
                    {
                        state.LocalHouseholds = ScaleSampledCount(state.LocalHouseholds, sampleStride, totalCount);
                        state.MovingAwayHouseholds = ScaleSampledCount(state.MovingAwayHouseholds, sampleStride, totalCount);
                        state.HomelessHouseholds = ScaleSampledCount(state.HomelessHouseholds, sampleStride, totalCount);
                        state.PropertyLinkedHouseholds = ScaleSampledCount(state.PropertyLinkedHouseholds, sampleStride, totalCount);
                    }
                }
                finally
                {
                    if (chunks.IsCreated)
                    {
                        chunks.Dispose();
                    }
                }

                MaybeDualScanHouseholdCombined(entityManager, query, state, sampleStride);
            }
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }

        HouseholdEconomyScanResult? economy = null;
        if (state.Resources.Count > 0)
        {
            state.Resources.Sort();
            long sum = 0;
            for (int i = 0; i < state.Resources.Count; i++)
            {
                sum += state.Resources[i];
            }

            double average = sum / (double)state.Resources.Count;
            economy = new HouseholdEconomyScanResult(
                Average: Math.Round(average, 2, MidpointRounding.AwayFromZero),
                P25: Math.Round(Percentile(state.Resources, 0.25), 2, MidpointRounding.AwayFromZero),
                P50: Math.Round(Percentile(state.Resources, 0.50), 2, MidpointRounding.AwayFromZero),
                P75: Math.Round(Percentile(state.Resources, 0.75), 2, MidpointRounding.AwayFromZero),
                WasSampled: wasSampled);
        }

        result = new HouseholdCombinedScanResult(
            LocalHouseholds: state.LocalHouseholds,
            MovingAwayHouseholds: state.MovingAwayHouseholds,
            HomelessHouseholds: state.HomelessHouseholds,
            PropertyLinkedHouseholds: state.PropertyLinkedHouseholds,
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

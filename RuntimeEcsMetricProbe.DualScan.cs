using System;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Companies;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace CS2DataExport;

public sealed partial class RuntimeEcsMetricProbe
{
    private void MaybeDualScanHouseholdCombined(
        EntityManager entityManager,
        EntityQuery query,
        EcsScanAccumulators.HouseholdCombinedState chunkState,
        int sampleStride)
    {
        if (!_dualScanEnabled)
        {
            return;
        }

        if (sampleStride != 1)
        {
            _log?.Invoke("dual-scan [household]: skipped (chunk stride is " + sampleStride + "; compare only at stride 1)");
            return;
        }

        var legacy = new EcsScanAccumulators.HouseholdCombinedState();
        AccumulateHouseholdCombinedLegacy(entityManager, query, legacy);
        int mismatches = 0;
        mismatches += LogDualScanIntDiff("household", "LocalHouseholds", chunkState.LocalHouseholds, legacy.LocalHouseholds);
        mismatches += LogDualScanIntDiff("household", "MovingAwayHouseholds", chunkState.MovingAwayHouseholds, legacy.MovingAwayHouseholds);
        mismatches += LogDualScanIntDiff("household", "HomelessHouseholds", chunkState.HomelessHouseholds, legacy.HomelessHouseholds);
        mismatches += LogDualScanIntDiff(
            "household",
            "PropertyLinkedHouseholds",
            chunkState.PropertyLinkedHouseholds,
            legacy.PropertyLinkedHouseholds);
        mismatches += LogDualScanIntDiff("household", "Resources.Count", chunkState.Resources.Count, legacy.Resources.Count);
        mismatches += LogDualScanLongDiff(
            "household",
            "Resources.Sum",
            SumIntList(chunkState.Resources),
            SumIntList(legacy.Resources));

        LogDualScanSummary("household", mismatches);
    }

    private void MaybeDualScanPopulationAndWorkforce(
        EntityManager entityManager,
        EntityQuery query,
        EcsScanAccumulators.PopulationWorkforceState chunkState,
        int sampleStride)
    {
        if (!_dualScanEnabled)
        {
            return;
        }

        if (sampleStride != 1)
        {
            _log?.Invoke("dual-scan [population]: skipped (chunk stride is " + sampleStride + "; compare only at stride 1)");
            return;
        }

        var legacy = new EcsScanAccumulators.PopulationWorkforceState();
        AccumulatePopulationAndWorkforceLegacy(entityManager, query, legacy);
        int mismatches = 0;
        mismatches += LogDualScanIntDiff("population", "LocalPopulation", chunkState.LocalPopulation, legacy.LocalPopulation);
        mismatches += LogDualScanIntDiff("population", "TouristPopulation", chunkState.TouristPopulation, legacy.TouristPopulation);
        mismatches += LogDualScanIntDiff("population", "CommuterPopulation", chunkState.CommuterPopulation, legacy.CommuterPopulation);
        mismatches += LogDualScanIntDiff("population", "MovingAwayPopulation", chunkState.MovingAwayPopulation, legacy.MovingAwayPopulation);
        mismatches += LogDualScanIntDiff("population", "HomelessPopulation", chunkState.HomelessPopulation, legacy.HomelessPopulation);
        mismatches += LogDualScanIntDiff("population", "ChildrenPopulation", chunkState.ChildrenPopulation, legacy.ChildrenPopulation);
        mismatches += LogDualScanIntDiff("population", "ElderlyPopulation", chunkState.ElderlyPopulation, legacy.ElderlyPopulation);
        mismatches += LogDualScanIntDiff("population", "WorkingAgePopulation", chunkState.WorkingAgePopulation, legacy.WorkingAgePopulation);
        mismatches += LogDualScanArrayDiff("population", "LocalByEducation", chunkState.LocalByEducation, legacy.LocalByEducation);
        mismatches += LogDualScanArrayDiff("population", "PotentialByEducation", chunkState.PotentialByEducation, legacy.PotentialByEducation);
        mismatches += LogDualScanArrayDiff("population", "WorkersByEducation", chunkState.WorkersByEducation, legacy.WorkersByEducation);
        mismatches += LogDualScanArrayDiff("population", "UnemployedByEducation", chunkState.UnemployedByEducation, legacy.UnemployedByEducation);
        mismatches += LogDualScanArrayDiff("population", "HomelessByEducation", chunkState.HomelessByEducation, legacy.HomelessByEducation);
        mismatches += LogDualScanArrayDiff("population", "OutsideByEducation", chunkState.OutsideByEducation, legacy.OutsideByEducation);
        mismatches += LogDualScanArrayDiff("population", "UnderByEducation", chunkState.UnderByEducation, legacy.UnderByEducation);

        LogDualScanSummary("population", mismatches);
    }

    private void MaybeDualScanWorkplaces(
        EntityManager entityManager,
        EntityQuery query,
        EcsScanAccumulators.WorkplacesScanState chunkState,
        int sampleStride)
    {
        if (!_dualScanEnabled)
        {
            return;
        }

        if (sampleStride != 1)
        {
            _log?.Invoke("dual-scan [workplaces]: skipped (chunk stride is " + sampleStride + "; compare only at stride 1)");
            return;
        }

        var legacy = new EcsScanAccumulators.WorkplacesScanState();
        AccumulateWorkplacesLegacy(entityManager, query, legacy);
        int mismatches = 0;
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersTotal", chunkState.ProvidersTotal, legacy.ProvidersTotal);
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersService", chunkState.ProvidersService, legacy.ProvidersService);
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersCommercial", chunkState.ProvidersCommercial, legacy.ProvidersCommercial);
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersLeisure", chunkState.ProvidersLeisure, legacy.ProvidersLeisure);
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersExtractor", chunkState.ProvidersExtractor, legacy.ProvidersExtractor);
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersIndustrial", chunkState.ProvidersIndustrial, legacy.ProvidersIndustrial);
        mismatches += LogDualScanIntDiff("workplaces", "ProvidersOffice", chunkState.ProvidersOffice, legacy.ProvidersOffice);

        for (int i = 0; i < 5; i++)
        {
            string prefix = "Levels[" + i + "].";
            EcsScanAccumulators.WorkplaceLevelCounter chunkLevel = chunkState.Levels[i];
            EcsScanAccumulators.WorkplaceLevelCounter legacyLevel = legacy.Levels[i];
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Total", chunkLevel.Total, legacyLevel.Total);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Employees", chunkLevel.Employees, legacyLevel.Employees);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Open", chunkLevel.Open, legacyLevel.Open);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Commuter", chunkLevel.Commuter, legacyLevel.Commuter);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Service", chunkLevel.Service, legacyLevel.Service);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Commercial", chunkLevel.Commercial, legacyLevel.Commercial);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Leisure", chunkLevel.Leisure, legacyLevel.Leisure);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Extractor", chunkLevel.Extractor, legacyLevel.Extractor);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Industrial", chunkLevel.Industrial, legacyLevel.Industrial);
            mismatches += LogDualScanIntDiff("workplaces", prefix + "Office", chunkLevel.Office, legacyLevel.Office);
        }

        LogDualScanSummary("workplaces", mismatches);
    }

    private void AccumulateHouseholdCombinedLegacy(
        EntityManager entityManager,
        EntityQuery query,
        EcsScanAccumulators.HouseholdCombinedState state)
    {
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity householdEntity = entities[i];
                Household household = entityManager.GetComponentData<Household>(householdEntity);
                bool hasPropertyLink = entityManager.HasComponent<PropertyRenter>(householdEntity);
                var facts = new EcsScanAccumulators.HouseholdEntityFacts(
                    isMovedIn: (household.m_Flags & HouseholdFlags.MovedIn) != 0,
                    hasPropertyLink: hasPropertyLink,
                    isHomelessHousehold: entityManager.HasComponent<HomelessHousehold>(householdEntity),
                    isMovingAway: entityManager.HasComponent<MovingAway>(householdEntity),
                    isTourist: (household.m_Flags & HouseholdFlags.Tourist) != 0,
                    isCommuter: (household.m_Flags & HouseholdFlags.Commuter) != 0,
                    resources: household.m_Resources);

                EcsScanAccumulators.AccumulateHouseholdCombined(state, in facts);
            }
        }
        finally
        {
            if (entities.IsCreated)
            {
                entities.Dispose();
            }
        }
    }

    private void AccumulatePopulationAndWorkforceLegacy(
        EntityManager entityManager,
        EntityQuery query,
        EcsScanAccumulators.PopulationWorkforceState state)
    {
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity citizenEntity = entities[i];
                Citizen citizen = entityManager.GetComponentData<Citizen>(citizenEntity);
                HouseholdMember householdMember = entityManager.GetComponentData<HouseholdMember>(citizenEntity);
                Entity householdEntity = householdMember.m_Household;

                bool householdMissing = !entityManager.HasComponent<Household>(householdEntity);
                HouseholdFlags householdFlags = HouseholdFlags.None;
                bool isHomeless = false;
                bool isMovingAwayHousehold = false;
                bool isMovedIn = false;
                if (!householdMissing)
                {
                    Household household = entityManager.GetComponentData<Household>(householdEntity);
                    householdFlags = household.m_Flags;
                    isMovedIn = (householdFlags & HouseholdFlags.MovedIn) != 0;
                    isHomeless = entityManager.HasComponent<HomelessHousehold>(householdEntity) ||
                                 !entityManager.HasComponent<PropertyRenter>(householdEntity);
                    isMovingAwayHousehold = entityManager.HasComponent<MovingAway>(householdEntity);
                }

                bool isDead = entityManager.HasComponent<HealthProblem>(citizenEntity) &&
                              CitizenUtils.IsDead(entityManager.GetComponentData<HealthProblem>(citizenEntity));
                CitizenAge age = citizen.GetAge();
                bool hasWorker = entityManager.HasComponent<Worker>(citizenEntity);
                bool workplaceIsOutside = false;
                int workerLevel = 0;
                if (hasWorker)
                {
                    Worker worker = entityManager.GetComponentData<Worker>(citizenEntity);
                    workerLevel = worker.m_Level;
                    workplaceIsOutside = entityManager.HasComponent<Game.Objects.OutsideConnection>(worker.m_Workplace);
                }

                var facts = new EcsScanAccumulators.CitizenEntityFacts(
                    householdMissing: householdMissing,
                    householdFlagsNone: householdFlags == HouseholdFlags.None,
                    isDead: isDead,
                    isTourist: (citizen.m_State & CitizenFlags.Tourist) != 0,
                    isCommuter: (citizen.m_State & CitizenFlags.Commuter) != 0,
                    isMovedIn: isMovedIn,
                    isHomeless: isHomeless,
                    isMovingAwayHousehold: isMovingAwayHousehold,
                    age: (EcsScanAccumulators.CitizenAgeBucket)(int)age,
                    educationLevel: citizen.GetEducationLevel(),
                    isStudent: entityManager.HasComponent<Game.Citizens.Student>(citizenEntity),
                    isWorkingAge: IsWorkingAge(age),
                    hasWorker: hasWorker,
                    workplaceIsOutsideConnection: workplaceIsOutside,
                    workerLevel: workerLevel);

                EcsScanAccumulators.AccumulatePopulationAndWorkforce(state, in facts);
            }
        }
        finally
        {
            if (entities.IsCreated)
            {
                entities.Dispose();
            }
        }
    }

    private void AccumulateWorkplacesLegacy(
        EntityManager entityManager,
        EntityQuery query,
        EcsScanAccumulators.WorkplacesScanState state)
    {
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            var commuterByLevel = new int[5];
            for (int i = 0; i < entities.Length; i++)
            {
                Entity providerEntity = entities[i];
                PrefabRef prefabRef = entityManager.GetComponentData<PrefabRef>(providerEntity);
                Entity providerPrefab = prefabRef.m_Prefab;

                if (!entityManager.HasComponent<WorkplaceData>(providerPrefab))
                {
                    continue;
                }

                WorkProvider workProvider = entityManager.GetComponentData<WorkProvider>(providerEntity);
                WorkplaceData workplaceData = entityManager.GetComponentData<WorkplaceData>(providerPrefab);
                DynamicBuffer<Employee> employees = entityManager.GetBuffer<Employee>(providerEntity);

                int buildingLevel = 1;
                if (entityManager.HasComponent<PropertyRenter>(providerEntity))
                {
                    Entity propertyEntity = entityManager.GetComponentData<PropertyRenter>(providerEntity).m_Property;
                    if (entityManager.HasComponent<PrefabRef>(propertyEntity))
                    {
                        Entity propertyPrefab = entityManager.GetComponentData<PrefabRef>(propertyEntity).m_Prefab;
                        if (entityManager.HasComponent<SpawnableBuildingData>(propertyPrefab))
                        {
                            buildingLevel = (int)entityManager.GetComponentData<SpawnableBuildingData>(propertyPrefab).m_Level;
                        }
                    }
                }

                EmploymentData workplacesData = EmploymentData.GetWorkplacesData(
                    workProvider.m_MaxWorkers,
                    buildingLevel,
                    workplaceData.m_Complexity);

                int freePositions = Math.Max(0, workplacesData.total - employees.Length);
                EmploymentData employeesData = EmploymentData.GetEmployeesData(employees, freePositions);

                bool isExtractor = entityManager.HasComponent<Game.Companies.ExtractorCompany>(providerEntity);
                bool isIndustrial = entityManager.HasComponent<Game.Companies.IndustrialCompany>(providerEntity);
                bool isCommercial = entityManager.HasComponent<Game.Companies.CommercialCompany>(providerEntity);
                bool isService = !isIndustrial && !isCommercial;

                bool isOffice = false;
                bool isLeisure = false;
                if (entityManager.HasComponent<IndustrialProcessData>(providerPrefab))
                {
                    IndustrialProcessData process = entityManager.GetComponentData<IndustrialProcessData>(providerPrefab);
                    Resource output = process.m_Output.m_Resource;
                    isLeisure = (output & kLeisureResources) != Resource.NoResource;
                    isOffice = (output & kOfficeResources) != Resource.NoResource;
                }

                for (int c = 0; c < 5; c++)
                {
                    commuterByLevel[c] = 0;
                }

                for (int e = 0; e < employees.Length; e++)
                {
                    Employee employee = employees[e];
                    Entity workerEntity = employee.m_Worker;
                    if (!entityManager.HasComponent<Citizen>(workerEntity))
                    {
                        continue;
                    }

                    Citizen workerCitizen = entityManager.GetComponentData<Citizen>(workerEntity);
                    if ((workerCitizen.m_State & CitizenFlags.Commuter) != 0)
                    {
                        int level = ClampEducationLevel(employee.m_Level);
                        commuterByLevel[level]++;
                    }
                }

                var facts = new EcsScanAccumulators.WorkplaceProviderFacts(
                    hasWorkplaceData: true,
                    workplacesUneducated: workplacesData.uneducated,
                    workplacesPoorlyEducated: workplacesData.poorlyEducated,
                    workplacesEducated: workplacesData.educated,
                    workplacesWellEducated: workplacesData.wellEducated,
                    workplacesHighlyEducated: workplacesData.highlyEducated,
                    employeesUneducated: employeesData.uneducated,
                    employeesPoorlyEducated: employeesData.poorlyEducated,
                    employeesEducated: employeesData.educated,
                    employeesWellEducated: employeesData.wellEducated,
                    employeesHighlyEducated: employeesData.highlyEducated,
                    commutersUneducated: commuterByLevel[0],
                    commutersPoorlyEducated: commuterByLevel[1],
                    commutersEducated: commuterByLevel[2],
                    commutersWellEducated: commuterByLevel[3],
                    commutersHighlyEducated: commuterByLevel[4],
                    isService: isService,
                    isCommercial: isCommercial,
                    isLeisure: isLeisure,
                    isExtractor: isExtractor,
                    isOffice: isOffice);

                EcsScanAccumulators.AccumulateWorkplaceProvider(state, in facts);
            }
        }
        finally
        {
            if (entities.IsCreated)
            {
                entities.Dispose();
            }
        }
    }

    private int LogDualScanIntDiff(string scan, string field, int chunkValue, int legacyValue)
    {
        if (chunkValue == legacyValue)
        {
            return 0;
        }

        _log?.Invoke(
            "dual-scan mismatch [" + scan + "]." + field +
            ": chunk=" + chunkValue + " legacy=" + legacyValue);
        return 1;
    }

    private int LogDualScanLongDiff(string scan, string field, long chunkValue, long legacyValue)
    {
        if (chunkValue == legacyValue)
        {
            return 0;
        }

        _log?.Invoke(
            "dual-scan mismatch [" + scan + "]." + field +
            ": chunk=" + chunkValue + " legacy=" + legacyValue);
        return 1;
    }

    private int LogDualScanArrayDiff(string scan, string field, int[] chunkValues, int[] legacyValues)
    {
        int mismatches = 0;
        int length = Math.Min(chunkValues.Length, legacyValues.Length);
        for (int i = 0; i < length; i++)
        {
            mismatches += LogDualScanIntDiff(scan, field + "[" + i + "]", chunkValues[i], legacyValues[i]);
        }

        if (chunkValues.Length != legacyValues.Length)
        {
            mismatches += LogDualScanIntDiff(scan, field + ".Length", chunkValues.Length, legacyValues.Length);
        }

        return mismatches;
    }

    private void LogDualScanSummary(string scan, int mismatches)
    {
        if (mismatches == 0)
        {
            _log?.Invoke("dual-scan OK [" + scan + "]: all compared fields matched at stride 1");
            return;
        }

        _log?.Invoke("dual-scan FAILED [" + scan + "]: " + mismatches + " field mismatch(es) at stride 1");
    }

    private static long SumIntList(System.Collections.Generic.List<int> values)
    {
        long sum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum;
    }
}

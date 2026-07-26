using System;
using System.Collections.Generic;

namespace CS2DataExport;

/// <summary>
/// Pure sampling math and per-entity accumulation for the three ECS scans.
/// Kept free of Unity.Entities / Game.dll so offline unit tests can exercise the same logic
/// the in-game ToArchetypeChunkArray loops call. Chunk iteration should call these too.
/// </summary>
public static class EcsScanAccumulators
{
    /// <summary>
    /// Returns the stride used when sampling entity arrays.
    /// <paramref name="maxSamples"/> of 0 or less means exact (stride 1).
    /// </summary>
    public static int ComputeSamplingStride(int totalCount, int maxSamples)
    {
        if (maxSamples <= 0 || totalCount <= maxSamples)
        {
            return 1;
        }

        return (int)Math.Ceiling(totalCount / (double)maxSamples);
    }

    /// <summary>
    /// Extrapolates a sampled counter back toward the full population.
    /// When stride is 1 the value is returned unchanged. Scaled values are capped at
    /// <paramref name="maxCap"/> when maxCap is positive (typically the entity array length).
    /// </summary>
    public static int ScaleSampledCount(int sampledCount, int sampleStride, int maxCap)
    {
        if (sampleStride <= 1)
        {
            return sampledCount;
        }

        long scaled = (long)sampledCount * sampleStride;
        if (maxCap > 0 && scaled > maxCap)
        {
            scaled = maxCap;
        }

        return (int)Math.Max(0, scaled);
    }

    public static void ScaleSampledArray(int[] values, int sampleStride, int maxCap)
    {
        if (sampleStride <= 1)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = ScaleSampledCount(values[i], sampleStride, maxCap);
        }
    }

    /// <summary>
    /// Plain facts for one household entity after ECS component reads.
    /// </summary>
    public readonly struct HouseholdEntityFacts
    {
        public HouseholdEntityFacts(
            bool isMovedIn,
            bool hasPropertyLink,
            bool isHomelessHousehold,
            bool isMovingAway,
            bool isTourist,
            bool isCommuter,
            int resources)
        {
            IsMovedIn = isMovedIn;
            HasPropertyLink = hasPropertyLink;
            IsHomelessHousehold = isHomelessHousehold;
            IsMovingAway = isMovingAway;
            IsTourist = isTourist;
            IsCommuter = isCommuter;
            Resources = resources;
        }

        public bool IsMovedIn { get; }
        public bool HasPropertyLink { get; }
        public bool IsHomelessHousehold { get; }
        public bool IsMovingAway { get; }
        public bool IsTourist { get; }
        public bool IsCommuter { get; }
        public int Resources { get; }
    }

    public sealed class HouseholdCombinedState
    {
        public int LocalHouseholds;
        public int MovingAwayHouseholds;
        public int HomelessHouseholds;
        public int PropertyLinkedHouseholds;
        public readonly List<int> Resources = new(capacity: 4096);
    }

    /// <summary>
    /// Accumulate one household into combined pressure + economy state.
    /// Mirrors the body of TryScanHouseholdCombined's per-entity loop.
    /// </summary>
    public static void AccumulateHouseholdCombined(HouseholdCombinedState state, in HouseholdEntityFacts facts)
    {
        if (!facts.IsMovedIn)
        {
            return;
        }

        state.LocalHouseholds++;

        bool isHomeless = facts.IsHomelessHousehold || !facts.HasPropertyLink;
        if (isHomeless)
        {
            state.HomelessHouseholds++;
        }

        if (facts.HasPropertyLink)
        {
            state.PropertyLinkedHouseholds++;
        }

        if (facts.IsMovingAway)
        {
            state.MovingAwayHouseholds++;
        }

        if (!facts.IsTourist && !facts.IsCommuter)
        {
            state.Resources.Add(facts.Resources);
        }
    }

    /// <summary>
    /// Age bucket matching Game.Citizens.CitizenAge ordinals used by the probe.
    /// Child = 0, Teen = 1, Adult = 2, Elderly = 3.
    /// </summary>
    public enum CitizenAgeBucket
    {
        Child = 0,
        Teen = 1,
        Adult = 2,
        Elderly = 3
    }

    /// <summary>
    /// Plain facts for one citizen after ECS component reads (no EntityManager).
    /// </summary>
    public readonly struct CitizenEntityFacts
    {
        public CitizenEntityFacts(
            bool householdMissing,
            bool householdFlagsNone,
            bool isDead,
            bool isTourist,
            bool isCommuter,
            bool isMovedIn,
            bool isHomeless,
            bool isMovingAwayHousehold,
            CitizenAgeBucket age,
            int educationLevel,
            bool isStudent,
            bool isWorkingAge,
            bool hasWorker,
            bool workplaceIsOutsideConnection,
            int workerLevel)
        {
            HouseholdMissing = householdMissing;
            HouseholdFlagsNone = householdFlagsNone;
            IsDead = isDead;
            IsTourist = isTourist;
            IsCommuter = isCommuter;
            IsMovedIn = isMovedIn;
            IsHomeless = isHomeless;
            IsMovingAwayHousehold = isMovingAwayHousehold;
            Age = age;
            EducationLevel = educationLevel;
            IsStudent = isStudent;
            IsWorkingAge = isWorkingAge;
            HasWorker = hasWorker;
            WorkplaceIsOutsideConnection = workplaceIsOutsideConnection;
            WorkerLevel = workerLevel;
        }

        public bool HouseholdMissing { get; }
        public bool HouseholdFlagsNone { get; }
        public bool IsDead { get; }
        public bool IsTourist { get; }
        public bool IsCommuter { get; }
        public bool IsMovedIn { get; }
        public bool IsHomeless { get; }
        public bool IsMovingAwayHousehold { get; }
        public CitizenAgeBucket Age { get; }
        public int EducationLevel { get; }
        public bool IsStudent { get; }
        public bool IsWorkingAge { get; }
        public bool HasWorker { get; }
        public bool WorkplaceIsOutsideConnection { get; }
        public int WorkerLevel { get; }
    }

    public sealed class PopulationWorkforceState
    {
        public readonly int[] LocalByEducation = new int[5];
        public readonly int[] PotentialByEducation = new int[5];
        public readonly int[] WorkersByEducation = new int[5];
        public readonly int[] UnemployedByEducation = new int[5];
        public readonly int[] HomelessByEducation = new int[5];
        public readonly int[] OutsideByEducation = new int[5];
        public readonly int[] UnderByEducation = new int[5];

        public int LocalPopulation;
        public int TouristPopulation;
        public int CommuterPopulation;
        public int MovingAwayPopulation;
        public int HomelessPopulation;
        public int ChildrenPopulation;
        public int ElderlyPopulation;
        public int WorkingAgePopulation;
    }

    /// <summary>
    /// Accumulate one citizen into population/workforce counters.
    /// Mirrors TryScanPopulationAndWorkforce's per-entity loop after component reads.
    /// </summary>
    public static void AccumulatePopulationAndWorkforce(PopulationWorkforceState state, in CitizenEntityFacts facts)
    {
        if (facts.HouseholdMissing || facts.HouseholdFlagsNone || facts.IsDead)
        {
            return;
        }

        if (facts.IsTourist)
        {
            state.TouristPopulation++;
            return;
        }

        if (facts.IsCommuter)
        {
            state.CommuterPopulation++;
            return;
        }

        if (!facts.IsMovedIn)
        {
            return;
        }

        if (facts.IsMovingAwayHousehold)
        {
            state.MovingAwayPopulation++;
            return;
        }

        state.LocalPopulation++;

        if (facts.IsHomeless)
        {
            state.HomelessPopulation++;
        }

        if (facts.Age == CitizenAgeBucket.Child)
        {
            state.ChildrenPopulation++;
        }
        else if (facts.Age == CitizenAgeBucket.Elderly)
        {
            state.ElderlyPopulation++;
        }
        else
        {
            state.WorkingAgePopulation++;
        }

        int educationLevel = ClampEducationLevel(facts.EducationLevel);
        state.LocalByEducation[educationLevel]++;

        if (!facts.IsWorkingAge || facts.IsStudent)
        {
            return;
        }

        state.PotentialByEducation[educationLevel]++;
        if (facts.HasWorker)
        {
            state.WorkersByEducation[educationLevel]++;

            if (facts.WorkplaceIsOutsideConnection)
            {
                state.OutsideByEducation[educationLevel]++;
            }

            if (facts.WorkerLevel < educationLevel)
            {
                state.UnderByEducation[educationLevel]++;
            }
        }
        else
        {
            state.UnemployedByEducation[educationLevel]++;
            if (facts.IsHomeless)
            {
                state.HomelessByEducation[educationLevel]++;
            }
        }
    }

    public struct WorkplaceLevelCounter
    {
        public int Total;
        public int Service;
        public int Commercial;
        public int Leisure;
        public int Extractor;
        public int Industrial;
        public int Office;
        public int ServiceEmployees;
        public int CommercialEmployees;
        public int LeisureEmployees;
        public int ExtractorEmployees;
        public int IndustrialEmployees;
        public int OfficeEmployees;
        public int Employees;
        public int Open;
        public int Commuter;
    }

    public sealed class WorkplacesScanState
    {
        public readonly WorkplaceLevelCounter[] Levels = new WorkplaceLevelCounter[5];
        public int ProvidersTotal;
        public int ProvidersService;
        public int ProvidersCommercial;
        public int ProvidersLeisure;
        public int ProvidersExtractor;
        public int ProvidersIndustrial;
        public int ProvidersOffice;
    }

    /// <summary>
    /// Plain facts for one workplace provider after ECS reads (employees already summarized).
    /// </summary>
    public readonly struct WorkplaceProviderFacts
    {
        public WorkplaceProviderFacts(
            bool hasWorkplaceData,
            int workplacesUneducated,
            int workplacesPoorlyEducated,
            int workplacesEducated,
            int workplacesWellEducated,
            int workplacesHighlyEducated,
            int employeesUneducated,
            int employeesPoorlyEducated,
            int employeesEducated,
            int employeesWellEducated,
            int employeesHighlyEducated,
            int commutersUneducated,
            int commutersPoorlyEducated,
            int commutersEducated,
            int commutersWellEducated,
            int commutersHighlyEducated,
            bool isService,
            bool isCommercial,
            bool isLeisure,
            bool isExtractor,
            bool isOffice)
        {
            HasWorkplaceData = hasWorkplaceData;
            WorkplacesUneducated = workplacesUneducated;
            WorkplacesPoorlyEducated = workplacesPoorlyEducated;
            WorkplacesEducated = workplacesEducated;
            WorkplacesWellEducated = workplacesWellEducated;
            WorkplacesHighlyEducated = workplacesHighlyEducated;
            EmployeesUneducated = employeesUneducated;
            EmployeesPoorlyEducated = employeesPoorlyEducated;
            EmployeesEducated = employeesEducated;
            EmployeesWellEducated = employeesWellEducated;
            EmployeesHighlyEducated = employeesHighlyEducated;
            CommutersUneducated = commutersUneducated;
            CommutersPoorlyEducated = commutersPoorlyEducated;
            CommutersEducated = commutersEducated;
            CommutersWellEducated = commutersWellEducated;
            CommutersHighlyEducated = commutersHighlyEducated;
            IsService = isService;
            IsCommercial = isCommercial;
            IsLeisure = isLeisure;
            IsExtractor = isExtractor;
            IsOffice = isOffice;
        }

        public bool HasWorkplaceData { get; }
        public int WorkplacesUneducated { get; }
        public int WorkplacesPoorlyEducated { get; }
        public int WorkplacesEducated { get; }
        public int WorkplacesWellEducated { get; }
        public int WorkplacesHighlyEducated { get; }
        public int EmployeesUneducated { get; }
        public int EmployeesPoorlyEducated { get; }
        public int EmployeesEducated { get; }
        public int EmployeesWellEducated { get; }
        public int EmployeesHighlyEducated { get; }
        public int CommutersUneducated { get; }
        public int CommutersPoorlyEducated { get; }
        public int CommutersEducated { get; }
        public int CommutersWellEducated { get; }
        public int CommutersHighlyEducated { get; }
        public bool IsService { get; }
        public bool IsCommercial { get; }
        public bool IsLeisure { get; }
        public bool IsExtractor { get; }
        public bool IsOffice { get; }
    }

    /// <summary>
    /// Accumulate one workplace provider into level counters and provider totals.
    /// EmploymentData.GetWorkplacesData / GetEmployeesData and employee buffer walks stay in the probe
    /// (Game.dll); only the counter updates are extracted here.
    /// </summary>
    public static void AccumulateWorkplaceProvider(WorkplacesScanState state, in WorkplaceProviderFacts facts)
    {
        if (!facts.HasWorkplaceData)
        {
            return;
        }

        AccumulateWorkplaceLevel(
            state.Levels,
            level: 0,
            workplaces: facts.WorkplacesUneducated,
            employees: facts.EmployeesUneducated,
            commuters: facts.CommutersUneducated,
            isService: facts.IsService,
            isCommercial: facts.IsCommercial,
            isLeisure: facts.IsLeisure,
            isExtractor: facts.IsExtractor,
            isOffice: facts.IsOffice);

        AccumulateWorkplaceLevel(
            state.Levels,
            level: 1,
            workplaces: facts.WorkplacesPoorlyEducated,
            employees: facts.EmployeesPoorlyEducated,
            commuters: facts.CommutersPoorlyEducated,
            isService: facts.IsService,
            isCommercial: facts.IsCommercial,
            isLeisure: facts.IsLeisure,
            isExtractor: facts.IsExtractor,
            isOffice: facts.IsOffice);

        AccumulateWorkplaceLevel(
            state.Levels,
            level: 2,
            workplaces: facts.WorkplacesEducated,
            employees: facts.EmployeesEducated,
            commuters: facts.CommutersEducated,
            isService: facts.IsService,
            isCommercial: facts.IsCommercial,
            isLeisure: facts.IsLeisure,
            isExtractor: facts.IsExtractor,
            isOffice: facts.IsOffice);

        AccumulateWorkplaceLevel(
            state.Levels,
            level: 3,
            workplaces: facts.WorkplacesWellEducated,
            employees: facts.EmployeesWellEducated,
            commuters: facts.CommutersWellEducated,
            isService: facts.IsService,
            isCommercial: facts.IsCommercial,
            isLeisure: facts.IsLeisure,
            isExtractor: facts.IsExtractor,
            isOffice: facts.IsOffice);

        AccumulateWorkplaceLevel(
            state.Levels,
            level: 4,
            workplaces: facts.WorkplacesHighlyEducated,
            employees: facts.EmployeesHighlyEducated,
            commuters: facts.CommutersHighlyEducated,
            isService: facts.IsService,
            isCommercial: facts.IsCommercial,
            isLeisure: facts.IsLeisure,
            isExtractor: facts.IsExtractor,
            isOffice: facts.IsOffice);

        state.ProvidersTotal++;
        if (facts.IsService)
        {
            state.ProvidersService++;
        }
        else if (facts.IsCommercial)
        {
            if (facts.IsLeisure)
            {
                state.ProvidersLeisure++;
            }
            else
            {
                state.ProvidersCommercial++;
            }
        }
        else if (facts.IsExtractor)
        {
            state.ProvidersExtractor++;
        }
        else if (facts.IsOffice)
        {
            state.ProvidersOffice++;
        }
        else
        {
            state.ProvidersIndustrial++;
        }
    }

    public static void AccumulateWorkplaceLevel(
        WorkplaceLevelCounter[] levels,
        int level,
        int workplaces,
        int employees,
        int commuters,
        bool isService,
        bool isCommercial,
        bool isLeisure,
        bool isExtractor,
        bool isOffice)
    {
        ref WorkplaceLevelCounter counter = ref levels[level];
        counter.Total += workplaces;

        if (isService)
        {
            counter.Service += workplaces;
            counter.ServiceEmployees += employees;
        }
        else if (isCommercial)
        {
            if (isLeisure)
            {
                counter.Leisure += workplaces;
                counter.LeisureEmployees += employees;
            }
            else
            {
                counter.Commercial += workplaces;
                counter.CommercialEmployees += employees;
            }
        }
        else if (isExtractor)
        {
            counter.Extractor += workplaces;
            counter.ExtractorEmployees += employees;
        }
        else if (isOffice)
        {
            counter.Office += workplaces;
            counter.OfficeEmployees += employees;
        }
        else
        {
            counter.Industrial += workplaces;
            counter.IndustrialEmployees += employees;
        }

        counter.Employees += employees;
        counter.Open += workplaces - employees;
        counter.Commuter += commuters;
    }

    public static void ScaleWorkplaceCounters(WorkplaceLevelCounter[] counters, int sampleStride, int maxCap)
    {
        if (sampleStride <= 1)
        {
            return;
        }

        for (int i = 0; i < counters.Length; i++)
        {
            ref WorkplaceLevelCounter counter = ref counters[i];
            counter.Total = ScaleSampledCount(counter.Total, sampleStride, maxCap);
            counter.Service = ScaleSampledCount(counter.Service, sampleStride, maxCap);
            counter.Commercial = ScaleSampledCount(counter.Commercial, sampleStride, maxCap);
            counter.Leisure = ScaleSampledCount(counter.Leisure, sampleStride, maxCap);
            counter.Extractor = ScaleSampledCount(counter.Extractor, sampleStride, maxCap);
            counter.Industrial = ScaleSampledCount(counter.Industrial, sampleStride, maxCap);
            counter.Office = ScaleSampledCount(counter.Office, sampleStride, maxCap);
            counter.ServiceEmployees = ScaleSampledCount(counter.ServiceEmployees, sampleStride, maxCap);
            counter.CommercialEmployees = ScaleSampledCount(counter.CommercialEmployees, sampleStride, maxCap);
            counter.LeisureEmployees = ScaleSampledCount(counter.LeisureEmployees, sampleStride, maxCap);
            counter.ExtractorEmployees = ScaleSampledCount(counter.ExtractorEmployees, sampleStride, maxCap);
            counter.IndustrialEmployees = ScaleSampledCount(counter.IndustrialEmployees, sampleStride, maxCap);
            counter.OfficeEmployees = ScaleSampledCount(counter.OfficeEmployees, sampleStride, maxCap);
            counter.Employees = ScaleSampledCount(counter.Employees, sampleStride, maxCap);
            counter.Open = ScaleSampledCount(counter.Open, sampleStride, maxCap);
            counter.Commuter = ScaleSampledCount(counter.Commuter, sampleStride, maxCap);
        }
    }

    public static int ClampEducationLevel(int level)
    {
        if (level < 0)
        {
            return 0;
        }

        if (level > 4)
        {
            return 4;
        }

        return level;
    }
}

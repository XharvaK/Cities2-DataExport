using Xunit;

namespace CS2DataExport.Tests;

/// <summary>
/// Offline gates for Wave 5a scan math. Game.dll / Unity.Entities types are not referenced here.
/// Not extracted offline: EmploymentData.GetWorkplacesData / GetEmployeesData, CitizenUtils.IsDead,
/// and EntityManager component reads — the probe still gathers those into plain facts before calling
/// the Accumulate* helpers under test.
/// </summary>
public sealed class EcsScanAccumulatorsTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(100, 0, 1)]
    [InlineData(100, -5, 1)]
    [InlineData(50, 100, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(101, 100, 2)]
    [InlineData(250, 100, 3)]
    [InlineData(1000, 1000, 1)]
    [InlineData(3803, 1000, 4)]
    public void ComputeSamplingStride_EdgeCases(int totalCount, int maxSamples, int expectedStride)
    {
        Assert.Equal(expectedStride, EcsScanAccumulators.ComputeSamplingStride(totalCount, maxSamples));
    }

    [Fact]
    public void ScaleSampledCount_StrideOne_ReturnsUnchanged()
    {
        Assert.Equal(42, EcsScanAccumulators.ScaleSampledCount(42, sampleStride: 1, maxCap: 100));
    }

    [Fact]
    public void ScaleSampledCount_ExtrapolatesAndCapsAtMaxCap()
    {
        Assert.Equal(40, EcsScanAccumulators.ScaleSampledCount(10, sampleStride: 4, maxCap: 100));
        Assert.Equal(100, EcsScanAccumulators.ScaleSampledCount(40, sampleStride: 4, maxCap: 100));
    }

    [Fact]
    public void ScaleSampledCount_MaxCapZero_DoesNotCap()
    {
        Assert.Equal(400, EcsScanAccumulators.ScaleSampledCount(100, sampleStride: 4, maxCap: 0));
    }

    [Fact]
    public void AccumulateHouseholdCombined_IgnoresNotMovedIn()
    {
        var state = new EcsScanAccumulators.HouseholdCombinedState();
        EcsScanAccumulators.AccumulateHouseholdCombined(
            state,
            new EcsScanAccumulators.HouseholdEntityFacts(
                isMovedIn: false,
                hasPropertyLink: true,
                isHomelessHousehold: false,
                isMovingAway: false,
                isTourist: false,
                isCommuter: false,
                resources: 10));

        Assert.Equal(0, state.LocalHouseholds);
        Assert.Empty(state.Resources);
    }

    [Fact]
    public void AccumulateHouseholdCombined_CountsHomelessMovingAwayAndWealth()
    {
        var state = new EcsScanAccumulators.HouseholdCombinedState();

        EcsScanAccumulators.AccumulateHouseholdCombined(
            state,
            new EcsScanAccumulators.HouseholdEntityFacts(
                isMovedIn: true,
                hasPropertyLink: false,
                isHomelessHousehold: false,
                isMovingAway: true,
                isTourist: false,
                isCommuter: false,
                resources: 25));

        EcsScanAccumulators.AccumulateHouseholdCombined(
            state,
            new EcsScanAccumulators.HouseholdEntityFacts(
                isMovedIn: true,
                hasPropertyLink: true,
                isHomelessHousehold: true,
                isMovingAway: false,
                isTourist: true,
                isCommuter: false,
                resources: 99));

        Assert.Equal(2, state.LocalHouseholds);
        Assert.Equal(1, state.MovingAwayHouseholds);
        Assert.Equal(2, state.HomelessHouseholds);
        Assert.Equal(1, state.PropertyLinkedHouseholds);
        Assert.Equal(new[] { 25 }, state.Resources);
    }

    [Fact]
    public void AccumulatePopulationAndWorkforce_DeadTouristCommuterMovingAway()
    {
        var state = new EcsScanAccumulators.PopulationWorkforceState();

        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(isDead: true));
        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(isTourist: true));
        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(isCommuter: true));
        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(isMovingAwayHousehold: true));

        Assert.Equal(0, state.LocalPopulation);
        Assert.Equal(1, state.TouristPopulation);
        Assert.Equal(1, state.CommuterPopulation);
        Assert.Equal(1, state.MovingAwayPopulation);
    }

    [Fact]
    public void AccumulatePopulationAndWorkforce_StudentSkippedFromPotential_UnderemployedCounted()
    {
        var state = new EcsScanAccumulators.PopulationWorkforceState();

        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(
                age: EcsScanAccumulators.CitizenAgeBucket.Adult,
                educationLevel: 3,
                isStudent: true,
                isWorkingAge: true));

        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(
                age: EcsScanAccumulators.CitizenAgeBucket.Adult,
                educationLevel: 3,
                isWorkingAge: true,
                hasWorker: true,
                workerLevel: 1,
                workplaceIsOutsideConnection: true));

        EcsScanAccumulators.AccumulatePopulationAndWorkforce(
            state,
            MakeCitizen(
                age: EcsScanAccumulators.CitizenAgeBucket.Adult,
                educationLevel: 2,
                isWorkingAge: true,
                isHomeless: true,
                hasWorker: false));

        Assert.Equal(3, state.LocalPopulation);
        Assert.Equal(1, state.HomelessPopulation);
        Assert.Equal(1, state.PotentialByEducation[3]); // worker only; student skipped
        Assert.Equal(1, state.WorkersByEducation[3]);
        Assert.Equal(1, state.OutsideByEducation[3]);
        Assert.Equal(1, state.UnderByEducation[3]);
        Assert.Equal(1, state.UnemployedByEducation[2]);
        Assert.Equal(1, state.HomelessByEducation[2]);
    }

    [Fact]
    public void AccumulateWorkplaceProvider_ClassifiesServiceAndLeisure()
    {
        var state = new EcsScanAccumulators.WorkplacesScanState();

        EcsScanAccumulators.AccumulateWorkplaceProvider(
            state,
            new EcsScanAccumulators.WorkplaceProviderFacts(
                hasWorkplaceData: true,
                workplacesUneducated: 2,
                workplacesPoorlyEducated: 0,
                workplacesEducated: 0,
                workplacesWellEducated: 0,
                workplacesHighlyEducated: 0,
                employeesUneducated: 1,
                employeesPoorlyEducated: 0,
                employeesEducated: 0,
                employeesWellEducated: 0,
                employeesHighlyEducated: 0,
                commutersUneducated: 1,
                commutersPoorlyEducated: 0,
                commutersEducated: 0,
                commutersWellEducated: 0,
                commutersHighlyEducated: 0,
                isService: true,
                isCommercial: false,
                isLeisure: false,
                isExtractor: false,
                isOffice: false));

        EcsScanAccumulators.AccumulateWorkplaceProvider(
            state,
            new EcsScanAccumulators.WorkplaceProviderFacts(
                hasWorkplaceData: true,
                workplacesUneducated: 0,
                workplacesPoorlyEducated: 3,
                workplacesEducated: 0,
                workplacesWellEducated: 0,
                workplacesHighlyEducated: 0,
                employeesUneducated: 0,
                employeesPoorlyEducated: 2,
                employeesEducated: 0,
                employeesWellEducated: 0,
                employeesHighlyEducated: 0,
                commutersUneducated: 0,
                commutersPoorlyEducated: 0,
                commutersEducated: 0,
                commutersWellEducated: 0,
                commutersHighlyEducated: 0,
                isService: false,
                isCommercial: true,
                isLeisure: true,
                isExtractor: false,
                isOffice: false));

        Assert.Equal(2, state.ProvidersTotal);
        Assert.Equal(1, state.ProvidersService);
        Assert.Equal(1, state.ProvidersLeisure);
        Assert.Equal(2, state.Levels[0].Service);
        Assert.Equal(1, state.Levels[0].ServiceEmployees);
        Assert.Equal(1, state.Levels[0].Open);
        Assert.Equal(1, state.Levels[0].Commuter);
        Assert.Equal(3, state.Levels[1].Leisure);
        Assert.Equal(2, state.Levels[1].LeisureEmployees);
    }

    [Fact]
    public void AccumulateWorkplaceProvider_SkipsWhenNoWorkplaceData()
    {
        var state = new EcsScanAccumulators.WorkplacesScanState();
        EcsScanAccumulators.AccumulateWorkplaceProvider(
            state,
            new EcsScanAccumulators.WorkplaceProviderFacts(
                hasWorkplaceData: false,
                workplacesUneducated: 9,
                workplacesPoorlyEducated: 0,
                workplacesEducated: 0,
                workplacesWellEducated: 0,
                workplacesHighlyEducated: 0,
                employeesUneducated: 0,
                employeesPoorlyEducated: 0,
                employeesEducated: 0,
                employeesWellEducated: 0,
                employeesHighlyEducated: 0,
                commutersUneducated: 0,
                commutersPoorlyEducated: 0,
                commutersEducated: 0,
                commutersWellEducated: 0,
                commutersHighlyEducated: 0,
                isService: true,
                isCommercial: false,
                isLeisure: false,
                isExtractor: false,
                isOffice: false));

        Assert.Equal(0, state.ProvidersTotal);
        Assert.Equal(0, state.Levels[0].Total);
    }

    private static EcsScanAccumulators.CitizenEntityFacts MakeCitizen(
        bool householdMissing = false,
        bool householdFlagsNone = false,
        bool isDead = false,
        bool isTourist = false,
        bool isCommuter = false,
        bool isMovedIn = true,
        bool isHomeless = false,
        bool isMovingAwayHousehold = false,
        EcsScanAccumulators.CitizenAgeBucket age = EcsScanAccumulators.CitizenAgeBucket.Adult,
        int educationLevel = 0,
        bool isStudent = false,
        bool isWorkingAge = false,
        bool hasWorker = false,
        bool workplaceIsOutsideConnection = false,
        int workerLevel = 0)
    {
        return new EcsScanAccumulators.CitizenEntityFacts(
            householdMissing,
            householdFlagsNone,
            isDead,
            isTourist,
            isCommuter,
            isMovedIn,
            isHomeless,
            isMovingAwayHousehold,
            age,
            educationLevel,
            isStudent,
            isWorkingAge,
            hasWorker,
            workplaceIsOutsideConnection,
            workerLevel);
    }
}

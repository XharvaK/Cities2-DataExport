using System;

using System.Collections.Generic;

using Game.Citizens;

using Game.Common;

using Game.Net;

using Game.Objects;

using Game.Pathfind;

using Game.Prefabs;

using Unity.Collections;

using Unity.Entities;



namespace CS2DataExport;



using NetOutsideConnection = Game.Net.OutsideConnection;

using ObjectOutsideConnection = Game.Objects.OutsideConnection;

using ObjectTransform = Game.Objects.Transform;

using RouteTransportStop = Game.Routes.TransportStop;



public sealed class TransitAccessGapRuntimeObserver

{

    private const ulong TripGraceTicks = 2048;

    private const string NoProvenPassengerCarrierNote = "no proven passenger-trip runtime carrier";



    private readonly TransitAccessGapCaptureCoordinator _coordinator;

    private readonly Dictionary<Entity, ActiveTrip> _activeTrips = new();

    private readonly List<Entity> _tripRemovals = new();

    private ulong _observeTick;

    private int _simulationFrameCounter;

    private bool _capturePrepared;
    private bool _wasCaptureActive;
    private bool _passengerTripCarrierAvailable;

    private int _lastObservedStopCount = -1;

    private EntityQuery? _citizenPathQuery;

    private EntityQuery? _stopQuery;



    public TransitAccessGapRuntimeObserver(TransitAccessGapCaptureCoordinator coordinator)

    {

        _coordinator = coordinator;

    }



    public void ResetForCaptureWindow()

    {

        _capturePrepared = false;

        _passengerTripCarrierAvailable = false;

        _lastObservedStopCount = -1;

        _simulationFrameCounter = 0;

        _activeTrips.Clear();

        _tripRemovals.Clear();

    }



    public void Observe(EntityManager entityManager, ExportSettings settings)
    {
        if (!_coordinator.IsCaptureActive)
        {
            _wasCaptureActive = false;
            return;
        }

        if (!_wasCaptureActive)
        {
            ResetForCaptureWindow();
            _wasCaptureActive = true;
        }

        _simulationFrameCounter++;
        int observeEveryNFrames = settings.EffectiveTransitObserveEveryNFrames;
        if (_simulationFrameCounter % observeEveryNFrames != 0)
        {
            return;
        }

        if (!_capturePrepared)
        {
            _passengerTripCarrierAvailable = HasProvenPassengerTripCarrier(entityManager);
            _capturePrepared = true;
            if (!_passengerTripCarrierAvailable)
            {
                _coordinator.MarkPassengerTripCarrierUnavailable(NoProvenPassengerCarrierNote);
                return;
            }

            ReplaceStops(entityManager, force: true);
        }
        else if (!_passengerTripCarrierAvailable)
        {
            return;
        }

        _observeTick++;

        EntityQuery citizenPathQuery = GetCitizenPathQuery(entityManager);
        int stopCount = GetStopQuery(entityManager).CalculateEntityCount();
        if (stopCount != _lastObservedStopCount)
        {
            ReplaceStops(entityManager, force: true);
        }

        using NativeArray<Entity> humans = citizenPathQuery.ToEntityArray(Allocator.Temp);
        for (int index = 0; index < humans.Length; index++)
        {
            ProcessHuman(entityManager, humans[index], settings);
        }

        PruneInactiveTrips();

        if (ExportProfiler.Enabled && _observeTick % 120 == 0)
        {
            ExportProfiler.Log(
                "profile observer entities=" + humans.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " active_trips=" + _activeTrips.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private void ProcessHuman(EntityManager entityManager, Entity human, ExportSettings settings)

    {

        if (!entityManager.HasBuffer<PathElement>(human))

        {

            return;

        }



        DynamicBuffer<PathElement> pathElements = entityManager.GetBuffer<PathElement>(human);

        if (!HasUsablePath(pathElements))

        {

            return;

        }



        Entity target = entityManager.HasComponent<Target>(human)

            ? entityManager.GetComponentData<Target>(human).m_Target

            : Entity.Null;

        Entity destination = ResolveLastPathTarget(pathElements);

        bool includesOutsideConnection = IsOutsideConnection(entityManager, target) || IsOutsideConnection(entityManager, destination);



        if (_activeTrips.TryGetValue(human, out ActiveTrip activeTrip))

        {

            if (activeTrip.Target != target || activeTrip.Destination != destination)

            {

                _activeTrips.Remove(human);

                RegisterTrip(entityManager, human, target, destination, pathElements, includesOutsideConnection);

                return;

            }



            _activeTrips[human] = new ActiveTrip(target, destination, _observeTick);

            return;

        }



        RegisterTrip(entityManager, human, target, destination, pathElements, includesOutsideConnection);

    }



    private void RegisterTrip(

        EntityManager entityManager,

        Entity human,

        Entity target,

        Entity destination,

        DynamicBuffer<PathElement> pathElements,

        bool includesOutsideConnection)

    {

        var trip = new CapturedTransitTrip

        {

            IncludesOutsideConnection = includesOutsideConnection

        };



        if (TryResolveAnchor(entityManager, ResolveFirstPathTarget(pathElements), out TransitAccessGapAnchor originAnchor))

        {

            trip.Anchors.Add(originAnchor);

        }



        Entity preferredDestination = destination != Entity.Null ? destination : target;

        if (TryResolveAnchor(entityManager, preferredDestination, out TransitAccessGapAnchor destinationAnchor))

        {

            trip.Anchors.Add(destinationAnchor);

        }



        for (int index = 0; index < pathElements.Length; index++)

        {

            Entity routeEntity = pathElements[index].m_Target;

            if (routeEntity == Entity.Null)

            {

                continue;

            }



            trip.RouteSegments.Add(new TransitAccessGapRouteSegmentRecord(routeEntity.Index, routeEntity.Version, null));

        }



        if (trip.Anchors.Count > 0)

        {

            _coordinator.RecordTrip(trip);

        }



        _activeTrips[human] = new ActiveTrip(target, destination, _observeTick);

    }



    private void ReplaceStops(EntityManager entityManager, bool force)

    {

        EntityQuery stopQuery = GetStopQuery(entityManager);

        int stopCount = stopQuery.CalculateEntityCount();

        if (!force && stopCount == _lastObservedStopCount)

        {

            return;

        }



        using NativeArray<Entity> stops = stopQuery.ToEntityArray(Allocator.Temp);

        var observedStops = new List<TransitAccessGapStop>(stops.Length);



        for (int index = 0; index < stops.Length; index++)

        {

            Entity stop = stops[index];

            PrefabRef prefabRef = entityManager.GetComponentData<PrefabRef>(stop);

            bool includeStop = !entityManager.HasComponent<TransportStopData>(prefabRef.m_Prefab)

                || entityManager.GetComponentData<TransportStopData>(prefabRef.m_Prefab).m_PassengerTransport;



            if (includeStop && TryResolveAnchor(entityManager, stop, out TransitAccessGapAnchor anchor))

            {

                observedStops.Add(new TransitAccessGapStop(anchor.X, anchor.Y, anchor.Z, 250));

            }

        }



        _coordinator.ReplaceStops(observedStops);

        _lastObservedStopCount = stopCount;

    }



    private EntityQuery GetCitizenPathQuery(EntityManager entityManager)

    {

        if (_citizenPathQuery.HasValue)

        {

            return _citizenPathQuery.Value;

        }



        _citizenPathQuery = entityManager.CreateEntityQuery(

            new EntityQueryDesc

            {

                All = new[]

                {

                    ComponentType.ReadOnly<Citizen>(),

                    ComponentType.ReadOnly<HouseholdMember>(),

                    ComponentType.ReadOnly<PathOwner>()

                },

                None = new[]

                {

                    ComponentType.ReadOnly<Deleted>()

                }

            });

        return _citizenPathQuery.Value;

    }



    private EntityQuery GetStopQuery(EntityManager entityManager)

    {

        if (_stopQuery.HasValue)

        {

            return _stopQuery.Value;

        }



        _stopQuery = entityManager.CreateEntityQuery(

            new EntityQueryDesc

            {

                All = new[]

                {

                    ComponentType.ReadOnly<RouteTransportStop>(),

                    ComponentType.ReadOnly<PrefabRef>()

                },

                None = new[]

                {

                    ComponentType.ReadOnly<Deleted>()

                }

            });

        return _stopQuery.Value;

    }



    private static bool HasUsablePath(DynamicBuffer<PathElement> pathElements)

    {

        if (pathElements.Length < 2)

        {

            return false;

        }



        Entity previous = pathElements[0].m_Target;

        for (int index = 1; index < pathElements.Length; index++)

        {

            Entity current = pathElements[index].m_Target;

            if (previous != Entity.Null && current != Entity.Null && previous != current)

            {

                return true;

            }



            previous = current;

        }



        return false;

    }



    private static Entity ResolveFirstPathTarget(DynamicBuffer<PathElement> pathElements)

    {

        for (int index = 0; index < pathElements.Length; index++)

        {

            if (pathElements[index].m_Target != Entity.Null)

            {

                return pathElements[index].m_Target;

            }

        }



        return Entity.Null;

    }



    private static Entity ResolveLastPathTarget(DynamicBuffer<PathElement> pathElements)

    {

        for (int index = pathElements.Length - 1; index >= 0; index--)

        {

            if (pathElements[index].m_Target != Entity.Null)

            {

                return pathElements[index].m_Target;

            }

        }



        return Entity.Null;

    }



    private static bool TryResolveAnchor(EntityManager entityManager, Entity entity, out TransitAccessGapAnchor anchor)

    {

        Entity current = entity;

        for (int depth = 0; depth < 6 && current != Entity.Null; depth++)

        {

            if (entityManager.HasComponent<ObjectTransform>(current))

            {

                var transform = entityManager.GetComponentData<ObjectTransform>(current);

                anchor = new TransitAccessGapAnchor(transform.m_Position.x, transform.m_Position.y, transform.m_Position.z);

                return true;

            }



            if (!entityManager.HasComponent<Owner>(current))

            {

                break;

            }



            Entity owner = entityManager.GetComponentData<Owner>(current).m_Owner;

            if (owner == Entity.Null || owner == current)

            {

                break;

            }



            current = owner;

        }



        anchor = new TransitAccessGapAnchor(0, 0, 0);

        return false;

    }



    private static bool IsOutsideConnection(EntityManager entityManager, Entity entity)

    {

        Entity current = entity;

        for (int depth = 0; depth < 6 && current != Entity.Null; depth++)

        {

            if (entityManager.HasComponent<NetOutsideConnection>(current) || entityManager.HasComponent<ObjectOutsideConnection>(current))

            {

                return true;

            }



            if (!entityManager.HasComponent<Owner>(current))

            {

                return false;

            }



            Entity owner = entityManager.GetComponentData<Owner>(current).m_Owner;

            if (owner == Entity.Null || owner == current)

            {

                return false;

            }



            current = owner;

        }



        return false;

    }



    private void PruneInactiveTrips()

    {

        if (_activeTrips.Count == 0)

        {

            return;

        }



        _tripRemovals.Clear();

        foreach (KeyValuePair<Entity, ActiveTrip> pair in _activeTrips)

        {

            if (_observeTick - pair.Value.LastObservedTick > TripGraceTicks)

            {

                _tripRemovals.Add(pair.Key);

            }

        }



        for (int index = 0; index < _tripRemovals.Count; index++)

        {

            _activeTrips.Remove(_tripRemovals[index]);

        }

    }



    private static bool HasProvenPassengerTripCarrier(EntityManager entityManager)

    {

        try

        {

            using EntityQuery query = entityManager.CreateEntityQuery(

                new EntityQueryDesc

                {

                    All = new[]

                    {

                        ComponentType.ReadOnly<Citizen>(),

                        ComponentType.ReadOnly<HouseholdMember>(),

                        ComponentType.ReadOnly<PathOwner>()

                    },

                    None = new[]

                    {

                        ComponentType.ReadOnly<Deleted>()

                    }

                });

            return query.CalculateEntityCount() > 0;

        }

        catch

        {

            return false;

        }

    }



    private readonly struct ActiveTrip

    {

        public ActiveTrip(Entity target, Entity destination, ulong lastObservedTick)

        {

            Target = target;

            Destination = destination;

            LastObservedTick = lastObservedTick;

        }



        public Entity Target { get; }

        public Entity Destination { get; }

        public ulong LastObservedTick { get; }

    }

}



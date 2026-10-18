using System;
using System.Collections.Generic;
using System.Threading;
using KSP.Game;
using KSP.Sim;
using KSP.Sim.DeltaV;
using KSP.Sim.Maneuver;
using KSP.Sim.impl;
using Newtonsoft.Json;
using SpaceWarp2.API.Mods;
using UnityEngine;

namespace KerbalistTelemetry
{
    public sealed class TelemetryBridgeMod : MonoBehaviourMod
    {
        public static TelemetryBridgeMod Instance { get; private set; }

        private readonly object _snapshotLock = new object();
        private string _json = "{\"schema\":\"kerbalist.telemetry.v3\",\"sequence\":0,\"game\":{},\"vessel\":null,\"solarSystem\":{\"staticRevision\":0,\"stateRevision\":0,\"bodies\":[],\"states\":[]}}";
        private long _sequence;
        private float _publishTimer;
        private TelemetryServer _server;
        private int _lastManeuverSignature;
        private long _maneuverRevision;
        private long _bodyStaticRevision;
        private long _bodyStateRevision;
        private string _bodyStaticSignature;
        private double _bodyStateTimer;
        private SolarSystemTelemetry _solarSystem = new SolarSystemTelemetry();

        // Unity calls this at startup for every loaded assembly, so the bridge starts even when the
        // mod loader doesn't call OnInitialized. It creates the component that runs Update.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Bootstrap()
        {
            WriteStartupStatus("bootstrap: Unity assembly hook ran");
            try
            {
                if (Instance == null)
                {
                    var go = new GameObject("KerbalistTelemetryBridge");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.AddComponent<TelemetryBridgeMod>();
                }
            }
            catch (Exception ex)
            {
                WriteStartupStatus("FAILED at 'bootstrap component': " + ex);
            }
        }

        public override void OnInitialized()
        {
            WriteStartupStatus("OnInitialized called");
            StartServer();
        }

        private void Awake()
        {
            Instance = this;
            StartServer();
        }

        // Starts the HTTP/WebSocket server once. Safe to call from both startup paths.
        private void StartServer()
        {
            if (_server != null)
                return;
            string stage = "begin";
            try
            {
                stage = "create server";
                _server = new TelemetryServer(8765, GetJsonSnapshot);
                stage = "start listener";
                _server.Start();
                stage = "started";
                UnityEngine.Debug.Log("[KerbalistTelemetry] Local telemetry server started on 127.0.0.1:8765");
                WriteStartupStatus("OK: listening on 127.0.0.1:8765");
            }
            catch (Exception ex)
            {
                _server = null;
                UnityEngine.Debug.LogError("[KerbalistTelemetry] startup failed at '" + stage + "': " + ex);
                WriteStartupStatus("FAILED at '" + stage + "': " + ex);
            }
        }

        // Writes startup progress to bridge-startup.txt. The DLL may be loaded from memory, in which case
        // Assembly.Location is empty, so try several folders and always echo to the game log.
        private static void WriteStartupStatus(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text;
            UnityEngine.Debug.Log("[KerbalistTelemetry] status: " + text);
            string[] folders =
            {
                SafeDir(() => System.IO.Path.GetDirectoryName(typeof(TelemetryBridgeMod).Assembly.Location)),
                SafeDir(() => System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "mods", "KerbalistTelemetryBridge")),
                SafeDir(() => UnityEngine.Application.persistentDataPath)
            };
            foreach (string dir in folders)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "bridge-startup.txt"), line + Environment.NewLine);
                    return;
                }
                catch { }
            }
        }

        private static string SafeDir(Func<string> f)
        {
            try { return f(); } catch { return null; }
        }

        private void Update()
        {
            _publishTimer += Time.unscaledDeltaTime;
            _bodyStateTimer += Time.unscaledDeltaTime;
            if (_publishTimer < 0.1f)
                return;

            _publishTimer = 0f;
            try
            {
                PublishSnapshot();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[KerbalistTelemetry] Snapshot error: " + ex);
            }
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.Stop();
                _server = null;
            }
            Instance = null;
        }

        public string GetJsonSnapshot()
        {
            lock (_snapshotLock)
                return _json;
        }

        private void PublishSnapshot()
        {
            var game = GameManager.Instance != null ? GameManager.Instance.Game : null;
            if (game == null) return;
            var universe = game.UniverseModel;
            if (universe == null) return;

            var snapshot = new TelemetrySnapshot();
            snapshot.sequence = Interlocked.Increment(ref _sequence);
            snapshot.game.universalTime = universe.UniverseTime;
            snapshot.game.timeScale = universe.TimeScale;
            snapshot.game.paused = universe.IsTimePaused;
            snapshot.game.gameState = game.GlobalGameState != null ? game.GlobalGameState.ToString() : null;
            snapshot.game.simulationRunning = game.IsSimulationRunning();

            VesselComponent activeVessel;
            if (game.ViewController != null && game.ViewController.TryGetActiveSimVessel(out activeVessel))
            {
                snapshot.vessel = CaptureVessel(activeVessel);
                // The vessel's own Orbit is null in this build, so fall back to the vessel-vehicle orbit.
                if (snapshot.vessel != null && snapshot.vessel.orbit == null)
                    snapshot.vessel.orbit = CaptureVesselVehicleOrbit(game);
            }

            var bodies = universe.GetAllCelestialBodies();
            UpdateSolarSystem(bodies, universe.UniverseTime);
            snapshot.solarSystem = _solarSystem;

            string json = JsonConvert.SerializeObject(snapshot, Formatting.None);
            lock (_snapshotLock) _json = json;
        }

        private void UpdateSolarSystem(System.Collections.IEnumerable bodies, double ut)
        {
            var current = new List<CelestialBodyComponent>();
            foreach (var item in bodies)
            {
                var body = item as CelestialBodyComponent;
                if (body != null) current.Add(body);
            }

            var signature = new System.Text.StringBuilder();
            foreach (var body in current)
                signature.Append(body.Guid).Append('|').Append(body.bodyName).Append('|').Append(body.radius).Append('|').Append(body.referenceBody != null ? body.referenceBody.bodyName : "").Append(';');
            string sig = signature.ToString();
            if (!string.Equals(sig, _bodyStaticSignature, StringComparison.Ordinal))
            {
                _bodyStaticSignature = sig;
                _bodyStaticRevision++;
                _solarSystem.bodies.Clear();
                foreach (var body in current)
                {
                    _solarSystem.bodies.Add(new CelestialBodyStaticTelemetry
                    {
                        id = body.Guid.ToString(), name = body.bodyName, displayName = body.bodyDisplayName,
                        parent = body.referenceBody != null ? body.referenceBody.bodyName : null,
                        isStar = body.IsStar, hasAtmosphere = body.hasAtmosphere, hasOcean = body.hasOcean,
                        radius = body.radius, mass = body.Mass, gravitationalParameter = body.gravParameter,
                        sphereOfInfluence = body.sphereOfInfluence, rotationPeriod = body.rotationPeriod
                    });
                }
                _solarSystem.staticRevision = _bodyStaticRevision;
            }

            if (_bodyStateTimer >= 0.4 || _solarSystem.states.Count == 0)
            {
                _bodyStateTimer = 0;
                _bodyStateRevision++;
                _solarSystem.states.Clear();
                foreach (var body in current)
                {
                    var state = new CelestialBodyStateTelemetry
                    { id = body.Guid.ToString(), position = ToVector(body.Position.localPosition), velocity = ToVector(body.Velocity.relativeVelocity.vector) };
                    if (body.Orbit != null) state.orbit = CaptureOrbit(body.Orbit);
                    _solarSystem.states.Add(state);
                }
                _solarSystem.stateRevision = _bodyStateRevision;
            }
        }

        private VesselTelemetry CaptureVessel(VesselComponent vessel)
        {
            var result = new VesselTelemetry();
            result.id = ReadString(vessel, "GlobalIdGuidString", "Guid", "Id", "GlobalId");
            result.name = string.IsNullOrEmpty(vessel.DisplayName) ? vessel.Name : vessel.DisplayName;
            result.body = vessel.mainBody != null ? vessel.mainBody.bodyName : null;
            result.position = ReadVector(ReadObject(vessel, "Position", "position", "WorldPosition"));
            result.velocity = ReadVector(ReadObject(vessel, "Velocity", "velocity", "WorldVelocity"));
            result.speed = result.velocity.Magnitude();
            result.orbitalSpeed = vessel.OrbitalSpeed;
            result.altitude = vessel.AltitudeFromSeaLevel;
            result.latitude = vessel.Latitude;
            result.longitude = vessel.Longitude;
            result.horizontalSpeed = vessel.HorizontalSrfSpeed;
            result.verticalSpeed = vessel.VerticalSrfSpeed;
            result.heading = vessel.Heading;
            result.pitch = vessel.Pitch_HorizonRelative;
            result.roll = vessel.Roll_HorizonRelative;
            result.landed = vessel.Landed;
            result.splashed = vessel.Splashed;
            result.inAtmosphere = vessel.IsInAtmosphere;
            result.mass = vessel.totalMass;
            CapturePropulsionTelemetry(vessel, result);

            if (vessel.VesselDeltaV != null)
            {
                result.deltaVVacuum = vessel.VesselDeltaV.TotalDeltaVVac;
                result.deltaVActual = vessel.VesselDeltaV.TotalDeltaVActual;
            }

            if (vessel.Orbit != null)
                result.orbit = CaptureOrbit(vessel.Orbit);

            result.target = CaptureTarget(vessel);

            object maneuverPlan = ReadObject(vessel, "ManeuverPlan", "maneuverPlan");
            object maneuverNodes = maneuverPlan == null ? null : InvokeObject(maneuverPlan, "GetNodes");
            var maneuverNodeList = maneuverNodes as System.Collections.IEnumerable;
            if (maneuverNodeList != null)
            {
                foreach (object node in maneuverNodeList)
                {
                    if (node == null)
                        continue;
                    var m = new ManeuverTelemetry();
                    m.id = ReadString(node, "NodeID", "Id", "ID");
                    m.name = ReadString(node, "NodeName", "Name");
                    m.universalTime = ReadDouble(node, "Time", "UniversalTime");
                    m.burnVector = ReadVector(ReadObject(node, "BurnVector", "burnVector"));
                    m.deltaVMagnitude = ReadDouble(node, "BurnRequiredDV", "DeltaV", "DeltaVMagnitude");
                    m.burnDuration = ReadDouble(node, "BurnDuration", "Duration");
                    m.isOnManeuverTrajectory = ReadBool(node, "IsOnManeuverTrajectory", "OnManeuverTrajectory");
                    m.relatedSimId = ReadString(node, "RelatedSimID", "RelatedSimId");
                    m.cachedTrajectoryEndUT = ReadDouble(node, "CachedManeuverPatchEndUT", "TrajectoryEndUT");
                    result.maneuverNodes.Add(m);
                }

                int signature = 17;
                foreach (var n in result.maneuverNodes)
                {
                    signature = signature * 31 + (n.id ?? string.Empty).GetHashCode();
                    signature = signature * 31 + n.universalTime.GetHashCode();
                    signature = signature * 31 + n.deltaVMagnitude.GetHashCode();
                }
                if (signature != _lastManeuverSignature)
                {
                    _lastManeuverSignature = signature;
                    _maneuverRevision++;
                }
                result.maneuverRevision = _maneuverRevision;
            }

            return result;
        }


        // Redux's propulsion/part APIs have changed between snapshots. Keep this layer
        // reflection-based so the telemetry protocol remains stable while the bridge
        // tolerates renamed optional properties.
        private TargetTelemetry CaptureTarget(VesselComponent vessel)
        {
            object target = ReadObject(vessel, "Target", "CurrentTarget", "TargetObject", "SelectedTarget");
            if (target == null)
                return null;

            var result = new TargetTelemetry();
            result.id = ReadString(target, "GlobalIdGuidString", "Guid", "Id", "GlobalId");
            result.name = ReadString(target, "DisplayName", "Name", "bodyName");
            result.type = target.GetType().Name;

            object targetBody = ReadObject(target, "mainBody", "MainBody", "Body", "ReferenceBody");
            result.body = ReadString(targetBody, "bodyName", "BodyName", "Name", "DisplayName");

            object position = ReadObject(target, "Position", "position", "WorldPosition");
            object velocity = ReadObject(target, "Velocity", "velocity", "WorldVelocity");
            result.position = ReadVector(position);
            result.velocity = ReadVector(velocity);
            result.distance = ReadDouble(target, "Distance", "TargetDistance", "DistanceToTarget");
            result.relativeSpeed = ReadDouble(target, "RelativeSpeed", "ClosingSpeed", "RelativeVelocityMagnitude");
            result.latitude = ReadDouble(target, "Latitude", "latitude");
            result.longitude = ReadDouble(target, "Longitude", "longitude");
            result.altitude = ReadDouble(target, "Altitude", "AltitudeFromSeaLevel", "altitude");

            object orbit = ReadObject(target, "Orbit", "orbit");
            var patched = orbit as IPatchedOrbit;
            if (patched != null)
                result.orbit = CaptureOrbit(patched);

            // If Redux exposes a target object but not derived relative values, derive them
            // from the two world vectors where possible.
            if (result.distance <= 0 && result.position.Magnitude() > 0)
                result.distance = result.position.Magnitude();
            if (result.relativeSpeed <= 0 && result.velocity.Magnitude() > 0)
                result.relativeSpeed = result.velocity.Magnitude();

            return result;
        }

        private static bool _orbitRouteLogged;

        // Active vessel orbit read from game.ViewController.ActiveVesselVehicle.Orbit.
        // Reads by member name, so it doesn't depend on the exact interfaces at compile time.
        private OrbitTelemetry CaptureVesselVehicleOrbit(object game)
        {
            try
            {
                object controller = ReadObject(game, "ViewController");
                object vehicle = ReadObject(controller, "ActiveVesselVehicle");
                object orbit = ReadObject(vehicle, "Orbit");
                if (orbit == null)
                {
                    if (!_orbitRouteLogged) UnityEngine.Debug.Log("[KerbalistTelemetry] active vessel orbit: not available from any route");
                    _orbitRouteLogged = true;
                    return null;
                }

                var t = new OrbitTelemetry();
                t.apoapsis = ReadDouble(orbit, "Apoapsis");
                t.periapsis = ReadDouble(orbit, "Periapsis");
                t.semiMajorAxis = ReadDouble(orbit, "semiMajorAxis");
                t.eccentricity = ReadDouble(orbit, "eccentricity");
                t.inclination = ReadDouble(orbit, "inclination");
                t.longitudeOfAscendingNode = ReadDouble(orbit, "longitudeOfAscendingNode");
                t.argumentOfPeriapsis = ReadDouble(orbit, "argumentOfPeriapsis");
                t.meanAnomaly = ReadDouble(orbit, "meanAnomalyAtEpoch");
                t.period = ReadDouble(orbit, "period");
                t.startUT = ReadDouble(orbit, "epoch");
                t.referenceBody = ReadString(ReadObject(orbit, "referenceBody"), "bodyName", "Name", "name");
                t.localPosition = ReadVector(ReadObject(orbit, "localPosition"));
                t.relativeVelocity = ReadVector(ReadObject(orbit, "relativeVelocity"));

                if (!_orbitRouteLogged)
                {
                    UnityEngine.Debug.Log("[KerbalistTelemetry] active vessel orbit: route 2 (ViewController.ActiveVesselVehicle.Orbit)");
                    _orbitRouteLogged = true;
                }
                return t;
            }
            catch (Exception ex)
            {
                if (!_orbitRouteLogged) UnityEngine.Debug.LogError("[KerbalistTelemetry] vessel orbit route 2 failed: " + ex.Message);
                _orbitRouteLogged = true;
                return null;
            }
        }

        private static Vector3d ReadVector(object source)
        {
            if (source == null) return new Vector3d();
            object value = source;
            object vector = ReadObject(source, "relativeVelocity", "vector", "localPosition", "position", "Vector", "Value");
            if (vector != null) value = vector;
            double x = ReadDouble(value, "x", "X");
            double y = ReadDouble(value, "y", "Y");
            double z = ReadDouble(value, "z", "Z");
            return new Vector3d(x, y, z);
        }

        private static void CapturePropulsionTelemetry(VesselComponent vessel, VesselTelemetry result)
        {
            object source = vessel;
            result.throttle = ReadDouble(source, "Throttle", "throttle", "ThrottleInput", "CurrentThrottle");
            result.sas = ReadBool(source, "SAS", "Sas", "SASActive", "SasActive");
            result.rcs = ReadBool(source, "RCS", "Rcs", "RCSActive", "RcsActive");
            result.currentStage = ReadInt(source, "CurrentStage", "Stage", "CurrentStageIndex");
            result.thrust = ReadDouble(source, "CurrentThrust", "Thrust", "TotalThrust");
            result.maxThrust = ReadDouble(source, "MaxThrust", "AvailableThrust", "TotalMaxThrust");
            result.fuelBurnRate = ReadDouble(source, "FuelBurnRate", "TotalFuelBurnRate", "FuelFlowRate");

            if (result.mass > 0 && result.thrust > 0)
                result.thrustToWeight = result.thrust / (result.mass * 9.80665);

            object dv = ReadObject(source, "VesselDeltaV", "DeltaV");
            if (dv != null)
            {
                object partInfo = ReadObject(dv, "PartInfo", "Parts");
                var parts = partInfo as System.Collections.IEnumerable;
                if (parts != null)
                {
                    foreach (object part in parts)
                    {
                        if (part == null) continue;
                        string name = ReadString(part, "Name", "PartName", "DisplayName");
                        double thrust = ReadDouble(part, "Thrust", "CurrentThrust");
                        double max = ReadDouble(part, "MaxThrust", "AvailableThrust");
                        double flow = ReadDouble(part, "FuelFlow", "FuelBurnRate", "FlowRate");
                        if (!string.IsNullOrEmpty(name) || thrust != 0 || max != 0)
                            result.engines.Add(new EngineTelemetry { name = name, active = thrust > 0, thrust = thrust, maxThrust = max, fuelFlow = flow });
                    }
                }
            }

            object resourceSource = ReadObject(source, "Resources", "ResourceContainer", "ResourceManager", "VesselResources");
            var resources = resourceSource as System.Collections.IEnumerable;
            if (resources != null)
            {
                foreach (object resource in resources)
                {
                    if (resource == null) continue;
                    string name = ReadString(resource, "Name", "ResourceName", "DisplayName");
                    double amount = ReadDouble(resource, "Amount", "CurrentAmount", "Value");
                    double max = ReadDouble(resource, "MaxAmount", "Capacity", "Max");
                    if (!string.IsNullOrEmpty(name))
                        result.resources.Add(new ResourceTelemetry { name = name, amount = amount, maxAmount = max, fraction = max > 0 ? amount / max : 0 });
                }
            }

            if (result.mass > 0)
            {
                // Dry mass is not consistently exposed by Redux; use an explicit property
                // when available rather than guessing from resource masses.
                result.dryMass = ReadDouble(source, "DryMass", "dryMass", "DryMassTotal");
            }
        }

        private static object ReadObject(object source, params string[] names)
        {
            if (source == null) return null;
            var type = source.GetType();
            foreach (var name in names)
            {
                try
                {
                    var p = type.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
                    if (p != null) return p.GetValue(source, null);
                    var f = type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
                    if (f != null) return f.GetValue(source);
                }
                catch { }
            }
            return null;
        }

        private static object InvokeObject(object source, string methodName)
        {
            if (source == null) return null;
            try
            {
                var method = source.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
                return method == null ? null : method.Invoke(source, null);
            }
            catch { return null; }
        }

        private static double ReadDouble(object source, params string[] names)
        {
            object v = ReadObject(source, names);
            if (v == null) return 0;
            try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); } catch { return 0; }
        }

        private static int ReadInt(object source, params string[] names)
        {
            object v = ReadObject(source, names);
            if (v == null) return -1;
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); } catch { return -1; }
        }

        private static bool ReadBool(object source, params string[] names)
        {
            object v = ReadObject(source, names);
            if (v == null) return false;
            try { return Convert.ToBoolean(v, System.Globalization.CultureInfo.InvariantCulture); } catch { return false; }
        }

        private static string ReadString(object source, params string[] names)
        {
            object v = ReadObject(source, names);
            return v == null ? null : v.ToString();
        }

        private OrbitTelemetry CaptureOrbit(IPatchedOrbit orbitInterface)
        {
            var orbit = orbitInterface as PatchedConicsOrbit;
            if (orbit == null)
                return null;

            var result = new OrbitTelemetry();
            result.apoapsis = orbit.Apoapsis;
            result.periapsis = orbit.Periapsis;
            result.semiMajorAxis = orbit.semiMajorAxis;
            result.eccentricity = orbit.eccentricity;
            result.inclination = orbit.inclination;
            result.longitudeOfAscendingNode = orbit.longitudeOfAscendingNode;
            result.argumentOfPeriapsis = orbit.argumentOfPeriapsis;
            result.meanAnomaly = orbit.meanAnomalyAtEpoch;
            result.meanMotion = orbit.meanMotion;
            result.period = orbit.period;
            result.timeToApoapsis = orbit.TimeToAp;
            result.timeToPeriapsis = orbit.TimeToPe;
            result.startUT = orbit.StartUT;
            result.endUT = orbit.EndUT;
            result.referenceBody = orbit.referenceBody != null ? orbit.referenceBody.bodyName : null;
            return result;
        }

        private static KerbalistTelemetry.Vector3d ToVector(global::Vector3d v)
        {
            return new Vector3d(v.x, v.y, v.z);
        }
    }

    internal static class VectorExtensions
    {
        public static double Magnitude(this Vector3d v)
        {
            return Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }
    }
}

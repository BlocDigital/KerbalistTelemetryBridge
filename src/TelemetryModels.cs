using System;
using System.Collections.Generic;

namespace KerbalistTelemetry
{
    public sealed class TelemetrySnapshot
    {
        public string schema = "kerbalist.telemetry.v3";
        public long sequence;
        public GameTelemetry game = new GameTelemetry();
        public VesselTelemetry vessel;
        public SolarSystemTelemetry solarSystem = new SolarSystemTelemetry();
    }

    public sealed class GameTelemetry
    {
        public double universalTime;
        public float timeScale;
        public bool paused;
        public string gameState;
        public bool simulationRunning;
    }

    public sealed class VesselTelemetry
    {
        public string id; public string name; public string body;
        public Vector3d position; public Vector3d velocity;
        public double speed; public double orbitalSpeed; public double altitude;
        public double latitude; public double longitude; public double horizontalSpeed; public double verticalSpeed;
        public double heading; public double pitch; public double roll;
        public bool landed; public bool splashed; public bool inAtmosphere;
        public double mass; public double dryMass; public double thrust; public double maxThrust;
        public double thrustToWeight; public double throttle; public double fuelBurnRate;
        public bool sas; public bool rcs; public int currentStage = -1;
        public List<ResourceTelemetry> resources = new List<ResourceTelemetry>();
        public List<EngineTelemetry> engines = new List<EngineTelemetry>();
        public double deltaVVacuum; public double deltaVActual;
        public OrbitTelemetry orbit;
        public List<ManeuverTelemetry> maneuverNodes = new List<ManeuverTelemetry>();
        public long maneuverRevision;
        public TargetTelemetry target;
    }

    public sealed class TargetTelemetry
    {
        public string id; public string name; public string type; public string body;
        public Vector3d position; public Vector3d velocity;
        public double distance; public double relativeSpeed;
        public double latitude; public double longitude; public double altitude;
        public OrbitTelemetry orbit;
    }

    public sealed class BodyTelemetry
    {
        public string id; public string name; public string displayName; public string parent;
        public bool isStar; public bool hasAtmosphere; public bool hasOcean;
        public double radius; public double mass; public double gravitationalParameter;
        public double sphereOfInfluence; public double rotationPeriod;
        public Vector3d position; public Vector3d velocity;
        public OrbitTelemetry orbit;
    }

    public sealed class ResourceTelemetry
    { public string name; public double amount; public double maxAmount; public double fraction; }

    public sealed class EngineTelemetry
    { public string name; public bool active; public double thrust; public double maxThrust; public double fuelFlow; }

    public sealed class OrbitTelemetry
    {
        public double apoapsis; public double periapsis; public double semiMajorAxis; public double eccentricity;
        public double inclination; public double longitudeOfAscendingNode; public double argumentOfPeriapsis;
        public double meanAnomaly; public double meanMotion; public double period;
        public double timeToApoapsis; public double timeToPeriapsis; public double startUT; public double endUT;
        public string referenceBody;
    }

    public sealed class ManeuverTelemetry
    {
        public string id; public string name; public double universalTime;
        public Vector3d burnVector; public double deltaVMagnitude; public double burnDuration;
        public bool isOnManeuverTrajectory; public string relatedSimId; public double cachedTrajectoryEndUT;
    }

    public sealed class SolarSystemTelemetry
    {
        public long staticRevision;
        public long stateRevision;
        public List<CelestialBodyStaticTelemetry> bodies = new List<CelestialBodyStaticTelemetry>();
        public List<CelestialBodyStateTelemetry> states = new List<CelestialBodyStateTelemetry>();
    }

    public sealed class CelestialBodyStaticTelemetry
    {
        public string id; public string name; public string displayName; public string parent;
        public bool isStar; public bool hasAtmosphere; public bool hasOcean;
        public double radius; public double mass; public double gravitationalParameter;
        public double sphereOfInfluence; public double rotationPeriod;
    }

    public sealed class CelestialBodyStateTelemetry
    {
        public string id; public Vector3d position; public Vector3d velocity; public OrbitTelemetry orbit;
    }

    public sealed class Vector3d
    {
        public double x; public double y; public double z;
        public Vector3d() { }
        public Vector3d(double x, double y, double z) { this.x=x; this.y=y; this.z=z; }
        public double Magnitude() { return Math.Sqrt(x*x+y*y+z*z); }
    }
}

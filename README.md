# Kerbalist Telemetry Bridge — User Guide

**Version 1.3.2** · [Changelog](CHANGELOG.md)

A local HTTP/WebSocket telemetry bridge for **Kerbal Space Program 2** (KSP2) Redux / SpaceWarp2. It exposes the running KSP2 simulation to browser-based applications (like Kerbalist).

---

## Requirements

- KSP2 with **KSP2 Redux 0.2.9.0.106120** (snapshot 26w42c). The bridge is built and tested against this version.
- Windows, since the build script and the install steps use Windows paths.

## Quick Start

1. Download the bridge from the [release page](https://github.com/BlocDigital/KerbalistTelemetryBridge/releases).
2. Create a folder named `KerbalistTelemetryBridge` inside `Kerbal Space Program 2\mods\`, and copy `KerbalistTelemetryBridge.dll` and `swinfo.json` into it. The path should be `mods\KerbalistTelemetryBridge\KerbalistTelemetryBridge.dll`.
3. Start KSP2, load a save, and wait a few seconds.
4. Check that `bridge-startup.txt` appears in the `KerbalistTelemetryBridge` folder. Its last line should read `OK: listening on 127.0.0.1:8765`.
5. Open `http://127.0.0.1:8765/api/status` in a browser. You should see the service information.

The server listens on port **8765**. Use `127.0.0.1` in addresses, not `localhost`.

> The server binds to `127.0.0.1` only — it is not exposed on your LAN or the internet.

### Endpoints

| Endpoint | Method | What you get |
|----------|--------|---------------|
| `/` | GET | Service info (name, schema, WebSocket URL) |
| `/api/status` | GET | Health check + service info |
| `/api/state` | GET | Full current telemetry snapshot |
| `/api/ut` | GET | Current universal time (Unix timestamp) |
| `/api/bodies` | GET | Solar system catalog + live body states |
| `/ws` | WebSocket | Real-time telemetry streaming (`ws://127.0.0.1:8765/ws`) |

## Troubleshooting

- **`bridge-startup.txt` does not appear.** The mod did not start. Check the folder path, and make sure the files are not nested in an extra folder. Check that `disabled_plugins.cfg` in the game folder does not list `com.blocdigital.kerbalist.telemetry`.
- **The file ends with `FAILED at 'start listener'`.** Another program is using port 8765. In PowerShell, run `netstat -ano | findstr 8765` to see which process it is.
- **The file says OK, but the page returns nothing.** Use `http://127.0.0.1:8765/` rather than `localhost`, and make sure the campaign has the bridge enabled. Some campaigns ask about mod features when you load a save.
- **Nothing is logged at all.** The game log is `Ksp2-2.log` in the game folder. Search it for `KerbalistTelemetry`.

## Building From Source

Run this from the repository folder (the one containing `build` and `src`):

```powershell
powershell -ExecutionPolicy Bypass -File .\build\build.ps1
```

- `-Ksp2Root "<path>"` sets the game folder if it isn't in the default Steam location.
- `-Deploy` copies the built files into `mods\KerbalistTelemetryBridge`.

The output goes to `.build\KerbalistTelemetryBridge\`, with a zip alongside it. The build compiles against the DLLs in the game's `KSP2_x64_Data\Managed` folder, so that folder must match the installed Redux version. It needs `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.ScriptingModule.dll`, `Unity.Entities.dll`, `Ksp2Redux.dll`, `Assembly-CSharp.dll` and `Newtonsoft.Json.dll`.

---

## Data Dictionary

### Snapshot Root

| Field | Type | Description |
|-------|------|-------------|
| `schema` | string | Protocol version (`"kerbalist.telemetry.v3"`) |
| `sequence` | long | Monotonically increasing snapshot counter |

### Game State

| Field | Type | Description |
|-------|------|-------------|
| `game.universalTime` | double | KSP2 universal time (Unix seconds since epoch) |
| `game.timeScale` | float | Simulation time scale (`0.0` = stopped, `1.0` = normal speed) |
| `game.paused` | boolean | Whether time is paused |
| `game.gameState` | string | Global game state (e.g., `"RUNNING"`, `"PAUSED"`) |
| `game.simulationRunning` | boolean | Whether the simulation is actively running |

### Active Vessel

| Field | Type | Description |
|-------|------|-------------|
| `vessel.id` | string | Vessel identifier (GUID string) |
| `vessel.name` | string | Vessel name (display name if available) |
| `vessel.body` | string | Name of the celestial body the vessel is on/in |
| `vessel.position` | object | World position in meters: `{x, y, z}` |
| `vessel.velocity` | object | World velocity in m/s: `{x, y, z}` |
| `vessel.speed` | double | Magnitude of velocity (m/s) |
| `vessel.orbitalSpeed` | double | Orbital speed around the current body (m/s) |
| `vessel.altitude` | double | Altitude above sea level (meters) |
| `vessel.latitude` | double | Latitude in degrees |
| `vessel.longitude` | double | Longitude in degrees |
| `vessel.horizontalSpeed` | double | Surface horizontal speed (m/s) |
| `vessel.verticalSpeed` | double | Surface vertical speed (m/s) |
| `vessel.heading` | double | Heading in degrees |
| `vessel.pitch` | double | Pitch (horizon-relative, degrees) |
| `vessel.roll` | double | Roll (horizon-relative, degrees) |
| `vessel.landed` | boolean | Whether the vessel is landed |
| `vessel.splashed` | boolean | Whether the vessel is splashed |
| `vessel.inAtmosphere` | boolean | Whether the vessel is inside an atmosphere |
| `vessel.mass` | double | Total current mass (kg) |
| `vessel.dryMass` | double | Dry mass (kg), if exposed by Redux |
| `vessel.thrust` | double | Current total thrust (N) |
| `vessel.maxThrust` | double | Maximum available thrust (N) |
| `vessel.thrustToWeight` | double | TWR (dimensionless) |
| `vessel.throttle` | double | Current throttle setting (`0.0`–`1.0`) |
| `vessel.sas` | boolean | SAS active |
| `vessel.rcs` | boolean | RCS active |
| `vessel.currentStage` | int | Current stage index (`-1` if none) |
| `vessel.deltaVVacuum` | double | Δv in vacuum (m/s) |
| `vessel.deltaVActual` | double | Actual Δv accounting for gravity/drag (m/s) |
| `vessel.orbit` | object | Orbit parameters (see **Orbit**) |
| `vessel.maneuverNodes` | array | Maneuver node list (see **Maneuver Nodes**) |
| `vessel.maneuverRevision` | long | Revision counter — increments when nodes change |
| `vessel.target` | object | Targeting info (see **Target**) |
| `vessel.engines` | array | Engine summary list (see **Engine**) |
| `vessel.resources` | array | Resource list (see **Resource**) |

### Orbit

| Field | Type | Description |
|-------|------|-------------|
| `apoapsis` | double | Apoapsis altitude (meters) |
| `periapsis` | double | Periapsis altitude (meters) |
| `semiMajorAxis` | double | Semi-major axis (meters) |
| `eccentricity` | double | Orbital eccentricity |
| `inclination` | double | Inclination (degrees) |
| `longitudeOfAscendingNode` | double | RAAN (degrees) |
| `argumentOfPeriapsis` | double | Argument of periapsis (degrees) |
| `meanAnomaly` | double | Mean anomaly at epoch (degrees) |
| `meanMotion` | double | Mean motion (revolutions per second) |
| `period` | double | Orbital period (seconds) |
| `timeToApoapsis` | double | Time to apoapsis (seconds) |
| `timeToPeriapsis` | double | Time to periapsis (seconds) |
| `startUT` | double | UT when the patch started |
| `endUT` | double | UT when the patch ends |
| `referenceBody` | string | Name of the orbit's reference body |

### Target

| Field | Type | Description |
|-------|------|-------------|
| `target.id` | string | Target object identifier (GUID string) |
| `target.name` | string | Target name (body, part, or vessel) |
| `target.type` | string | Target type (e.g., `"Vessel"`, `"CelestialBody"`) |
| `target.body` | string | Name of the target's body |
| `target.position` | object | Position relative to the vessel (meters): `{x, y, z}` |
| `target.velocity` | object | Relative velocity (m/s): `{x, y, z}` |
| `target.distance` | double | Distance from vessel (meters) |
| `target.relativeSpeed` | double | Relative speed (m/s) |
| `target.latitude` | double | Target latitude (degrees) |
| `target.longitude` | double | Target longitude (degrees) |
| `target.altitude` | double | Target altitude above sea level (meters) |
| `target.orbit` | object | Orbit of the target (if applicable) |

> **Note:** If Redux does not expose derived relative values directly, they are computed from the world-position vectors where possible.

### Engine

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Engine name |
| `active` | boolean | Whether the engine is currently firing (`thrust > 0`) |
| `thrust` | double | Current thrust (N) |
| `maxThrust` | double | Maximum thrust (N) |
| `fuelFlow` | double | Fuel flow rate (kg/s) |

### Resource

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Resource name (e.g., `"LiquidFuel"`, `"Oxidizer"`) |
| `amount` | double | Current amount (kg) |
| `maxAmount` | double | Capacity (kg) |
| `fraction` | double | Fraction remaining (`0.0`–`1.0`) |

### Maneuver Node

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Node identifier |
| `name` | string | Node name |
| `universalTime` | double | Planned burn UT (seconds since epoch) |
| `burnVector` | object | Burn vector direction: `{x, y, z}` (m/s total Δv direction) |
| `deltaVMagnitude` | double | Total Δv for the burn (m/s) |
| `burnDuration` | double | Estimated burn duration (seconds) |
| `isOnManeuverTrajectory` | boolean | Whether the vessel is currently on the maneuver trajectory |
| `relatedSimId` | string | Related simulation ID |
| `cachedTrajectoryEndUT` | double | Cached trajectory end UT |

### Solar System — Static Bodies

Static body metadata (revision changes only when the catalog changes):

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Body GUID |
| `name` | string | Body identifier (e.g., `"Kerbin"`) |
| `displayName` | string | Display name (e.g., `"Kerbin"`) |
| `parent` | string | Name of the parent body (or `null` for the star) |
| `isStar` | boolean | Whether this is the star |
| `hasAtmosphere` | boolean | Whether the body has an atmosphere |
| `hasOcean` | boolean | Whether the body has an ocean |
| `radius` | double | Radius (meters) |
| `mass` | double | Mass (kg) |
| `gravitationalParameter` | double | μ (m³/s²) |
| `sphereOfInfluence` | double | SOI radius (meters) |
| `rotationPeriod` | double | Rotation period (seconds) |

### Solar System — Live States

Live state for each body (updates ~2.5 Hz):

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Body GUID |
| `position` | object | World position (meters): `{x, y, z}` |
| `velocity` | object | World velocity (m/s): `{x, y, z}` |
| `orbit` | object | Orbit around the parent body (same fields as **Orbit**) |

---

## Example JSON Response

```json
{
  "schema": "kerbalist.telemetry.v3",
  "sequence": 1,
  "game": {
    "universalTime": 274931786.462315,
    "timeScale": 1.0,
    "paused": false,
    "gameState": "RUNNING",
    "simulationRunning": true
  },
  "vessel": {
    "id": "guid-string-here",
    "name": "Command Pod",
    "body": "Kerbin",
    "position": {"x": 123456.7, "y": 0.0, "z": 78901.2},
    "velocity": {"x": 1234.5, "y": 0.0, "z": 678.9},
    "speed": 1523.45,
    "orbitalSpeed": 1499.12,
    "altitude": 70000.0,
    "latitude": 45.23,
    "longitude": -120.56,
    "horizontalSpeed": 1500.0,
    "verticalSpeed": 23.45,
    "heading": 123.45,
    "pitch": 5.67,
    "roll": 2.34,
    "landed": false,
    "splashed": false,
    "inAtmosphere": false,
    "mass": 1234.5,
    "dryMass": 890.2,
    "thrust": 0.0,
    "maxThrust": 500000.0,
    "thrustToWeight": 0.0,
    "throttle": 0.0,
    "sas": false,
    "rcs": false,
    "currentStage": -1,
    "deltaVVacuum": 4500.0,
    "deltaVActual": 4320.5,
    "orbit": {
      "apoapsis": 1000000.0,
      "periapsis": 700000.0,
      "semiMajorAxis": 850000.0,
      "eccentricity": 0.176,
      "inclination": 28.47,
      "longitudeOfAscendingNode": 45.3,
      "argumentOfPeriapsis": 12.8,
      "meanAnomaly": 180.5,
      "meanMotion": 0.000123,
      "period": 6850.2,
      "timeToApoapsis": 1234.5,
      "timeToPeriapsis": 4515.7,
      "startUT": 274931700.0,
      "endUT": 274931768.5,
      "referenceBody": "Kerbin"
    },
    "maneuverNodes": [
      {
        "id": "maneuver-node-1",
        "name": "Mun Transfer",
        "universalTime": 274931800.0,
        "burnVector": {"x": 0.0, "y": 0.0, "z": 1.0},
        "deltaVMagnitude": 350.0,
        "burnDuration": 12.3,
        "isOnManeuverTrajectory": false,
        "relatedSimId": "sim-id",
        "cachedTrajectoryEndUT": 274931950.0
      }
    ],
    "maneuverRevision": 3,
    "target": {
      "id": "target-guid",
      "name": "Mun",
      "type": "Vessel",
      "body": "Mun",
      "position": {"x": 123450.0, "y": 100.0, "z": 78900.0},
      "velocity": {"x": 100.0, "y": 0.5, "z": 100.0},
      "distance": 12345.6,
      "relativeSpeed": 12.3,
      "latitude": 42.1,
      "longitude": -118.3,
      "altitude": 65000.0,
      "orbit": {
        "apoapsis": 650000.0,
        "periapsis": 640000.0,
        "semiMajorAxis": 645000.0,
        "eccentricity": 0.008,
        "inclination": 28.5,
        "longitudeOfAscendingNode": 44.2,
        "argumentOfPeriapsis": 10.1,
        "meanAnomaly": 185.2,
        "meanMotion": 0.000124,
        "period": 6848.1,
        "timeToApoapsis": 1180.0,
        "timeToPeriapsis": 4458.1,
        "startUT": 274931700.0,
        "endUT": 274931768.0,
        "referenceBody": "Kerbin"
      }
    },
    "engines": [],
    "resources": [],
    "dryMass": 890.2
  },
  "solarSystem": {
    "staticRevision": 1,
    "stateRevision": 42,
    "bodies": [
      {
        "id": "star",
        "name": "Sun",
        "displayName": "Sun",
        "parent": null,
        "isStar": true,
        "hasAtmosphere": false,
        "hasOcean": false,
        "radius": 696342000.0,
        "mass": 1.989e30,
        "gravitationalParameter": 1.327e20,
        "sphereOfInfluence": 1.16e11,
        "rotationPeriod": 25050000.0
      },
      {
        "id": "kerbin",
        "name": "Kerbin",
        "displayName": "Kerbin",
        "parent": "Sun",
        "isStar": false,
        "hasAtmosphere": true,
        "hasOcean": true,
        "radius": 6000000.0,
        "mass": 5.29e23,
        "gravitationalParameter": 3.53e12,
        "sphereOfInfluence": 8.0e7,
        "rotationPeriod": 22678.0
      },
      {
        "id": "mun",
        "name": "Mun",
        "displayName": "Mun",
        "parent": "Kerbin",
        "isStar": false,
        "hasAtmosphere": false,
        "hasOcean": false,
        "radius": 2000000.0,
        "mass": 9.8e22,
        "gravitationalParameter": 6.67e11,
        "sphereOfInfluence": 3.0e7,
        "rotationPeriod": 546240.0
      }
    ],
    "states": [
      {
        "id": "star",
        "position": {"x": 0.0, "y": 0.0, "z": 0.0},
        "velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
        "orbit": null
      },
      {
        "id": "kerbin",
        "position": {"x": 0.0, "y": 0.0, "z": 0.0},
        "velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
        "orbit": null
      },
      {
        "id": "mun",
        "position": {"x": 1234567.8, "y": 123456.7, "z": 87654.3},
        "velocity": {"x": -5.67, "y": 0.0, "z": 0.0},
        "orbit": {
          "apoapsis": 10000000.0,
          "periapsis": 8000000.0,
          "semiMajorAxis": 9000000.0,
          "eccentricity": 0.111,
          "inclination": 0.0,
          "longitudeOfAscendingNode": 0.0,
          "argumentOfPeriapsis": 0.0,
          "meanAnomaly": 90.0,
          "meanMotion": 1.8e-5,
          "period": 115920.0,
          "timeToApoapsis": 57960.0,
          "timeToPeriapsis": 57960.0,
          "startUT": 274931600.0,
          "endUT": 274931716.0,
          "referenceBody": "Kerbin"
        }
      }
    ]
  }
}
```

---

## WebSocket Stream

Connect to `ws://127.0.0.1:8765/ws` to receive a continuous stream of JSON snapshots (same structure as `/api/state`). Each message is a complete snapshot object.

---

## Revision Counts

| Field | Meaning |
|-------|---------|
| `solarSystem.staticRevision` | Increments when the catalog of celestial bodies changes |
| `solarSystem.stateRevision` | Increments when any body's live state (position/velocity/orbit) changes |
| `vessel.maneuverRevision` | Increments when the active vessel's maneuver nodes change |

Use these revision counters to avoid polling the full state when only a subset of data has changed.

---

## Notes

- All vector fields (`position`, `velocity`, `burnVector`) are in **world coordinates**.
- Angles (`inclination`, `longitudeOfAscendingNode`, `argumentOfPeriapsis`, `heading`, `pitch`, `roll`, `latitude`, `longitude`) are in **degrees**.
- Times (`universalTime`, `timeToApoapsis`, etc.) are in **seconds since Unix epoch**.
- Masses are in **kilograms**, distances in **meters**, velocities in **m/s**.
- The `dryMass` field is optional: it is included only if Redux exposes the underlying `DryMass` property. If not available, the field is omitted.
- The `target` field is `null` if no target is currently assigned.

---

## License / Attribution

This documentation describes the public telemetry API of the Kerbalist Telemetry Bridge mod for KSP2 Redux / SpaceWarp2.

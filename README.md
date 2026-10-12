# Kerbalist Telemetry Bridge — User Guide

A local HTTP/WebSocket telemetry bridge for **Kerbal Space Program 2** (KSP2) Redux / SpaceWarp2. It exposes the running KSP2 simulation to browser-based applications (like Kerbalist).

---

## Quick Start

1. Unzip [KerbalistTelemetryBridge.zip](https://github.com/BlocDigital/KerbalistTelemetryBridge/releases/tag/v1.0) into the `Kerbal Space Program 2\mods` folder.
2. Restart KSP2.
3. The bridge will start automatically and will be available at `http://127.0.0.1:8765/`.
   The server will log the port to the console, so you can check it there if needed.
   The default port is 8765.

   
## What it exposes

### Game
- Universal Time
- time scale / pause state
- game state
- simulation-running state

### Active vessel
- vessel identity and current celestial body
- world/local position and velocity vectors
- speed, orbital speed
- altitude, latitude, longitude
- horizontal and vertical surface speed
- heading, pitch, roll
- landed / splashed / atmosphere state
- mass and dry mass when exposed
- throttle, SAS, RCS, current stage
- thrust, maximum thrust, TWR, fuel burn rate where exposed
- resource amount/capacity/fraction where exposed
- engine summaries where exposed
- vacuum and actual Δv

### Orbit
- apoapsis / periapsis
- semi-major axis
- eccentricity
- inclination
- LAN / argument of periapsis
- mean anomaly / mean motion / period
- time to apoapsis / periapsis
- patch start/end UT
- reference body

### Maneuvers
- node ID/name
- burn UT
- burn vector and Δv magnitude
- burn duration
- maneuver-trajectory state
- related simulation ID
- cached trajectory end UT
- revision number so the browser can detect node changes

### Target
Where the current Redux object model exposes targeting information:
- target ID/name/type
- body
- position/velocity
- distance and relative speed
- surface coordinates
- orbit

Target/propulsion/resource fields use reflection where Redux has varied between snapshots. Missing optional fields are returned as defaults rather than crashing the bridge.

### Solar system
All celestial bodies returned by `UniverseModel.GetAllCelestialBodies()` are synchronised, including planets, moons and the star.

Static body metadata is separated from live state:

- static: ID, name, display name, parent, star flag, atmosphere/ocean flags, radius, mass, gravitational parameter, SOI, rotation period
- live: position, velocity and orbit

Static data gets a revision only when the body catalogue changes. Live body state updates at roughly 2.5 Hz while vessel telemetry updates at roughly 10 Hz.

## Local API

`GET http://127.0.0.1:8765/api/status`

`GET http://127.0.0.1:8765/api/state`

`GET http://127.0.0.1:8765/api/bodies`

`WS ws://127.0.0.1:8765/ws`

The listener binds to loopback only (`127.0.0.1`). It is not exposed directly to the LAN or internet.

The browser API enables CORS for local development and GitHub Pages. No external network connection is required by the mod.

## Installation / build

This repository is intentionally source-first. It does **not** redistribute KSP2/Unity/Redux assemblies.

1. Extract this package.
2. Keep the supplied current KSP2 `KSP2_x64_Data\\Managed` directory available on the build machine.
3. Run `build\\build.ps1` from Windows PowerShell.
4. Point `-Ksp2Root` at your KSP2 installation.
5. Add `-Deploy` if you want the resulting mod copied to the Redux `mods` directory.

Example:

`powershell -ExecutionPolicy Bypass -File .\\build\\build.ps1 -Ksp2Root "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Kerbal Space Program 2"`

The script compiles `src\\*.cs`, writes `KerbalistTelemetryBridge.dll`, copies `swinfo.json`, and creates a deployable ZIP under `.build`.

## Current verification status

The implementation has been checked against the supplied assembly archive for the presence of key Redux/KSP2 API symbols such as `UniverseModel`, `TryGetActiveSimVessel`, `GetAllCelestialBodies`, `GetNodes`, `TotalDeltaVVac`, `TimeToAp`, `TimeToPe`, `bodyDisplayName`, `gravParameter` and `sphereOfInfluence`. 
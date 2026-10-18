# Changelog

All notable changes to the Kerbalist Telemetry Bridge are listed here.

## [1.3.1] - 2026-10-18

### Security
- The HTTP responses no longer allow every origin. Only the Kerbalist pages can read the data: `null` (a page opened from disk), `https://blocdigital.github.io`, and localhost on any port.
- Requests from other origins get `403 Forbidden`, for both HTTP and WebSocket.
- Requests whose `Host` header isn't this machine get `403 Forbidden`, which blocks DNS-rebinding attacks.
- At most 8 clients can be connected at once.

## [1.3.0] - 2026-10-18

### Added
- The active vessel's orbit is read from `game.ViewController.ActiveVesselVehicle.Orbit`, used when the vessel's own `Orbit` is null. It provides `apoapsis`, `periapsis`, the orbital elements, `referenceBody`, and the state vectors `localPosition` and `relativeVelocity`.
- The log says which route supplied the orbit: `route 2 (ViewController.ActiveVesselVehicle.Orbit)`, or that no route worked.

### Known Issues
- The active vessel's position is still not sent in the vessel block. Use `vessel.orbit.localPosition` for now.

## [1.2.0] - 2026-10-18

### Changed
- Built and tested against KSP2 Redux **0.2.9.0.105810** (snapshot 26w42a). The build now references `Ksp2Redux.dll`, which provides the Space Warp API, instead of the removed `ReduxLib.dll` and `SpaceWarp2.dll`.
- Logging uses Unity's `Debug.Log` and `Debug.LogError`, so messages appear in the game log without the removed ReduxLib logger.
- The mod starts through a Unity startup hook as well as the mod loader's `OnInitialized`, so the server starts even when the loader does not call `OnInitialized`.
- Startup progress is written to `bridge-startup.txt` in the mod folder, and echoed to the game log.
- `swinfo.json` version is 1.2.0.

### Removed
- The unused body capture method, and the logger parameter of the telemetry server, which was never used.

### Known Issues
- The active vessel's position is not included in the snapshot yet. The bridge reports it as zero, so the vessel can only be shown approximately.
- `vessel.orbit` is `null` for the active vessel. Clients can work out apoapsis and periapsis from `altitude`, `horizontalSpeed` and `orbitalSpeed`.

## [1.0.0]

- Initial release: HTTP and WebSocket telemetry for the active vessel, celestial bodies and maneuver nodes.

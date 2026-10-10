# Telemetry protocol v3

The websocket and `/api/state` endpoint emit a `TelemetrySnapshot`.

- `schema`: `kerbalist.telemetry.v3`
- `sequence`: monotonically increasing snapshot number
- `game`: current simulation clock/state
- `vessel`: active vessel, or `null`
- `solarSystem.staticRevision`: changes when the celestial-body catalogue/static data changes
- `solarSystem.stateRevision`: changes when a new body-state sample is published
- `solarSystem.bodies[]`: static celestial-body metadata
- `solarSystem.states[]`: current position/velocity/orbit for each body

The intended Kerbalist browser flow is to cache `solarSystem.bodies` by `staticRevision` and use `states` for the live map/transfer visualisation.

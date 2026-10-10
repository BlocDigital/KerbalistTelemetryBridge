# Installation

1. Build on Windows with the same KSP2 installation that will run the mod.
2. Run `build\\build.ps1`.
3. For automatic deployment, add `-Deploy`.
4. Start KSP2 Redux.
5. Open the included `browser\\index.html` or connect the Kerbalist app to `ws://127.0.0.1:8765/ws`.
6. Check `http://127.0.0.1:8765/api/status` if the browser reports no telemetry.

Do not copy the game's DLLs into the mod distribution. The build script references them from the local KSP2 Managed directory.

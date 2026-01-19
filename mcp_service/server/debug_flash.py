"""Debug script to trace flash calculation issues."""
import sys
sys.path.insert(0, ".")

from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.config.server_settings import ServerSettings
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
import asyncio

async def main():
    settings = ServerSettings()
    session_client = LimitedSessionClient(settings.resource_limits)
    session_client.start_monitoring()

    # Create session
    session_id = await session_client.create_session(flowsheet_name="debug-flash")
    print(f"Session created: {session_id}")

    try:
        # Get session manager and context
        from System import Guid
        guid = Guid.Parse(session_id)
        result = session_client.session_manager.GetSession(guid)
        if not result.Success:
            print(f"Failed to get session: {result.Message}")
            return
        context = result.Data
        print(f"Got context: {context}")

        # Set property package
        worker = load_dwsim_worker()
        from Serilog import LoggerConfiguration
        logger = LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger()

        pp_adapter = worker.Adapters.PropertyPackageAdapter(logger, context)
        pp_result = pp_adapter.SetPropertyPackage("peng-robinson", None)
        print(f"Property package set: Success={pp_result.Success}, Message={pp_result.Message}")

        # Add compound
        stream_adapter = worker.Adapters.StreamAdapter(logger, context)
        compound_result = stream_adapter.AddCompound("Methane")
        print(f"Compound added: Success={compound_result.Success}, Message={compound_result.Message}")

        # Create ThermodynamicsAdapter and run flash
        thermo_adapter = worker.Adapters.ThermodynamicsAdapter(logger, context)
        print("ThermodynamicsAdapter created")

        compounds = ["Methane"]
        composition = [1.0]
        temperature = 200.0  # K
        pressure = 101325.0  # Pa

        print(f"Calling FlashTP with: compounds={compounds}, composition={composition}, T={temperature}K, P={pressure}Pa")
        flash_result = thermo_adapter.FlashTP(compounds, composition, temperature, pressure)

        print(f"\nFlash Result:")
        print(f"  Converged: {flash_result.Converged}")
        print(f"  CalculationType: {flash_result.CalculationType}")
        print(f"  TemperatureK: {flash_result.TemperatureK}")
        print(f"  PressurePa: {flash_result.PressurePa}")
        print(f"  Phases count: {len(list(flash_result.Phases)) if flash_result.Phases else 0}")

        if flash_result.Phases:
            for i, phase in enumerate(flash_result.Phases):
                print(f"\n  Phase {i}:")
                print(f"    Label: {phase.PhaseLabel}")
                print(f"    Fraction: {phase.PhaseFraction}")

    finally:
        await session_client.close_session(session_id)
        await session_client.stop_monitoring()
        print("Session closed")

if __name__ == "__main__":
    asyncio.run(main())

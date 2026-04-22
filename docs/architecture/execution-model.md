# Execution Model

## Current Model: pythonnet In-Process

The DWSIM engine runs **in-process** within the Python MCP server. The
`DwsimWorker` .NET Framework assembly is loaded directly into the Python
process via [pythonnet](https://github.com/pythonnet/pythonnet) (the `clr`
module), which hosts the .NET CLR inside CPython.

```
Python MCP Server process
  |
  +-- pythonnet / CLR runtime
        |
        +-- DwsimWorker.dll  (assembly loader, engine facade)
        +-- DWSIM assemblies  (thermodynamics, flowsheet solver, etc.)
```

### How it works

1. The Python server starts and initialises the CLR via `pythonnet`.
2. `AssemblyLoader` (C#) locates and loads DWSIM assemblies from the
   configured `dwsim_binaries/` directory.
3. Python code calls into the C# engine classes directly through pythonnet's
   interop bridge -- no serialisation or network hops.
4. Results are returned as .NET objects that pythonnet exposes as Python
   objects.

### Implications

- **Single process** -- no IPC overhead, no second process to manage.
- **GIL interaction** -- pythonnet releases the GIL during .NET calls, so
  other Python threads can run while DWSIM computes.
- **Windows-only** -- DWSIM's .NET Framework 4.8 assemblies require Windows
  (with Desktop Experience for Eto.Forms/WinForms dependencies).

## Named-Pipe IPC (Not Implemented)

Early design explored a separate C# worker process (`DwsimWorker.exe`)
communicating with the Python MCP server over named pipes using JSON-RPC 2.0.

This path was **not implemented**. The `Program.cs` entry point contained only
TODO stubs for the named-pipe server loop and was removed in DIS-6. The
in-process pythonnet approach proved simpler and sufficient for the current
requirements.

If out-of-process execution is ever needed (e.g., for crash isolation or
running multiple DWSIM versions side by side), the named-pipe design could be
revisited. The `DwsimWorker` assembly's engine and loader classes are
process-agnostic and would work in either model.

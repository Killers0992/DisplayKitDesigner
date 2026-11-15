# DisplayKIT Designer

DisplayKIT Designer is a Unity 6 editor package for designing SCP: Secret Laboratory DisplayKit interfaces with UI Builder and generating C# for LabAPI projects.

It can generate either:

- partial `*.Designer.cs` class for a regular .NET Framework 4.8 project.
- standalone ScriptLoader `.cs` file containing the script entry point and generated panel.

## Requirements

- Unity 6 or newer
- SCP: Secret Laboratory Dedicated Server installation
- `Northwood.SCPSL.dll` from the server's `SCPSL_Data/Managed` directory
- LabAPI project targeting .NET Framework 4.8 (`net48`)
- `ScriptLoader.dll` only when using standalone ScriptLoader generation

The target project is expected to reference:

- `Northwood.SCPSL.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.TextRenderingModule.dll`
- `UnityEngine.UIElementsModule.dll`
- `ScriptLoader.dll` for ScriptLoader-file mode

## Installation

### Unity Package Manager

In Unity, open **Window → Package Management → Package Manager**, choose **Install package from git URL**, and enter:

```text
https://github.com/killers0992/DisplayKitDesigner.git?path=/Packages/com.killers0992.displaykit-designer
```

### Release archive

Download the `.unitypackage` from [GitHub Releases](https://github.com/killers0992/DisplayKitDesigner/releases) and import it into Unity. Releases also contain a `.tgz` suitable for **Install package from tarball** in Package Manager.

## Usage

1. Open **Tools → DisplayKit Designer**.
2. Select the server's `Northwood.SCPSL.dll`.
3. Create a UXML document or connect an existing one.
4. Select the LabAPI `.csproj` and generated-code destination.
5. Set the namespace and panel class.
6. Click **Generate now**, or enable **Generate on save**.

For a standalone script, enable **ScriptLoader file**, choose the LabAPI scripts directory, and configure the script class and display name.

Generated standalone scripts normally go to:

```text
%APPDATA%\SCP Secret Laboratory\LabAPI\scripts
```
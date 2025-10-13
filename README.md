# revit-mcp-commandset
🔄 Revit-MCP Client | Core implementation of the Revit-MCP protocol that connects LLMs with Revit. Includes essential CRUD commands for Revit elements enabling AI-driven BIM automation.

## Branch: dev/cli-based

This branch provides MCP commands optimized for **ai-cli tool integration**, with enhanced CLI interaction and automation capabilities.

# Custom Commands Setup

## Installation

1. Create folder: `RevitMCPCommandSet` at the end of the usual Revit addins directory like so `C:\Users\[USERNAME]\AppData\Roaming\Autodesk\Revit\Addins\20XX\RevitMCPCommandSet\`

2. Add files:
   - Copy `command.json` from this repo to the `RevitMCPCommandSet` folder
   - Create `20XX` subfolder
   - Place compiled output from this repo in the `20XX` subfolder

3. In Revit: Go to **Add-ins** > **Settings** > **Refresh** > **Save**

## Important Note

   - Command names must be identical between `revit-mcp` and `revit-mcp-commandset` repositories, otherwise Claude cannot find them.
   - The `commandRegistry.json` is created automatically, do not import it from the installer.
# SqlBulkSyncExport

.NET global tool that exports SQL Server **change tracking** deltas (and optional interval **full sync**) to CSV files in a target folder.

Inspired by [SqlBulkSyncFunction](https://github.com/WCOMAB/SqlBulkSyncFunction), without a target database: source schema drives CSV columns, and sync watermarks live in a YAML state file.

## Install

```bash
dotnet tool install --global SqlBulkSyncExport
```

## Usage

```bash
sqlbulksyncexport sync <config.yml> <state.yml> <outputfolder> [OPTIONS]
```

Options:

| Option | Description |
|--------|-------------|
| `--seed` | Force a full snapshot for change-tracking jobs |
| `--include-table <key>` | Only export the given table key (repeatable) |
| `--exclude-table <key>` | Skip the given table key (repeatable) |

## Authentication

Configure `source.connectionString` in YAML.

Set `source.entraIdAuth: true` to authenticate with `DefaultAzureCredential` (optional `source.tenantId` or `AZURE_TENANT_ID`).

## Configuration

See [`samples/config.yml`](samples/config.yml).

Important defaults:

- `includeHeader` defaults to **true** when omitted
- `writeDeleted` defaults to **false** (opt-in deleted PK files)
- `newLine` defaults to CRLF (`\r\n`)
- `progressLogBatchSize` defaults to **10000** (log every N written rows; `<= 0` disables)
- `targetFile` / `deletedFile` are **file names with extension**, no path
- Timestamp/version tokens are inserted **before** the extension

## Sync state

See [`samples/state.yml`](samples/state.yml). Missing table entries are treated as never synced (`currentVersion: -1`) and trigger a full snapshot export.

Do not flip a job between change tracking and `fullSync` without clearing state for those tables.

## Modes

- **Change tracking (default):** incremental I/U CSV; optional deleted-PK CSV; first run / invalid watermark / `--seed` writes a full snapshot
- **Full sync:** when `fullSync` is present, exports a full table snapshot when the interval elapses (Unix-ms watermark)

## Prerequisites

- .NET 10 SDK / runtime
- SQL Server with change tracking enabled on source tables (CT mode)

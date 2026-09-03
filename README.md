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

| Option                  | Description                                    |
|-------------------------|------------------------------------------------|
| `--seed`                | Force a full snapshot for change-tracking jobs |
| `--include-table <key>` | Only export the given table key (repeatable)   |
| `--exclude-table <key>` | Skip the given table key (repeatable)          |

## Authentication

Configure `source.connectionString` in YAML.

Set `source.entraIdAuth: true` to authenticate with `DefaultAzureCredential` (optional `source.tenantId` or `AZURE_TENANT_ID`).

## Configuration

See [`samples/config.yml`](samples/config.yml) for a fuller example.

Minimal config (table key becomes the default `{key}.csv` file name):

```yaml
source:
  connectionString: "Server=localhost;Initial Catalog=SyncTest;Integrated Security=True;TrustServerCertificate=True"
  # entraIdAuth: true          # optional; DefaultAzureCredential
  # tenantId: "00000000-0000-0000-0000-000000000000"  # optional; or AZURE_TENANT_ID

tables:
  dbo_Customers:
    source: dbo.Customers
  dbo_Orders:
    source: dbo.Orders
```

Optional `source` fields:

- `entraIdAuth` — defaults to **false**; set **true** for Entra / `DefaultAzureCredential`
- `tenantId` — optional tenant for Entra auth (falls back to `AZURE_TENANT_ID`)

Important defaults:

- `includeHeader` defaults to **true** when omitted
- `writeDeleted` defaults to **false** (opt-in deleted PK files)
- `newLine` defaults to CRLF (`\r\n`)
- `progressLogBatchSize` omitted/`null` = auto via `COUNT` (logs percent + ETA; step size by row count: &lt;1k → 50%, &lt;5k → 25%, &lt;10k → 20%, &lt;25k → 10%, &lt;250k → 5%, &lt;500k → 2%, else 1%); `<= 0` disables; positive = fixed row interval (no COUNT/%/ETA)
- `targetFile` / `deletedFile` are **file names with extension**, no path
- Timestamp/version tokens are inserted **before** the extension, for example:
  - delta: `dbo_Customers_20260101120000_0000000010_0000000020.csv`
  - full snapshot: `dbo_Customers_20260101120000_0000000020_full.csv`
  - deleted PKs (when enabled): `dbo_Customers.deleted_20260101120000_0000000010_0000000020.csv`

## Sync state

See [`samples/state.yml`](samples/state.yml). Missing table entries are treated as never synced (`currentVersion: -1`) and trigger a full snapshot export.

Do not flip a job between change tracking and `fullSync` without clearing state for those tables.

## Modes

- **Change tracking (default):** incremental I/U CSV; optional deleted-PK CSV; first run / invalid watermark / `--seed` writes a full snapshot
- **Full sync:** when `fullSync` is present, exports a full table snapshot when the interval elapses (Unix-ms watermark)

## Prerequisites

- .NET 10 SDK / runtime
- SQL Server with change tracking enabled on source tables (CT mode)

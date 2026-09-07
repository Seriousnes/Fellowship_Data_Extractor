# Fellowship extractor

Reads the Fellowship game install and writes the export: every entity as its type's JSON document
under `export-<steam build id>/`, and every image under `ui/`.

## Run it

```powershell
dotnet run --project src/FellowshipExtractor
```

With no command, the extractor writes the whole export to `output/`, unless an option names a
remote store.

## Options

The export takes the following options:

- **`--usmap`**: Recovers the property mappings first, and stops before exporting if they come back
  short.
- **`--output PATH`**: Writes to a folder, `output/` unless a remote store is named.
- **`--connection-string CONNECTION_STRING`**: Writes to an Azure Blob container, also read from
  the `ConnectionStrings__icons` environment variable.
- **`--bucket BUCKET`**: Writes to an S3 bucket, whose region and credentials come from the AWS SDK
  configuration chain, also read from the `S3__Bucket` environment variable.

Naming more than one destination writes each of them in the one pass.

## Commands

The extractor takes the following commands in place of the export:

- **`usmap`**: Recovers the property mappings without exporting.
- **`dump FILTER [COUNT]`**: Writes every export of the matching cooked assets as JSON under
  `output/dump`, for reading an asset the export does not carry. `COUNT` defaults to 1.
- **`dump-all FOLDER`**: Writes the evidence base behind every ability and gameplay effect into the
  named folder. Give it a folder of its own rather than `output/`.

## Environment variables

The extractor reads the following environment variables:

- **`FELLOWSHIP_USMAP`**: A `.usmap` file for this build. Unset, the extractor reads the highest
  build id under `mappings/` at the repository root.
- **`FELLOWSHIP_BUILD`**: The build the export folder is filed under. Unset, the Steam
  `appmanifest_*.acf` file for the install supplies it.
- **`FELLOWSHIP_UE_VERSION`**: An engine version override, such as `5.6` or `GAME_UE5_6`.
- **`FELLOWSHIP_LOG`**: Any value writes read failures to standard error.

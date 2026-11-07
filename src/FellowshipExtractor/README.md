# Fellowship extractor

Reads the game install and writes the export to `output/`.

```shell
dotnet run --project src/FellowshipExtractor [-- <command>] [options]
```

| Option | |
| --- | --- |
| `--install PATH` | Game install folder. Default: found in the Steam libraries. |
| `--output PATH` | Folder to write to. Default: `output/`. |
| `--connection-string VALUE` | Also write to an Azure Blob container. |
| `--bucket NAME` | Also write to an S3 bucket (AWS SDK credentials). |
| `--usmap` | Recover the property mappings first. Needed when the game updates. |

| Command | |
| --- | --- |
| `usmap` | Recover the property mappings only. |
| `patch-notes` | Rewrite each export's patch notes. No game install needed. |
| `dump FILTER [COUNT]` | Write matching cooked assets as JSON to `output/dump`. |
| `blueprints [FILTER]` | Write cooked blueprints as pseudo-code to `blueprints/`. |
| `dump-all FOLDER` | Write the raw evidence behind every ability and effect. |

| Environment variable | |
| --- | --- |
| `FELLOWSHIP_INSTALL` | Game install folder, same as `--install`. |
| `FELLOWSHIP_USMAP` | A `.usmap` file. Default: the newest in `mappings/`. |
| `FELLOWSHIP_BUILD` | Build id for the export folder. Default: from the Steam appmanifest. |
| `FELLOWSHIP_UE_VERSION` | Engine version override, such as `5.6`. |
| `FELLOWSHIP_LOG` | Any value prints read failures to standard error. |

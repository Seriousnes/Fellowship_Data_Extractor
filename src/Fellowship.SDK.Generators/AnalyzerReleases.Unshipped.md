; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FSDK001 | Fellowship.SDK | Error | The vocabulary is not among the additional files
FSDK002 | Fellowship.SDK | Error | The vocabulary cannot be read
FSDK003 | Fellowship.SDK | Error | The vocabulary states a word that is not a legal C# identifier
FSDK004 | Fellowship.SDK | Error | The vocabulary states no members for an enumerated field

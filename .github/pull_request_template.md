## What

<!-- What changes, and why. Link the issue if there is one. -->

## Checks

- [ ] `dotnet build -c Release` — zero warnings
- [ ] `dotnet test -c Release` — green
- [ ] Public API change: `PublicAPI.Unshipped.txt` updated (the build fails otherwise)
- [ ] XML output change: snapshot diffs reviewed before re-blessing, and `CHANGELOG.md` updated
- [ ] Integration tests run if this touches XML generation:
      `ANAF_INTEGRATION=1 dotnet test tests/EFactura.Net.IntegrationTests -c Release`
- [ ] No real CUI, IBAN, company name or CNP anywhere in the diff

## Anything a reviewer should know

<!-- Surprising ANAF behaviour, a rule that forced a decision, a trade-off you made. -->

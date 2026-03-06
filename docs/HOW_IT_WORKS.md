# CTO Engine v1 Workflow

## Initial Setup (One Time)

1. Build CLI:
```bash
/Users/paulofmbarros/.dotnet/dotnet build /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli/Cto.Cli.csproj
```
2. Export Jira credentials in the same shell session used to run commands:
```bash
export JIRA_EMAIL="you@company.com"
export JIRA_API_TOKEN="your_api_token"
```
3. Ensure your project root has required files:
- `charter.md`
- `context.md`
- `weeklylog.md`
- `plan.yaml`
- `.cto-engine/jira-config.yaml`
- `.cto-engine/challenge-log.yaml`
4. Replace template placeholders before first planning run:
- `YYYY-MM-DD`
- `[Project Name]`
- bracketed placeholders like `[Your Name]`
5. Validate Jira config and run first snapshot:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- validate --project /path/to/project --target jira-config
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- snapshot --project /path/to/project
```

## Weekly Ritual

1. Run snapshot telemetry:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- snapshot --project /path/to/project
```
2. Generate plan-vs-reality report:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- reality-check --project /path/to/project
```
3. Update `context.md` and `weeklylog.md` manually.
4. Generate planning bundle:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- plan --interactive --project /path/to/project
```
Note: this command copies current file contents into prompts verbatim. If `charter.md`, `context.md`, or `weeklylog.md` still has template text, that same template text will appear in generated prompt files.
5. Manually consolidate proposal into `plan.yaml`.
6. Validate contracts:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- validate --project /path/to/project --target all
```
7. Approve plan commit:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- approve --project /path/to/project
```
8. Execute Jira creation/update:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- execute --project /path/to/project
```

## Daily Optional Automation

Run snapshot only:
```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- snapshot --project /path/to/project
```

## Guardrails

- `plan --interactive` is blocked if `context.md` or `weeklylog.md` is invalid.
- `approve` is blocked if validation fails or git changes exceed approval scope.
- `execute` is blocked unless HEAD is an approval commit.
- v1 supports Jira `project.mode: company_managed` and `project.mode: team_managed`.
- Idempotency checks Jira issue property first, then commit label fallback.

## Troubleshooting

- `command not found: cto-engine`: run via `/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- <command>`.
- `jira-config.yaml is missing`: create `.cto-engine/` in your project root and copy the template from `schemas/jira-config.yaml.template`.
- `Missing Jira credentials`: run `printenv JIRA_EMAIL` and `printenv JIRA_API_TOKEN` in the same shell before running CLI commands.

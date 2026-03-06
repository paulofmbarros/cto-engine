# CTO Engine

Local-first .NET CLI that turns approved planning artifacts into auditable Jira execution.

## Initial Setup

### Prerequisites

- .NET SDK available via `dotnet` or `/Users/paulofmbarros/.dotnet/dotnet`
- Jira account email and API token
- A project root where the pack files will live

### 1) Build the CLI

```bash
/Users/paulofmbarros/.dotnet/dotnet build /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli/Cto.Cli.csproj
```

### 2) Pick how to run commands

Installed tool:

```bash
cto-engine --help
```

Run directly from source (works even if `cto-engine` is not on PATH):

```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- --help
```

### 3) Set Jira credentials in your shell

```bash
export JIRA_EMAIL="you@company.com"
export JIRA_API_TOKEN="your_api_token"
printenv JIRA_EMAIL
printenv JIRA_API_TOKEN
```

If you keep them in `~/.zshrc`, reload before running CLI commands:

```bash
source ~/.zshrc
```

### 4) Prepare your project pack

For a new project:

```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- init --path /path/to/project
```

For an existing project, ensure Jira config exists:

```bash
mkdir -p /path/to/project/.cto-engine
cp /Users/paulofmbarros/Documents/cto-engine/schemas/jira-config.yaml.template /path/to/project/.cto-engine/jira-config.yaml
```

Then ensure these files exist and are filled with real project content (not placeholder text):

- `/path/to/project/charter.md`
- `/path/to/project/context.md`
- `/path/to/project/weeklylog.md`
- `/path/to/project/plan.yaml`
- `/path/to/project/.cto-engine/challenge-log.yaml`

If you copied templates, replace `YYYY-MM-DD`, `[Project Name]`, and bracketed placeholders before running `plan --interactive`.

### 5) Validate config and run first snapshot

```bash
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- validate --project /path/to/project --target jira-config
/Users/paulofmbarros/.dotnet/dotnet run --project /Users/paulofmbarros/Documents/cto-engine/src/Cto.Cli -- snapshot --project /path/to/project
```

`plan --interactive` behavior:
- Writes prompt bundle files under `/path/to/project/docs/proposals/<YYYY-WW>/`.
- Embeds `charter.md`, `context.md`, `weeklylog.md`, `reality-check.md`, and `challenge-log.yaml` exactly as they exist.
- If those files still contain template placeholders, the generated prompts will also contain placeholders.

## Commands

- `cto-engine init --path <target> [--force]`
- `cto-engine snapshot --project <project-root>`
- `cto-engine reality-check --project <project-root>`
- `cto-engine validate --project <project-root> --target <all|plan|context|weeklylog|jira-config>`
- `cto-engine plan --interactive --project <project-root>`
- `cto-engine approve --project <project-root>`
- `cto-engine execute --project <project-root> [--dry-run]`

## Canonical Contracts

- `/Users/paulofmbarros/Documents/cto-engine/schemas/plan.schema.json`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/jira-config.yaml.template`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/context.md.template`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/weeklylog.md.template`

## Project Root Contract

Each `--project` path must point to a root directory containing:

- `charter.md`
- `context.md`
- `weeklylog.md`
- `plan.yaml`
- `.cto-engine/jira-config.yaml`
- `.cto-engine/challenge-log.yaml`

See `/Users/paulofmbarros/Documents/cto-engine/docs/HOW_IT_WORKS.md` for weekly operation.

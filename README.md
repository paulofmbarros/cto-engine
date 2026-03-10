# CTO Engine

Local-first .NET CLI that turns approved planning artifacts into auditable Jira execution.

## Initial Setup

### Prerequisites

- .NET SDK available via `dotnet` or `/Users/paulofmbarros/.dotnet/dotnet`
- Jira account email and API token
- Gemini API key (only if using LLM-backed planning)
- A project root where the pack files will live

### 1) Build the CLI

```bash
make build
```

If `dotnet` is not on `PATH`, override it explicitly:

```bash
make build DOTNET=/Users/paulofmbarros/.dotnet/dotnet
```

### 2) Pick how to run commands

Recommended operator interface:

```bash
make help
```

Raw CLI fallback (works even if `cto-engine` is not on PATH):

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

If you want direct LLM planning through Gemini, also set:

```bash
export GEMINI_API_KEY="your_gemini_api_key"
printenv GEMINI_API_KEY
```

### 4) Prepare your project pack

For a new project:

```bash
make init PROJECT=/path/to/project
```

For an existing project, ensure Jira config exists:

```bash
mkdir -p /path/to/project/.cto-engine
cp /Users/paulofmbarros/Documents/cto-engine/schemas/jira-config.yaml.template /path/to/project/.cto-engine/jira-config.yaml
cp /Users/paulofmbarros/Documents/cto-engine/schemas/llm-config.yaml.template /path/to/project/.cto-engine/llm-config.yaml
```

Then ensure these files exist and are filled with real project content (not placeholder text):

- `/path/to/project/charter.md`
- `/path/to/project/context.md`
- `/path/to/project/weeklylog.md`
- `/path/to/project/plan.yaml`
- `/path/to/project/.cto-engine/challenge-log.yaml`

For LLM planning mode, also ensure:

- `/path/to/project/.cto-engine/llm-config.yaml`

If you copied templates, replace `YYYY-MM-DD`, `[Project Name]`, and bracketed placeholders before running `make plan-interactive ...` or `make plan-llm ...`.

### 5) Validate config and run first snapshot

```bash
make doctor PROJECT=/path/to/project
make validate-jira PROJECT=/path/to/project
make snapshot PROJECT=/path/to/project
```

`plan --interactive` behavior:
- Writes prompt bundle files under `/path/to/project/docs/proposals/<YYYY-WW>/`.
- Embeds `charter.md`, `context.md`, `weeklylog.md`, `reality-check.md`, and `challenge-log.yaml` exactly as they exist.
- If those files still contain template placeholders, the generated prompts will also contain placeholders.

## Make Targets

- `make help`
- `make build`
- `make test`
- `make init PROJECT=<project-root> [FORCE=1]`
- `make doctor PROJECT=<project-root> [REQUIRE_LLM=1]`
- `make validate PROJECT=<project-root> [TARGET=all]`
- `make validate-jira PROJECT=<project-root>`
- `make validate-llm PROJECT=<project-root>`
- `make snapshot PROJECT=<project-root>`
- `make reality-check PROJECT=<project-root>`
- `make plan-interactive PROJECT=<project-root>`
- `make plan-llm PROJECT=<project-root> [CANDIDATES=3] [VISION_FILE=/path/to/company-vision.md]`
- `make plan-list PROJECT=<project-root>`
- `make plan-select PROJECT=<project-root> CANDIDATE=<n>`
- `make approve PROJECT=<project-root>`
- `make execute-dry-run PROJECT=<project-root>`
- `make execute PROJECT=<project-root>`
- `make weekly-prep PROJECT=<project-root>`
- `make weekly-interactive PROJECT=<project-root>`
- `make weekly-llm PROJECT=<project-root> [CANDIDATES=3]`
- `make weekly-review PROJECT=<project-root> [CANDIDATES=3]`

Raw CLI commands are still supported. `make` is only a wrapper around the same CLI entrypoints.

Recommended weekly shortcut:

```bash
make weekly-review PROJECT=/path/to/project CANDIDATES=3
```

This runs preflight checks, refreshes Jira state, generates three LLM-backed plan candidates, and lists the candidates for human review. It stops before approval and execution.

## Canonical Contracts

- `/Users/paulofmbarros/Documents/cto-engine/schemas/plan.schema.json`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/llm-config.schema.json`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/jira-config.yaml.template`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/context.md.template`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/weeklylog.md.template`
- `/Users/paulofmbarros/Documents/cto-engine/schemas/llm-config.yaml.template`

## Architecture Diagrams

- `/Users/paulofmbarros/Documents/cto-engine/diagrams/weekly-operations-flow.puml`
- `/Users/paulofmbarros/Documents/cto-engine/diagrams/llm-candidate-planning-flow.puml`
- `/Users/paulofmbarros/Documents/cto-engine/diagrams/execution-guardrails-flow.puml`

## Project Root Contract

Each `--project` path must point to a root directory containing:

- `charter.md`
- `context.md`
- `weeklylog.md`
- `plan.yaml`
- `.cto-engine/jira-config.yaml`
- `.cto-engine/challenge-log.yaml`
- `.cto-engine/llm-config.yaml` (required for `plan --llm`)

See `/Users/paulofmbarros/Documents/cto-engine/docs/HOW_IT_WORKS.md` for weekly operation.

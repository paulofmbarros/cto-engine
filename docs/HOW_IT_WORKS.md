# CTO Engine v1 Workflow

## Initial Setup (One Time)

1. Build CLI:
```bash
make build
```
2. Export Jira credentials in the same shell session used to run commands:
```bash
export JIRA_EMAIL="you@company.com"
export JIRA_API_TOKEN="your_api_token"
```
3. (Optional, only for Gemini planning) Export Gemini API key:
```bash
export GEMINI_API_KEY="your_gemini_api_key"
```
4. Ensure your project root has required files:
- `charter.md`
- `context.md`
- `weeklylog.md`
- `plan.yaml`
- `.cto-engine/jira-config.yaml`
- `.cto-engine/challenge-log.yaml`
- `.cto-engine/llm-config.yaml` (required only for `plan --llm`)
5. Replace template placeholders before first planning run:
- `YYYY-MM-DD`
- `[Project Name]`
- bracketed placeholders like `[Your Name]`
6. Validate Jira config and run first snapshot:
```bash
make doctor PROJECT=/path/to/project
make validate-jira PROJECT=/path/to/project
make snapshot PROJECT=/path/to/project
```

## Weekly Ritual

1. Run snapshot telemetry:
```bash
make snapshot PROJECT=/path/to/project
```
2. Generate plan-vs-reality report:
```bash
make reality-check PROJECT=/path/to/project
```
3. Update `context.md` and `weeklylog.md` manually.
4. Generate planning bundle (manual prompt copy mode):
```bash
make plan-interactive PROJECT=/path/to/project
```
Note: this command copies current file contents into prompts verbatim. If `charter.md`, `context.md`, or `weeklylog.md` still has template text, that same template text will appear in generated prompt files.
5. Or generate three candidate plans directly via Gemini:
```bash
make plan-llm PROJECT=/path/to/project CANDIDATES=3
```
This creates candidate plan files under `docs/proposals/<YYYY-WW>/` and a `candidates-summary.md`.
Fastest review path:
```bash
make weekly-review PROJECT=/path/to/project CANDIDATES=3
```
This runs doctor, snapshot, reality-check, LLM planning, and candidate listing in one command.
If you want to preflight LLM readiness separately, run `make doctor PROJECT=/path/to/project REQUIRE_LLM=1`.
6. List candidate files and choose one:
```bash
make plan-list PROJECT=/path/to/project
make plan-select PROJECT=/path/to/project CANDIDATE=2
```
7. Validate contracts:
```bash
make validate PROJECT=/path/to/project TARGET=all
```
8. Approve plan commit:
```bash
make approve PROJECT=/path/to/project
```
9. Execute Jira creation/update:
```bash
make execute PROJECT=/path/to/project
```

Convenience wrappers:
```bash
make weekly-prep PROJECT=/path/to/project
make weekly-interactive PROJECT=/path/to/project
make weekly-llm PROJECT=/path/to/project CANDIDATES=3
make weekly-review PROJECT=/path/to/project CANDIDATES=3
```

## Daily Optional Automation

Run snapshot only:
```bash
make snapshot PROJECT=/path/to/project
```

## Guardrails

- `plan --interactive` is blocked if `context.md` or `weeklylog.md` is invalid.
- `plan --llm` is blocked if `context.md`, `weeklylog.md`, or `.cto-engine/llm-config.yaml` is invalid.
- `approve` is blocked if validation fails or git changes exceed approval scope.
- `execute` is blocked unless HEAD is an approval commit.
- v1 supports Jira `project.mode: company_managed` and `project.mode: team_managed`.
- Idempotency checks Jira issue property first, then commit label fallback.
- Gemini API keys are read only from env vars (for example `GEMINI_API_KEY`); keys are not stored in source files.

## Troubleshooting

- `Unknown workflow step`: run `make help` to see the supported wrapper targets.
- `Doctor failed`: fix the missing files or env vars reported by `make doctor PROJECT=...` before running weekly targets.
- `jira-config.yaml is missing`: create `.cto-engine/` in your project root and copy the template from `schemas/jira-config.yaml.template`.
- `Missing Jira credentials`: run `printenv JIRA_EMAIL` and `printenv JIRA_API_TOKEN` in the same shell before running CLI commands.
- `llm-config.yaml is missing`: copy `schemas/llm-config.yaml.template` to `.cto-engine/llm-config.yaml`.
- `Missing Gemini credentials`: run `printenv GEMINI_API_KEY` before `plan --llm`.

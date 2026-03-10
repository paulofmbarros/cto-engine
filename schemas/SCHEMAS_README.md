# CTO Engine Schema Documentation

This directory contains the complete schema definitions and templates for the CTO Engine system.

## Quick Start

**For a new project:**

1. Copy the templates to your project directory:
   ```bash
   mkdir -p my-project/{.cto-engine,docs}
   cp charter.md.template my-project/charter.md
   cp context.md.template my-project/context.md
   cp weeklylog.md.template my-project/weeklylog.md
   cp jira-config.yaml.template my-project/.cto-engine/jira-config.yaml
   cp challenge-log.yaml.template my-project/.cto-engine/challenge-log.yaml
   cp llm-config.yaml.template my-project/.cto-engine/llm-config.yaml
   ```

2. Fill in your charter.md (this never changes)
3. Fill in your jira-config.yaml (your Jira connection details)
4. Start your first weekly cycle

---

## Jira Ticket Structure

All Jira tickets created by the CTO Engine follow this standardized structure:

```
[Summary]

h2. Objective
[What this achieves and why it matters - 1-2 sentences]

h2. Scope
h3. In scope
* [Feature 1]
* [Feature 2]

h3. Out of scope
* [Thing explicitly excluded 1]
* [Thing explicitly excluded 2]

h2. Acceptance Criteria
# [Testable criterion 1]
# [Testable criterion 2]

h2. References
* [Link title 1|URL]
* [Link title 2|URL]

h2. Constraints / Rules
* [Constraint 1]
* [Constraint 2]

h2. Definition of Done
* [DoD item 1]
* [DoD item 2]
```

### Why this structure?

**Objective** - Forces clarity on what you're trying to achieve, not just what you're building.

**Scope** - Explicit in/out boundaries prevent scope creep and align expectations.

**Acceptance Criteria** - Numbered, testable statements that define "done".

**References** - Links to decisions, designs, or related docs provide context.

**Constraints** - Technical or business rules that must be respected.

**Definition of Done** - Checklist ensures nothing is forgotten (tests, docs, updates).

### Example from real project

```
Summary: Implement Supabase JWT authentication middleware

h2. Objective
Allow the backend to validate Supabase-issued JWTs and extract the authenticated user identity.

h2. Scope
h3. In scope
* JWT bearer authentication
* Supabase JWKS validation  
* User ID extraction from sub claim

h3. Out of scope
* Authorization rules
* Role-based access
* Frontend auth UI

h2. Acceptance Criteria
# Unauthenticated requests are rejected
# Valid Supabase JWT is accepted
# userId is extracted from token
# Protected endpoints require auth

h2. References
* [Backend Work Contract|https://notion.so/backend-contract]
* [Supabase Auth (Option A decision)|https://notion.so/auth-decision]

h2. Constraints / Rules
* Backend validates JWT only
* No custom auth logic
* No user table required yet

h2. Definition of Done
* Auth middleware works
* AC verified
* Jira updated
```

---

## File Hierarchy

```
project-root/
├── charter.md              # Unchanging project definition
├── context.md              # Current reality snapshot (updated weekly)
├── weeklylog.md            # Surprise/learning signals (updated async)
├── plan.yaml               # Approved execution plan (git-committed)
├── .cto-engine/
│   ├── jira-config.yaml    # Jira integration settings
│   ├── llm-config.yaml     # Gemini planning settings (optional unless using --llm)
│   ├── challenge-log.yaml  # AI warning outcomes tracker
│   ├── snapshot.json       # Generated Jira state (read-only)
│   ├── reality-check.md    # Plan vs. actual comparison (generated)
│   └── logs/               # Error and execution logs
└── docs/
    └── proposals/          # AI-generated proposals (archived)
```

---

## Schema Files

### 1. plan.schema.json

**Purpose:** Defines the structure of approved execution plans.

**Key sections:**
- `goal` - One-sentence outcome
- `success_criteria` - Measurable success definition
- `risks` - Identified risks with mitigation
- `work_breakdown` - EPICs and stories for Jira
- `assumptions` - Explicit assumptions that could break
- `dependencies` - Optional external blockers

**Validation:** JSON Schema validation runs before approval and uses strict object validation (`additionalProperties: false`) for plan structures.

**Example:** See `plan.example.yaml`

---

### 2. charter.md.template

**Purpose:** Immutable project definition that provides strategic context.

**Key sections:**
- What this project is (and is NOT)
- Success definition
- Core principles
- User archetypes
- Technical philosophy
- Governance rules

**Update frequency:** Quarterly review, rarely changes.

**Anti-pattern:** Treating this as a roadmap (it's not).

---

### 3. context.md.template

**Purpose:** Schema-enforced snapshot of current reality.

**Required sections:**
- What exists? (production state)
- What's in flight? (current work)
- What's broken? (known issues)
- What changed? (since last week)

**Update frequency:** Weekly, before planning session.

**Validation:** Sections must be present and non-empty.

---

### 4. weeklylog.md.template

**Purpose:** Structured signal capture for decision-making.

**Required questions:**
- What surprised you?
- What assumption broke?
- What did users actually do?
- What blocked you?
- What did you learn?

**Update frequency:** Throughout the week (2-3 min per entry).

**Anti-pattern:** Using this as a diary or todo list.

---

### 5. jira-config.yaml.template

**Purpose:** Defines Jira integration, JQL queries, and ticket creation rules.

**Key sections:**
- Jira authentication (env vars)
- Project settings (keys, issue types, custom fields, project mode)
- JQL queries (snapshot generation)
- Ticket creation templates
- Idempotency rules (issue property primary, label fallback)
- Validation rules (estimates, acceptance criteria)

**Update frequency:** Rarely (only when Jira config changes).

**Compatibility (v1):** Company-managed and team-managed Jira projects are supported. For team-managed projects, set `project.mode: team_managed` and use `project.custom_fields.epic_link: parent`.

---

### 6. challenge-log.yaml.template

**Purpose:** Tracks AI warnings and their outcomes to calibrate trust.

**Structure:**
Each challenge entry captures:
- What AI warned about
- What human decided
- What actually happened
- Lessons learned
- Trust impact score

**Generated statistics:**
- AI accuracy by category
- AI accuracy by severity
- Overall trust score (-10 to +10)
- Cost of ignoring AI warnings

**Ownership:** `statistics` is engine-computed and should not be edited manually.

**Update frequency:** Log when human overrides AI; update outcome 1-4 weeks later.

---

### 7. llm-config.yaml.template

**Purpose:** Configures provider-backed planning candidate generation (Gemini in v1).

**Key sections:**
- Provider and model (`provider`, `providers.gemini.*`)
- Generation strategy (`generation.candidates`, temperature, token limits)
- Input/output contracts (required files, candidate naming, summary file)
- Validation and budget controls (minimum valid plans, max tokens/cost)
- Logging and security controls

**Security rule:** API keys are never stored in this file. Use `api_key_env_var` and shell/CI secrets.

**Update frequency:** Rarely (model or budget changes).

---

## Schema Validation Rules

### plan.yaml validation

**Required fields:**
- version, goal, success_criteria, risks, work_breakdown, assumptions

**Constraints:**
- Goal: 10-200 characters
- Success criteria: 1-5 items
- Story estimates: 1, 2, 3, 5, 8, 13 (Fibonacci)
- Acceptance criteria: minimum 1 per story
- Story dependencies must be valid Jira keys (e.g., `PROJ-123`; placeholders not allowed in approved plans)
- Unknown fields in plan objects are rejected (strict schema)

**Enforcement:** Validation blocks approval if schema is invalid.

---

### context.md validation

**Required sections:**
- All four main sections must be present
- At least one item in "What exists?"
- Blockers must be explicit (or "None")
- Metrics must be quantified

**Enforcement:** Warning logged on save. `make plan-interactive PROJECT=...` and `make execute PROJECT=...` are blocked if validation fails.

---

### weeklylog.md validation

**Required sections:**
- All five questions must be answered
- Each section has content OR explicit "None" statement
- Surprises must be specific (not vague)
- Blockers must quantify time impact

**Enforcement:** Warning logged on save. `make plan-interactive PROJECT=...` and `make execute PROJECT=...` are blocked if validation fails.

---

### llm-config.yaml validation

**Required fields:**
- `provider` must be `gemini`
- `providers.gemini.api_key_env_var`
- `providers.gemini.model`
- `providers.gemini.base_url`
- `output.candidate_file_pattern` must contain `{n}`

**Constraints:**
- `generation.candidates` range: 1..5
- `generation.max_output_tokens_per_candidate` >= 256
- Budget limits must be positive

**Enforcement:** `make plan-llm PROJECT=...` is blocked if llm-config is invalid.

---

## Weekly Workflow with Schemas

### Step 1: Reality Check (automated)

```bash
make doctor PROJECT=/path/to/project
make reality-check PROJECT=/path/to/project
```

**Reads:**
- Last week's `plan.yaml`
- Current `snapshot.json` from Jira

**Generates:**
- `reality-check.md` (plan vs. actual comparison)

**Validates:**
- Nothing (pure data fetch)

---

### Step 2: Update Context (manual)

```bash
# You edit these files manually
vim context.md
vim weeklylog.md
```

**Validates on save:**
- `context.md` → Required sections present (warning-only)
- `weeklylog.md` → Required questions answered (warning-only)

---

### Step 3: AI Planning Session (semi-automated)

```bash
make plan-interactive PROJECT=/path/to/project
```

**Reads:**
- `charter.md`
- `context.md`
- `weeklylog.md`
- `reality-check.md`
- `challenge-log.yaml`

**Generates:**
- `proposal.md` (consolidated AI recommendations)

**Validates:**
- Input files against schemas before sending to AI
- Planning session is blocked if `context.md` or `weeklylog.md` fails validation

**LLM review shortcut:**
```bash
make weekly-review PROJECT=/path/to/project CANDIDATES=3
```
This runs doctor, snapshot, reality-check, LLM planning, and candidate listing in one pass.

---

### Step 4: Approve Plan (manual + automated)

```bash
# You edit plan.yaml based on proposal
vim plan.yaml

# Validate schema
make validate PROJECT=/path/to/project TARGET=plan

# If valid, approve (creates git commit)
make approve PROJECT=/path/to/project
```

**Validates:**
- `plan.yaml` against JSON Schema
- Git repository is clean (no uncommitted changes)
- Plan commit is signed (if configured)
- `metadata.commit_sha` is a full 40-character lowercase SHA-1 hash

**Generates:**
- Git commit with SHA
- Updated `challenge-log.yaml` (if AI was overridden)

---

### Step 5: Execute (automated)

```bash
make execute PROJECT=/path/to/project
```

**Reads:**
- `plan.yaml` (must be git-committed)
- `jira-config.yaml`

**Creates in Jira:**
- EPICs with descriptions
- Stories linked to EPICs
- Issue property with plan commit SHA (primary idempotency key)
- Labels including commit SHA (fallback idempotency key)

**Validates:**
- Idempotency (checks issue property first, then commit SHA labels)
- Jira connection is healthy
- All required fields are present
- Latest `context.md` and `weeklylog.md` pass validation gates

**Generates:**
- `snapshot.json` (updated)
- Execution log in `.cto-engine/logs/`

---

## Error Handling

### Schema validation failures

**When:** Running `make validate ...` or `make approve ...`

**Behavior:**
- Validation errors printed to console
- Approval blocked until fixed
- No partial commits

**Example error:**
```
plan.yaml:3 - 'goal' field is too short (minimum 10 characters)
plan.yaml:12 - 'estimate' must be a Fibonacci number (1,2,3,5,8,13)
```

---

### Missing required files

**When:** Running any command

**Behavior:**
- Command exits with error
- Helpful message suggests which file to create
- No partial execution

**Example error:**
```
Error: charter.md not found
Hint: Run 'cto-engine init' to create project structure
```

---

### Jira API failures

**When:** Snapshot or execute commands

**Behavior:**
- Uses last successful snapshot (if available)
- Logs error to `.cto-engine/logs/jira-errors.log`
- Non-blocking for snapshot (warns only)
- Blocking for execute (no tickets created on failure)

**Example error:**
```
Warning: Jira API unreachable, using snapshot from 2026-02-06
Execute blocked: Cannot create tickets without Jira connection
```

---

### Duplicate ticket prevention

**When:** Running `make execute PROJECT=...` multiple times

**Behavior:**
- Checks for existing tickets with same commit SHA issue property
- Falls back to commit SHA label matching if issue property is missing
- Updates existing tickets instead of creating duplicates
- Preserves manual changes (status, assignee, estimates)

**Example log:**
```
PROJ-123 already exists (commit: abcdef1234567890abcdef1234567890abcdef12)
Updated description and labels
Preserved status: In Progress (manual override)
```

---

## Schema Evolution

### Versioning strategy

All schemas include a `version` field (e.g., `"1.0"`).

**Breaking changes** increment major version (1.0 → 2.0):
- Required fields added
- Field types changed
- Validation rules tightened

**Non-breaking changes** increment minor version (1.0 → 1.1):
- Optional fields added
- Validation rules relaxed
- New enum values

---

### Migration process

When schemas change:

1. New schema version released with CTO Engine update
2. Migration script provided (e.g., `migrate-1.0-to-2.0.sh`)
3. Projects pin their required schema version in `.cto-engine/config`
4. Backward compatibility maintained for 1 major version

**Example migration:**
```bash
# Upgrade from schema 1.0 to 2.0
cto-engine migrate --from=1.0 --to=2.0

# Review changes
git diff plan.yaml

# Commit if acceptable
git commit -m "Migrate plan.yaml to schema 2.0"
```

---

### Schema compatibility matrix

| CTO Engine | plan.yaml | context.md | weeklylog.md | jira-config.yaml |
|------------|-----------|------------|--------------|------------------|
| 1.0.x      | 1.0       | 1.0        | 1.0          | 1.0              |
| 1.1.x      | 1.0-1.1   | 1.0        | 1.0-1.1      | 1.0              |
| 2.0.x      | 2.0       | 2.0        | 2.0          | 1.0-2.0          |

---

## Best Practices

### Schema hygiene

✅ **DO:**
- Validate before every approval
- Keep schemas in version control
- Review schema on every CTO Engine upgrade
- Use migration scripts (don't hand-edit)

❌ **DON'T:**
- Skip validation to "move faster"
- Modify generated files manually
- Create custom schema versions
- Ignore deprecation warnings

---

### Context freshness

✅ **DO:**
- Update context.md weekly (minimum)
- Quantify all metrics (no vague "improving")
- Be honest about blockers
- Call out "None" explicitly when nothing changed

❌ **DON'T:**
- Copy-paste from Jira without interpretation
- Leave sections blank
- Use relative terms ("recently", "soon")
- Hide problems to look good

---

### Weekly log discipline

✅ **DO:**
- Capture surprises in the moment (not retrospectively)
- Be specific (numbers, dates, names)
- Focus on decision-relevant signals
- Keep entries <3 sentences

❌ **DON'T:**
- Turn it into a novel
- Log every minor detail
- Wait until planning session to fill it
- Use it as a todo list

---

### Plan quality

✅ **DO:**
- Write goal as one clear sentence
- Make success criteria measurable
- List risks with specific mitigation
- Estimate stories honestly (Fibonacci only)

❌ **DON'T:**
- Make goal vague or aspirational
- Write unmeasurable success criteria
- Skip risk identification
- Inflate estimates to pad timelines

---

### Challenge log honesty

✅ **DO:**
- Log every time you override AI warnings
- Update outcomes truthfully (even when AI was wrong)
- Review trust scores monthly
- Learn from patterns (why was AI right/wrong?)

❌ **DON'T:**
- Cherry-pick only AI successes
- Ignore outcomes that prove you wrong
- Blame AI for your decisions
- Use trust score as vanity metric

---

## Troubleshooting

### "Schema validation failed"

**Cause:** plan.yaml doesn't match schema

**Fix:**
```bash
# See specific errors
make validate PROJECT=/path/to/project TARGET=plan

# Common fixes:
# - goal too short: must be 10-200 chars
# - estimate not Fibonacci: use 1,2,3,5,8,13
# - missing acceptance_criteria: add at least one per story
# - unknown fields in nested objects: remove non-schema properties
# - metadata.commit_sha must be a full lowercase 40-char SHA-1
```

---

### "Context sections missing"

**Cause:** context.md doesn't have all required sections

**Fix:**
```bash
# Check which sections are missing
make validate PROJECT=/path/to/project TARGET=context

# Required sections:
# - What exists?
# - What's in flight?
# - What's broken?
# - What changed?
```

---

### "Jira config invalid"

**Cause:** jira-config.yaml has wrong format or missing fields

**Fix:**
```bash
# Validate config
make validate-jira PROJECT=/path/to/project

# Common issues:
# - Missing JIRA_EMAIL or JIRA_API_TOKEN env vars
# - Wrong project key format
# - Invalid JQL syntax in queries
# - Unsupported project mode (use company_managed or team_managed)
```

---

### "Plan not approved"

**Cause:** Plan is not in an approved state (not committed, dirty repo, or failed validation)

**Fix:**
```bash
# Approve the plan first
make approve PROJECT=/path/to/project

# This requires a clean repo and creates a git commit with SHA
# Execute will now work
make execute PROJECT=/path/to/project
```

---

### "Doctor failed"

**Cause:** Project contract files or required environment variables are missing

**Fix:**
```bash
make doctor PROJECT=/path/to/project

# Common issues:
# - Missing charter.md/context.md/weeklylog.md/plan.yaml
# - Missing .cto-engine/jira-config.yaml
# - Missing JIRA_EMAIL or JIRA_API_TOKEN
# - Missing GEMINI_API_KEY when using weekly-review / plan-llm
```

---

## Advanced Usage

### Custom validation rules

Add project-specific rules to `.cto-engine/custom-validation.yaml`:

```yaml
plan:
  max_stories_per_epic: 8
  require_labels: ["mvp", "backend", "frontend"]
  forbidden_words: ["ASAP", "urgent", "whenever"]

context:
  max_blockers: 3  # Force splitting if >3 blockers
  require_metrics: true
  
weeklylog:
  min_surprises_per_week: 1  # Flag if nothing surprising (unrealistic)
```

---

### Multi-project setup

Use the same CTO Engine for multiple projects:

```
cto-engine/
├── engine/              # Shared CLI tool
└── projects/
    ├── project-a/
    │   ├── charter.md
    │   ├── plan.yaml
    │   └── .cto-engine/
    └── project-b/
        ├── charter.md
        ├── plan.yaml
        └── .cto-engine/

# Run commands with PROJECT=...
make plan-interactive PROJECT=projects/project-a
make execute PROJECT=projects/project-b
```

---

### CI/CD integration

Automate daily snapshots with GitHub Actions:

```yaml
# .github/workflows/daily-snapshot.yml
name: Daily Jira Snapshot
on:
  schedule:
    - cron: '0 9 * * *'  # 9am daily
jobs:
  snapshot:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run snapshot
        run: make snapshot PROJECT=./projects/my-product
        env:
          JIRA_EMAIL: ${{ secrets.JIRA_EMAIL }}
          JIRA_API_TOKEN: ${{ secrets.JIRA_API_TOKEN }}
      - name: Commit if changed
        run: |
          git config user.name "CTO Engine Bot"
          git add .cto-engine/snapshot.json
          git diff --cached --quiet || git commit -m "Daily snapshot"
          git push
```

---

## FAQ

**Q: Can I modify the schemas?**  
A: You can add custom validation rules, but modifying core schemas breaks compatibility with the CTO Engine CLI.

**Q: What if my Jira has different custom fields?**  
A: Configure field mappings in `jira-config.yaml` to match your Jira instance.

**Q: How do I version control generated files?**  
A: Commit `snapshot.json`, `reality-check.md`, and `proposal.md` to track history. Logs can be gitignored.

**Q: Can I skip validation for urgent changes?**  
A: No. Validation exists to prevent future pain. If validation blocks you, the schema is revealing unclear thinking.

**Q: What if AI proposals are always wrong?**  
A: The challenge log will show low trust scores. Either improve your context.md quality or reduce reliance on AI.

---

## Next Steps

1. ✅ Read this documentation
2. ⬜ Copy templates to your project
3. ⬜ Fill in charter.md
4. ⬜ Configure jira-config.yaml
5. ⬜ Run first mock week manually
6. ⬜ Build the CLI tool
7. ⬜ Automate daily snapshots

See `CTO_ENGINE_OVERVIEW.md` for the full system architecture.

DOTNET ?= $(if $(wildcard /Users/paulofmbarros/.dotnet/dotnet),/Users/paulofmbarros/.dotnet/dotnet,dotnet)
CLI_PROJECT ?= $(CURDIR)/src/Cto.Cli/Cto.Cli.csproj
SOLUTION ?= $(CURDIR)/CtoEngine.slnx

PROJECT ?=
TARGET ?= all
PROVIDER ?= gemini
CANDIDATES ?= 3
CANDIDATE ?=
VISION_FILE ?=
MAX_INPUT_TOKENS ?= 12000
MAX_OUTPUT_TOKENS ?= 3500
BUDGET_USD ?= 3.0
FORCE ?=
REQUIRE_LLM ?=

.PHONY: help build test init doctor validate validate-jira validate-llm snapshot reality-check \
	plan-interactive plan-llm plan-list plan-select approve execute execute-dry-run \
	weekly-prep weekly-llm weekly-interactive weekly-review

define require_project
	@if [ -z "$(PROJECT)" ]; then \
		echo "PROJECT is required. Example: make $@ PROJECT=/path/to/project"; \
		exit 1; \
	fi
endef

define require_candidate
	@if [ -z "$(CANDIDATE)" ]; then \
		echo "CANDIDATE is required. Example: make plan-select PROJECT=/path/to/project CANDIDATE=2"; \
		exit 1; \
	fi
endef

define optional_force
$(if $(FORCE),--force,)
endef

define optional_vision
$(if $(VISION_FILE),--vision-file "$(VISION_FILE)",)
endef

help:
	@echo "CTO Engine Make Targets"
	@echo ""
	@echo "Core:"
	@echo "  make build"
	@echo "  make test"
	@echo "  make init PROJECT=/path/to/project [FORCE=1]"
	@echo "  make doctor PROJECT=/path/to/project [REQUIRE_LLM=1]"
	@echo "  make validate PROJECT=/path/to/project [TARGET=all]"
	@echo "  make snapshot PROJECT=/path/to/project"
	@echo "  make reality-check PROJECT=/path/to/project"
	@echo "  make approve PROJECT=/path/to/project"
	@echo "  make execute PROJECT=/path/to/project"
	@echo "  make execute-dry-run PROJECT=/path/to/project"
	@echo ""
	@echo "Planning:"
	@echo "  make plan-interactive PROJECT=/path/to/project"
	@echo "  make plan-llm PROJECT=/path/to/project [CANDIDATES=3] [VISION_FILE=/path/to/company-vision.md]"
	@echo "  make plan-list PROJECT=/path/to/project"
	@echo "  make plan-select PROJECT=/path/to/project CANDIDATE=2"
	@echo ""
	@echo "Weekly wrappers:"
	@echo "  make weekly-prep PROJECT=/path/to/project"
	@echo "  make weekly-interactive PROJECT=/path/to/project"
	@echo "  make weekly-llm PROJECT=/path/to/project [CANDIDATES=3]"
	@echo "  make weekly-review PROJECT=/path/to/project [CANDIDATES=3]"
	@echo ""
	@echo "Optional overrides:"
	@echo "  make build DOTNET=/Users/paulofmbarros/.dotnet/dotnet"

build:
	"$(DOTNET)" build "$(CLI_PROJECT)"

test:
	"$(DOTNET)" test "$(SOLUTION)"

init:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- init --path "$(PROJECT)" $(optional_force)

doctor:
	$(call require_project)
	@status=0; \
	echo "CTO Engine doctor for $(PROJECT)"; \
	echo ""; \
	if [ -f "$(CLI_PROJECT)" ]; then \
		echo "[ok] CLI project found: $(CLI_PROJECT)"; \
	else \
		echo "[error] CLI project missing: $(CLI_PROJECT)"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/charter.md" ]; then \
		echo "[ok] charter.md present"; \
	else \
		echo "[error] Missing $(PROJECT)/charter.md"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/context.md" ]; then \
		echo "[ok] context.md present"; \
	else \
		echo "[error] Missing $(PROJECT)/context.md"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/weeklylog.md" ]; then \
		echo "[ok] weeklylog.md present"; \
	else \
		echo "[error] Missing $(PROJECT)/weeklylog.md"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/plan.yaml" ]; then \
		echo "[ok] plan.yaml present"; \
	else \
		echo "[error] Missing $(PROJECT)/plan.yaml"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/.cto-engine/jira-config.yaml" ]; then \
		echo "[ok] jira-config.yaml present"; \
	else \
		echo "[error] Missing $(PROJECT)/.cto-engine/jira-config.yaml"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/.cto-engine/challenge-log.yaml" ]; then \
		echo "[ok] challenge-log.yaml present"; \
	else \
		echo "[error] Missing $(PROJECT)/.cto-engine/challenge-log.yaml"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/.cto-engine/llm-config.yaml" ]; then \
		echo "[ok] llm-config.yaml present"; \
	else \
		echo "[warn] Missing optional $(PROJECT)/.cto-engine/llm-config.yaml (required for make plan-llm / weekly-review)"; \
	fi; \
	if [ -n "$$JIRA_EMAIL" ]; then \
		echo "[ok] JIRA_EMAIL is set"; \
	else \
		echo "[error] JIRA_EMAIL is not set"; \
		status=1; \
	fi; \
	if [ -n "$$JIRA_API_TOKEN" ]; then \
		echo "[ok] JIRA_API_TOKEN is set"; \
	else \
		echo "[error] JIRA_API_TOKEN is not set"; \
		status=1; \
	fi; \
	if [ -f "$(PROJECT)/.cto-engine/llm-config.yaml" ]; then \
		if [ -n "$$GEMINI_API_KEY" ]; then \
			echo "[ok] GEMINI_API_KEY is set"; \
		elif [ -n "$(REQUIRE_LLM)" ]; then \
			echo "[error] GEMINI_API_KEY is not set"; \
			status=1; \
		else \
			echo "[warn] GEMINI_API_KEY is not set; LLM planning targets will fail"; \
		fi; \
	fi; \
	echo ""; \
	if [ $$status -eq 0 ]; then \
		echo "Doctor passed."; \
	else \
		echo "Doctor failed."; \
	fi; \
	exit $$status

validate:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- validate --project "$(PROJECT)" --target "$(TARGET)"

validate-jira:
	$(MAKE) validate PROJECT="$(PROJECT)" TARGET=jira-config

validate-llm:
	$(MAKE) validate PROJECT="$(PROJECT)" TARGET=llm-config

snapshot:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- snapshot --project "$(PROJECT)"

reality-check:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- reality-check --project "$(PROJECT)"

plan-interactive:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- plan --interactive --project "$(PROJECT)"

plan-llm:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- plan --llm --provider "$(PROVIDER)" --candidates "$(CANDIDATES)" $(optional_vision) --max-input-tokens "$(MAX_INPUT_TOKENS)" --max-output-tokens "$(MAX_OUTPUT_TOKENS)" --budget-usd "$(BUDGET_USD)" --project "$(PROJECT)"

plan-list:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- plan --list-candidates --project "$(PROJECT)"

plan-select:
	$(call require_project)
	$(call require_candidate)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- plan --select "$(CANDIDATE)" --project "$(PROJECT)"

approve:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- approve --project "$(PROJECT)"

execute:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- execute --project "$(PROJECT)"

execute-dry-run:
	$(call require_project)
	"$(DOTNET)" run --project "$(CLI_PROJECT)" -- execute --project "$(PROJECT)" --dry-run

weekly-prep: snapshot reality-check

weekly-interactive: weekly-prep plan-interactive

weekly-llm: weekly-prep plan-llm

weekly-review: REQUIRE_LLM=1
weekly-review: doctor weekly-prep plan-llm plan-list

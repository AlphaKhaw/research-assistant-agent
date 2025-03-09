SHELL := /bin/bash

# Default arguments are empty
ARGS ?=

# Build configuration
CONFIGURATION ?= Release
OUTPUT_DIR = ./bin
CLI_PROJECT = src/ResearchAssistant.Cli/ResearchAssistant.Cli.csproj


# CLI tool to test individual components
test-connector:
	set -a; source .env; set +a; cd tools/ConnectorTester && dotnet run -- $(ARGS)

test-search:
	set -a; source .env; set +a; cd tools/SearchTester && dotnet run -- ${ARGS}

test-planner:
	set -a; source .env; set +a; cd tools/PlannerTester && dotnet run -- ${ARGS}


# Build the research assistant CLI tool
build-cli:
	dotnet build $(CLI_PROJECT) -c $(CONFIGURATION) -o $(OUTPUT_DIR)
	@echo "Research Assistant CLI built successfully. Execute with 'make run-research' or 'make research"


# Clean build artifacts
clean:
	dotnet clean
	rm -rf $(OUTPUT_DIR)


# Run the CLI directly without building first
run-research-dev:
	set -a; source .env; set +a; dotnet run --project $(CLI_PROJECT) -- $(ARGS)


# Run the built CLI application
run-research:
	set -a; source .env; set +a; $(OUTPUT_DIR)/ResearchAssistant.Cli $(ARGS)


# Alias for a more intuitive command name
research: run-research


# Show help/usage information
help:
	@echo "Research Assistant CLI - Make Targets"
	@echo "======================================"
	@echo "make build-cli            - Build the CLI application"
	@echo "make run-research         - Run the built CLI application"
	@echo "make research             - Alias for run-research"
	@echo "make run-research-dev     - Run directly without separate build step"
	@echo "make clean                - Clean build artifacts"
	@echo ""
	@echo "Add arguments with ARGS='--topic \"Quantum Computing\" --temperature 0.3'"
	@echo ""
	@echo "Example:"
	@echo "  make research ARGS='--topic \"Climate Change\" --max-concurrent 3'"
	@echo "  make run-research-dev ARGS='--help'"

.PHONY: test-connector test-search test-planner build-cli clean run-research-dev run-research research help

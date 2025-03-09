SHELL := /bin/bash

# Default arguments are empty
ARGS ?=

test-connector:
	set -a; source .env; set +a; cd tools/ConnectorTester && dotnet run -- $(ARGS)

test-search:
	set -a; source .env; set +a; cd tools/SearchTester && dotnet run -- ${ARGS}

test-planner:
	set -a; source .env; set +a; cd tools/PlannerTester && dotnet run -- ${ARGS}

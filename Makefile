SHELL := /bin/bash

# Default arguments are empty
ARGS ?=

test-search:
	set -a; source .env; set +a; cd tools/SearchTester && dotnet run -- ${ARGS}

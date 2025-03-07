SHELL := /bin/bash

test-search:
	set -a; source .env; set +a; cd tools/SearchTester && dotnet run

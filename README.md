# Research Assistant Agent

A powerful CLI tool for generating comprehensive research reports on any topic using LLMs, with web search integration for up-to-date information.

## Overview

Research Assistant is a C# implementation of an AI-powered research agent that can:

1. Generate structured research plans based on user topics
2. Search the web for relevant information
3. Write comprehensive section content using LLMs
4. Assemble complete research reports with proper citations

The application leverages Microsoft's Semantic Kernel framework to interact with large language models and provides a streamlined command-line interface for easy use.

## Features

- **Topic-based Research**: Generate reports on any topic of interest
- **Interactive Planning**: Review and revise the research plan before execution
- **Web Search Integration**: Automatically search the web for up-to-date information
- **Concurrent Processing**: Process multiple sections simultaneously for faster generation
- **Progress Tracking**: Real-time progress updates as your report is being generated
- **Citations**: Automatically include citations for information sources
- **Customizable Output**: Save reports as markdown files

## Architecture

The project follows a service-oriented architecture with clear separation of concerns:

```bash
    ┌────────────────────────┐                              ┌────────────────────────┐
    │  ResearchAssistant.Cli │                              │ ResearchAssistant.Core │
    │  (User Interface)      │─────────────────────────────▶│   (Business Logic)     │
    └────────────────────────┘                              └────────────────────────┘
                                                                        │
                                                                        ▼
                                                            ┌────────────────────────┐
                                                            │   External Services    │
                                                            │  - OpenAI              │
                                                            │  - Google Search       │
                                                            └────────────────────────┘
```

### Key Components

- **PlannerService**: Generates and revises research plans
- **SectionWriter**: Writes content for each section of the report
- **GoogleSearchTool**: Searches the web for relevant information
- **OpenAIConnector**: Interfaces with OpenAI's models for text generation
- **CommandLineParser**: Handles CLI arguments and options
- **ConsoleUI**: Renders information to the console

## Setup

### Prerequisites

- `.NET 9.0 SDK`
- `OpenAI API key`
- `Google Search API key` and `Custom Search Engine ID` **(for search functionality)**

### Installation

1. Clone the repository:
   ```
   git clone https://github.com/yourusername/research-assistant-agent.git
   cd research-assistant-agent
   ```

2. Build the application:
   ```
   make build
   ```

3. Set up environment variables (or create a .env file):
   ```
   OPENAI_API_KEY=your_openai_api_key
   OPENAI_MODEL=gpt-4o
   GOOGLE_API_KEY=your_google_api_key
   GOOGLE_SEARCH_ENGINE_ID=your_search_engine_id
   ```

## Usage

Generate a research report using the CLI:

```bash
make research ARGS='--topic "Quantum Computing" --max-concurrent 3'
```

Or run directly:

```bash
make run-research-dev ARGS='--topic "Climate Change" --no-search'

```


### Command Line Options

- `--topic <text>`: Research topic to investigate
- `--organization <text>`: Specific organization structure for the report
- `--context <text>`: Additional context for the research
- `--no-search`: Disable web search capability
- `--temperature <number>`: Set temperature (0.0-1.0, default: 0.2)
- `--max-concurrent <num>`: Maximum concurrent sections (default: 2)
- `--max-queries <num>`: Maximum search queries per section (default: 3)
- `--output <directory>`: Directory to save the report (default: current)
- `--help, -h`: Show help message

### Environment Variables

- `OPENAI_API_KEY`: Your OpenAI API key
- `OPENAI_MODEL`: Model to use (default: gpt-4o)
- `GOOGLE_API_KEY`: Your Google API key (for search)
- `GOOGLE_SEARCH_ENGINE_ID`: Your Google Search Engine ID (for search)

## Project Structure

```bash
├── src/
│ ├── ResearchAssistant.Core/ # Core business logic and models
│ │ ├── Interfaces/           # Contract definitions
│ │ ├── Models/               # Data models
│ │ └── Services/             # Core services implementation
│ │ ├── LLM/                  # Language model interaction
│ │ └── Search/               # Web search functionality
│ └── ResearchAssistant.Cli/  # Command-line interface application
│ ├── Models/                 # CLI-specific models
│ ├── Services/               # CLI services
│ ├── UI/                     # Console user interface
│ └── Utils/                  # Utility functions
├── tools/                    # Development and testing utilities
│ ├── ConnectorTester/        # Test LLM connectivity
│ ├── PlannerTester/          # Test plan generation
│ └── SearchTester/           # Test search functionality
└── Makefile                  # Build and run scripts
```

## Development

### Testing Individual Components

You can test individual components using the provided testing tools:

**Test the OpenAI connector:**
```bash
make test-connector
```

**Test the Google search functionality:**
```bash
make test-search ARGS='--max-results 5'
```

**Test the planner functionality:**
```bash
make test-planner ARGS='--topic "Artificial Intelligence"'
```

### Building

**Build the CLI application:**
```bash
make build
```

**Clean build artifacts:**
```bash
make clean
```

## Workflow

1. **Planning Stage**: The application creates a structured research plan based on your topic
2. **User Revision**: Review and provide feedback to refine the research plan
3. **Execution Preparation**: The plan is converted into executable tasks
4. **Report Generation**: Content is generated for each section in parallel
5. **Report Completion**: The final report is assembled and saved to a markdown file

## Attribution

This project is a C# reimplementation of [Open Deep Research](https://github.com/langchain-ai/open_deep_research) with modifications.

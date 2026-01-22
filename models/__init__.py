"""Shared object models for DWSIM MCP Server.

This package contains all shared models that facilitate interoperability between:
- Python MCP server and C# engine worker (via JSON serialization)
- LLM agents (via MCP tool schemas and CAPE-OPEN vocabulary)
- External systems (via CAPE-OPEN standard interfaces)
"""

__version__ = "0.1.0"

# Resource models for MCP resource providers
from models.resources import (
    ResourceMetadata,
    SampleCaseInfo,
    DocumentationTopic,
    SessionResultResource,
)

__all__ = [
    # Resource models
    "ResourceMetadata",
    "SampleCaseInfo", 
    "DocumentationTopic",
    "SessionResultResource",
]

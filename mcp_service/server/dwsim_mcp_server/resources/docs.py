# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Documentation resource provider for DWSIM reference materials."""

from __future__ import annotations

import asyncio
from pathlib import Path
from typing import Dict, List, Optional

import aiofiles
from mcp import types

from dwsim_mcp_server.resources.base import (
    BaseResourceProvider,
    ResourceNotFoundError,
)


class DocsProvider(BaseResourceProvider):
    """Resource provider for DWSIM documentation.

    Serves documentation topics from markdown files in a configured directory.
    Supports caching to avoid repeated file reads.

    Resource URIs:
        - resource://docs - List all available topics
        - resource://docs/{topic} - Get specific topic content
    """

    SCHEME = "docs"

    def __init__(self, docs_path: str) -> None:
        """Initialize DocsProvider.

        Args:
            docs_path: Path to directory containing documentation markdown files.
        """
        super().__init__()
        self._docs_path = Path(docs_path)
        self._cache: Dict[str, str] = {}
        self._topics_cache: Optional[List[Dict[str, str]]] = None

    def get_resource_templates(self) -> List[types.ResourceTemplate]:
        """Return resource templates for documentation URIs."""
        return [
            types.ResourceTemplate(
                uriTemplate="resource://docs",
                name="Documentation Index",
                description="List all available DWSIM documentation topics",
                mimeType="application/json",
            ),
            types.ResourceTemplate(
                uriTemplate="resource://docs/{topic}",
                name="Documentation Topic",
                description="Get documentation for a specific topic (e.g., unit-operations, property-packages)",
                mimeType="text/markdown",
            ),
        ]

    async def list_resources(self) -> List[types.Resource]:
        """Return all available documentation resources."""
        resources = [
            self.create_resource(
                uri="resource://docs",
                name="Documentation Index",
                description="List all available DWSIM documentation topics",
                mime_type="application/json",
            )
        ]

        topics = await self._get_available_topics()
        for topic in topics:
            resources.append(
                self.create_resource(
                    uri=f"resource://docs/{topic['topic']}",
                    name=topic["title"],
                    description=topic["description"],
                    mime_type="text/markdown",
                )
            )

        return resources

    async def read_resource(self, uri: str) -> types.ReadResourceResult:
        """Read documentation content by URI."""
        scheme, segments = self.parse_uri(uri)

        if scheme != self.SCHEME:
            raise ResourceNotFoundError(
                f"Invalid scheme '{scheme}' for docs provider",
                suggestions=["resource://docs", "resource://docs/unit-operations"],
            )

        # resource://docs - return index
        if not segments:
            topics = await self._get_available_topics()
            return self.create_json_result(uri, {"topics": topics})

        # resource://docs/{topic} - return topic content
        topic = segments[0]
        content = await self._get_topic_content(topic)

        if content is None:
            available = await self._get_available_topics()
            available_names = [t["topic"] for t in available]
            raise ResourceNotFoundError(
                f"Documentation topic '{topic}' not found",
                suggestions=available_names[:5],
            )

        return self.create_text_result(uri, content, "text/markdown")

    async def _get_available_topics(self) -> List[Dict[str, str]]:
        """Get list of available documentation topics.

        Returns cached list or scans docs directory.
        """
        if self._topics_cache is not None:
            return self._topics_cache

        topics = []

        if not self._docs_path.exists():
            self._logger.warning("docs_path_missing", path=str(self._docs_path))
            return topics

        # Scan for markdown files
        loop = asyncio.get_event_loop()
        md_files = await loop.run_in_executor(
            None, lambda: list(self._docs_path.glob("*.md"))
        )

        for md_file in md_files:
            topic_id = md_file.stem  # filename without extension
            if topic_id == "index":
                continue  # Skip index file in topic list

            # Extract title and description from file
            metadata = await self._extract_metadata(md_file)
            topics.append(
                {
                    "topic": topic_id,
                    "title": metadata.get("title", topic_id.replace("-", " ").title()),
                    "description": metadata.get(
                        "description", f"Documentation for {topic_id}"
                    ),
                }
            )

        # Sort alphabetically
        topics.sort(key=lambda t: t["topic"])
        self._topics_cache = topics

        self._logger.info("docs_topics_loaded", count=len(topics))
        return topics

    async def _get_topic_content(self, topic: str) -> Optional[str]:
        """Get content for a specific topic.

        Args:
            topic: Topic identifier (filename without extension)

        Returns:
            Markdown content or None if not found.
        """
        # Check cache
        if topic in self._cache:
            return self._cache[topic]

        # Try to read file
        file_path = self._docs_path / f"{topic}.md"

        if not file_path.exists():
            return None

        try:
            async with aiofiles.open(file_path, "r", encoding="utf-8") as f:
                content = await f.read()

            # Cache for future requests
            self._cache[topic] = content
            self._logger.debug("docs_topic_loaded", topic=topic)
            return content

        except Exception as e:
            self._logger.error("docs_read_error", topic=topic, error=str(e))
            return None

    async def _extract_metadata(self, file_path: Path) -> Dict[str, str]:
        """Extract title and description from markdown file.

        Looks for first H1 heading as title and first paragraph as description.

        Args:
            file_path: Path to markdown file

        Returns:
            Dict with 'title' and 'description' keys.
        """
        metadata: Dict[str, str] = {}

        try:
            async with aiofiles.open(file_path, "r", encoding="utf-8") as f:
                lines = []
                async for line in f:
                    lines.append(line)
                    if len(lines) > 20:  # Only read first 20 lines for metadata
                        break

            # Find H1 title
            for line in lines:
                if line.startswith("# "):
                    metadata["title"] = line[2:].strip()
                    break

            # Find first paragraph (non-heading, non-empty line)
            in_paragraph = False
            paragraph_lines = []
            for line in lines:
                stripped = line.strip()
                if not stripped:
                    if in_paragraph:
                        break
                    continue
                if stripped.startswith("#"):
                    if in_paragraph:
                        break
                    continue
                in_paragraph = True
                paragraph_lines.append(stripped)
                if len(paragraph_lines) >= 3:
                    break

            if paragraph_lines:
                metadata["description"] = " ".join(paragraph_lines)[:200]

        except Exception as e:
            self._logger.warning(
                "metadata_extraction_failed", file=str(file_path), error=str(e)
            )

        return metadata

    def clear_cache(self) -> None:
        """Clear the content and topics cache."""
        self._cache.clear()
        self._topics_cache = None
        self._logger.debug("docs_cache_cleared")

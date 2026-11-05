# Theme Builder Architecture

Assessment: 2026-11-05. Evidence-based baseline, not a parity certification.

## Data flow

ThemeDocument -> schema/token validation -> named-base DAG resolution -> variable resolution -> immutable Theme -> preview -> complete source/JSON export.

## Implemented

Custom-base snapshot inheritance, variables, cycle rejection, versioned JSON documents and strict import checks.

## Required extensions

Typed color/font/spacing/density tokens; component token contracts; undoable editing; brand manifests; white-label identity metadata; schema migrations; compatible profile variants; generated-source compilation and pixel comparison.

## Marketplace architecture

Distribute data-only brand/theme packs, never executable plugin code. Manifest: ID, numeric/schema version, license, author, base dependencies, target compatibility, content hashes and provenance. Validate limits, integrity, dependency cycles and trust before installation. Marketplace signing, retrieval and migration are planned, not implemented here.

# marketing-site Specification

## Purpose

The public marketing site's pricing and features surfaces render tier, price, and
entitlement content from the shared plan catalog rather than page-local copy, so they
cannot drift from what the hosted API enforces; every catalog entitlement has display
copy, checked at build time.

## Requirements

### Requirement: The pricing page renders from the plan catalog
The public pricing page SHALL derive its tier cards and its feature-comparison table from
the shared plan catalog. It SHALL NOT contain a page-local list of tiers, prices, or
per-tier feature values. Adding, removing, or re-pricing a tier in the catalog SHALL
change the pricing page with no edit to the page component.

#### Scenario: A catalog tier change is reflected on the pricing page
- **WHEN** the catalog's set of tiers or a tier's entitlement values change
- **THEN** the rendered pricing page shows the new tiers and values without any change to
  the page component

#### Scenario: Placeholder prices are marked
- **WHEN** a tier's price is flagged as a placeholder in the catalog
- **THEN** the pricing page shows that tier's price with the early-access caveat

### Requirement: A features page lists every capability with its minimum tier
The site SHALL have a features page that lists the hosted capabilities as a table
generated from the catalog's entitlement keys, each row showing a human label, a
one-line description, and the lowest tier whose entitlement set includes that capability.
Capabilities of the open-source engine that are always available without an account SHALL
be listed separately and SHALL NOT appear in the tier-gated table.

#### Scenario: Each hosted entitlement appears once with its lowest including tier
- **WHEN** the features page renders
- **THEN** every entitlement key in the catalog has exactly one row, showing the lowest
  tier that grants it

### Requirement: Every catalog entitlement has display copy, checked at build time
There SHALL be a mapping from every catalog entitlement key to a display label and
description. The build SHALL fail if the catalog contains an entitlement key with no copy
entry, or the copy map contains a key not in the catalog.

#### Scenario: A new entitlement without copy fails the build
- **WHEN** a new entitlement key is added to the catalog and no display copy is added for it
- **THEN** the marketing site build fails with an error naming the missing key

#### Scenario: Stale copy for a removed entitlement fails the build
- **WHEN** an entitlement key is removed from the catalog but its copy entry remains
- **THEN** the build fails with an error naming the orphaned key

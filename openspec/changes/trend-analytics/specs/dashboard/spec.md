## ADDED Requirements

### Requirement: The dashboard presents trend views for entitled organizations
For an organization holding the `trendAnalytics` entitlement, the dashboard SHALL present
a trends view at both the project level and the organization level, showing the case pass
rate, flag-proof pass rate, and run volume over a selectable window (7 / 30 / 90 days), a
failure-classification breakdown, and the flakiest-cases list. For an organization without
that entitlement, the dashboard SHALL show an upgrade prompt in place of the trends view
rather than the charts.

#### Scenario: An entitled organization sees trend charts
- **WHEN** a customer whose organization holds `trendAnalytics` opens the trends view
- **THEN** the pass-rate, flag-proof-rate, and volume charts and the flakiest-cases list
  are shown for the selected window

#### Scenario: The window can be changed
- **WHEN** the customer switches the window from 30 to 90 days
- **THEN** the charts re-render with weekly buckets over the 90-day window

#### Scenario: An unentitled organization sees an upgrade prompt
- **WHEN** a customer whose organization lacks `trendAnalytics` opens the trends view
- **THEN** an upgrade prompt is shown in place of the charts

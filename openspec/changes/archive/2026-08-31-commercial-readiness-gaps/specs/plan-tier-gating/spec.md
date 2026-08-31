## ADDED Requirements

### Requirement: Run notifications and evidence sharing are Team-gated entitlements
The plan catalog SHALL define two additional entitlement keys — one for run
notifications and one for evidence sharing — and the entitlement service SHALL
report them as granted for `Team` and `Enterprise` and denied for `Free`.
Enforcement of these features SHALL route through the entitlement service and
SHALL NOT compare the tier value inline.

#### Scenario: Free is denied both entitlements
- **WHEN** the entitlement service is asked whether a `Free` organization may configure run notifications or create evidence share links
- **THEN** both are reported as denied, each with a reason naming the required tier

#### Scenario: Team is granted both entitlements
- **WHEN** the entitlement service is asked the same for a `Team` organization
- **THEN** both are reported as granted

#### Scenario: Losing the tier revokes the entitlement
- **WHEN** an organization moves from `Team` to `Free`
- **THEN** the entitlement service immediately reports both entitlements as denied, and features that depend on them stop operating

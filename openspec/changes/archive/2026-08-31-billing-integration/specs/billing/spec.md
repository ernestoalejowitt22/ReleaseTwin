## Purpose

Connects the hosted platform to a Merchant of Record (Polar) so customers pay for the Team tier through hosted checkout, and subscription lifecycle events — payment, renewal, card failure, cancellation — drive the organization's tier, billing status, and per-project subscription quantity without any tax, invoicing, or dunning logic on our side.

## ADDED Requirements

### Requirement: Upgrade to a paid tier goes through Merchant-of-Record checkout
A customer SHALL be able to start a paid upgrade from the dashboard, be taken to the Merchant of Record's hosted checkout to enter payment details, and choose a billing cadence offered by the plan catalog. Payment details, card data, and billing address SHALL be entered only on the Merchant of Record's surface, never captured or stored by the hosted platform.

#### Scenario: Customer starts an upgrade
- **WHEN** a Free-tier customer chooses to upgrade to Team from the dashboard
- **THEN** they are taken to the Merchant of Record's hosted checkout for the selected cadence, and no card or billing-address field is presented by the hosted platform itself

#### Scenario: Customer abandons checkout
- **WHEN** a customer opens the hosted checkout and closes it without paying
- **THEN** their organization remains on its prior tier and billing status, with no partial change

### Requirement: The subscription webhook is the only writer of billing-driven tier and status changes
The hosted platform SHALL expose an endpoint that receives subscription lifecycle notifications from the Merchant of Record. A successful payment or active subscription SHALL move the organization to the paid tier; a cancellation or unrecoverable non-payment SHALL move it back to the free tier's entitlements. Dashboard or redirect-return code SHALL NOT change the tier directly — the organization's tier and billing status change only as a result of processing a notification.

#### Scenario: Payment activates the paid tier
- **WHEN** the endpoint receives a notification that a customer's subscription is paid and active
- **THEN** the corresponding organization is on the Team tier and its billing status is active

#### Scenario: Cancellation returns the organization to free entitlements
- **WHEN** the endpoint receives a notification that a customer's subscription is canceled
- **THEN** the organization's effective entitlements are those of the Free tier

#### Scenario: Redirect return does not grant the tier
- **WHEN** a customer is redirected back to the dashboard after checkout but no notification has been processed yet
- **THEN** the organization's tier is unchanged until a notification is processed

### Requirement: Webhook notifications are authenticated and processed idempotently
The endpoint SHALL reject any notification whose signature does not verify against the Merchant of Record's signing secret. Each notification SHALL be processed at most once in effect: receiving the same notification again SHALL be acknowledged successfully and produce no additional change. A notification whose processing fails SHALL NOT be recorded as processed and SHALL be retried when the Merchant of Record redelivers it.

#### Scenario: Unsigned or wrongly signed notification is rejected
- **WHEN** a request to the endpoint has a missing or invalid signature
- **THEN** it is rejected and no state changes

#### Scenario: Duplicate delivery is a no-op
- **WHEN** a notification that has already been processed is delivered again
- **THEN** the endpoint acknowledges it successfully and the organization's state is unchanged

#### Scenario: Failed processing is retried
- **WHEN** processing a notification fails partway through
- **THEN** the endpoint returns a non-success response, the notification is not marked processed, and a later redelivery is processed normally

### Requirement: Billing status modifies entitlements independently of tier
An organization SHALL carry a billing status of active, past-due, or canceled, separate from its tier. While past-due, the organization SHALL retain its tier's full entitlements for a fixed grace window measured from the status change; after the grace window, and when canceled, its effective entitlements SHALL be those of the Free tier. Recovery to active within the grace window SHALL restore full entitlements with no further action.

#### Scenario: Past-due within grace keeps full entitlements
- **WHEN** an organization's subscription is past-due and the grace window has not elapsed
- **THEN** its effective entitlements are still those of its paid tier

#### Scenario: Past-due beyond grace degrades to Free
- **WHEN** an organization has been past-due longer than the grace window
- **THEN** its effective entitlements are those of the Free tier

#### Scenario: Payment recovery restores entitlements
- **WHEN** a past-due organization's payment succeeds and a notification marks it active again
- **THEN** its billing status is active and its full tier entitlements are restored immediately

### Requirement: Per-project subscription quantity tracks the organization's project count
For an organization with an active paid subscription, the Merchant-of-Record subscription quantity SHALL reflect the number of projects in the organization. Creating a project SHALL increase the quantity before the project is created; if the Merchant of Record rejects the increase (for example, a declined proration charge), the project creation SHALL fail with a message directing the customer to the Merchant of Record's customer portal, and no project SHALL be created. Deleting a project SHALL decrease the quantity, and a failure to decrease SHALL NOT block the deletion.

#### Scenario: Creating a project raises the quantity
- **WHEN** a customer with a paid subscription and N projects creates another project
- **THEN** the subscription quantity is set to N+1 and the project is created

#### Scenario: A rejected quantity increase blocks the project
- **WHEN** the Merchant of Record rejects the quantity increase during project creation
- **THEN** the project is not created and the customer is told to update their payment method in the customer portal

#### Scenario: Deleting a project lowers the quantity but never blocks on billing
- **WHEN** a customer deletes a project and the quantity decrease fails
- **THEN** the project is still deleted and the discrepancy is left for reconciliation

### Requirement: A scheduled reconciliation job corrects subscription-quantity drift
The hosted platform SHALL run a scheduled job that, for each organization with an active subscription, compares the Merchant-of-Record subscription quantity to the organization's actual project count and corrects the subscription quantity to match the actual count. The job SHALL also re-evaluate which projects are read-only under the current tier. The job SHALL log every correction it makes.

#### Scenario: Drift is corrected toward actual project count
- **WHEN** the reconciliation job finds a subscription quantity that does not match the organization's project count
- **THEN** it sets the subscription quantity to the actual project count and logs the correction

#### Scenario: Organizations without a Merchant-of-Record subscription are skipped
- **WHEN** the reconciliation job encounters an organization that has no Merchant-of-Record subscription (for example, an operator-set or hand-invoiced organization)
- **THEN** it makes no billing calls for that organization

### Requirement: Discounts and price locks are delegated to the Merchant of Record
The hosted platform SHALL NOT implement promotional codes, coupons, or per-customer price locks. It SHALL treat the amount reported by the Merchant of Record as authoritative and SHALL NOT derive a customer's charged amount from the plan catalog. Customer-facing plan information for a paying customer SHALL point to the Merchant of Record's portal for the authoritative amount rather than asserting a catalog price.

#### Scenario: A discounted customer needs no special handling
- **WHEN** a customer checks out with a Merchant-of-Record discount applied
- **THEN** their organization reaches the paid tier normally and the hosted platform records no discount state of its own

#### Scenario: Paid customers see a portal link rather than a catalog price
- **WHEN** a paying customer views their plan in the dashboard
- **THEN** the tier and cadence are shown along with a link to the Merchant of Record's portal for the exact amount, not a hardcoded catalog price

### Requirement: Managing billing is a redirect to the Merchant of Record's portal
A paying customer SHALL be able to reach the Merchant of Record's hosted customer portal from the dashboard to update payment methods, view invoices, and cancel. The hosted platform SHALL NOT render invoice history, payment-method forms, or billing-address forms itself.

#### Scenario: Customer opens the billing portal
- **WHEN** a paying customer chooses "Manage billing" in the dashboard
- **THEN** they are taken to the Merchant of Record's hosted portal for their subscription

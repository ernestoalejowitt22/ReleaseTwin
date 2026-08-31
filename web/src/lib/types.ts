export interface DashboardProjectSummary {
  id: string;
  name: string;
  /** billing: true when the org is over its current tier's project limit and this project is one of the excess (read-only) ones — still listed with its evidence, but ingest is blocked. */
  readOnly?: boolean;
  /** onboarding-activation: true for the virtual seeded sample project shown until the org's first real run. Not persisted, not counted toward the plan limit, rejects every mutation. */
  isExample?: boolean;
}

/** billing: mirrors the C# `BillingStatus` enum — a second axis alongside the tier. */
export type BillingStatus = "Active" | "PastDue" | "Canceled";

/** billing: mirrors the C# `BillingCadence` enum. */
export type BillingCadence = "Monthly" | "Annual";

export interface DashboardConnectionView {
  provider: string;
  externalRepo: string;
  connectedAt: string;
}

export interface DashboardTokenView {
  id: string;
  displayPrefix: string;
  createdAt: string;
  isRevoked: boolean;
}

/** evidence-store: per-report evidence state — see DashboardService.EvidenceStatus. */
export type EvidenceStatus = "none" | "available" | "expired" | "not-entitled";

export interface DashboardCaseReportView {
  caseId: string;
  passed: boolean;
  classification: string | null;
  cleanupStatus: string;
  uploadedAt: string;
  reportId: string;
  evidenceStatus: EvidenceStatus;
}

export interface DashboardFlagProofReportView {
  caseId: string;
  buildIdentity: string;
  outcome: string;
  knownBadLegPassed: boolean | null;
  knownGoodLegPassed: boolean | null;
  uploadedAt: string;
  reportId: string;
  evidenceStatus: EvidenceStatus;
}

export interface EvidenceAssertion {
  expression: string;
  expected: string | null;
  observed: string | null;
}

export interface EvidenceScreenshotRef {
  id: string;
  bestEffortRedacted: boolean;
}

export interface EvidenceStep {
  index: number;
  operationName: string;
  outcome: string;
  durationMs: number;
  assertion: EvidenceAssertion | null;
  adapter: unknown;
  screenshots: EvidenceScreenshotRef[] | null;
}

export interface EvidenceLeg {
  leg: string | null;
  steps: EvidenceStep[];
}

export interface EvidenceDocument {
  caseId: string;
  oracleLocator: string;
  legs: EvidenceLeg[];
  redactionNote: string | null;
}

export interface EvidenceDetailView {
  document: EvidenceDocument;
  screenshotIds: string[];
  uploadedAt: string;
}

/**
 * evidence-sharing: the ONLY payload a share-link viewer receives (mirrors the C# `SharedEvidenceView`
 * record). Carries the redacted evidence document + the run's result — nothing that identifies or
 * links to the organization, project, or any other run.
 */
export interface SharedEvidenceView {
  caseId: string;
  reportKind: string;
  result: string;
  classification: string | null;
  fixtureSha256: string;
  hasEvidenceDocument: boolean;
  evidenceUploadedAt: string | null;
  document: EvidenceDocument | null;
  screenshotIds: string[];
}

export interface EvidenceConfigView {
  captureDefault: boolean;
  retentionDays: number;
  maxRetentionDays: number;
  available: boolean;
}

export interface DashboardUsageSummary {
  caseReportCount: number;
  flagProofReportCount: number;
  periodStart: string;
}

export type PlanTier = "Free" | "Team" | "Enterprise";

/** plan-catalog-and-entitlements: mirrors the C# `Entitlements` record — the resolved entitlement set for the org's tier. */
export interface Entitlements {
  maxProjects: number | null;
  evidenceViewer: boolean;
  maxEvidenceRetentionDays: number | null;
  customRedactionRules: boolean;
  projectSecrets: boolean;
  trendAnalytics: boolean;
  releaseRollup: boolean;
  ciIntegration: boolean;
  runNotifications: boolean;
  evidenceSharing: boolean;
  sso: boolean;
  auditLog: boolean;
}

export interface DashboardView {
  /** The caller's own organization id (the one every query here is scoped to). */
  organizationId: string;
  projects: DashboardProjectSummary[];
  selectedProject: DashboardProjectSummary | null;
  connection: DashboardConnectionView | null;
  tokens: DashboardTokenView[];
  caseReports: DashboardCaseReportView[];
  flagProofReports: DashboardFlagProofReportView[];
  usage: DashboardUsageSummary;
  planTier: PlanTier;
  entitlements: Entitlements;
  isSelectedProjectStale: boolean;
  /** billing: the org's billing status; degrades entitlements independently of the tier. */
  billingStatus: BillingStatus;
  /** billing: renewal cadence for a paying org; null when there is no paid subscription. */
  billingCadence: BillingCadence | null;
  /** billing: true once the org has a Merchant-of-Record subscription — show cadence + portal link instead of a catalog price. */
  hasBillingLinkage: boolean;
  /** billing: true when at least one project is read-only under the current tier. */
  hasReadOnlyProjects: boolean;
  /** billing: the customer-facing upgrade / portal actions are live (Polar configured AND switched on after a verified sandbox checkout). */
  billingEnabled: boolean;
  /** onboarding-activation: the guided first-run panel, present only until the org's first real run. */
  guidedSetup: GuidedSetupView | null;
}

/** onboarding-activation: mirrors the C# `GuidedSetupView` record. */
export interface GuidedSetupView {
  hasProject: boolean;
  hasToken: boolean;
  apiUrl: string;
  cliCommand: string;
}

/** trend-analytics: mirrors the C# `TrendBucket`. Rates are null (a gap) when their denominator is zero. */
export interface TrendBucket {
  start: string;
  casePassRate: number | null;
  flagProofPassRate: number | null;
  runVolume: number;
  classificationBreakdown: Record<string, number>;
}

export interface FlakiestCase {
  caseId: string;
  flipCount: number;
  lastActivity: string;
}

export interface TrendReport {
  window: string;
  granularity: "daily" | "weekly";
  buckets: TrendBucket[];
  flakiestCases: FlakiestCase[];
}

export type TrendWindowParam = "7d" | "30d" | "90d";

/** release-readiness-rollup: mirrors the C# `ReleaseCaseResult` / `ReleaseRollup`. */
export type ReleaseCaseState = "Green" | "Failing" | "Stale";
export type ReleaseHeadlineState = "Proven" | "NotProven" | "Incomplete";
export type ReleaseWindowParam = "7d" | "14d" | "30d" | "90d";

export interface ReleaseCaseResult {
  caseId: string;
  state: ReleaseCaseState;
  latestOutcome: string;
  latestReportAt: string;
}

export interface ReleaseRollup {
  release: string;
  headline: ReleaseHeadlineState;
  greenCount: number;
  failingCount: number;
  staleCount: number;
  windowDays: number;
  cases: ReleaseCaseResult[];
}

export interface GitHubAuthorizeResult {
  configured: boolean;
  authorizeUrl: string | null;
}

export interface GitHubCallbackResult {
  projectId: string;
  repositories: string[];
}

export interface JourneySummary {
  id: string;
  name: string;
  projectId: string;
  createdAt: string;
}

export interface JourneyVersionSummary {
  version: number;
  createdByDisplayName: string;
  createdAt: string;
}

export interface AdapterCredentialSummary {
  adapter: string;
  lastSetByDisplayName: string;
  updatedAt: string;
}

export interface ProjectSecretSummary {
  name: string;
  lastSetByDisplayName: string;
  updatedAt: string;
}

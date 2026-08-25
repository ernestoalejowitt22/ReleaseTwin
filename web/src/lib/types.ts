export interface DashboardProjectSummary {
  id: string;
  name: string;
}

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

export interface DashboardCaseReportView {
  caseId: string;
  passed: boolean;
  classification: string | null;
  cleanupStatus: string;
  uploadedAt: string;
}

export interface DashboardFlagProofReportView {
  caseId: string;
  buildIdentity: string;
  outcome: string;
  knownBadLegPassed: boolean | null;
  knownGoodLegPassed: boolean | null;
  uploadedAt: string;
}

export interface DashboardUsageSummary {
  caseReportCount: number;
  flagProofReportCount: number;
  periodStart: string;
}

export interface DashboardView {
  projects: DashboardProjectSummary[];
  selectedProject: DashboardProjectSummary | null;
  connection: DashboardConnectionView | null;
  tokens: DashboardTokenView[];
  caseReports: DashboardCaseReportView[];
  flagProofReports: DashboardFlagProofReportView[];
  usage: DashboardUsageSummary;
}

export interface GitHubAuthorizeResult {
  configured: boolean;
  authorizeUrl: string | null;
}

export interface GitHubCallbackResult {
  projectId: string;
  repositories: string[];
}

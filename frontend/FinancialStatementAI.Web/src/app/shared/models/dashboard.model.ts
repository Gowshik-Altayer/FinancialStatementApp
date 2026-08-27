export interface DashboardKpis {
  totalStatements: number;
  completedCount: number;
  inProgressCount: number;
  failedCount: number;
  transactionsProcessed: number;
  transactionsNeedingReview: number;
  reconciledCount: number;
  mismatchCount: number;
  pendingReconciliationCount: number;
  averageClassificationConfidence: number | null;
  averageProcessingTimeSeconds: number | null;
}

export type PipelineStageState = 'pending' | 'in-progress' | 'complete' | 'failed';

export interface PipelineStage {
  key: string;
  label: string;
  count: number;
  state: PipelineStageState;
}

export interface NamedCount {
  name: string;
  count: number;
}

export interface DailyTrendPoint {
  date: string;
  uploadedCount: number;
  completedCount: number;
  failedCount: number;
}

export interface CategoryBreakdown {
  categoryName: string;
  transactionCount: number;
  totalAmount: number;
}

export interface ReviewStatistics {
  pendingCount: number;
  correctedCount: number;
  aiAcceptedCount: number;
}

export interface ActivityItem {
  type: 'Upload' | 'Completed' | 'Failed' | 'Correction';
  description: string;
  timestamp: string;
  statementId: string | null;
}

export interface UsersOverview {
  totalUsers: number;
  activeUsers: number;
  roleBreakdown: NamedCount[];
}

export interface DashboardSummary {
  kpis: DashboardKpis;
  pipelineStages: PipelineStage[];
  processingStatusBreakdown: NamedCount[];
  processingTrend: DailyTrendPoint[];
  transactionsByCategory: CategoryBreakdown[];
  confidenceDistribution: NamedCount[];
  reconciliationStatusBreakdown: NamedCount[];
  reviewStatistics: ReviewStatistics;
  recentActivity: ActivityItem[];
  usersOverview: UsersOverview | null;
}

export interface DashboardWidgetPreference {
  widgetKey: string;
  isVisible: boolean;
  sortOrder: number;
  source: 'UserOverride' | 'RoleDefault' | 'SystemDefault';
}

export interface WidgetPreferenceItem {
  widgetKey: string;
  isVisible: boolean;
  sortOrder: number;
}

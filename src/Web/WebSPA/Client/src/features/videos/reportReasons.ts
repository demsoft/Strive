export const REPORT_REASONS = ['illegal', 'copyright', 'sexual', 'violence', 'spam', 'other'] as const;
export type ReportReason = typeof REPORT_REASONS[number];

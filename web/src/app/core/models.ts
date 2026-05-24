export type ReviewStatus = 'Pending' | 'Reviewed' | 'Rejected';
export type EnrichmentStatusType = 'Pending' | 'InProgress' | 'Completed' | 'Failed';

export interface CandidateSummary {
  id: string;
  fullName: string;
  email: string;
  location?: string | null;
  reviewStatus: ReviewStatus;
  aiSeniorityLevel?: string | null;
  aiYearsExperienceEstimate?: number | null;
  topSkill?: string | null;
  aiSummary?: string | null;
  skills: string[];
  updatedAt: string;
}

export interface SkillRow { id: string; name: string; orderIndex: number; }
export interface WorkExperienceRow {
  id: string; title: string; company: string;
  startDate: string; endDate?: string | null;
  description: string; orderIndex: number;
}
export interface EducationRow {
  id: string; institution: string; degree: string;
  field: string; graduationYear?: number | null; orderIndex: number;
}
export interface CertificationRow {
  id: string; name: string; issuer: string; year?: number | null; orderIndex: number;
}
export interface ProjectRow {
  id: string; name: string; description: string; techStack: string; orderIndex: number;
}

export interface AiFields {
  aiSummary?: string | null;
  aiSeniorityLevel?: string | null;
  aiSeniorityRationale?: string | null;
  aiTopStrengths?: string | null;
  aiSkillCategories?: string | null;
  aiYearsExperienceEstimate?: number | null;
  aiSuggestedRoles?: string | null;
  aiInterviewFocusAreas?: string | null;
  lastEnrichedAt?: string | null;
  enrichmentStatus: EnrichmentStatusType;
  lastError?: string | null;
}

export interface AiOverride {
  fieldName: string;
  originalAiValue?: string | null;
  currentValue?: string | null;
  isUserEdited: boolean;
  updatedAt: string;
}

export interface CandidateDetail {
  id: string;
  fullName: string;
  email: string;
  phone?: string | null;
  location?: string | null;
  linkedInUrl?: string | null;
  gitHubUrl?: string | null;
  summary: string;
  sourceFileName: string;
  reviewStatus: ReviewStatus;
  createdAt: string;
  updatedAt: string;
  lastEditedBy: string;
  skills: SkillRow[];
  workExperiences: WorkExperienceRow[];
  education: EducationRow[];
  certifications: CertificationRow[];
  projects: ProjectRow[];
  aiFields?: AiFields | null;
  aiOverrides: AiOverride[];
}

export interface AiHistoryEntry {
  id: string;
  fieldName: string;
  modelName: string;
  promptText: string;
  extraInstructions?: string | null;
  responseText: string;
  latencyMs: number;
  tokenUsage?: string | null;
  requestedBy: string;
  requestedAt: string;
  status: string;
  errorMessage?: string | null;
}

export const AI_FIELD_LABELS: Record<string, string> = {
  AiSummary: 'AI summary',
  AiSeniorityLevel: 'AI seniority level',
  AiSeniorityRationale: 'AI seniority rationale',
  AiTopStrengths: 'AI top strengths',
  AiSkillCategories: 'AI skill categories',
  AiYearsExperienceEstimate: 'AI years of experience',
  AiSuggestedRoles: 'AI suggested roles',
  AiInterviewFocusAreas: 'AI interview focus areas',
};

import { CandidateDetail } from '../../core/models';

export interface NameValue { name: string; value: number; }
export interface NamedSeries { name: string; series: NameValue[]; }

export interface LeaderboardEntry {
  id: string;
  name: string;
  score: number;
  seniority: string;
  yearsExperience: number;
  components: { label: string; weight: number; value: number; weighted: number }[];
}

export interface ReportData {
  seniorityDistribution: NameValue[];
  skillsHeatmap: NamedSeries[];
  experienceTimeline: { name: string; series: { name: string; value: [number, number] }[] }[];
  techCloud: NameValue[];
  educationBreakdown: NamedSeries[];
  leaderboard: LeaderboardEntry[];
}

const SENIORITY_WEIGHT: Record<string, number> = {
  Junior: 1, Mid: 2, Senior: 3, Staff: 4, Principal: 5,
};

export function computeReportData(rows: CandidateDetail[]): ReportData {
  return {
    seniorityDistribution: seniorityDistribution(rows),
    skillsHeatmap: skillsHeatmap(rows),
    experienceTimeline: experienceTimeline(rows),
    techCloud: techCloud(rows),
    educationBreakdown: educationBreakdown(rows),
    leaderboard: leaderboard(rows),
  };
}

function seniorityDistribution(rows: CandidateDetail[]): NameValue[] {
  const counts: Record<string, number> = {};
  for (const r of rows) {
    const s = r.aiFields?.aiSeniorityLevel || 'Unknown';
    counts[s] = (counts[s] ?? 0) + 1;
  }
  return Object.entries(counts).map(([name, value]) => ({ name, value }));
}

function skillsHeatmap(rows: CandidateDetail[]): NamedSeries[] {
  const allSkillNames = Array.from(
    new Set(rows.flatMap((r) => r.skills.map((s) => s.name)))
  ).slice(0, 12); // top 12 by frequency
  return rows.map((r) => ({
    name: r.fullName,
    series: allSkillNames.map((skill) => ({
      name: skill,
      value: r.skills.find((s) => s.name === skill) ? 1 : 0,
    })),
  }));
}

function experienceTimeline(rows: CandidateDetail[]) {
  return rows.map((r) => ({
    name: r.fullName,
    series: r.workExperiences.map((e) => ({
      name: `${e.title} @ ${e.company}`,
      value: [
        new Date(e.startDate).getTime(),
        (e.endDate ? new Date(e.endDate) : new Date()).getTime(),
      ] as [number, number],
    })),
  }));
}

function techCloud(rows: CandidateDetail[]): NameValue[] {
  const counts: Record<string, number> = {};
  for (const r of rows) {
    for (const s of r.skills) counts[s.name] = (counts[s.name] ?? 0) + 1;
    for (const p of r.projects) {
      for (const t of (p.techStack ?? '').split(/[,;]/).map((x) => x.trim()).filter(Boolean)) {
        counts[t] = (counts[t] ?? 0) + 1;
      }
    }
  }
  return Object.entries(counts)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 25)
    .map(([name, value]) => ({ name, value }));
}

function educationBreakdown(rows: CandidateDetail[]): NamedSeries[] {
  const byInstitution: Record<string, Record<string, number>> = {};
  for (const r of rows) {
    for (const e of r.education) {
      const inst = e.institution || 'Unknown';
      byInstitution[inst] ??= {};
      byInstitution[inst][e.degree || 'Other'] = (byInstitution[inst][e.degree || 'Other'] ?? 0) + 1;
    }
  }
  return Object.entries(byInstitution).map(([inst, degrees]) => ({
    name: inst,
    series: Object.entries(degrees).map(([name, value]) => ({ name, value })),
  }));
}

function leaderboard(rows: CandidateDetail[]): LeaderboardEntry[] {
  const seniorityWeight = 0.5;
  const yearsWeight = 0.3;
  const skillsWeight = 0.2;
  const allSkills = new Set(rows.flatMap((r) => r.skills.map((s) => s.name)));
  const denomSkills = allSkills.size || 1;

  return rows
    .map((r) => {
      const sen = r.aiFields?.aiSeniorityLevel ?? 'Unknown';
      const senValue = (SENIORITY_WEIGHT[sen] ?? 0) * 20;
      const years = Number(r.aiFields?.aiYearsExperienceEstimate ?? 0);
      const yearsValue = Math.min(100, years * 8);
      const skillsValue = (r.skills.length / denomSkills) * 100;

      const components = [
        { label: `Seniority (${sen})`, weight: seniorityWeight, value: senValue, weighted: senValue * seniorityWeight },
        { label: `Years experience (${years})`, weight: yearsWeight, value: yearsValue, weighted: yearsValue * yearsWeight },
        { label: `Skill breadth (${r.skills.length})`, weight: skillsWeight, value: skillsValue, weighted: skillsValue * skillsWeight },
      ];
      const score = Math.round(components.reduce((s, c) => s + c.weighted, 0));
      return { id: r.id, name: r.fullName, score, seniority: sen, yearsExperience: years, components };
    })
    .sort((a, b) => b.score - a.score);
}

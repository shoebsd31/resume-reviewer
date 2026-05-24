namespace ResumeReview.SampleGenerator;

public record ExperienceProfile(
    string Title,
    string Company,
    DateOnly Start,
    DateOnly? End,
    string Description);

public record EducationProfile(
    string Institution,
    string Degree,
    string Field,
    int Year);

public record CandidateProfile(
    string FullName,
    string Email,
    string Phone,
    string Location,
    string LinkedIn,
    string GitHub,
    string Title,
    string Summary,
    IReadOnlyList<string> Skills,
    IReadOnlyList<ExperienceProfile> Experiences,
    IReadOnlyList<EducationProfile> Education,
    IReadOnlyList<string> Awards);
